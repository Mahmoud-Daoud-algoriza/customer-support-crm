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
/// <b>Story 05 defines the set; it does not define the transitions between them.</b> Legality is
/// <c>TicketLifecycle</c> and authority is <c>TransitionAuthority</c>, both Story 06's — which is
/// why <see cref="Ticket"/> exposes no status mutator in this story.
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
