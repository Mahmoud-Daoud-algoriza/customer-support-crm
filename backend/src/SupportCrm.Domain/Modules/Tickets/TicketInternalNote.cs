namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// A staff-only note on a ticket — requirements §4.5, T2-C, docs/data-model.md §2.9.
///
/// <para>
/// <b>This is the whole of "team collaboration" for this assessment.</b> No @mentions, no presence,
/// no agent-to-agent chat, no shared ownership (product-scope §8, the intake's Out of scope).
/// </para>
///
/// <para>
/// <b>It is a separate entity from <see cref="TicketMessage"/> on purpose</b>, and that is the whole
/// design of the visibility rule (§2.9's closing invariant): <em>"a customer-visible read assembles
/// from messages and never touches this table, so the visibility rule is structural rather than a
/// filter someone must remember to apply."</em> There is no <c>visibility</c> column here to set
/// wrongly and no flag to forget — a customer cannot reach this table because <b>no customer-facing
/// query names it</b>.
/// </para>
///
/// <para>
/// <b>Immutable once written</b> (§5 constraint 16), exactly like <see cref="TicketMessage"/> and
/// <c>CustomerNote</c>. Immutability is <b>structural</b> — every setter is private and there is no
/// mutator — so there is nothing for a later story to expose by accident. A correction is a new
/// note.
/// </para>
///
/// <para>
/// <b>One invariant lives outside this type</b> because it cannot see it, and it has exactly one
/// home in the Application layer's <c>TicketInternalNoteService</c>: a note is never accepted on a
/// <c>Closed</c> or <c>Cancelled</c> ticket (A-5, §5 constraint 8). The paired
/// <c>InternalNotePosted</c> activity row — <b>always <see cref="TicketActivityVisibility.Internal"/></b>
/// (§2.7) — is written on the same path, by the same service.
/// </para>
///
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class TicketInternalNote
{
    private TicketInternalNote()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>The owner. Inherits the ticket's scope (docs/data-model.md §2.9).</summary>
    public Guid TicketId { get; private set; }

    /// <summary>
    /// The staff member who wrote it — always a resolved caller, never a claim. Attribution is
    /// required: the intake's <em>"recorded with actor and timestamp"</em> is this field and
    /// <see cref="CreatedAt"/>.
    /// </summary>
    public Guid AuthorUserId { get; private set; }

    /// <summary>Plain text (docs/data-model.md §2.9). The <c>Text</c> tier of §6.1 — never indexed.</summary>
    public string Body { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The only way a note comes into existence — and there is deliberately no counterpart that
    /// changes or removes one (§5 constraint 16).
    /// </summary>
    public static TicketInternalNote Write(
        Guid id,
        Guid ticketId,
        Guid authorUserId,
        string body,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("An internal note requires a ticket.", nameof(ticketId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("An internal note requires an author.", nameof(authorUserId));
        }

        return new TicketInternalNote
        {
            Id = id,
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            Body = body.Trim(),
            CreatedAt = createdAt,
        };
    }
}
