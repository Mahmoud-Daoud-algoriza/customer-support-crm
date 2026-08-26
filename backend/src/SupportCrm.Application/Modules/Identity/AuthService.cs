using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Identity;

/// <summary>
/// Sign-in and the resolved-identity read (docs/api-design.md §5.2).
/// </summary>
public sealed class AuthService(
    IApplicationDbContext db,
    IPasswordHasher<User> passwordHasher,
    ITokenIssuer tokenIssuer,
    IAuditRecorder audit,
    ICurrentUser currentUser)
{
    /// <summary>
    /// The stable Problem Details slug for every sign-in failure (docs/api-design.md §6.12).
    /// </summary>
    private const string InvalidCredentials = "invalid-credentials";

    /// <summary>
    /// Exchanges credentials for a token.
    /// <para>
    /// <b>A wrong password, an unknown email and a deactivated account all return the same
    /// <c>401 invalid-credentials</c>.</b> Distinguishing them would confirm which emails have
    /// accounts (docs/api-design.md §6.11), so the three paths below deliberately converge on one
    /// failure.
    /// </para>
    /// </summary>
    public async Task<AuthTokenDto> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = (request.Email ?? string.Empty).Trim();

        // Plain equality, so the unique index on Email serves the lookup docs/data-model.md §6
        // justifies it for. Case-insensitivity comes from the column's collation
        // (SQL_Latin1_General_CP1_CI_AS, §6.1) rather than from a LOWER() call that would defeat
        // the index.
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            await FailAsync(email, ct);
        }

        // Verified before the IsActive check, so a deactivated account takes the same code path and
        // the same amount of work as a wrong password.
        var verification = passwordHasher.VerifyHashedPassword(user!, user!.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed || !user.IsActive)
        {
            await FailAsync(email, ct);
        }

        var token = tokenIssuer.Issue(user.Id);

        // The actor is passed explicitly: this endpoint is anonymous, so there is no request identity
        // to resolve from, but the user has just been established. actorDescriptor is deliberately
        // left null — docs/data-model.md §2.14 uses it only when actorUserId could not be resolved.
        await audit.RecordAsync(
            AuditAction.SignInSucceeded, AuditOutcome.Success,
            AuditTargetType.User, user.Id, actorUserId: user.Id, ct: ct);

        await db.SaveChangesAsync(ct);

        return new AuthTokenDto(token.AccessToken, token.ExpiresAt, ToIdentity(user));
    }

    /// <summary>
    /// The per-request resolved identity (AP-9) — <b>not a decoded token</b>. It reads
    /// <see cref="ICurrentUser"/>, which the API's resolution step filled from the authoritative
    /// row, so a role change made by an Administrator is visible here immediately and without a new
    /// token being issued.
    /// </summary>
    public Task<IdentityDto> GetMeAsync(CancellationToken ct)
    {
        // BranchId is not part of ICurrentUser — it is a reporting attribute and no authorization
        // decision may read it (A-2) — so the one field the payload needs beyond the resolved
        // context is read here.
        return db.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUser.Id)
            .Select(u => new IdentityDto(
                u.Id, u.DisplayName, u.Email, u.Role, u.DepartmentId, u.BranchId, u.CustomerId, u.IsActive))
            .SingleAsync(ct);
    }

    /// <summary>
    /// Records the failed attempt and throws. <c>ActorUserId</c> is null and the submitted email
    /// becomes <c>ActorDescriptor</c>, so the attempt stays attributable even though no user was
    /// resolved (docs/data-model.md §2.14). The recorder truncates an over-long identifier rather
    /// than throwing, so an absurd input cannot suppress its own audit entry (§6.1).
    /// </summary>
    private async Task FailAsync(string email, CancellationToken ct)
    {
        await audit.RecordAsync(
            AuditAction.SignInFailed, AuditOutcome.Failure, actorDescriptor: email, ct: ct);

        await db.SaveChangesAsync(ct);

        throw new UnauthorizedException(InvalidCredentials, "Email or password is incorrect.");
    }

    private static IdentityDto ToIdentity(User user) => new(
        user.Id, user.DisplayName, user.Email, user.Role,
        user.DepartmentId, user.BranchId, user.CustomerId, user.IsActive);
}
