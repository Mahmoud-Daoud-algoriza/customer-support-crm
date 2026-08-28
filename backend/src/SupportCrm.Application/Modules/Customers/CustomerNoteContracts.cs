using System.ComponentModel.DataAnnotations;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// <c>CustomerNote</c> — docs/api-design.md §6.3.
/// <para>
/// <b>There is no <c>updatedAt</c>, because the entity is immutable</b> (docs/data-model.md §2.5,
/// §5 constraint 16). Its absence is the contract stating that no edit path exists, so adding one
/// here would advertise a capability the server does not have.
/// </para>
/// </summary>
public sealed record CustomerNoteDto(
    Guid Id,
    UserSummaryDto Author,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>
/// <c>POST /customers/{id}/notes</c> — docs/api-design.md §5.5. <b>Exactly one field.</b>
/// <para>
/// Author and timestamp are server-set from <c>ICurrentUser</c> and are absent here rather than
/// accepted and ignored (AP-10, docs/api-design.md §7), so a request carrying either is a
/// <c>400</c> and the client is never misled into thinking it worked. The customer is the route
/// parameter, not a body field, for the same reason.
/// </para>
/// </summary>
public sealed record CreateNoteRequest
{
    [Required] public string Body { get; init; } = default!;
}

/// <summary>
/// <c>TimelineEntry</c> — <c>GET /customers/{id}/timeline</c>, docs/api-design.md §6.3.
///
/// <para>
/// A <b>read projection</b> over the customer's tickets and their <c>TicketActivity</c>, never a
/// stored row (requirements §1.3, docs/architecture.md §2.5, docs/data-model.md §2.4).
/// </para>
///
/// <para>
/// <b><c>activityType</c> and <c>actorKind</c> are strings, not enums, and deliberately so.</b>
/// Both belong to the <c>Tickets</c> domain module, which Story 05 creates; defining the enums here
/// would be inventing that module's vocabulary a story early. They are persisted and published as
/// stable string codes in any case (docs/api-design.md §2), so the wire shape is identical either
/// way, and Story 06 can project its enums straight into these fields.
/// </para>
///
/// <para>
/// <b>An entry whose visibility is <c>Internal</c> never becomes one of these</b>, and no
/// <c>TicketInternalNote</c> ever does either — see <see cref="CustomerTimelineService"/> for why
/// that is structural rather than a filter.
/// </para>
/// </summary>
public sealed record TimelineEntryDto(
    DateTimeOffset OccurredAt,
    Guid TicketId,
    string TicketSubject,
    string ActivityType,
    string ActorKind,

    /// <summary>Absent when <c>actorKind</c> is <c>System</c> — the SLA monitor (§2.7).</summary>
    UserSummaryDto? Actor,

    /// <summary>The before value, for change types. Absent otherwise.</summary>
    string? OldValue,

    /// <summary>The after value, for change types. Absent otherwise.</summary>
    string? NewValue);
