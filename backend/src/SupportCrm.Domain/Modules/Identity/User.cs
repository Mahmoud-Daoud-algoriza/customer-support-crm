namespace SupportCrm.Domain.Modules.Identity;

/// <summary>
/// The authoritative identity record (docs/data-model.md §2.1).
/// <para>
/// <b><see cref="Role"/>, <see cref="DepartmentId"/> and <see cref="IsActive"/> are the
/// authoritative values, re-read on every authenticated request</b> (AD-15,
/// docs/architecture.md §4.1.1, docs/data-model.md §5 constraint 4). No token claim substitutes for
/// them and nothing caches them.
/// </para>
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class User
{
    private User()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>Unique, case-insensitive. The sign-in identifier (A-9).</summary>
    public string Email { get; private set; } = default!;

    /// <summary>
    /// ASP.NET Core standard password hashing (docs/architecture.md §4.1).
    /// <b>Never leaves the server</b> — it appears in no response payload.
    /// </summary>
    public string PasswordHash { get; private set; } = default!;

    /// <summary>Shown as the actor on messages, notes and activity.</summary>
    public string DisplayName { get; private set; } = default!;

    public UserRole Role { get; private set; }

    /// <summary>Required for a staff role, null for <c>Customer</c> (DM-1).</summary>
    public Guid? DepartmentId { get; private set; }

    /// <summary>Required for <c>Customer</c>, null for a staff role (DM-1).</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>Staff location. A reporting attribute only — never an access rule (T2-K, A-2).</summary>
    public Guid? BranchId { get; private set; }

    /// <summary>
    /// Deactivation flag. Read on every request, not just at sign-in
    /// (docs/architecture.md §4.1.1). Deactivating never deletes.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates an <c>Agent</c>, <c>Manager</c> or <c>Administrator</c>.
    /// <para>
    /// Throws for the two shapes DM-1 forbids — a staff user without a department, and a staff user
    /// carrying a customer link — and for <c>role == Customer</c>, which cannot be created through
    /// this path at all: customers arrive through registration or an agent creating a profile
    /// (docs/api-design.md §5.3). <c>UserAdminService</c> rejects the same three cases with proper
    /// Problem Details; this factory is the invariant that makes bypassing it impossible.
    /// </para>
    /// <c>CreateCustomerUser</c> is added by Story 04, when <c>Customer</c> exists.
    /// </summary>
    public static User CreateStaff(
        Guid id,
        string email,
        string passwordHash,
        string displayName,
        UserRole role,
        Guid departmentId,
        Guid? branchId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (!role.IsStaff())
        {
            throw new ArgumentException(
                "The Customer role cannot be created as a staff user (DM-1).", nameof(role));
        }

        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staff user requires a department (DM-1).", nameof(departmentId));
        }

        return new User
        {
            Id = id,
            Email = email.Trim(),
            PasswordHash = passwordHash,
            DisplayName = displayName.Trim(),
            Role = role,
            DepartmentId = departmentId,
            CustomerId = null,
            BranchId = branchId,
            IsActive = true,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// Sets or replaces the stored hash.
    /// <para>
    /// ASP.NET Core's <c>PasswordHasher&lt;TUser&gt;</c> needs a user instance to hash against, so a
    /// new user is constructed first and its hash applied immediately afterwards, before anything is
    /// persisted. <c>CreateStaff</c> therefore requires a non-empty hash: there is no state in which
    /// a user exists without one.
    /// </para>
    /// </summary>
    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    /// <summary>
    /// Deactivates the account. The user's <b>next</b> request is refused with <c>401</c>, because
    /// the flag is re-read per request (docs/architecture.md §4.1.1). Nothing is deleted.
    /// </summary>
    public void Deactivate() => IsActive = false;

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    /// <summary>
    /// Administrator-only, never self-set (docs/api-design.md §7). Keeps the DM-1 shape: a staff
    /// role must have a department, and <c>Customer</c> is not reachable from a staff record.
    /// </summary>
    public void ChangeRole(UserRole role)
    {
        if (!role.IsStaff())
        {
            throw new ArgumentException(
                "A staff user cannot be demoted to the Customer role (DM-1).", nameof(role));
        }

        Role = role;
    }

    /// <summary>Administrator-only, never self-set (docs/api-design.md §7).</summary>
    public void ChangeDepartment(Guid departmentId)
    {
        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException(
                "A staff user requires a department (DM-1).", nameof(departmentId));
        }

        DepartmentId = departmentId;
    }

    /// <summary>A reporting attribute only. Clearing it is allowed — it is optional for staff.</summary>
    public void ChangeBranch(Guid? branchId) => BranchId = branchId;

    /// <summary>
    /// Sets the sign-in address. <b>Story 04, A-19 — and it has exactly one legitimate caller:
    /// <c>CustomerService.UpdateAsync</c></b>, propagating a change to the linked
    /// <c>Customer.Email</c> in the same unit of work (docs/product-scope.md A-19,
    /// docs/api-design.md §5.5, docs/data-model.md §5 constraints 1a and 1b).
    ///
    /// <para>
    /// <b>This does not make email patchable through <c>PATCH /users/{id}</c>.</b> That endpoint
    /// still cannot change it, and <c>PatchUserRequest</c> still has <b>no <c>email</c> property</b>
    /// (AP-10, docs/api-design.md §5.3) — the restriction lives in the request model, where AP-10
    /// puts every such restriction, not in the absence of a domain mutator. A customer's address and
    /// their sign-in are one address; a server-side consequence of editing the profile is precisely
    /// how A-19 says that is kept true.
    /// </para>
    ///
    /// <para>
    /// <b>Uniqueness is not checked here and cannot be.</b> Constraint 1 applies the
    /// case-insensitive uniqueness of <c>User.email</c> across <em>all</em> users, staff included,
    /// which is a cross-row rule the Domain layer cannot see. The caller checks it and rejects the
    /// whole operation on a collision, writing neither row.
    /// </para>
    ///
    /// <para><b>The change is audited</b> by the caller as <c>UserEmailChanged</c>, against this
    /// user, in that same unit of work — a sign-in identifier changing is a security event even
    /// though the profile edit that caused it is not (AD-10).</para>
    /// </summary>
    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
    }
}
