using System.ComponentModel.DataAnnotations;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <c>Message</c> — docs/api-design.md §6.4, member for member. The shape of
/// <c>GET /tickets/{id}/messages</c> and of the staff reply's <c>201</c>.
///
/// <para>
/// <b><see cref="Direction"/> and <see cref="Channel"/> are server-derived</b> (docs/api-design.md
/// §7, <b>PF-7</b>): direction from the author's role, channel from the endpoint used. Neither
/// appears in any request model, so a body carrying one is a <c>400</c> rather than
/// accepted-and-ignored (<b>AP-10</b>).
/// </para>
///
/// <para>
/// <b>This is the staff shape.</b> <see cref="PortalMessageDto"/> is the portal one, and the two are
/// deliberately different types rather than one type with optional members — AP-5 splits the path
/// spaces precisely so a narrowing cannot be forgotten in a projection.
/// </para>
/// </summary>
public sealed record MessageDto(
    Guid Id,
    Guid TicketId,
    UserSummaryDto Author,
    string AuthorRole,
    string Direction,
    string Channel,
    string Body,
    DateTimeOffset PostedAt);

/// <summary>
/// The <b>portal</b> message shape — docs/api-design.md §6.4: <em>"The portal variant omits
/// <c>channel</c> and <c>authorRole</c>, keeping <c>direction</c> so the thread can distinguish the
/// two sides."</em>
///
/// <para>
/// <b>The omission is the point.</b> Channel is the seam's internal fact and a customer has no use
/// for it; author role is staff vocabulary the portal does not speak (UI-11). <c>direction</c> stays
/// because the thread has two sides and must render them as such.
/// </para>
///
/// <para>
/// <b><c>TicketInternalNote</c> has no representation here at all</b> — it is a different entity
/// read through a different endpoint that no portal path reaches (T2-C, AP-5,
/// docs/data-model.md §2.9). There is no field to omit and no filter to remember.
/// </para>
/// </summary>
public sealed record PortalMessageDto(
    Guid Id,
    Guid TicketId,
    UserSummaryDto Author,
    string Direction,
    string Body,
    DateTimeOffset PostedAt)
{
    /// <summary>
    /// The one narrowing from the staff shape to the portal one, written once so no projection can
    /// forget it (AP-5). Adding a member to <see cref="MessageDto"/> does not leak it here.
    /// </summary>
    public static PortalMessageDto From(MessageDto message) =>
        new(message.Id, message.TicketId, message.Author, message.Direction, message.Body, message.PostedAt);
}

/// <summary>
/// The body of <c>POST /tickets/{id}/messages</c> and <c>POST /portal/tickets/{id}/messages</c> —
/// docs/api-design.md §5.6 and §5.7: <c>{ "body": "..." }</c>.
///
/// <para>
/// <b>One property, and deliberately no others.</b> No <c>direction</c>, no <c>channel</c>, no
/// author and no timestamp: all four are server-derived (docs/api-design.md §7), and a body
/// carrying one is a <c>400</c> because <c>UnmappedMemberHandling.Disallow</c> is set once on the
/// MVC JSON options (<b>AP-10</b>, finding I-9). <b>No <c>visibility</c> either</b> — a
/// <c>TicketMessage</c> is customer-visible by definition; the internal counterpart is a different
/// entity with a different endpoint (T2-C, Story 14).
/// </para>
///
/// <para>
/// <b>The same request model serves both path spaces</b>, and that is not an AP-5 violation: AP-5
/// separates the <em>paths</em>, the <em>scoping</em> and the <em>response</em> shapes, and this one
/// has a single member that means the same thing on both sides. The responses differ, and they are
/// two types.
/// </para>
/// </summary>
public sealed record PostMessageRequest
{
    [Required] public string Body { get; init; } = default!;
}

/// <summary>
/// What <c>TicketMessageService.PostAsync</c> returns — the created message <b>plus the ticket's
/// current status and whether posting changed it</b>.
///
/// <para>
/// <b>The status travels with the message because posting one may have moved the ticket</b> (R-13).
/// The portal endpoint publishes all three as the envelope of docs/api-design.md §6.4,
/// <c>{ message, ticketStatus, statusChanged }</c>, <em>"so the client never has to guess whether
/// the transition happened"</em> and never has to re-fetch. The staff endpoint returns the message
/// alone: an agent reply never transitions a ticket, so there would be nothing to report.
/// </para>
///
/// <para>
/// <see cref="StatusChanged"/> is true <b>only</b> when the automatic <c>Pending → Open</c> fired.
/// It is not "the status differs from what you last saw".
/// </para>
/// </summary>
public sealed record PostedMessageDto(MessageDto Message, string TicketStatus, bool StatusChanged);

/// <summary>
/// The portal envelope of docs/api-design.md §6.4 — the same three facts, with the message narrowed
/// to <see cref="PortalMessageDto"/>.
/// </summary>
public sealed record PortalPostedMessageDto(
    PortalMessageDto Message,
    string TicketStatus,
    bool StatusChanged)
{
    public static PortalPostedMessageDto From(PostedMessageDto posted) =>
        new(PortalMessageDto.From(posted.Message), posted.TicketStatus, posted.StatusChanged);
}
