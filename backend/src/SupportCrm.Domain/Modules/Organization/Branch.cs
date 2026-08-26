namespace SupportCrm.Domain.Modules.Organization;

/// <summary>
/// A location, used for <b>reporting and filtering only</b> (T2-K, A-2, docs/data-model.md §2.3).
/// <para>
/// <b>The invariant that matters is a negative one: <c>Branch</c> never appears in an authorization
/// predicate anywhere in this codebase</b> (docs/data-model.md §5 constraint 6,
/// docs/architecture.md §4.3). Ticket visibility is department-based. <c>Ticket</c> deliberately
/// has no <c>BranchId</c>; a ticket's branch is derived <c>Ticket -> Customer -> Branch</c>, and
/// its absence makes misuse impossible rather than merely discouraged.
/// </para>
/// <para>
/// Should a branch-level access rule ever be required, that contradicts A-2 and is a
/// <b>scope change</b> to be raised against docs/product-scope.md first — not a model tweak.
/// </para>
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class Branch
{
    private Branch()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>Unique across the organization (docs/data-model.md §2.3).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// Branches are seeded and configured, never created through an admin UI (T2-I), so the id is
    /// supplied by the caller and is a deterministic constant in the seeder.
    /// </summary>
    public static Branch Create(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Branch { Id = id, Name = name.Trim() };
    }
}
