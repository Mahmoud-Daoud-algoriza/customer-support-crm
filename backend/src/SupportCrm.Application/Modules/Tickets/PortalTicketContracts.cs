using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <c>POST /portal/tickets</c> — the web form of requirements §3.5, docs/api-design.md §5.7.
///
/// <para>
/// <b>Exactly four members, and the three that are missing are the contract.</b>
/// </para>
/// <list type="bullet">
///   <item><description><b>No <c>customerId</c></b> — it is the caller's own profile (docs/api-design.md §7).</description></item>
///   <item><description><b>No <c>departmentId</c></b> — derived from <see cref="CategoryCode"/> through the configured map (<b>A-14</b>). <em>"Customers do not choose a department."</em></description></item>
///   <item><description><b>No <c>priority</c></b> — customers do not set priority (<b>A-6</b>). <see cref="IsUrgent"/> is the boolean they may send (<b>A-17</b>), and it does <b>not</b> set priority.</description></item>
/// </list>
///
/// <para>
/// Each is absent rather than accepted-and-ignored, so a body carrying one is a <c>400</c> —
/// <c>UnmappedMemberHandling.Disallow</c> is set once on the MVC JSON options (<b>AP-10</b>,
/// finding I-9). A client is never misled into thinking it worked.
/// </para>
///
/// <para>
/// <b>This is a different type from <see cref="CreateTicketRequest"/> on purpose</b> (AP-5): the
/// two path spaces have different scoping, different authority and different payloads, and sharing
/// one request model would be the shape that makes the omissions above optional.
/// </para>
/// </summary>
public sealed record SubmitPortalTicketRequest
{
    [Required, MaxLength(512)] public string Subject { get; init; } = default!;

    [Required] public string Description { get; init; } = default!;

    /// <summary>
    /// The customer's category choice. <b>It is what routes the ticket</b> (A-14) — an unmapped or
    /// unknown value is a <c>400</c> naming what is allowed, never a silent default.
    /// </summary>
    [Required, MaxLength(64)] public string CategoryCode { get; init; } = default!;

    /// <summary>
    /// A-17's <em>indication</em>. It is stored and stays visible to agents and to the AI
    /// suggestion, and it <b>does not set priority</b>. Absent means false.
    /// </summary>
    public bool IsUrgent { get; init; }
}

/// <summary>
/// <c>Ticket (portal)</c> — docs/api-design.md §6.4, member for member.
///
/// <para>
/// <b>No assignee (AP-16), no department, no priority, no SLA or breach fields, no internal
/// anything.</b> The portal speaks no staff vocabulary (UI-11), and the omissions are the payload's
/// whole design: no requirement gives a customer the name of their agent, their ticket's department
/// or its internal priority.
/// </para>
///
/// <para>
/// <see cref="Status"/> <b>is</b> present, and it uses the same six-value vocabulary as the staff
/// side — docs/ui-design.md §8: <em>"the portal uses the same status vocabulary; no separate
/// customer wording was authorized."</em>
/// </para>
/// </summary>
public sealed record PortalTicketDto(
    Guid Id,
    string Subject,
    string Description,
    string CategoryCode,
    string Status,
    bool IsUrgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,

    // hasFeedback is computed from the existence of a CustomerFeedback row — a response projection,
    // not a stored field (docs/api-design.md §7).
    //
    // Story 13: replace PortalTicketService's constant `false` with the feedback-row check.
    // CustomerFeedback does not exist yet — it is Story 13's, and its rating scale is still OQ-1.
    // On a just-submitted ticket the value is false under either implementation, which is why the
    // member can be honest here rather than omitted from the contract.
    bool HasFeedback);
