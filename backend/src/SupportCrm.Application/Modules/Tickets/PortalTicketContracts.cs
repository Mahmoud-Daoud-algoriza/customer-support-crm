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
    // not a stored field (docs/api-design.md §7, finding N-4). Story 13 replaced Story 07's
    // constant `false` with the real existence check; it is what tells the portal detail screen
    // whether to offer the feedback control, and it must never become a column.
    bool HasFeedback);

/// <summary>
/// The body of <c>POST /portal/tickets/{id}/transition</c> — docs/api-design.md §5.7:
/// <c>{ "targetStatus": "Cancelled" | "Open" }</c>.
///
/// <para>
/// <b>A separate type from <c>TransitionTicketRequest</c>, and that is AP-5.</b> The two path spaces
/// have different authority, and the two values above are the whole of a customer's column in
/// <b>A-16</b> — cancel own while <c>New</c> (the window <b>A-18</b> keeps genuinely open), and
/// reopen own <c>Resolved</c>. The <em>string</em> is deliberately not an enum of those two: the
/// authority table has exactly one home (<c>TransitionAuthority</c>), and a narrowed type here would
/// be a second, quieter copy of it that could drift — and would answer <c>400</c> where A-16 says
/// <c>403 transition-not-permitted</c>.
/// </para>
///
/// <para>
/// <b>There is no <c>Pending → Open</c> for a customer to request.</b> R-13 makes that automatic on
/// a reply, which is why docs/ui-design.md §7.3 forbids the UI from offering a manual reopen on a
/// <c>Pending</c> request.
/// </para>
/// </summary>
public sealed record PortalTransitionRequest
{
    [Required] public string TargetStatus { get; init; } = default!;
}

/// <summary>
/// The body of <c>POST /portal/tickets/{id}/feedback</c> — docs/api-design.md §5.7,
/// <c>{ rating, comment? }</c>. T2-F's <em>"one-question satisfaction rating with an optional
/// comment"</em>.
///
/// <para>
/// <b>⚠ No range attribute, and that is OQ-1 — not an oversight.</b> A
/// <c>[Range(min, max)]</c> here would fix the scale in the contract, which docs/api-design.md §5.7
/// explicitly refuses to do: <em>"The permitted values are not fixed by this contract."</em> The
/// range comes from the <c>Feedback rating scale</c> configuration key and is checked in
/// <c>CustomerFeedbackService</c>, which returns <c>400</c> outside it. <b>Do not add one.</b>
/// </para>
///
/// <para>
/// <b>No <c>submittedAt</c> and no submitter.</b> Both are server-derived (docs/api-design.md §7):
/// the timestamp is the clock's and the submitter is the ticket's customer, already known from the
/// caller. A body carrying either is a <c>400</c> (AP-10).
/// </para>
///
/// <para>
/// <b>And no "declined" flag.</b> Declining is simply never calling this endpoint — the absence of a
/// row is the meaningful outcome (T2-F, docs/data-model.md §2.15), so there is no state to send.
/// </para>
/// </summary>
public sealed record SubmitFeedbackRequest
{
    /// <summary>The ordinal answer. Validated against configuration, never against a constant.</summary>
    [Required] public int? Rating { get; init; }

    /// <summary>Optional (T2-F). Absent, empty and whitespace all mean "no comment".</summary>
    public string? Comment { get; init; }
}

/// <summary>
/// <c>Feedback</c> — docs/api-design.md §6.4, member for member:
/// <c>{ id, ticketId, rating, comment, submittedAt }</c>.
///
/// <para>
/// <c>rating</c> <em>"is an ordinal whose permitted range comes from configuration; this contract
/// fixes no range"</em> (<b>OQ-1</b>). Nothing in this type asserts one.
/// </para>
/// </summary>
public sealed record FeedbackDto(
    Guid Id,
    Guid TicketId,
    int Rating,
    string? Comment,
    DateTimeOffset SubmittedAt);
