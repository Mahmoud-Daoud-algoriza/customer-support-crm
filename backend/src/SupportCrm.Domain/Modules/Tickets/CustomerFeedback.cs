namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The customer's satisfaction rating for one ticket — requirements §8.5, T2-F,
/// docs/data-model.md §2.15. <b>The sole CSAT input in the system</b>, and the source of the §9.4
/// satisfaction metric.
///
/// <para>
/// <b>It lives in the <c>Tickets</c> module, and that is <b>DM-7</b>, not a filing preference.</b>
/// Feedback is domain behaviour attached to a ticket — one row per ticket, offered when that ticket
/// reaches <c>Resolved</c>, which is a lifecycle event. <c>customer-portal</c> is an Angular area
/// and a planning slug (docs/architecture.md §1); <b>there is no <c>Portal</c> backend module and
/// this entity must not create one</b>.
/// </para>
///
/// <para>
/// <b>Write-once, structurally.</b> §2.15: <em>"Not editable, not resubmittable."</em> Every setter
/// is private and <b>there is no mutator at all</b> — the same shape <c>TicketMessage</c> and
/// <c>CustomerNote</c> use, so there is nothing for a later story to expose by accident. One row per
/// ticket is enforced by a <b>unique index on <c>TicketId</c></b> (§6), and the "only after
/// <c>Resolved</c>" precondition is an Application rule because this type cannot see the ticket's
/// status.
/// </para>
///
/// <para>
/// <b>Declining is a normal outcome</b> (T2-F, §2.15): a customer who never submits leaves
/// <b>no row</b>, and the absence is meaningful — reporting reads it as <em>"no response"</em>, never
/// as a zero. There is therefore no "declined" state, and none may be added.
/// </para>
///
/// <para>
/// <b>⚠ <see cref="Rating"/> carries no range, and no range constant exists in this project
/// (OQ-1).</b> docs/data-model.md §2.15 types it as <em>"an ordinal value"</em> and states that the
/// model <em>"encodes no range … and none may be inferred from this document into a validation rule,
/// a check constraint, or a UI control."</em> The permitted set comes from the
/// <c>Feedback rating scale</c> configuration key (docs/architecture.md §6.3) and is validated in
/// <c>CustomerFeedbackService</c> alone. <b>Do not add a minimum, a maximum, a step or a check
/// constraint here.</b> Finding <b>N-5</b> — §2.15 says "ordinal" while OQ-1's candidates include a
/// binary thumbs pair — stays recorded rather than fixed, because fixing it would pre-empt OQ-1.
/// </para>
///
/// Plain C# with no EF attributes and no framework type (AD-4).
/// </summary>
public sealed class CustomerFeedback
{
    private CustomerFeedback()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// The ticket rated. <b>Unique</b> — one rating per ticket (§2.15, §5 constraint 21, §6).
    /// </summary>
    public Guid TicketId { get; private set; }

    /// <summary>
    /// The ordinal answer to the one question. <b>Its permitted values are configuration, not a
    /// property of this type</b> — see the class remarks and OQ-1.
    /// </summary>
    public int Rating { get; private set; }

    /// <summary>T2-F's optional comment. The <c>Text</c> tier of §6.1 — never indexed.</summary>
    public string? Comment { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>
    /// The only way feedback comes into existence — <b>and there is deliberately no counterpart that
    /// changes or withdraws one</b> (§2.15: write-once).
    ///
    /// <para>
    /// <b>There is no submitter parameter.</b> §2.15: <em>"The submitter is the ticket's customer,
    /// already reachable through the ticket; no separate column."</em> Adding one would let two
    /// answers disagree about who rated the ticket.
    /// </para>
    ///
    /// <para>
    /// <b><paramref name="rating"/> is not range-checked here, and that is deliberate</b> (OQ-1).
    /// The factory refuses only what is structurally impossible — no ticket. The configured range is
    /// checked once, in <c>CustomerFeedbackService</c>, so answering OQ-1 is a configuration edit
    /// rather than a hunt through the Domain.
    /// </para>
    /// </summary>
    public static CustomerFeedback Submit(
        Guid id, Guid ticketId, int rating, string? comment, DateTimeOffset submittedAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("Feedback requires a ticket.", nameof(ticketId));
        }

        return new CustomerFeedback
        {
            Id = id,
            TicketId = ticketId,
            Rating = rating,

            // An empty comment is the same as no comment: §2.15 makes it optional, and storing "" as
            // distinct from null would give reporting two shapes for one meaning.
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            SubmittedAt = submittedAt,
        };
    }
}
