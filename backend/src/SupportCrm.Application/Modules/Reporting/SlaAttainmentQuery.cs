using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Reporting;

/// <summary>
/// §9.2 — SLA attainment per priority: met, breached and the percentage (T2-G,
/// docs/api-design.md §6.8).
///
/// <para>
/// <b>"Breached" is <c>ResolutionBreached</c></b> — the latching flag Story 09's monitor sets. It is
/// the resolution deadline, not the first-response one: T2-G words the metric as attainment of the
/// SLA, and §9.2 pairs it with resolution. <b>First-response breach is deliberately not folded in</b>,
/// because a single "breached" number mixing two deadlines would be unreadable and no document asks
/// for it.
/// </para>
///
/// <para>
/// <b>Every priority is enumerated, even one with no tickets</b> — and this is the one place in the
/// dashboard that does that. The question "which priorities are meeting their target" cannot be
/// answered if a priority silently vanishes from the list, so an empty priority appears as
/// <c>met: 0, breached: 0, attainmentPercent: null</c>.
/// </para>
///
/// <para>
/// <b><c>attainmentPercent</c> is null, never <c>100.0</c>, when there is nothing to measure.</b>
/// Reporting perfect attainment on no evidence is the same error as reporting a <c>0.0</c>
/// satisfaction average with no ratings — "empty is not zero" cuts both ways (api-design §6.8).
/// </para>
///
/// <para>
/// <b>OQ-2 does not reach this query.</b> Breach flags <em>latch</em> (data-model §2.6 invariant 5,
/// §5 constraint 13), so a ticket that breached stays counted as breached even after a later
/// priority change moved it into another row — which is what keeps the tile honest whichever way
/// OQ-2 had been decided. A test asserts exactly that.
/// </para>
/// </summary>
public static class SlaAttainmentQuery
{
    public static async Task<IReadOnlyList<SlaAttainmentRowDto>> RunAsync(
        IQueryable<Ticket> filtered, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtered);

        // One grouped round trip; the priorities with no rows are filled in afterwards, in memory,
        // over a fixed four-member enum — not by a second query.
        var observed = await filtered
            .GroupBy(t => t.Priority)
            .Select(g => new
            {
                Priority = g.Key,
                Breached = g.Count(t => t.ResolutionBreached),
                Total = g.Count(),
            })
            .ToListAsync(ct);

        var rows = new List<SlaAttainmentRowDto>();

        // Enum declaration order is the severity order (data-model, TicketPriority), so the tile
        // reads Low → Urgent consistently rather than in whatever order the database grouped.
        foreach (var priority in Enum.GetValues<TicketPriority>())
        {
            var row = observed.FirstOrDefault(o => o.Priority == priority);

            var total = row?.Total ?? 0;
            var breached = row?.Breached ?? 0;
            var met = total - breached;

            // null, not 100.0 — see the class remarks. This is the whole point of the row.
            double? attainmentPercent = total == 0
                ? null
                : Math.Round(met * 100.0 / total, 1);

            rows.Add(new SlaAttainmentRowDto(priority.ToString(), met, breached, attainmentPercent));
        }

        return rows;
    }
}
