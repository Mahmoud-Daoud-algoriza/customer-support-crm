namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The unit of support work — requirements §2, T1-B, docs/data-model.md §2.6.
///
/// <para>
/// <b><see cref="DepartmentId"/> is the authorization boundary</b> (A-2, docs/architecture.md §4.3).
/// Every ticket read and write is narrowed by it, in exactly one place — <c>TicketScope</c>.
/// </para>
///
/// <para>
/// <b>There is no branch column here, and there must never be one.</b> A ticket's branch is derived
/// <c>Ticket → Customer → Branch</c> (docs/data-model.md §2.3). Adding one would put a branch value
/// within arm's reach of the scoping helper, where A-2 and §5 constraint 6 forbid its use — an
/// agent sees in-department tickets regardless of the customer's branch. A test asserts the absence
/// at the type level, not merely at the schema level.
/// </para>
///
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class Ticket
{
    private Ticket()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>Exactly one customer (docs/data-model.md §2.6, the <c>ticket-core</c> intake).</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Exactly one department, and <b>the authorization edge</b> (A-2). Set at creation from the
    /// category → department map, or directly by a staff creator — A-14 makes the mapping a default
    /// and not a cage (docs/data-model.md §5 constraint 11a).
    /// </summary>
    public Guid DepartmentId { get; private set; }

    public string Subject { get; private set; } = default!;

    /// <summary>
    /// The originating text — the web-form body, or what the agent typed. <b>Replies are
    /// <c>TicketMessage</c> rows, not edits to this</b> (docs/data-model.md §2.6, Story 07).
    /// </summary>
    public string Description { get; private set; } = default!;

    /// <summary>
    /// Validated against the <b>configured</b> flat list, never a table (A-6,
    /// docs/architecture.md §6.3). The Domain stores the code; the Application layer is where the
    /// configured list can be seen, so validation lives there (§5 constraint 11).
    /// </summary>
    public string CategoryCode { get; private set; } = default!;

    public TicketPriority Priority { get; private set; }

    /// <summary>
    /// <b>Written by exactly one method: <see cref="TransitionTo"/></b> (Story 06). There is no
    /// setter and no second path, so no caller can bypass the A-5 state machine — not even
    /// accidentally. Created tickets start at <see cref="TicketStatus.New"/>.
    /// </summary>
    public TicketStatus Status { get; private set; }

    /// <summary>
    /// The current assignee (DM-2 — a field, not an assignment history table).
    /// <para>
    /// <b>May be set while <see cref="Status"/> is <c>New</c></b> — assignment is not the start of
    /// work (A-18, invariant 2a). Nothing may infer status from the presence of an assignee, or an
    /// assignee from the status.
    /// </para>
    /// </summary>
    public Guid? AssignedUserId { get; private set; }

    /// <summary>
    /// <b>Customer input only</b> (A-17). An urgency indication that does <b>not</b> set
    /// <see cref="Priority"/>; agents and the AI suggestion may use it when deciding priority, so
    /// it is stored and stays visible after creation. The staff create endpoint does not accept it
    /// — Story 07's portal creation sets it.
    /// </summary>
    public bool IsUrgent { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    /// <summary><b>The SLA clock origin</b> (A-3, docs/data-model.md §2.6).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Computed once at creation from <see cref="CreatedAt"/> and the priority the ticket had then
    /// (A-3). <b>Required and non-null</b>, which is why this story computes it rather than Story 09.
    /// <para>
    /// <b>A later priority change does not move it</b> — the timestamps freeze (<b>A-20</b>,
    /// 2026-08-30, closing OQ-2). The rule has one home: <c>SlaClock.OnPriorityChanged</c>.
    /// </para>
    /// </summary>
    public DateTimeOffset FirstResponseDueAt { get; private set; }

    /// <summary>Same basis and same rule as <see cref="FirstResponseDueAt"/> (A-3, A-20).</summary>
    public DateTimeOffset ResolutionDueAt { get; private set; }

    /// <summary>
    /// Set once, on the first outbound message (Story 07). <b>May remain null on a resolved
    /// ticket</b> — finding PF-5, and not a defect.
    /// </summary>
    public DateTimeOffset? FirstRespondedAt { get; private set; }

    /// <summary>Set on entering <c>Resolved</c> — Story 06's lifecycle side effect.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Set on entering <c>Closed</c> or <c>Cancelled</c> — Story 06's.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>
    /// <b>Latching</b> — once true it never returns to false (docs/data-model.md §2.6 invariant 5),
    /// so history and SLA reporting stay honest even if priority later changes. Story 09 sets it;
    /// nothing in this story does, and no mutator clears it.
    /// </summary>
    public bool FirstResponseBreached { get; private set; }

    /// <summary>Latching, same rule as <see cref="FirstResponseBreached"/>. Story 09 sets it.</summary>
    public bool ResolutionBreached { get; private set; }

    /// <summary>
    /// The only way a ticket comes into existence.
    /// <para>
    /// The due timestamps are <b>parameters, not computed here</b>: A-3's arithmetic needs the
    /// configured per-priority hours, which are configuration and therefore not visible to the
    /// Domain. <c>SlaClock.ComputeAtCreation</c> does the arithmetic and the Application layer
    /// supplies the targets — so this entity cannot be constructed with the clock unset.
    /// </para>
    /// <para>
    /// <see cref="Status"/> is always <c>New</c> and is not a parameter (A-5); <see cref="IsUrgent"/>
    /// defaults false and is set only by the portal path (A-17); the breach flags default false and
    /// belong to Story 09.
    /// </para>
    /// </summary>
    public static Ticket Create(
        Guid id,
        Guid customerId,
        Guid departmentId,
        string subject,
        string description,
        string categoryCode,
        TicketPriority priority,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset firstResponseDueAt,
        DateTimeOffset resolutionDueAt,
        bool isUrgent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A ticket requires a customer.", nameof(customerId));
        }

        // A-2: the authorization boundary cannot be empty, or the scoping helper would compare
        // against Guid.Empty and quietly match nothing — a failure that reads as "no tickets".
        if (departmentId == Guid.Empty)
        {
            throw new ArgumentException("A ticket requires a department (A-2).", nameof(departmentId));
        }

        return new Ticket
        {
            Id = id,
            CustomerId = customerId,
            DepartmentId = departmentId,
            Subject = subject.Trim(),
            Description = description.Trim(),
            CategoryCode = categoryCode.Trim(),
            Priority = priority,
            Status = TicketStatus.New,
            AssignedUserId = null,
            IsUrgent = isUrgent,
            CreatedByUserId = createdByUserId,
            CreatedAt = createdAt,
            FirstResponseDueAt = firstResponseDueAt,
            ResolutionDueAt = resolutionDueAt,
            FirstRespondedAt = null,
            ResolvedAt = null,
            ClosedAt = null,
            FirstResponseBreached = false,
            ResolutionBreached = false,
        };
    }

    /// <summary>
    /// Assignment is <b>not</b> the start of work (A-18): this sets the assignee and
    /// <b>does not touch <see cref="Status"/></b>. A ticket assigned while `New` stays `New` until
    /// an agent deliberately starts work, which is Story 06's transition.
    /// <para>
    /// That the assignee is an <b>active staff user in this ticket's department</b> is a cross-row
    /// rule this entity cannot see (docs/data-model.md §5 constraint 10); <c>TicketService</c>
    /// enforces it and answers <c>422 assignee-out-of-department</c>.
    /// </para>
    /// </summary>
    public void Assign(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An assignee is required.", nameof(userId));
        }

        AssignedUserId = userId;
    }

    /// <summary>
    /// Changes the priority. <b>The SLA due timestamps do not move</b> — A-20 freezes them at
    /// creation, and the rule lives in <c>SlaClock.OnPriorityChanged</c>, which the Application
    /// layer calls on the same path. This method deliberately does not touch them itself: one rule,
    /// one home, so Stories 06 and 09 reuse it rather than restating it.
    /// </summary>
    public void ChangePriority(TicketPriority priority) => Priority = priority;

    /// <summary>
    /// Changes the category. <b>It does not re-derive <see cref="DepartmentId"/>.</b> A-14 maps
    /// category to department <em>at creation, before assignment</em>; no approved source moves a
    /// ticket between departments — and therefore out of its assignee's sight — as a side effect of
    /// a category edit. Reassignment across departments is a Manager's deliberate act.
    /// </summary>
    public void ChangeCategory(string categoryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryCode);
        CategoryCode = categoryCode.Trim();
    }

    /// <summary>
    /// <b>The only way a ticket's status ever changes</b> — the guarded mutator Story 05
    /// deliberately withheld until A-5's graph existed (Story 06 task 1).
    ///
    /// <para>
    /// <b>The guard is here, not in a service.</b> A-5 is a Domain rule, so an illegal edge is
    /// refused however the ticket was reached — endpoint, seeder, or a caller written later. There
    /// is no property setter and no second path: <see cref="Status"/> is <c>private set</c>, and
    /// this method is the only writer of it in the codebase.
    /// </para>
    ///
    /// <para>
    /// <b>Legality only.</b> Whether the <em>caller</em> may invoke a legal edge is A-16, and this
    /// entity cannot see the caller — <c>TransitionAuthority</c> in the Application layer owns that
    /// and answers <c>403</c> before this method is reached, so the two failures stay
    /// distinguishable (docs/api-design.md §5.6).
    /// </para>
    ///
    /// <para>
    /// <b><see cref="ResolvedAt"/> and <see cref="ClosedAt"/> are lifecycle side effects</b>, set
    /// here and <b>never accepted from a client</b> (docs/api-design.md §7, AP-10). <c>Cancelled</c>
    /// stamps <see cref="ClosedAt"/> too: docs/data-model.md §2.6 treats it as the closing time of a
    /// ticket that reached a terminal status, and leaving it null would make a cancelled ticket
    /// indistinguishable from an open one in any "when did this end" query.
    /// </para>
    /// </summary>
    /// <exception cref="IllegalTransitionException">The edge is not in A-5's graph.</exception>
    public void TransitionTo(TicketStatus target, DateTimeOffset now)
    {
        if (!TicketLifecycle.IsLegal(Status, target))
        {
            throw new IllegalTransitionException(Status, target, TicketLifecycle.LegalFrom(Status));
        }

        Status = target;

        if (target is TicketStatus.Resolved)
        {
            ResolvedAt = now;
        }

        if (target is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            ClosedAt = now;
        }
    }
}
