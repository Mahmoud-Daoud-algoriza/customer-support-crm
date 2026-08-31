using Microsoft.Extensions.Logging;
using SupportCrm.Application.Modules.Sla;

namespace SupportCrm.Infrastructure.Notifications;

/// <summary>
/// <b>A temporary implementation of <see cref="INotificationPublisher"/> that logs.</b>
///
/// <para>
/// <b>Story 09 replaces the registration</b> with the persistent implementation that writes
/// <c>Notification</c> rows (docs/data-model.md §2.12). The interface, the call sites and the type
/// set do not change when it does — so Story 06's escalation path is written against its final
/// shape, and Story 09 changes one line in <c>DependencyInjection</c>.
/// </para>
///
/// <para>
/// <b>Why logging rather than nothing.</b> The <c>Notifications</c> table is Story 09's, so there is
/// nowhere to write today; a no-op would make Story 06's escalation impossible to verify by hand,
/// and the plan's verification step 5 asks for exactly this line. It is <c>Information</c>, not
/// <c>Warning</c>: publishing to a resolved recipient is the <b>normal</b> path. The
/// <c>Warning</c> belongs to <c>EscalationRecipientPolicy</c>'s fallback rungs (A-21), and putting
/// one here too would make an ordinary escalation look like a degraded one.
/// </para>
/// </summary>
public sealed class LoggingNotificationPublisher(
    ILogger<LoggingNotificationPublisher> logger) : INotificationPublisher
{
    public Task PublishAsync(
        Guid recipientUserId, NotificationType type, Guid ticketId, CancellationToken ct)
    {
        logger.LogInformation(
            "Notification {Type} raised for recipient {RecipientUserId} on ticket {TicketId}. "
            + "No row is written: the Notification entity arrives with Story 09 (A-13).",
            type, recipientUserId, ticketId);

        _ = ct;

        return Task.CompletedTask;
    }
}
