using SupportCrm.Application.Modules.Sla;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Infrastructure.Persistence;

namespace SupportCrm.Infrastructure.Notifications;

/// <summary>
/// <b>The implementation of <see cref="INotificationPublisher"/> that writes rows</b> — A-13's in-app
/// notifications, docs/data-model.md §2.12.
///
/// <para>
/// <b>This is the swap Story 06's plan promised.</b> The interface, its four types and every call
/// site are unchanged; only the implementation is. <c>LoggingNotificationPublisher</c> was deleted in
/// the same change, so there is <b>one</b> implementation rather than two — which is the point of
/// having declared the abstraction in Application ahead of its persistence (AD-11): Story 06's
/// escalation code needed no edit to start producing real notifications.
/// </para>
///
/// <para>
/// <b>It does not commit</b>, exactly as the seam's contract says. The row is added to the change
/// tracker and the caller's single <c>SaveChangesAsync</c> writes it, so a notification and the
/// change that caused it land together or not at all (docs/architecture.md §3). A breach that fails
/// to save leaves no orphan notification claiming it happened.
/// </para>
///
/// <para>
/// <b>It validates no recipient and resolves none.</b> Who receives a notification is A-21's
/// question, answered once in <c>EscalationRecipientPolicy</c>; this seam is told an id and writes to
/// it. The <c>Restrict</c> foreign key is what makes a bad id a loud failure rather than a silent
/// orphan.
/// </para>
/// </summary>
public sealed class PersistentNotificationPublisher(
    SupportCrmDbContext db, TimeProvider clock) : INotificationPublisher
{
    public Task PublishAsync(
        Guid recipientUserId, NotificationType type, Guid ticketId, CancellationToken ct)
    {
        db.Notifications.Add(Notification.Create(
            Guid.NewGuid(), recipientUserId, type, ticketId, clock.GetUtcNow()));

        _ = ct;

        return Task.CompletedTask;
    }
}
