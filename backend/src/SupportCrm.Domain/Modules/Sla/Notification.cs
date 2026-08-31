namespace SupportCrm.Domain.Modules.Sla;

/// <summary>
/// One in-app notification — docs/data-model.md §2.12. <b>In-app only</b> (A-13, T2-D): there is no
/// email, SMS or push channel here, and no delivery-status column, because those are T3-A and are
/// not modelled at all (DM-6).
///
/// <para>
/// <b>Recipient-scoped, and that is the whole of its authorization.</b> A notification belongs to one
/// user; another user's row is a <c>404</c>. Nothing in this type names a department or a role — the
/// A-21 cascade decides who *gets* a row, and by the time one exists that decision is already made.
/// </para>
///
/// <para>
/// <b><see cref="ReadAt"/> transitions null -> timestamp and nothing else</b> (docs/data-model.md §5
/// constraint 22). <see cref="MarkRead"/> is the only mutator on this entity and it <b>no-ops when
/// already read</b>, so a second <c>POST /{id}/read</c> is idempotent rather than a rewrite of the
/// first timestamp. There is deliberately no <c>MarkUnread</c>: no requirement asks for one, and its
/// absence is what makes the constraint unbreakable rather than merely unbroken.
/// </para>
///
/// <para>
/// <b>There is no create endpoint</b> (docs/api-design.md §5.10). Notifications are raised by the
/// server, through <c>INotificationPublisher</c>, as a consequence of something else happening.
/// </para>
/// </summary>
public sealed class Notification
{
    /// <summary>EF's constructor. Nothing else may leave the invariants unset.</summary>
    private Notification()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>The one user who may read this row. <c>Restrict</c> FK to <c>User</c>.</summary>
    public Guid RecipientUserId { get; private set; }

    public NotificationType Type { get; private set; }

    /// <summary>
    /// The ticket the notification is about — never optional, because all four A-13 events are
    /// about a ticket. <c>Restrict</c> FK to <c>Ticket</c>: a ticket is cancelled, never deleted,
    /// so a notification can never be orphaned.
    /// </summary>
    public Guid TicketId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Null until read. Once set, never changed and never cleared.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>The only way a notification comes into existence.</summary>
    public static Notification Create(
        Guid id,
        Guid recipientUserId,
        NotificationType type,
        Guid ticketId,
        DateTimeOffset createdAt) =>
        new()
        {
            Id = id,
            RecipientUserId = recipientUserId,
            Type = type,
            TicketId = ticketId,
            CreatedAt = createdAt,
            ReadAt = null,
        };

    /// <summary>
    /// Marks the notification read. <b>A no-op when it already is</b> — the second read of the same
    /// row must not move the timestamp (docs/data-model.md §5 constraint 22), and returning quietly
    /// is what lets the endpoint answer <c>204</c> both times without the caller having to know
    /// which call was the first.
    /// </summary>
    public void MarkRead(DateTimeOffset now)
    {
        if (ReadAt is not null)
        {
            return;
        }

        ReadAt = now;
    }
}
