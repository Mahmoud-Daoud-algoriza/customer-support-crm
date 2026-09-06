using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Reporting;

/// <summary>
/// §9.1 — ticket counts by status, priority, category and department (T2-G,
/// docs/api-design.md §6.8).
///
/// <para>
/// <b>Four <c>GROUP BY</c> projections over the same filtered set</b>, each executed in SQL. No
/// ticket row is materialized: the intake's rule is <em>"aggregate on the server; do not ship raw
/// ticket lists to the browser to be counted there"</em>, and counting in memory would break the
/// same rule one layer earlier.
/// </para>
///
/// <para>
/// <b>Only groups that exist are returned.</b> A status with no tickets is absent from
/// <c>byStatus</c> rather than present with <c>0</c> — §6.8's example carries the observed groups
/// and nothing else, and inventing an exhaustive zero-filled list would be this document deciding
/// what "no data" looks like. The front end renders an empty region when the whole breakdown is
/// empty (ui-design §5.7). <b>SLA attainment is the deliberate exception</b>: it enumerates every
/// priority, because "which priorities are meeting their target" is unanswerable if a priority can
/// silently vanish.
/// </para>
/// </summary>
public static class TicketCountsQuery
{
    /// <param name="filtered">
    /// The already-filtered ticket set. The filter is applied once, by
    /// <see cref="DashboardReportService"/>, so no query in this module re-expresses it.
    /// </param>
    public static async Task<TicketCountsDto> RunAsync(
        IQueryable<Ticket> filtered, IApplicationDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtered);
        ArgumentNullException.ThrowIfNull(db);

        // Enums are grouped by their stored string code (the Code tier of data-model §6.1), so the
        // key on the wire is the same stable token every other endpoint uses (docs/api-design.md §2).
        var byStatus = await filtered
            .GroupBy(t => t.Status)
            .Select(g => new CountByKeyDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var byPriority = await filtered
            .GroupBy(t => t.Priority)
            .Select(g => new CountByKeyDto(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var byCategory = await filtered
            .GroupBy(t => t.CategoryCode)
            .Select(g => new CountByKeyDto(g.Key, g.Count()))
            .ToListAsync(ct);

        // Department is the one breakdown that needs a name: its key is a GUID, and a GUID is not
        // renderable. The join is part of the same SQL statement, not a second round trip.
        var byDepartment = await filtered
            .GroupBy(t => t.DepartmentId)
            .Select(g => new CountByNamedKeyDto(
                g.Key.ToString(),
                db.Departments.Where(d => d.Id == g.Key).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                g.Count()))
            .ToListAsync(ct);

        return new TicketCountsDto(byStatus, byPriority, byCategory, byDepartment);
    }
}
