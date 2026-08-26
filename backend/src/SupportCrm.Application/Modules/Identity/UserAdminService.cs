using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Identity;

/// <summary>
/// Administrator user management — the five endpoints of docs/api-design.md §5.3.
/// <para>
/// Every rule below is enforced <b>here, server-side</b>. The front end re-implements some of them
/// for immediate feedback, but that is UX: T1-D requires permissions and validity to be proven on
/// the server, never assumed from the UI (docs/architecture.md §4.2).
/// </para>
/// </summary>
public sealed class UserAdminService(
    IApplicationDbContext db,
    IPasswordHasher<User> passwordHasher,
    IAuditRecorder audit,
    TimeProvider clock)
{
    /// <summary>
    /// The sort whitelist for <c>GET /users</c> (AP-15). Anything not listed is a <c>400</c>.
    /// <para>
    /// <b>No approved document enumerates this set for <c>/users</c>.</b> docs/api-design.md §2.1
    /// requires <em>a</em> per-endpoint whitelist and names none for this endpoint, so it is drawn
    /// from the fields the <c>User</c> payload already publishes (§6.1). It deliberately omits
    /// <c>departmentId</c> and <c>branchId</c>, which are opaque ids and sort meaninglessly.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> SortableFields = new(StringComparer.Ordinal)
    {
        ["displayName"] = nameof(User.DisplayName),
        ["email"] = nameof(User.Email),
        ["role"] = nameof(User.Role),
        ["createdAt"] = nameof(User.CreatedAt),
    };

    /// <summary>Default sort — a stable order, without which paging is not meaningful.</summary>
    private const string DefaultSortField = nameof(User.DisplayName);

    public async Task<PagedResult<UserDto>> ListAsync(
        UserListFilter filter, PageQuery page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();
        var (sortField, descending) = page.ParseSort(SortableFields, DefaultSortField);

        var query = db.Users.AsNoTracking();

        // Filters are named for the field they filter; different parameters AND together
        // (docs/api-design.md §2.1).
        if (filter.Role is { } role)
        {
            query = query.Where(u => u.Role == role);
        }

        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(u => u.DepartmentId == departmentId);
        }

        if (filter.IsActive is { } isActive)
        {
            query = query.Where(u => u.IsActive == isActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = filter.Q.Trim();
            query = query.Where(u => u.DisplayName.Contains(term) || u.Email.Contains(term));
        }

        query = (sortField, descending) switch
        {
            (nameof(User.Email), false) => query.OrderBy(u => u.Email),
            (nameof(User.Email), true) => query.OrderByDescending(u => u.Email),
            (nameof(User.Role), false) => query.OrderBy(u => u.Role).ThenBy(u => u.DisplayName),
            (nameof(User.Role), true) => query.OrderByDescending(u => u.Role).ThenBy(u => u.DisplayName),
            (nameof(User.CreatedAt), false) => query.OrderBy(u => u.CreatedAt),
            (nameof(User.CreatedAt), true) => query.OrderByDescending(u => u.CreatedAt),
            (_, true) => query.OrderByDescending(u => u.DisplayName),
            _ => query.OrderBy(u => u.DisplayName),
        };

        return await query.Select(Projection).ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == id).Select(Projection).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("User not found.");

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        // Role is [Required] on the request, so a missing value is already a 400 at model binding.
        var role = request.Role!.Value;

        // The Customer role cannot be created here at all — customers arrive through registration or
        // by an agent creating a profile (DM-1, docs/api-design.md §5.3).
        if (!role.IsStaff())
        {
            throw new ValidationException(
                "The Customer role cannot be created through this endpoint. Customers arrive " +
                "through registration or by an agent creating a customer profile.");
        }

        if (request.DepartmentId is null || request.DepartmentId == Guid.Empty)
        {
            throw new ValidationException($"A {role} requires a departmentId.");
        }

        var email = request.Email.Trim();

        // Checked before insert so a duplicate is a 409 with the stable slug api-design §5.3
        // specifies, rather than a unique-index violation surfacing as a 500.
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("user-already-exists", "A user with that email already exists.");
        }

        if (!await db.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct))
        {
            throw new ValidationException("departmentId does not reference an existing department.");
        }

        if (request.BranchId is { } branchId && !await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            throw new ValidationException("branchId does not reference an existing branch.");
        }

        // PasswordHasher<TUser> needs an instance to hash against, so the entity is built first and
        // the real hash applied immediately, before anything is persisted.
        var user = User.CreateStaff(
            id: Guid.NewGuid(),
            email: email,
            passwordHash: UnhashedSentinel,
            displayName: request.DisplayName,
            role: role,
            departmentId: request.DepartmentId.Value,
            branchId: request.BranchId,
            createdAt: clock.GetUtcNow());

        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password));

        db.Users.Add(user);

        await audit.RecordAsync(
            AuditAction.UserCreated, AuditOutcome.Success, AuditTargetType.User, user.Id, ct: ct);

        await db.SaveChangesAsync(ct);

        return ToDto(user);
    }

    /// <summary>
    /// Applies only the fields present — a PATCH carries only what is changing
    /// (docs/api-design.md §2).
    /// <para>
    /// Email and password are not patchable, and there is no property on
    /// <see cref="PatchUserRequest"/> through which either could arrive. A role or department change
    /// is audited as its own action, because both are permission-relevant
    /// (docs/data-model.md §2.14, docs/architecture.md §2.4).
    /// </para>
    /// </summary>
    public async Task<UserDto> PatchAsync(Guid id, PatchUserRequest request, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User not found.");

        if (request.DisplayName is not null)
        {
            user.Rename(request.DisplayName);
        }

        if (request.Role is { } role && role != user.Role)
        {
            if (!role.IsStaff())
            {
                throw new ValidationException("A user cannot be changed to the Customer role.");
            }

            user.ChangeRole(role);

            await audit.RecordAsync(
                AuditAction.UserRoleChanged, AuditOutcome.Success, AuditTargetType.User, user.Id, ct: ct);
        }

        if (request.DepartmentId is { } departmentId && departmentId != user.DepartmentId)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            {
                throw new ValidationException("departmentId does not reference an existing department.");
            }

            user.ChangeDepartment(departmentId);

            await audit.RecordAsync(
                AuditAction.UserDepartmentChanged, AuditOutcome.Success, AuditTargetType.User, user.Id, ct: ct);
        }

        if (request.BranchId is { } branchId)
        {
            if (!await db.Branches.AnyAsync(b => b.Id == branchId, ct))
            {
                throw new ValidationException("branchId does not reference an existing branch.");
            }

            user.ChangeBranch(branchId);
        }

        await db.SaveChangesAsync(ct);

        return ToDto(user);
    }

    /// <summary>
    /// Deactivates the account. Nothing is deleted, and the user's <b>next</b> request is refused
    /// with <c>401</c> because the active flag is re-read per request (AD-15).
    /// </summary>
    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("User not found.");

        user.Deactivate();

        await audit.RecordAsync(
            AuditAction.UserDeactivated, AuditOutcome.Success, AuditTargetType.User, user.Id, ct: ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Never persisted: it is replaced by the real hash on the line after construction. It exists
    /// only because <c>CreateStaff</c> refuses an empty hash, so there is no window in which a user
    /// could be saved without one.
    /// </summary>
    private const string UnhashedSentinel = "!unhashed";

    /// <summary>
    /// The projection shared by the list and the single read, so the two cannot drift apart.
    /// <b><c>PasswordHash</c> is not selected — it appears in no response, ever.</b>
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<User, UserDto>> Projection => u => new UserDto(
        u.Id, u.Email, u.DisplayName, u.Role, u.DepartmentId, u.BranchId, u.IsActive, u.CreatedAt);

    private static UserDto ToDto(User u) => new(
        u.Id, u.Email, u.DisplayName, u.Role, u.DepartmentId, u.BranchId, u.IsActive, u.CreatedAt);
}

/// <summary>Filters for <c>GET /users</c> — docs/api-design.md §5.3.</summary>
public sealed record UserListFilter
{
    public UserRole? Role { get; init; }

    public Guid? DepartmentId { get; init; }

    public bool? IsActive { get; init; }

    /// <summary>Free-text match over display name and email.</summary>
    public string? Q { get; init; }
}
