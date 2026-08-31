namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// Customer-visible correspondence on a ticket — requirements §3.5 (web form) and §3.3 (portal
/// messaging), T2-B, docs/data-model.md §2.8.
///
/// <para>
/// <b>One normalized message model, carrying the channel it arrived on</b>
/// (docs/architecture.md §5.2). It is the model every future channel adapter writes into (T3-A,
/// DM-6), which is why <see cref="Channel"/> exists from day one even though only two values are
/// real today.
/// </para>
///
/// <para>
/// <b>Immutable once posted.</b> No edit, no delete; a correction is a new message
/// (docs/data-model.md §5 constraint 16). Immutability is <b>structural</b> — every setter is
/// private and there is no mutator — exactly like <c>CustomerNote</c>, so there is nothing for a
/// later story to expose by accident.
/// </para>
///
/// <para>
/// <b>This is not <c>TicketInternalNote</c></b> (docs/data-model.md §2.9). They are separate
/// entities on purpose: a customer-visible read assembles from <em>this</em> table and never touches
/// that one, so the T2-C visibility rule is structural rather than a filter someone must remember.
/// </para>
///
/// <para>
/// <b>Three invariants live outside this type</b> because it cannot see them, and each has exactly
/// one home in the Application layer's <c>TicketMessageService</c>: never accepted on a
/// <c>Closed</c> or <c>Cancelled</c> ticket (A-5); the first <see cref="MessageDirection.Outbound"/>
/// message sets <c>Ticket.firstRespondedAt</c>; and an <see cref="MessageDirection.Inbound"/>
/// message on a <c>Pending</c> ticket reopens it (R-13/R-14, whose rule lives in
/// <c>TicketLifecycleService</c>).
/// </para>
///
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class TicketMessage
{
    private TicketMessage()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>The owner. Inherits the ticket's scope (docs/data-model.md §2.8).</summary>
    public Guid TicketId { get; private set; }

    /// <summary>The agent or the customer who wrote it — always a resolved caller, never a claim.</summary>
    public Guid AuthorUserId { get; private set; }

    /// <summary>
    /// <b>Server-derived from the author's role</b> (<b>PF-7</b>, docs/api-design.md §7). It is not
    /// a parameter of any request model.
    /// </summary>
    public MessageDirection Direction { get; private set; }

    /// <summary>
    /// <b>Server-derived from the endpoint used</b> (docs/api-design.md §7). The seam of
    /// docs/architecture.md §5.2 — see <see cref="MessageChannel"/>.
    /// </summary>
    public MessageChannel Channel { get; private set; }

    /// <summary>Plain text (docs/data-model.md §2.8). The <c>Text</c> tier of §6.1 — never indexed.</summary>
    public string Body { get; private set; } = default!;

    /// <summary>The thread's ordering key, with <see cref="TicketId"/> (docs/data-model.md §6).</summary>
    public DateTimeOffset PostedAt { get; private set; }

    /// <summary>
    /// The only way a message comes into existence — and there is deliberately no counterpart that
    /// changes one.
    /// <para>
    /// <paramref name="direction"/> and <paramref name="channel"/> are parameters because the
    /// <em>server</em> derives them (docs/api-design.md §7); no request model carries either, so
    /// there is no path by which a client value could reach this factory.
    /// </para>
    /// </summary>
    public static TicketMessage Post(
        Guid id,
        Guid ticketId,
        Guid authorUserId,
        MessageDirection direction,
        MessageChannel channel,
        string body,
        DateTimeOffset postedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A message requires a ticket.", nameof(ticketId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("A message requires an author.", nameof(authorUserId));
        }

        return new TicketMessage
        {
            Id = id,
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Direction = direction,
            Channel = channel,
            Body = body.Trim(),
            PostedAt = postedAt,
        };
    }
}
