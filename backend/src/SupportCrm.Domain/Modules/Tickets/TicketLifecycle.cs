namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// <b>The A-5 transition graph, in one place, as data.</b> Story 05 defined the six statuses and
/// deliberately withheld any way to move between them; this is that rule, and it is expressed
/// <b>once</b> so no caller can bypass it (docs/product-scope.md A-5, docs/data-model.md §2.6).
///
/// <para>
/// <b>Legality is not authority.</b> This type answers *"is this edge in the graph at all"* and
/// nothing else. *"May this caller invoke it"* is A-16, and lives in
/// <c>Application/Modules/Tickets/TransitionAuthority.cs</c>. Keeping them apart is what lets the
/// API return two distinguishable failures — <c>403 transition-not-permitted</c> for authority and
/// <c>409 illegal-transition</c> for legality (docs/api-design.md §5.6) — instead of one vague
/// refusal that tells a caller nothing about which rule it broke.
/// </para>
///
/// <para>
/// <b>The automatic <c>Pending → Open</c> edge is in this graph like any other.</b> R-13 makes a
/// customer reply reopen a pending ticket, and R-14 attributes it to the replying customer — but
/// the <em>edge</em> is ordinary. What is special is its trigger, which is Story 07's portal
/// message endpoint, not this table.
/// </para>
///
/// <para>
/// <b>No configurable workflow.</b> T4 excludes a rules engine, so this dictionary is the whole
/// state machine and there is no per-department variant of it.
/// </para>
/// </summary>
public static class TicketLifecycle
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Legal =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.New] = [TicketStatus.Open, TicketStatus.Cancelled],
            [TicketStatus.Open] = [TicketStatus.Pending, TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.Pending] = [TicketStatus.Open, TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.Resolved] = [TicketStatus.Open, TicketStatus.Closed, TicketStatus.Cancelled],
            [TicketStatus.Closed] = [],      // terminal
            [TicketStatus.Cancelled] = [],   // terminal
        };

    /// <summary>Is <paramref name="to"/> reachable from <paramref name="from"/> under A-5?</summary>
    public static bool IsLegal(TicketStatus from, TicketStatus to) => Legal[from].Contains(to);

    /// <summary>
    /// The legal targets from a status. Exists because docs/api-design.md §5.6 requires
    /// <c>allowedTransitions</c> <b>inside the <c>409</c> problem detail</b> — the only place the
    /// contract publishes this set today. <b>It is not on the ticket payload</b>, which is
    /// finding <b>F-1</b>, still open by design.
    /// </summary>
    public static IReadOnlyList<TicketStatus> LegalFrom(TicketStatus from) => Legal[from];

    /// <summary>
    /// <c>Closed</c> and <c>Cancelled</c> — no outgoing edge at all. A-5 makes them terminal, and
    /// terminality is read from the graph rather than asserted separately, so the two can never
    /// disagree.
    /// </summary>
    public static bool IsTerminal(TicketStatus status) => Legal[status].Length == 0;
}
