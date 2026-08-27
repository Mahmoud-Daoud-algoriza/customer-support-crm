namespace SupportCrm.Domain.Modules.Customers;

/// <summary>
/// The CRM profile of requirements §1 — who the support is for (docs/data-model.md §2.4, T1-A).
/// <para>
/// <b>A <c>Customer</c> is not a login.</b> DM-1 keeps the two apart: a customer who uses the portal
/// has a <c>User</c> row of role <c>Customer</c> pointing back here, and a customer who has never
/// signed in has a profile and no login at all — which is required, because an agent creates tickets
/// on behalf of customers who may never touch the portal.
/// </para>
/// <para>
/// <b>Deleting a customer is not an application operation</b> (docs/data-model.md §2.4), so this
/// type exposes no deletion or soft-deletion concept, and there is no merge or dedupe path either
/// (T2-A, docs/product-scope.md §8) — a duplicate email is <em>rejected</em>, never reconciled.
/// </para>
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class Customer
{
    private Customer()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>Requirements §1 contact details.</summary>
    public string FullName { get; private set; } = default!;

    /// <summary>
    /// Unique across the organization, case-insensitively — the identifier of A-10
    /// (docs/data-model.md §5 constraint 1).
    /// <para>
    /// <b>It is also the linked portal login's sign-in address (A-19).</b> Uniqueness is a
    /// cross-row rule that the Domain layer cannot see, and the propagation to <c>User.Email</c>
    /// spans two entities, so both live in <c>CustomerService.UpdateAsync</c> — one unit of work,
    /// committed once (docs/architecture.md §3, docs/data-model.md §5 constraints 1a and 1b).
    /// This mutator therefore takes the value it is given; it does not and cannot check it.
    /// </para>
    /// </summary>
    public string Email { get; private set; } = default!;

    /// <summary>Requirements §1 contact details. Optional.</summary>
    public string? Phone { get; private set; }

    /// <summary>
    /// A-2: a customer belongs to one branch, and the column is <b>required</b>. A self-registering
    /// customer receives the configured default branch and is never asked to choose one (A-15).
    /// <para>
    /// Branch is a reporting and filtering attribute only. It appears in no authorization predicate
    /// anywhere (docs/data-model.md §5 constraint 6).
    /// </para>
    /// </summary>
    public Guid BranchId { get; private set; }

    /// <summary>
    /// The ERP seam's single persisted field (DM-6, docs/architecture.md §5.3), unused by default.
    /// <para>
    /// <b>There is no public mutator, deliberately.</b> docs/api-design.md §8.3 makes it read-only
    /// and settable through no endpoint; §6.3 returns it and never accepts it. An adapter that one
    /// day needs to write it is a scope change that adds the mutator with a reason — absence makes
    /// accidental exposure through a request model impossible rather than merely discouraged.
    /// </para>
    /// </summary>
    public string? ExternalReference { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The only way a profile comes into existence — an agent creating one
    /// (<c>POST /customers</c>) or a self-registration (<c>POST /auth/register</c>, A-15).
    /// <para>
    /// <see cref="BranchId"/> is required by A-2, so an empty id is refused here rather than
    /// deferred to a foreign-key violation. <see cref="ExternalReference"/> is not a parameter: no
    /// caller may set it (DM-6).
    /// </para>
    /// </summary>
    public static Customer Create(
        Guid id,
        string fullName,
        string email,
        string? phone,
        Guid branchId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("A customer requires a branch (A-2).", nameof(branchId));
        }

        return new Customer
        {
            Id = id,
            FullName = fullName.Trim(),
            Email = email.Trim(),
            Phone = NormalizePhone(phone),
            BranchId = branchId,
            ExternalReference = null,
            CreatedAt = createdAt,
        };
    }

    public void Rename(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        FullName = fullName.Trim();
    }

    /// <summary>
    /// Sets the profile address.
    /// <para>
    /// <b>Call this only from the Application layer path that also propagates to the linked
    /// <c>User.Email</c> and audits it</b> (A-19, docs/data-model.md §5 constraints 1a/1b). There is
    /// no committed state in which a customer and their portal login hold different addresses, and
    /// this method alone cannot uphold that — it can see one row.
    /// </para>
    /// </summary>
    public void ChangeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        Email = email.Trim();
    }

    /// <summary>Clearing the phone is allowed — it is optional (docs/data-model.md §2.4).</summary>
    public void ChangePhone(string? phone) => Phone = NormalizePhone(phone);

    /// <summary>
    /// Moves the customer to another branch. Required, so it cannot be cleared.
    /// <para>Reporting only — never an access rule (A-2, T2-K).</para>
    /// </summary>
    public void ChangeBranch(Guid branchId)
    {
        if (branchId == Guid.Empty)
        {
            throw new ArgumentException("A customer requires a branch (A-2).", nameof(branchId));
        }

        BranchId = branchId;
    }

    /// <summary>An all-whitespace phone is an absent phone, not a stored blank.</summary>
    private static string? NormalizePhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}
