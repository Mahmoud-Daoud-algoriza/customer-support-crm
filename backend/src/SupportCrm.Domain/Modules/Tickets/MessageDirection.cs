namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// Which way a <see cref="TicketMessage"/> travelled — docs/data-model.md §2.8.
///
/// <para>
/// <b>Derived from the author's role, never from a request</b> (<b>PF-7</b>,
/// docs/api-design.md §7): a customer writes <see cref="Inbound"/>, staff write
/// <see cref="Outbound"/>. It is absent from every request model, so a body carrying it is a
/// <c>400</c> rather than accepted-and-ignored (<b>AP-10</b>).
/// </para>
///
/// Stored as a stable string code, never an ordinal (docs/api-design.md §2).
/// </summary>
public enum MessageDirection
{
    /// <summary>From the customer. On a <c>Pending</c> ticket it triggers R-13's automatic reopen.</summary>
    Inbound,

    /// <summary>To the customer. The first one sets <c>Ticket.firstRespondedAt</c>.</summary>
    Outbound,
}
