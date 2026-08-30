using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;
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
    ICurrentUser currentUser,
    IOptions<RegistrationOptions> registrationOptions,
    TimeProvider clock)
{
    /// <summary>
    /// The stable Problem Details slug for every sign-in failure (docs/api-design.md §6.12).
    /// </summary>
    private const string InvalidCredentials = "invalid-credentials";

    /// <summary>
    /// A placeholder hash, replaced before the entity is persisted.
    /// <see cref="User.CreateCustomerUser"/> refuses an empty one, and the password hasher needs the
    /// instance in order to produce the real hash — so there is no moment at which a user exists
    /// without one. <c>UserAdminService</c> carries the same constant for the same reason.
    /// </summary>
    private const string UnhashedSentinel = "!unhashed";

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
    /// Customer self-registration — <b>A-15</b>, docs/api-design.md §5.2, deferred here from
    /// Story 02 because it is the first point at which a <c>Customer</c>, a <c>Branch</c> and the
    /// configured default branch all exist.
    ///
    /// <para>
    /// <b>Exactly three outcomes, and only these three</b> (§5.2):
    /// <list type="table">
    /// <item><term>Neither a profile nor a login for the email</term>
    ///   <description><c>201</c> — create the <c>Customer</c> with the configured default branch,
    ///   and its login</description></item>
    /// <item><term>An agent-created profile, no login</term>
    ///   <description><c>201</c> — create the login and <b>link it to that profile</b>. No second
    ///   customer, and it does not fail</description></item>
    /// <item><term>A login already exists for the email</term>
    ///   <description><c>409 user-already-exists</c> — <b>PF-6</b></description></item>
    /// </list>
    /// The middle row is what keeps A-10's one-customer-per-email rule true.
    /// </para>
    ///
    /// <para>
    /// <b>The caller chooses no branch, no role and no customer id.</b> §5.2 states it and
    /// <see cref="RegisterRequest"/> enforces it by having no property for any of them (AP-10).
    /// </para>
    ///
    /// <para>
    /// <b>The default branch is the <em>customer's</em>, and the login gets none.</b> A-15 and
    /// docs/data-model.md §2.4 both attach it to <c>Customer.branchId</c>; §2.1 calls
    /// <c>User.branchId</c> a "staff location, reporting attribute only"; and
    /// docs/architecture.md §6.3 says the configured value is "assigned to self-registering
    /// <em>customers</em>". So <c>User.branchId</c> stays null — the resolution of finding
    /// <b>I-5</b>, which slice 3 left open for this task.
    /// </para>
    ///
    /// <para>
    /// <b>One unit of work.</b> Both success paths add their rows and call
    /// <c>SaveChangesAsync</c> exactly once, so a profile is never created without its login
    /// (docs/api-design.md §5.5's A-19 box uses the same discipline for the same reason). No
    /// explicit transaction is opened: one <c>SaveChanges</c> already is one.
    /// </para>
    ///
    /// <para>
    /// <b>The response is an <c>AuthToken</c></b>, not a bare identity — docs/api-design.md §6.1
    /// names <c>POST /auth/register</c> alongside <c>POST /auth/login</c> for that payload, so a
    /// registration signs the new customer in rather than sending them to the sign-in form.
    /// </para>
    /// </summary>
    public async Task<AuthTokenDto> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        // Case-insensitivity is the COLUMN's, not ToLower()'s: docs/data-model.md §6.1 puts
        // SQL_Latin1_General_CP1_CI_AS on both User.Email and Customer.Email precisely so that
        // "two addresses differing only in case are the same address" (A-10) is a property of the
        // schema — and lowering in C# would defeat the unique index each query seeks on.
        //
        // Outcome three, checked first: a login already exists. PF-6's slug, and the same one
        // POST /users raises for the same collision.
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("user-already-exists", "A user with that email already exists.");
        }

        // Outcomes one and two differ only in whether the profile is found or made.
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Email == email, ct);

        if (customer is null)
        {
            customer = Customer.Create(
                id: Guid.NewGuid(),
                fullName: request.FullName,
                email: email,
                phone: request.Phone,

                // A-15: assigned, never asked for. The value is validated against a real Branch row
                // at startup (ConfigurationValidator check 3), so it cannot be a dangling id here.
                branchId: registrationOptions.Value.DefaultBranchId,
                createdAt: clock.GetUtcNow());

            db.Customers.Add(customer);
        }
        else if (await db.Users.AnyAsync(u => u.CustomerId == customer.Id, ct))
        {
            // Not reachable through any approved flow — A-19 keeps a profile's email and its login's
            // in step, so a profile that has a login always matches the check above. It is guarded
            // anyway because the alternative is a unique-index violation surfacing as a 500:
            // docs/data-model.md §5 constraint 3 allows at most one login per profile. The slug is
            // the same because the fact is the same — this person already has a login.
            throw new ConflictException(
                "user-already-exists", "A user with that email already exists.");
        }

        // The hasher needs an instance to hash against, so the entity is built first and the real
        // hash applied immediately, before anything is persisted — the same order
        // UserAdminService.CreateAsync uses.
        var user = User.CreateCustomerUser(
            id: Guid.NewGuid(),
            email: email,
            passwordHash: UnhashedSentinel,
            displayName: request.FullName,
            customerId: customer.Id,
            createdAt: clock.GetUtcNow());

        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));

        db.Users.Add(user);

        // The LOGIN is audited; the customer profile beside it is not. Creating a profile is
        // business data, not a security event (AD-10) — CustomerService.CreateAsync says the same
        // of the agent-created path.
        //
        // actorUserId is passed explicitly for the reason LoginAsync passes it: this endpoint is
        // anonymous, so ICurrentUser is empty, while the actor is in fact known — they are the
        // person who just registered. Leaving it null would record "no user could be resolved"
        // (docs/data-model.md §2.14), which is not what happened.
        await audit.RecordAsync(
            AuditAction.UserCreated, AuditOutcome.Success,
            AuditTargetType.User, user.Id, actorUserId: user.Id, ct: ct);

        // One call, so the profile, the login and the audit entry commit together or not at all.
        await db.SaveChangesAsync(ct);

        var token = tokenIssuer.Issue(user.Id);

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
