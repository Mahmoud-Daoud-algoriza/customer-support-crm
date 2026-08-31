using System.ComponentModel.DataAnnotations;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <c>Ticket (staff)</c> — docs/api-design.md §6.4. The shape of <c>GET /tickets/{id}</c>.
/// <para>
/// <see cref="Assignee"/> is null until assignment <b>and may be non-null while
/// <see cref="Status"/> is <c>New</c></b> — assignment is not the start of work (A-18). Nothing
/// reading this payload may infer one from the other.
/// </para>
/// <para>
/// <see cref="FirstRespondedAt"/> stays null until the first outbound message and <b>may remain
/// null on a resolved ticket</b> (finding PF-5). That is not a defect and must not be rendered as
/// one.
/// </para>
/// </summary>
public sealed record TicketDto(
    Guid Id,
    string Subject,
    string Description,
    TicketCustomerDto Customer,
    Guid DepartmentId,
    string CategoryCode,
    string Priority,
    string Status,
    bool IsUrgent,
    UserSummaryDto? Assignee,
    UserSummaryDto CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset FirstResponseDueAt,
    DateTimeOffset ResolutionDueAt,
    DateTimeOffset? FirstRespondedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    bool FirstResponseBreached,
    bool ResolutionBreached);

/// <summary>
/// The customer as the staff ticket payload carries them — docs/api-design.md §6.4:
/// <c>{ id, fullName, email }</c>, and nothing more. The full profile is <c>GET /customers/{id}</c>.
/// <para>
/// <b>There is no branch here</b>, and adding one would put a branch value on the ticket payload,
/// where A-2 keeps it out of ticket concerns entirely.
/// </para>
/// </summary>
public sealed record TicketCustomerDto(Guid Id, string FullName, string Email);

/// <summary>
/// <c>TicketListItem</c> — the row shape of <c>GET /tickets</c> (docs/api-design.md §6.4).
/// <para>
/// <b>Everything the queue renders (docs/ui-design.md §5.1) and nothing more.</b> It deliberately
/// omits <c>description</c>, <c>isUrgent</c> and the lifecycle timestamps: a list must not ship
/// every ticket's full text, and the plan says not to return the full <see cref="TicketDto"/> here.
/// </para>
/// </summary>
public sealed record TicketListItemDto(
    Guid Id,
    string Subject,
    TicketListCustomerDto Customer,
    string Status,
    string Priority,
    string CategoryCode,
    Guid DepartmentId,
    UserSummaryDto? Assignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset ResolutionDueAt,
    bool FirstResponseBreached,
    bool ResolutionBreached);

/// <summary>
/// The list row's customer — <c>{ id, fullName }</c> (docs/api-design.md §6.4). No email: the queue
/// does not render one, and a list should not ship contact details it will not show.
/// </summary>
public sealed record TicketListCustomerDto(Guid Id, string FullName);

/// <summary>
/// <c>POST /tickets</c> — docs/api-design.md §5.6.
///
/// <para>
/// <b><see cref="DepartmentId"/> is optional.</b> Omitted, it is derived from the category →
/// department map (A-14); supplied by staff, it overrides — A-14 makes the mapping a default and
/// <em>not a cage for agents</em>. Either way the column is stored, not computed on read
/// (docs/data-model.md §5 constraint 11a).
/// </para>
///
/// <para>
/// <b><c>isUrgent</c> is not a property here, and its absence is the enforcement.</b> It is customer
/// input only (A-17, docs/api-design.md §7): an agent creating a ticket on behalf of a customer sets
/// priority directly. A request body carrying it is a <c>400</c> rather than being accepted and
/// ignored, because <c>UnmappedMemberHandling.Disallow</c> is set once on the MVC JSON options
/// (AP-10, finding I-9). Story 07's portal endpoint has its own request model that does accept it.
/// </para>
///
/// <para>
/// <c>status</c>, the SLA due dates and the breach flags are absent for the same reason — all are
/// server-derived (§7). <c>status</c> is never a create or patch field; transitions are Story 06's
/// dedicated endpoint (AP-1).
/// </para>
/// </summary>
public sealed record CreateTicketRequest
{
    [Required] public Guid? CustomerId { get; init; }

    [Required, MaxLength(512)] public string Subject { get; init; } = default!;

    [Required] public string Description { get; init; } = default!;

    [Required, MaxLength(64)] public string CategoryCode { get; init; } = default!;

    [Required] public string Priority { get; init; } = default!;

    /// <summary>Optional — see the remarks. Supplied by staff, it overrides the A-14 mapping.</summary>
    public Guid? DepartmentId { get; init; }
}

/// <summary>
/// <c>PATCH /tickets/{id}</c> — docs/api-design.md §5.6. <b>Exactly two patchable fields.</b>
/// <para>
/// Both are nullable because absent means "leave unchanged" — a PATCH carries only what is changing.
/// </para>
/// <para>
/// <b><c>status</c> is not here and must never be</b> (AP-1, docs/api-design.md §7): a status change
/// goes through Story 06's transition endpoint, which enforces A-5 legality and A-16 authority. A
/// patchable status would route around both. <c>assignedUserId</c> is not here either — assignment
/// is its own endpoint, so it can validate the department and write its activity row.
/// </para>
/// <para>
/// <b>Changing <c>priority</c> does not move the SLA due dates</b> (<b>A-20</b>). The rule lives in
/// <c>SlaClock.OnPriorityChanged</c>, which <c>TicketService.PatchAsync</c> calls.
/// </para>
/// </summary>
public sealed record PatchTicketRequest
{
    [MaxLength(64)] public string? CategoryCode { get; init; }

