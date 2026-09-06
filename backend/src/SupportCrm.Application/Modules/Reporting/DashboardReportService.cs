using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Reporting;

/// <summary>
/// <b>The one management-dashboard read</b> — requirements §9, T2-G, docs/api-design.md §5.11.
///
/// <para>
/// One public method composing four aggregate queries into the single response of §6.8. **One
/// dashboard with a small fixed metric set**: no report builder, no saved reports, no scheduling, no
/// export, and no second reporting endpoint (product-scope §8).
/// </para>
///
/// <para>
/// <b>§9 introduces no entity</b> (data-model §7) — every metric here is an aggregate over data
/// other stories own, which is why this story adds no migration.
/// </para>
///
/// <para>
/// <b>The filter is applied once, in <see cref="Filtered"/>, and nowhere else.</b> The four queries
/// receive an already-narrowed <see cref="IQueryable{T}"/>, so neither the department rule nor the
/// branch derivation is re-expressed four times and drifts in one of them.
/// </para>
/// </summary>
public sealed class DashboardReportService(IApplicationDbContext db, IOptions<FeedbackOptions> feedback)
{
    public async Task<DashboardReportDto> GetAsync(DashboardFilter filter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var filtered = Filtered(filter);

        // Sequential rather than concurrent: one DbContext is not thread-safe, and one unit of work
        // per request is the project's rule (architecture §3). These are four cheap aggregates.
        var ticketCounts = await TicketCountsQuery.RunAsync(filtered, db, ct);
        var slaAttainment = await SlaAttainmentQuery.RunAsync(filtered, ct);
        var agentPerformance = await AgentPerformanceQuery.RunAsync(filtered, db, ct);
        var satisfaction = await SatisfactionQuery.RunAsync(filtered, db, feedback.Value, ct);

        return new DashboardReportDto(ticketCounts, slaAttainment, agentPerformance, satisfaction);
    }

    /// <summary>
    /// The two filters of docs/api-design.md §5.11, applied once.
    ///
    /// <para>
    /// <b><c>TicketScope.ForCaller</c> is deliberately NOT composed here, and must not be added.</b>
    /// This endpoint is Manager+ only and both roles are unrestricted across departments
    /// (architecture §4.3, A-4), so composing the helper would be a no-op today — and wrong
    /// tomorrow: it would let a future role change <b>silently narrow an aggregate</b>, which is one
    /// of the four reasons AD-5 gives for rejecting EF global query filters in the first place
    /// (<em>"reporting aggregates must not be silently narrowed"</em>). The <c>departmentId</c>
    /// filter below is a <b>user's choice of view</b>, never a security scope. A test asserts that a
    /// Manager's unfiltered response spans both departments.
    /// </para>
    /// </summary>
    private IQueryable<Ticket> Filtered(DashboardFilter filter)
    {
        var query = db.Tickets.AsNoTracking();

        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(t => t.DepartmentId == departmentId);
        }

        if (filter.BranchId is { } branchId)
        {
            // docs/api-design.md §4.4 — Ticket has NO branch column; a ticket's branch is its
            // CUSTOMER's branch (data-model §2.3). Resolving it any other way would invent a field.
            query = query.Where(t => db.Customers.Any(c => c.Id == t.CustomerId && c.BranchId == branchId));
        }

        // Both filters compose: supplying each narrows further, which is the "filters combine"
        // criterion. Neither widens anything, because there is nothing wider than "all tickets".
        return query;
    }
}
