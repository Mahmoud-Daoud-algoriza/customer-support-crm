namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The history spine — requirements §2.5, the append-only trail of everything that happened to a
/// ticket (T1-B, docs/data-model.md §2.7, docs/architecture.md §2.5).
///
/// <para>
/// <b>This is not the audit log</b> (§2.14). Ticket history is business history, readable by staff
/// working the ticket and partly by the customer; the audit log is security history, Administrator
/// only. The two stay independently queryable and neither is derived from the other (AD-10).
/// </para>
///
/// <para>
/// <b>Append-only by construction, not by convention.</b> Every setter is private, there is no
/// mutator, and there is no delete — <em>"not merely no UI, but no service method"</em> (§2.7).
/// The single writer is <c>TicketActivityRecorder</c>, which exposes no update or delete either.
/// </para>
///
/// <para>
/// <b>Story 05 introduces the entity and writes four of its types</b>; Story 06 owns the state
/// machine, the lifecycle types, the <c>/activity</c> read endpoint and the append-only tests
/// (planning note S9-5).
/// </para>
///
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class TicketActivity
{
    private TicketActivity()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>The owner. Inherits the ticket's scope (docs/data-model.md §2.7).</summary>
    public Guid TicketId { get; private set; }

    /// <summary>The ordering key. Newest-first reads use it with <see cref="TicketId"/> (§6).</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    public TicketActivityType ActivityType { get; private set; }

    /// <summary>
    /// Null <b>exactly when</b> <see cref="ActorKind"/> is <see cref="TicketActorKind.System"/> —
    /// an invariant the factories below make structural rather than checked after the fact.
    /// </summary>
    public Guid? ActorUserId { get; private set; }

    public TicketActorKind ActorKind { get; private set; }

    /// <summary>The before value, for change types. Null otherwise.</summary>
    public string? OldValue { get; private set; }

    /// <summary>The after value, for change types. Null otherwise.</summary>
    public string? NewValue { get; private set; }

    public TicketActivityVisibility Visibility { get; private set; }

    /// <summary>
    /// Set <b>if and only if</b> <see cref="ActivityType"/> is
    /// <see cref="TicketActivityType.MessagePosted"/> (§2.7 invariants). The body lives on the
    /// message, never copied here (DM-4). Story 07 writes it.
    /// </summary>
    public Guid? MessageId { get; private set; }

    /// <summary>
    /// Set <b>if and only if</b> <see cref="ActivityType"/> is
    /// <see cref="TicketActivityType.InternalNotePosted"/>, which is <b>always</b>
    /// <see cref="TicketActivityVisibility.Internal"/> (§2.7 invariants). Story 14 writes it.
    /// </summary>
    public Guid? InternalNoteId { get; private set; }

    /// <summary>
    /// An entry caused by a person — every type Story 05 writes.
    /// <para>
    /// <see cref="ActorKind"/> is <see cref="TicketActorKind.User"/> and
    /// <see cref="ActorUserId"/> is required, which is the §2.7 invariant expressed as a signature:
    /// there is no way to build a `User` entry with a null actor.
    /// </para>
    /// </summary>
    public static TicketActivity ByUser(
        Guid id,
        Guid ticketId,
        TicketActivityType activityType,
        Guid actorUserId,
        DateTimeOffset occurredAt,
        string? oldValue = null,
        string? newValue = null,
        TicketActivityVisibility visibility = TicketActivityVisibility.CustomerVisible)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("An activity entry requires a ticket.", nameof(ticketId));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "A User activity entry requires an actor; use BySystem for the SLA monitor.",
                nameof(actorUserId));
        }

        return new TicketActivity
        {
            Id = id,
            TicketId = ticketId,
            OccurredAt = occurredAt,
            ActivityType = activityType,
            ActorUserId = actorUserId,
            ActorKind = TicketActorKind.User,
            OldValue = Normalize(oldValue),
            NewValue = Normalize(newValue),
            Visibility = visibility,
            MessageId = null,
            InternalNoteId = null,
        };
    }

    /// <summary>
    /// The <see cref="TicketActivityType.MessagePosted"/> entry for one message — <b>the only way
    /// <see cref="MessageId"/> is ever set</b> (Story 07).
    ///
    /// <para>
    /// <b>§2.7's "if and only if" is a signature here, not a checked rule.</b> The activity type is
    /// not a parameter: this factory always writes <c>MessagePosted</c>, and no other factory
    /// accepts a <c>messageId</c> — so an entry with a message id and the wrong type, or a
    /// <c>MessagePosted</c> entry with no message id, cannot be constructed at all.
    /// </para>
    ///
    /// <para>
    /// <b>The body is not copied here</b> (DM-4). Content lives once, on the message; this row is
    /// the ordering spine that points at it. <c>OldValue</c> and <c>NewValue</c> stay null because
    /// posting a message is not a change to a field.
    /// </para>
    ///
    /// <para>
    /// <b>Always <c>CustomerVisible</c>.</b> A <c>TicketMessage</c> is customer-facing
    /// correspondence by definition (§2.8); the <c>Internal</c> counterpart is
    /// <c>InternalNotePosted</c>, which is Story 14's and is a different entity on purpose.
    /// </para>
    /// </summary>
    public static TicketActivity MessagePosted(
        Guid id,
        Guid ticketId,
        Guid messageId,
        Guid authorUserId,
        DateTimeOffset occurredAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("An activity entry requires a ticket.", nameof(ticketId));
        }

        if (messageId == Guid.Empty)
        {
            throw new ArgumentException(
                "A MessagePosted entry requires the message it points at (§2.7).", nameof(messageId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "A MessagePosted entry requires its author as actor.", nameof(authorUserId));
        }

        return new TicketActivity
        {
            Id = id,
            TicketId = ticketId,
            OccurredAt = occurredAt,
            ActivityType = TicketActivityType.MessagePosted,
            ActorUserId = authorUserId,
            ActorKind = TicketActorKind.User,
            OldValue = null,
            NewValue = null,
            Visibility = TicketActivityVisibility.CustomerVisible,
            MessageId = messageId,
            InternalNoteId = null,
        };
    }

    /// <summary>
    /// The <see cref="TicketActivityType.InternalNotePosted"/> entry for one note — <b>the only way
    /// <see cref="InternalNoteId"/> is ever set</b> (Story 14).
    ///
    /// <para>
    /// <b>The mirror of <see cref="MessagePosted"/>, and deliberately so.</b> The activity type is
    /// not a parameter and neither is the visibility: this factory always writes
    /// <c>InternalNotePosted</c> at <see cref="TicketActivityVisibility.Internal"/>, and no other
    /// factory accepts an <c>internalNoteId</c>. That makes both of §2.7's invariants unbreakable
    /// rather than merely observed — <em>"<c>internalNoteId</c> is set if and only if
    /// <c>activityType = InternalNotePosted</c>"</em> and <em>"<c>InternalNotePosted</c> is always
    /// <c>Internal</c> visibility"</em>. Neither can be got wrong at a call site, because neither
    /// can be spelled there.
    /// </para>
    ///
    /// <para>
    /// <b>The body is not copied here</b> (DM-4). Content lives once, on the note; this row is the
    /// ordering spine that points at it. <c>OldValue</c> and <c>NewValue</c> stay null because
    /// writing a note is not a change to a field.
    /// </para>
    ///
    /// <para>
    /// <b>The <c>Internal</c> visibility is what keeps the note out of the customer timeline</b>,
    /// which excludes <c>Internal</c> entries and does not join the note table either — two
    /// independent reasons, which is the point (§5 constraint 18, architecture §2.5).
    /// </para>
    /// </summary>
    public static TicketActivity InternalNotePosted(
        Guid id,
        Guid ticketId,
        Guid internalNoteId,
        Guid authorUserId,
        DateTimeOffset occurredAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("An activity entry requires a ticket.", nameof(ticketId));
        }

        if (internalNoteId == Guid.Empty)
        {
            throw new ArgumentException(
                "An InternalNotePosted entry requires the note it points at (§2.7).",
                nameof(internalNoteId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "An InternalNotePosted entry requires its author as actor.", nameof(authorUserId));
        }

        return new TicketActivity
        {
            Id = id,
            TicketId = ticketId,
            OccurredAt = occurredAt,
            ActivityType = TicketActivityType.InternalNotePosted,
            ActorUserId = authorUserId,
            ActorKind = TicketActorKind.User,
            OldValue = null,
            NewValue = null,
            Visibility = TicketActivityVisibility.Internal,
            MessageId = null,
            InternalNoteId = internalNoteId,
        };
    }

    /// <summary>
    /// An entry caused by the system — <b>the SLA monitor and nothing else</b> (§2.7, Story 09).
    /// <see cref="ActorUserId"/> is null, which is the other half of the same invariant.
    /// </summary>
    public static TicketActivity BySystem(
        Guid id,
        Guid ticketId,
        TicketActivityType activityType,
        DateTimeOffset occurredAt,
        string? oldValue = null,
        string? newValue = null,
        TicketActivityVisibility visibility = TicketActivityVisibility.CustomerVisible)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("An activity entry requires a ticket.", nameof(ticketId));
        }

        return new TicketActivity
        {
            Id = id,
            TicketId = ticketId,
            OccurredAt = occurredAt,
            ActivityType = activityType,
            ActorUserId = null,
            ActorKind = TicketActorKind.System,
            OldValue = Normalize(oldValue),
            NewValue = Normalize(newValue),
            Visibility = visibility,
            MessageId = null,
            InternalNoteId = null,
        };
    }

    /// <summary>An all-whitespace value is an absent value, not a stored blank.</summary>
    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
