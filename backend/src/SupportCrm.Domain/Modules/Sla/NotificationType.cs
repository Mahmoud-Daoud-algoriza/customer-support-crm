namespace SupportCrm.Domain.Modules.Sla;

/// <summary>
/// <b>The four notification types of A-13 and no others</b> (docs/data-model.md §2.12, which
/// enumerates the set exhaustively). Adding a fifth member is a <b>scope change</b>, not an ordinary
/// use of this enum.
///
/// <para>
/// <b>It lives in the Domain because <see cref="Notification"/> carries it.</b> Story 06 declared it
/// beside <c>INotificationPublisher</c> in the Application layer, which was the right home while no
/// entity existed; Story 09 introduces the entity, and <c>SupportCrm.Domain</c> ends with zero
/// project references (AD-4), so it cannot reach into Application for a member of its own row. The
/// enum moved here and the Application declaration was deleted — <b>there is one</b>, and the seam
/// and the entity name the same values by construction rather than by agreement.
/// </para>
///
/// <para>
/// All four are about a ticket, which is why <c>ticketId</c> is not optional on either the entity or
/// the publishing seam.
/// </para>
/// </summary>
public enum NotificationType
{
    /// <summary>Story 09 — a ticket was assigned to the recipient (round-robin, or manually).</summary>
    TicketAssigned,

    /// <summary>Story 09 — an SLA target was missed. Raised by the sweep, the only system actor.</summary>
    SlaBreached,

    /// <summary>Story 06 — a ticket was escalated. Recipients come from A-21.</summary>
    TicketEscalated,

    /// <summary>Story 07 — the customer replied on a ticket.</summary>
    CustomerReplied,
}
