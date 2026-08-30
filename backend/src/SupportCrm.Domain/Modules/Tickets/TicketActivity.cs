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
