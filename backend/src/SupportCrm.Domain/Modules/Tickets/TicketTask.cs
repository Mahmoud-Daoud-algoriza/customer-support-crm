namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// A due-dated to-do attached to a ticket — requirements §4.3, T2-C, docs/data-model.md §2.10.
///
/// <para>
/// <b>Always attached to a ticket.</b> Standalone personal to-dos are out of scope (§2.10, the
/// intake's Out of scope), which is why <see cref="TicketId"/> is required and there is no factory
/// without one.
/// </para>
///
/// <para>
/// <b>Deliberately minimal.</b> No calendar, no recurrence, no push or email reminders — none of
/// them exists anywhere in the model, and none may be added here (T2-C, T3-A).
/// </para>
///
/// <para>
/// <b>Mutable, because completion is the point</b> (§2.10) — but through <b>one</b> mutator only.
/// <see cref="SetDone"/> is the single place <see cref="CompletedAt"/> is ever written, which is how
/// §5 constraint 23 — <em>"<c>completedAt</c> is set exactly when <c>isDone</c> is true"</em> —
/// becomes structural rather than a rule a caller must remember. Title, due date and assignee are
/// fixed at creation: no approved document defines an edit path for them, and inventing one would
/// be scope this story does not have.
/// </para>
///
/// <para>
/// <b>The assignee rule lives outside this type</b> because it cannot see it: an assignee must be an
/// active staff user in the ticket's department (§5 constraint 10), enforced in the Application
/// layer's <c>TicketTaskService</c> through the same check and the same
/// <c>assignee-out-of-department</c> slug Story 05's assignment uses.
/// </para>
///
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class TicketTask
{
    private TicketTask()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>The owner. Inherits the ticket's scope (docs/data-model.md §2.10).</summary>
    public Guid TicketId { get; private set; }

    /// <summary>The <c>Name</c> tier of docs/data-model.md §6.1.</summary>
    public string Title { get; private set; } = default!;

    /// <summary><b>Required</b> — the intake requires a due date (§2.10).</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>The agent responsible. Required (§2.10).</summary>
    public Guid AssignedUserId { get; private set; }

    public bool IsDone { get; private set; }

    /// <summary>
    /// Set <b>if and only if</b> <see cref="IsDone"/> is true (§5 constraint 23).
    /// <b>Server-set, never accepted from a client</b> (docs/api-design.md §5.6, §7, AP-10).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static TicketTask Create(
        Guid id,
        Guid ticketId,
        string title,
        DateTimeOffset dueAt,
        Guid assignedUserId,
        Guid createdByUserId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A task requires a ticket.", nameof(ticketId));
        }

        if (assignedUserId == Guid.Empty)
        {
            throw new ArgumentException("A task requires an assignee.", nameof(assignedUserId));
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("A task requires its creator.", nameof(createdByUserId));
        }

        return new TicketTask
        {
            Id = id,
            TicketId = ticketId,
            Title = title.Trim(),
            DueAt = dueAt,
            AssignedUserId = assignedUserId,
            IsDone = false,
            CompletedAt = null,
            CreatedByUserId = createdByUserId,
            CreatedAt = createdAt,
        };
    }

    /// <summary>
    /// Marks the task done or not done. <b>The two fields move together and cannot be moved
    /// apart</b> — docs/data-model.md §5 constraint 23, expressed as the only assignment path.
    /// Un-doing a task clears <see cref="CompletedAt"/>, which is the half a nullable column would
    /// otherwise leave stale.
    /// </summary>
    public void SetDone(bool done, DateTimeOffset now)
    {
        IsDone = done;
        CompletedAt = done ? now : null;   // data-model §5 constraint 23 — set iff IsDone
    }
}
