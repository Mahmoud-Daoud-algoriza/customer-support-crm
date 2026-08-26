namespace SupportCrm.Domain.Modules.Organization;

/// <summary>
/// The routing and permission boundary of A-2 and requirements §12 (docs/data-model.md §2.2).
/// A ticket belongs to exactly one; a staff user belongs to exactly one.
/// <para>
/// Plain C# with no EF attributes (AD-4). It carries <b>no</b> collection navigation to
/// <c>Ticket</c> or <c>User</c>: nothing reads a department's whole ticket set, and a
/// <c>Department.Tickets</c> property would invite exactly that.
/// </para>
/// </summary>
public sealed class Department
{
    private Department()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>Unique across the organization (docs/data-model.md §2.2).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// The escalation recipient for Story 09 — <b>optional</b>, because a department can exist
    /// before anyone is appointed to it.
    /// <para>
    /// <b>OQ-3 is open:</b> when this is null, the sources do not say who — if anyone — is notified
    /// on an SLA breach. No fallback is invented here, in the seeder, or anywhere else: not "all
    /// Managers", not "the Administrators", and not a silently dropped notification. Each is a
    /// product decision. The breach flag and the priority raise are unaffected and must still occur
    /// (docs/data-model.md §2.2). <b>Must be resolved before Story 09 is implemented.</b>
    /// </para>
    /// </summary>
    public Guid? ManagerUserId { get; private set; }

    /// <summary>
    /// Departments are seeded and configured, never created through an admin UI (T2-I), so the id
    /// is supplied by the caller and is a deterministic constant in the seeder.
    /// </summary>
    public static Department Create(Guid id, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Department { Id = id, Name = name.Trim() };
    }

    /// <summary>
    /// Appoints the escalation recipient.
    /// <para>
    /// The invariant — the user must exist, be active, and hold role <c>Manager</c> or
    /// <c>Administrator</c> — is an <b>Application-layer</b> rule, not a foreign key
    /// (docs/data-model.md §2.2). It is checked by <c>DepartmentValidator</c>, which is why this
    /// method takes an id and does not re-check it: the Domain layer cannot see other rows.
    /// </para>
    /// </summary>
    public void AssignManager(Guid managerUserId)
    {
        if (managerUserId == Guid.Empty)
        {
            throw new ArgumentException("A manager id is required.", nameof(managerUserId));
        }

        ManagerUserId = managerUserId;
    }
}
