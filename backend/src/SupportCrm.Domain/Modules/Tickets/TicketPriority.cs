namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The four-level priority scale of A-6 (docs/data-model.md §2.6). Configuration supplies the SLA
/// hours per level (A-3); it does not supply the levels themselves, which are fixed here.
/// <para>
/// Persisted as a <b>stable string code</b> (docs/api-design.md §2). The declaration order is the
/// severity order, which is what makes Story 06's "escalation raises priority exactly one level,
/// and <c>Urgent</c> stays <c>Urgent</c>" expressible without a lookup table.
/// </para>
/// <para>
/// <b>A customer never sets this.</b> They may indicate urgency with <c>Ticket.IsUrgent</c>, which
/// is a separate boolean and does not set priority (A-17).
/// </para>
/// </summary>
public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent,
}