    public string? Priority { get; init; }
}

/// <summary>
/// <c>POST /tickets/{id}/assignment</c> — docs/api-design.md §5.6. Assign or reassign.
/// <para>
/// One field. The assignee must be an <b>active staff user in the ticket's department</b>
/// (docs/data-model.md §5 constraint 10); a violation is <c>422 assignee-out-of-department</c>.
/// </para>
/// </summary>
public sealed record AssignTicketRequest
{
    [Required] public Guid? AssignedUserId { get; init; }
}

/// <summary>
/// The body of <c>POST /tickets/{id}/transition</c> — docs/api-design.md §5.6,
/// <c>{ "targetStatus": "Resolved" }</c>.
///
/// <para>
/// <b>One property, and deliberately no others.</b> No actor, no timestamp, no <c>resolvedAt</c> or
/// <c>closedAt</c>: those are lifecycle side effects the server sets (docs/api-design.md §7), and a
/// body carrying one is a <c>400</c> because <c>UnmappedMemberHandling.Disallow</c> is on
/// (AP-10, finding I-9). No comment or reason field either — no approved source defines one.
/// </para>
/// </summary>
public sealed record TransitionTicketRequest
{
    [Required] public string TargetStatus { get; init; } = default!;
}

/// <summary>
/// Filters for <c>GET /tickets</c> — docs/api-design.md §5.6, and exactly those seven.
///
/// <para>
/// <b><see cref="AssigneeId"/> accepts the literal <c>me</c></b>, which is what produces the agent's
/// own queue (§5.6). It is a string for that reason, parsed in the service.
/// </para>
///
/// <para>
/// <b><see cref="DepartmentId"/> narrows; it can never widen.</b> It is applied <em>after</em>
/// <c>TicketScope.ForCaller</c>, so an agent supplying another department's id gets an empty page —
/// not an error, and not another department's rows (docs/api-design.md §4.3).
/// </para>
///
/// <para>
/// <b>There is no branch filter, and there must never be one</b> (A-2, T2-K, docs/ui-design.md §5.2).
/// </para>
/// </summary>
public sealed record TicketListFilter
{
    public string? Status { get; init; }

    public string? Priority { get; init; }

    public string? CategoryCode { get; init; }

    /// <summary>A user id, or the literal <c>me</c> (docs/api-design.md §5.6).</summary>
    public string? AssigneeId { get; init; }

    public Guid? DepartmentId { get; init; }

    /// <summary>Either breach flag set. Latching, so it stays true once tripped (invariant 5).</summary>
    public bool? Breached { get; init; }

    /// <summary>Free-text match over subject and description.</summary>
    public string? Q { get; init; }
}

/// <summary>
/// Maps a configured priority string onto <see cref="TicketPriority"/>, and refuses anything else.
/// <para>
/// A-6 fixes the four levels; configuration supplies their SLA hours, not the levels themselves.
/// An unknown value is a <c>400</c> naming what is allowed, rather than a silent default — the
/// <c>ticket-core</c> acceptance criterion requires an unknown value to be <em>rejected</em>.
/// </para>
/// </summary>
public static class TicketPriorityParser
{
    public static TicketPriority Parse(string value)
    {
        if (Enum.TryParse<TicketPriority>(value, ignoreCase: true, out var priority) &&
            Enum.IsDefined(priority))
        {
            return priority;
        }

        throw new Abstractions.ValidationException(
            $"Unknown priority '{value}'. Allowed values: {string.Join(", ", Enum.GetNames<TicketPriority>())}.");
    }

    /// <summary>Null and whitespace mean "no filter", which is not an error.</summary>
    public static TicketPriority? ParseOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Parse(value);
}

/// <summary>
/// Maps a status string onto <see cref="TicketStatus"/>.
/// <para>
/// <b>Parsing is not authority and not legality.</b> This turns wire text into a value and nothing
/// more — an unknown name is a <c>400</c> here, while a known name the caller may not use is a
/// <c>403</c> and one the graph forbids is a <c>409</c>, both decided further in
/// (<c>TransitionAuthority</c>, <c>TicketLifecycle</c>). Story 05 used it for the list filter;
/// Story 06 adds <see cref="Parse"/> for the transition target.
/// </para>
/// </summary>
public static class TicketStatusParser
{
    /// <summary>
    /// The required form, for <c>POST /tickets/{id}/transition</c>. An unknown status is a
    /// <c>400 validation-failed</c> naming what is allowed — never silently ignored, and never
    /// mistaken for an illegal <em>transition</em>, which is a different failure with a different
    /// status code.
    /// </summary>
    public static TicketStatus Parse(string value) =>
        ParseOptional(value)
        ?? throw new Abstractions.ValidationException(
            $"A target status is required. Allowed values: {string.Join(", ", Enum.GetNames<TicketStatus>())}.");

    public static TicketStatus? ParseOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<TicketStatus>(value, ignoreCase: true, out var status) &&
            Enum.IsDefined(status))
        {
            return status;
        }

        throw new Abstractions.ValidationException(
            $"Unknown status '{value}'. Allowed values: {string.Join(", ", Enum.GetNames<TicketStatus>())}.");
    }
}
