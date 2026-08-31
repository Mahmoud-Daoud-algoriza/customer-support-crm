namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The six statuses of A-5 (docs/data-model.md §2.6) — <b>and no others</b>. There are no
/// sub-statuses, no per-department variants and no configurable workflow: T4 excludes a rules
/// engine, and A-5 fixes this set.
/// <para>
/// Persisted as a <b>stable string code</b>, never as an ordinal (docs/api-design.md §2), so the
/// wire value and the stored value survive reordering this enum.
/// </para>
/// <para>
/// <b>This enum is the set; it says nothing about the transitions between its members.</b> Legality
/// is <see cref="TicketLifecycle"/> and authority is <c>TransitionAuthority</c> (Application), and
/// the only writer of a ticket's status is <see cref="Ticket.TransitionTo"/> — all three added by
/// Story 06, which is why Story 05 could define this set without any way to move between it.
/// </para>
/// </summary>
public enum TicketStatus
{
    New,
    Open,
    Pending,
    Resolved,
    Closed,
    Cancelled,
}
