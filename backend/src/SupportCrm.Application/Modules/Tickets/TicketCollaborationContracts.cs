using System.ComponentModel.DataAnnotations;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <c>InternalNote</c> — docs/api-design.md §6.4, member for member:
/// <c>{ "id", "ticketId", "author": { UserSummary }, "body", "createdAt" }</c>.
///
/// <para>
/// <b>Returned only by <c>/tickets/{id}/internal-notes</c>, which no portal path reaches</b>
/// (T2-C, AP-5). There is deliberately <b>no portal counterpart of this type</b> — not a narrowed
/// one, not one with fields omitted. A narrowed DTO would imply a portal read exists to narrow,
/// and the whole T2-C design is that none does (docs/data-model.md §2.9).
/// </para>
/// </summary>
public sealed record InternalNoteDto(
    Guid Id,
    Guid TicketId,
    UserSummaryDto Author,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>
/// The body of <c>POST /tickets/{id}/internal-notes</c> — docs/api-design.md §5.6:
/// <c>{ "body": "..." }</c>.
///
/// <para>
/// <b>One property, and deliberately no others.</b> No author, no timestamp and — above all — <b>no
/// <c>visibility</c></b>: a note is internal by virtue of being a note, so a visibility field would
/// be a way to ask for a customer-visible one, which is exactly the thing T2-C forbids. All three
/// are server-derived (docs/api-design.md §7), and a body carrying one is a <c>400</c> because
/// <c>UnmappedMemberHandling.Disallow</c> is set once on the MVC JSON options (<b>AP-10</b>).
/// </para>
/// </summary>
public sealed record PostInternalNoteRequest
{
    [Required] public string Body { get; init; } = default!;
}

/// <summary>
/// <c>Task</c> — docs/api-design.md §6.4, member for member:
/// <c>{ "id", "ticketId", "title", "dueAt", "assignee": { UserSummary }, "isDone", "completedAt",
/// "createdBy": { UserSummary }, "createdAt" }</c>.
///
/// <para>
/// <b><see cref="CompletedAt"/> is server-set</b> (docs/api-design.md §5.6, §7): it appears in this
/// <em>response</em> and in no request model, so there is no path by which a client value could
/// reach it (<b>AP-10</b>).
/// </para>
///
/// <para>
/// <b>Staff only.</b> A task is never surfaced to a customer (docs/data-model.md §2.10), and no
/// portal path returns this type.
/// </para>
/// </summary>
public sealed record TicketTaskDto(
    Guid Id,
    Guid TicketId,
    string Title,
    DateTimeOffset DueAt,
    UserSummaryDto Assignee,
    bool IsDone,
    DateTimeOffset? CompletedAt,
    UserSummaryDto CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>
/// The body of <c>POST /tickets/{id}/tasks</c> — docs/api-design.md §5.6:
/// <c>{ title, dueAt, assignedUserId }</c>.
///
/// <para>
/// <b>All three are required</b> (docs/data-model.md §2.10): the intake requires a due date, and a
/// task with no assignee has nobody to appear for. <b><c>isDone</c> and <c>completedAt</c> are
/// absent</b> — a task is created not-done, and completion goes through the <c>PATCH</c> below.
/// </para>
/// </summary>
public sealed record CreateTicketTaskRequest
{
    [Required] public string Title { get; init; } = default!;

    [Required] public DateTimeOffset? DueAt { get; init; }

    [Required] public Guid? AssignedUserId { get; init; }
}

/// <summary>
/// The body of <c>PATCH /tickets/{id}/tasks/{taskId}</c> — docs/api-design.md §5.6:
/// <c>{ isDone }</c>, and <em>"<c>completedAt</c> is server-set"</em>.
///
/// <para>
/// <b><c>completedAt</c> is deliberately not a member</b>, and its absence is the enforcement: a
/// body carrying it is a <c>400</c> rather than accepted-and-ignored (<b>AP-10</b>, §7). Constraint
/// 23 is the server's to keep, and a client that could set the timestamp could break it.
/// </para>
///
/// <para>
/// <b>Title, due date and assignee are not patchable.</b> No approved document defines an edit path
/// for them — §5.6 publishes <c>{ isDone }</c> and nothing more — so adding one would be invented
/// scope.
/// </para>
/// </summary>
public sealed record UpdateTicketTaskRequest
{
    [Required] public bool? IsDone { get; init; }
}
