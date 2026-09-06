using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>Ticket-attached tasks and reminders</b> — requirements §4.3, T2-C, docs/data-model.md §2.10,
/// docs/api-design.md §5.6.
///
/// <para>
/// <b>Deliberately minimal.</b> No calendar, no recurrence, no push or email reminders — the intake
/// excludes all three, and none exists anywhere in the model. A task is a title, a due date, an
/// assignee and a done flag.
/// </para>
///
/// <para>
/// <b>Every method scopes through the ticket</b>, never through the task id alone: a task inherits
/// its ticket's visibility (§2.10), so an out-of-department agent gets <c>404</c> for the ticket
/// before the task is ever looked up (AP-4). There is no path here that loads a task by id without
/// first proving the caller may see its ticket.
/// </para>
///
/// <para>
/// <b>Tasks are staff-only.</b> No <c>/portal</c> counterpart exists and none may be added (AP-5);
/// the staff controller's <c>RequireAgent</c> gate is the role half.
/// </para>
/// </summary>
public sealed class TicketTaskService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    /// <summary>
    /// <c>GET /tickets/{id}/tasks</c> — paged, <b>soonest due first</b>.
    ///
    /// <para>
    /// Due order is the order the work is actually done in, and it is what makes an overdue row
    /// visible at the top of a short list rather than buried. No approved document fixes a default
    /// sort for this endpoint; due-ascending is the only ordering the region's own purpose implies,
    /// and it is stated here rather than left to the database (finding <b>I-37</b>).
    /// </para>
    /// </summary>
    public async Task<PagedResult<TicketTaskDto>> ListForTicketAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        _ = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        var (pageNumber, pageSize) = page.Normalize();

        return await db.TicketTasks.AsNoTracking()
            .Where(t => t.TicketId == ticketId)
            .OrderBy(t => t.DueAt)
            .ThenBy(t => t.Id) // a stable tiebreak, so paging is meaningful within one timestamp
            .Select(t => ToDto(t, db))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>POST /tickets/{id}/tasks</c>.
    ///
    /// <para>
    /// <b>The assignee must be an active staff user in the ticket's department</b> — the same rule
    /// and the same <c>422 assignee-out-of-department</c> slug Story 05's ticket assignment uses,
    /// read from <see cref="TicketAssigneePolicy"/> so there is one implementation
    /// (§5 constraint 10). A task assigned to someone who cannot open its ticket would be a to-do
    /// nobody can action.
    /// </para>
    ///
    /// <para>
    /// <b>A task is created not-done</b>, so <c>completedAt</c> starts null — the entity's factory
    /// fixes both, and neither is a parameter of the request (AP-10).
    /// </para>
    ///
    /// <para>
    /// <b>The terminal guard applies.</b> <c>Closed</c> and <c>Cancelled</c> accept no further work
    /// (§5 constraint 8, A-5) — adding a to-do to a closed ticket would promise action on something
    /// the lifecycle has ended.
    /// </para>
    /// </summary>
    public async Task<TicketTaskDto> CreateAsync(
        Guid ticketId, CreateTicketTaskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationException("A task title is required.");
        }

        // Scope first — missing or out of scope is one indistinguishable 404 (AP-4).
        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new ConflictException(TicketTerminal, TicketTerminalMessage);
        }

        var assigneeId = request.AssignedUserId!.Value;

        var assignee = await db.Users.FirstOrDefaultAsync(u => u.Id == assigneeId, ct);

        if (assignee is null || !TicketAssigneePolicy.IsEligible(assignee, ticket))
        {
            throw new UnprocessableException(
                TicketAssigneePolicy.OutOfDepartment, TicketAssigneePolicy.OutOfDepartmentMessage);
        }

        var task = TicketTask.Create(
            Guid.NewGuid(),
            ticket.Id,
            request.Title,
            request.DueAt!.Value,
            assigneeId,
            currentUser.Id,
            clock.GetUtcNow());

        db.TicketTasks.Add(task);

        await db.SaveChangesAsync(ct);

        // No activity row: docs/data-model.md §2.7 enumerates twelve activity types and none of them
        // is a task. Writing one would be a thirteenth type — a data-model change, not this story's.
        return new TicketTaskDto(
            task.Id,
            task.TicketId,
            task.Title,
            task.DueAt,
            new UserSummaryDto(assignee.Id, assignee.DisplayName),
            task.IsDone,
            task.CompletedAt,
            new UserSummaryDto(currentUser.Id, currentUser.DisplayName),
            task.CreatedAt);
    }

    /// <summary>
    /// <c>PATCH /tickets/{id}/tasks/{taskId}</c> — <c>{ isDone }</c>.
    ///
    /// <para>
    /// <b><c>completedAt</c> is server-set</b> (docs/api-design.md §5.6, §7): it is not a member of
    /// the request model, so a body carrying it is a <c>400</c> (AP-10), and the only code that ever
    /// writes it is <see cref="TicketTask.SetDone"/>. Marking a task done stamps it; un-doing the
    /// task clears it. That is §5 constraint 23, and it holds because there is one write path.
    /// </para>
    ///
    /// <para>
    /// <b>Scoped through the ticket, then filtered by ticket id.</b> A task id that belongs to a
    /// different ticket is <c>404</c> even when the caller can see both — the route says which
    /// ticket, and a task is addressed within it.
    /// </para>
    /// </summary>
    public async Task<TicketTaskDto> SetDoneAsync(
        Guid ticketId, Guid taskId, UpdateTicketTaskRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        var task = await db.TicketTasks.FirstOrDefaultAsync(
            t => t.Id == taskId && t.TicketId == ticket.Id, ct)
            ?? throw new NotFoundException(TaskNotFound);

        task.SetDone(request.IsDone!.Value, clock.GetUtcNow());

        await db.SaveChangesAsync(ct);

        var assignee = await db.Users
            .Where(u => u.Id == task.AssignedUserId)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
            .FirstAsync(ct);

        var createdBy = await db.Users
            .Where(u => u.Id == task.CreatedByUserId)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
            .FirstAsync(ct);

        return new TicketTaskDto(
            task.Id, task.TicketId, task.Title, task.DueAt, assignee,
            task.IsDone, task.CompletedAt, createdBy, task.CreatedAt);
    }

    /// <summary>
    /// The caller's own open tasks across every ticket they can see — <b>the read the agent
    /// dashboard's task region needs</b> (docs/ui-design.md §5.1, §13, this story's AC 2), ordered
    /// soonest-due first so overdue work leads.
    ///
    /// <para>
    /// ⛔ <b>S9-1: no endpoint publishes this. Do not add a route until the decision is recorded.</b>
    /// </para>
    ///
    /// <para>
    /// Four approved documents assume a cross-ticket task list — <c>ui-design.md</c> §5.1 and §13,
    /// this story's acceptance criterion, and <c>data-model.md</c> §6's index
    /// <c>TicketTask(assignedUserId, isDone, dueAt)</c>, which exists for exactly this query and is
    /// created. <b><c>api-design.md</c> §5.6 publishes none</b>: its three task endpoints are all
    /// ticket-scoped. The contract and the design documents disagree, and resolving that is a
    /// product decision, not an implementation one.
    /// </para>
    ///
    /// <para>
    /// <b>The method is written and left unreferenced on purpose</b>, exactly as this story's plan
    /// task 4 directs. It is the shape the decision would need, so taking Option A is publishing a
    /// route that calls this — not writing the query. Taking Option B deletes this method and the
    /// marker in <c>agent-queue.component.ts</c>. <b>Do not call it from a controller</b> until one
    /// of the two is recorded.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<TicketTaskDto>> ListForUserAsync(
        Guid assignedUserId, bool openOnly, CancellationToken ct)
    {
        // S9-1: no endpoint publishes this. Do not add a route until the decision is recorded.
        //
        // The ticket join is not decoration: it applies TicketScope, so this read can never widen
        // what its caller may see. A cross-ticket list is exactly where that would be easy to lose.
        var visibleTickets = db.Tickets.AsNoTracking().ForCaller(currentUser).Select(t => t.Id);

        return await db.TicketTasks.AsNoTracking()
            .Where(t => t.AssignedUserId == assignedUserId)
            .Where(t => !openOnly || !t.IsDone)
            .Where(t => visibleTickets.Contains(t.TicketId))
            .OrderBy(t => t.DueAt)
            .ThenBy(t => t.Id)
            .Select(t => ToDto(t, db))
            .ToListAsync(ct);
    }

    /// <summary>
    /// The <c>Task</c> projection of docs/api-design.md §6.4, written once so the three reads cannot
    /// drift. Both user summaries are resolved in the query rather than in memory.
    /// </summary>
    private static TicketTaskDto ToDto(TicketTask t, IApplicationDbContext db) =>
        new(
            t.Id,
            t.TicketId,
            t.Title,
            t.DueAt,
            db.Users.Where(u => u.Id == t.AssignedUserId)
                .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
            t.IsDone,
            t.CompletedAt,
            db.Users.Where(u => u.Id == t.CreatedByUserId)
                .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
            t.CreatedAt);

    /// <summary>
    /// One message for "no such task" and "not on this ticket". A distinct message per case would
    /// undo AP-4 after the scoped load had got it right.
    /// </summary>
    private const string TaskNotFound = "Task not found.";

    /// <summary>Reused, not minted — Story 07's slug for the same §5 constraint 8 rule.</summary>
    private const string TicketTerminal = "ticket-terminal";

    private const string TicketTerminalMessage =
        "This ticket is closed. No further tasks are accepted.";
}
