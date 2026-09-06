using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Reporting;

/// <summary>
/// §9.3 — per agent: tickets assigned, tickets resolved and average resolution time (T2-G,
/// docs/api-design.md §6.8).
///
/// <para>
/// <b>PF-4 / S9-9 is answered, and this file is the single place it is encoded.</b>
/// <em>"Tickets assigned"</em> was left undefined by every approved document — requirements §9.3 and
/// product-scope T2-G word it unqualified, api-design §5.11 says <em>"this contract does not decide
/// it"</em>, §6.8 calls the field <em>"deliberately unqualified"</em>, and §9 item 2 required it
/// pinned before this story was planned, which did not happen. <b>The product owner decided it on
/// 2026-09-06: it means CURRENTLY assigned</b> — open tickets on the agent's plate now.
/// </para>
///
/// <para>
/// <b>What that means concretely</b>, and the two consequences a reader should expect: a ticket
/// <em>reassigned away</em> from an agent stops counting for them, and a ticket they
/// <em>closed or cancelled</em> stops counting too. The rejected reading — <em>ever</em> assigned,
/// counted over <c>TicketActivity</c> rows of type <c>Assigned</c> — would have measured historical
/// throughput instead. <b>The response shape is identical under either reading</b>, exactly as §6.8
/// promised, so this decision changed no contract.
/// </para>
///
/// <para>
/// <b>The label is not qualified in the UI.</b> ui-design §11 requires the column to read exactly as
/// T2-G words it — <em>"Tickets assigned"</em> — with no clarifying tooltip. That instruction was
/// written while PF-4 was open; it is honoured here because the decision changed the semantics, not
/// the wording T2-G fixes.
/// </para>
///
/// <para>
/// <b><c>AverageResolutionHours</c> is null when the agent has resolved nothing</b>, never
/// <c>0.0</c> — §6.8 states this explicitly, and a zero would read as instantaneous resolution.
/// </para>
/// </summary>
public static class AgentPerformanceQuery
{
    public static async Task<IReadOnlyList<AgentPerformanceRowDto>> RunAsync(
        IQueryable<Ticket> filtered, IApplicationDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtered);
        ArgumentNullException.ThrowIfNull(db);

        // Every active staff user is a row, including one who has done nothing in this selection.
        // That is what makes the null-average path visible rather than hidden: an agent with no
        // resolutions must appear with "—", not be absent (ui-design §5.7, the plan's test 6).
        //
        // Customers are excluded because they are not agents; deactivated users are excluded
        // because a performance table of people who have left is noise, not a metric.
        var agents = await db.Users.AsNoTracking()
            .Where(u => u.Role != UserRole.Customer && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
            .ToListAsync(ct);

        // The two COUNTS aggregate in SQL, in one grouped round trip rather than two queries per
        // agent.
        //
        // PF-4 — ASSIGNED means CURRENTLY assigned: the ticket is still on their plate, so Closed
        // and Cancelled are excluded. Decided by the product owner 2026-09-06; see the class
        // remarks. This expression is the only home of that reading.
        var counts = await filtered
            .Where(t => t.AssignedUserId != null)
            .GroupBy(t => t.AssignedUserId!.Value)
            .Select(g => new
            {
                AgentId = g.Key,
                AssignedCount = g.Count(t =>
                    t.Status != TicketStatus.Closed && t.Status != TicketStatus.Cancelled),
                ResolvedCount = g.Count(t => t.ResolvedAt != null),
            })
            .ToListAsync(ct);

        // The AVERAGE is computed over a two-column projection rather than in SQL — finding I-38.
        //
        // Averaging a duration needs date arithmetic, and there is no expression for it that both
        // providers translate: EF.Functions.DateDiff* is SQL Server only and throws on the SQLite
        // test host, while TimeSpan subtraction translates on neither. A provider-conditional branch
        // here would put a database dialect inside the Application layer, which is worse.
        //
        // What is materialized is TWO TIMESTAMPS per RESOLVED ticket in the current selection — not
        // ticket rows, not subjects, not descriptions — and it never leaves the server. The intake's
        // actual rule, "do not ship raw ticket lists to the browser to be counted there", is
        // untouched: the response still carries no ticket array, which is asserted by its own test.
        var resolutionPoints = await filtered
            .Where(t => t.AssignedUserId != null && t.ResolvedAt != null)
            .Select(t => new { AgentId = t.AssignedUserId!.Value, t.CreatedAt, ResolvedAt = t.ResolvedAt!.Value })
            .ToListAsync(ct);

        var averageByAgent = resolutionPoints
            .GroupBy(p => p.AgentId)
            .ToDictionary(
                g => g.Key,
                g => Math.Round(g.Average(p => (p.ResolvedAt - p.CreatedAt).TotalHours), 1));

        return
        [
            .. agents.Select(agent =>
            {
                var row = counts.FirstOrDefault(s => s.AgentId == agent.Id);

                // Absent from the dictionary means "resolved nothing in this selection", which is
                // null — never 0.0 (docs/api-design.md §6.8).
                double? average = averageByAgent.TryGetValue(agent.Id, out var hours) ? hours : null;

                return new AgentPerformanceRowDto(
                    agent,
                    row?.AssignedCount ?? 0,
                    row?.ResolvedCount ?? 0,
                    average);
            })
        ];
    }
}
