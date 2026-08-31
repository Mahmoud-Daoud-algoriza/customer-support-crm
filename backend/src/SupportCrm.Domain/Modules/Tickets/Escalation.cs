namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// <b>Escalation's arithmetic — A-5's "raise priority exactly one level".</b>
///
/// <para>
/// <b>Escalation is an action, not a status change</b> (AP-7). Nothing here touches
/// <see cref="Ticket.Status"/>, and there is no escalation tier, level or L1/L2/L3 ladder: A-5
/// defines one rule and T4 excludes a configurable workflow.
/// </para>
///
/// <para>
/// <b>And nothing here touches the SLA due timestamps.</b> A-20 freezes them at creation, and that
/// rule has its own single home in <c>Sla/SlaClock.OnPriorityChanged</c>, which the Application
/// layer calls on the same path. Re-stating it here would give A-20 two homes.
/// </para>
/// </summary>
public static class Escalation
{
    /// <summary>
    /// One level up the severity scale; <b><c>Urgent</c> stays <c>Urgent</c></b> — escalating an
    /// already-urgent ticket is a legal no-op, not an error, because A-5 gives escalation no
    /// failure mode of its own.
    /// <para>
    /// The <c>+ 1</c> is <see cref="TicketPriority"/>'s <b>declaration order</b>, which that enum
    /// documents as the severity order for exactly this reason — so the rule needs no lookup table
    /// that could drift from it.
    /// </para>
    /// </summary>
    public static TicketPriority RaiseOneLevel(TicketPriority current) =>
        current == TicketPriority.Urgent ? TicketPriority.Urgent : current + 1;
}
