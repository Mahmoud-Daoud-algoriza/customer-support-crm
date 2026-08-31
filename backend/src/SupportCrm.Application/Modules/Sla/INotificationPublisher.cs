using SupportCrm.Domain.Modules.Sla;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// <b>The in-app notification seam</b> (A-13, T2-D: in-app only — no email, SMS or push, which are
/// T3-A and are not modelled at all, DM-6).
///
/// <para>
/// <b>Declared here, implemented in Infrastructure</b> — the same shape as <c>ITokenIssuer</c> and
/// <c>IAttachmentStorage</c> (AD-11, AD-2). Story 06 registers a <b>logging</b> implementation
/// because no <c>Notification</c> table exists yet; <b>Story 09 replaces the registration</b> with
/// the persistent one that writes rows. <b>The interface, the call sites and the type set do not
/// change when it does</b> — which is the point of introducing it now rather than then.
/// </para>
///
/// <para>
/// This is what the <c>ticket-lifecycle</c> intake asks for in as many words: *"Notification
/// delivery is defined by `sla-routing-escalation` (in-app only, A-13); if that story is not yet
/// planned, raise the manager notification through the same abstraction it will own."*
/// </para>
///
/// <para>
/// <b>The publisher does not decide recipients.</b> Who receives an escalation notification is
/// <b>A-21</b>, and it has its own single home in <see cref="IEscalationRecipientPolicy"/>. This
/// seam is told an id and publishes to it.
/// </para>
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Raises one notification. <b>Does not commit</b> — the caller's single
    /// <c>SaveChangesAsync</c> does, once Story 09 makes this write rows, so a notification and the
    /// change that caused it land together or not at all (docs/architecture.md §3).
    /// </summary>
    Task PublishAsync(
        Guid recipientUserId, NotificationType type, Guid ticketId, CancellationToken ct);
}
