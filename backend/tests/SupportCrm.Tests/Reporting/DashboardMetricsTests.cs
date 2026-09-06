using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Reporting;

/// <summary>
/// <b>What the four metric groups actually compute</b> — Story 15, requirements §9,
/// docs/api-design.md §6.8.
///
/// <para>
/// <b>"Empty is not zero" is the theme of this file.</b> Three separate tests assert a
/// <c>null</c> where a naive implementation would produce a confident number: no ratings is not
/// <c>0.0</c> satisfaction, no resolutions is not <c>0.0</c> hours, and no tickets at a priority is
/// not <c>100%</c> SLA attainment. Each of those zeros would be a lie a manager might act on.
/// </para>
///
/// <para>
/// Every fixture in this class uses <b>its own department or branch</b> where it can, because the
/// class fixture is shared: an assertion that counted "all tickets" would be coupled to whatever
/// other tests happened to create.
/// </para>
/// </summary>
public sealed class DashboardMetricsTests(ReportingFixture fixture) : IClassFixture<ReportingFixture>
{
    private const string Dashboard = "/api/v1/reports/dashboard";

    /// <summary>
    /// Test 3 — <b><c>byStatus</c> sums to the total ticket count in scope.</b> The four breakdowns
    /// are four views of one set, so a grouping that dropped or double-counted a row shows up here.
    /// </summary>
    [Fact]
    public async Task Status_counts_sum_to_the_tickets_in_scope()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Sum Branch");
        var customerId = await AddCustomerInBranchAsync("sum@reporting.local", branchId);

        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId);
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            status: TicketStatus.Open);
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            resolvedAfterHours: 3);

        var body = await GetAsync($"?branchId={branchId}");

        var counts = body.GetProperty("ticketCounts");

        var byStatus = counts.GetProperty("byStatus").EnumerateArray()
            .Sum(e => e.GetProperty("count").GetInt32());

        // The other three breakdowns are the same set sliced differently, so they must agree.
        var byPriority = counts.GetProperty("byPriority").EnumerateArray()
            .Sum(e => e.GetProperty("count").GetInt32());
        var byCategory = counts.GetProperty("byCategory").EnumerateArray()
            .Sum(e => e.GetProperty("count").GetInt32());
        var byDepartment = counts.GetProperty("byDepartment").EnumerateArray()
            .Sum(e => e.GetProperty("count").GetInt32());

        Assert.Equal(3, byStatus);
        Assert.Equal(3, byPriority);
        Assert.Equal(3, byCategory);
        Assert.Equal(3, byDepartment);
    }

    /// <summary>
    /// Test 4 — <b>the filters combine, and <c>branchId</c> narrows THROUGH THE CUSTOMER.</b>
    ///
    /// <para>
    /// The ticket below belongs to a <b>North Branch</b> customer but is worked by an agent whose
    /// own branch is different — the arrangement api-design §4.4 requires the filter to follow.
    /// Filtering by the customer's branch must find it; filtering by an unrelated branch must not.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Branch_filters_through_the_customer_and_combines_with_department()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Derivation Branch");
        var customerId = await AddCustomerInBranchAsync("derivation@reporting.local", branchId);

        // Customer is in `branchId`; the assigned agent is not.
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            assignedTo: fixture.BillingAgentId);

        var byBranch = await GetAsync($"?branchId={branchId}");
        Assert.Equal(1, TotalTickets(byBranch));

        // Both filters together, agreeing — the ticket is in this department AND this branch.
        var combined = await GetAsync(
            $"?departmentId={ReportingFixture.BillingDepartmentId}&branchId={branchId}");
        Assert.Equal(1, TotalTickets(combined));

        // Both filters together, disagreeing — the ticket is in this branch but NOT this
        // department, so combining must exclude it. This is what "the filters combine" means.
        var contradictory = await GetAsync(
            $"?departmentId={ReportingFixture.TechnicalDepartmentId}&branchId={branchId}");
        Assert.Equal(0, TotalTickets(contradictory));
    }

    /// <summary>
    /// Test 5 — <b>with no feedback rows, <c>averageRating</c> is <c>null</c>, explicitly NOT
    /// <c>0.0</c></b> (data-model §2.15, api-design §6.8). Declining is a normal outcome, so the
    /// absence of a row means "no response" and a zero would read as universal dissatisfaction.
    /// </summary>
    [Fact]
    public async Task With_no_ratings_the_average_is_null_and_not_zero()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("No Ratings Branch");
        var customerId = await AddCustomerInBranchAsync("noratings@reporting.local", branchId);

        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            resolvedAfterHours: 2);

        var satisfaction = (await GetAsync($"?branchId={branchId}")).GetProperty("satisfaction");

        Assert.Equal(JsonValueKind.Null, satisfaction.GetProperty("averageRating").ValueKind);
        Assert.Equal(0, satisfaction.GetProperty("responseCount").GetInt32());

        // The scale still travels, so the tile can render its denominator without hardcoding one
        // (OQ-1 is not answered by this story).
        var scale = satisfaction.GetProperty("ratingScale");
        Assert.True(scale.GetProperty("max").GetInt32() > scale.GetProperty("min").GetInt32());
    }

    /// <summary>The positive half: with ratings, the average is the real mean of them.</summary>
    [Fact]
    public async Task With_ratings_the_average_is_computed_from_them()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Ratings Branch");
        var customerId = await AddCustomerInBranchAsync("ratings@reporting.local", branchId);

        var first = await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            resolvedAfterHours: 2);
        var second = await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            resolvedAfterHours: 2);

        // Derived from the configured scale rather than written as numbers, so this file survives
        // whatever OQ-1 decides — the same rule story 13's FeedbackTests hold to.
        var scale = (await GetAsync($"?branchId={branchId}"))
            .GetProperty("satisfaction").GetProperty("ratingScale");

        var min = scale.GetProperty("min").GetInt32();
        var max = scale.GetProperty("max").GetInt32();

        await fixture.AddFeedbackAsync(first, min);
        await fixture.AddFeedbackAsync(second, max);

        var satisfaction = (await GetAsync($"?branchId={branchId}")).GetProperty("satisfaction");

        Assert.Equal(2, satisfaction.GetProperty("responseCount").GetInt32());
        Assert.Equal(
            Math.Round((min + max) / 2.0, 1),
            satisfaction.GetProperty("averageRating").GetDouble());
    }

    /// <summary>
    /// Test 6 — <b>an agent who has resolved nothing has <c>averageResolutionHours: null</c></b>,
    /// never <c>0.0</c> (api-design §6.8). The idle agent still appears as a row: an agent absent
    /// from the table is indistinguishable from an agent who does not exist.
    /// </summary>
    [Fact]
    public async Task An_agent_who_resolved_nothing_has_a_null_average()
    {
        var body = await GetAsync(string.Empty);

        var idle = body.GetProperty("agentPerformance").EnumerateArray()
            .Single(a => a.GetProperty("agent").GetProperty("id").GetGuid() == fixture.IdleAgentId);

        Assert.Equal(0, idle.GetProperty("resolvedCount").GetInt32());
        Assert.True(IsAbsentOrNull(idle, "averageResolutionHours"));
    }

    /// <summary>
    /// <b>PF-4 as decided: <c>assignedCount</c> is CURRENTLY assigned.</b> Decided by the product
    /// owner on 2026-09-06 and encoded in exactly one place, <c>AgentPerformanceQuery</c>.
    ///
    /// <para>
    /// The distinguishing case is the one asserted: a <b>closed</b> ticket that is still stamped
    /// with the agent's id must <b>not</b> count, because it is no longer on their plate. Under the
    /// rejected "ever assigned" reading it would. This test is what pins the decision.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Assigned_count_is_currently_assigned_and_excludes_terminal_tickets()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Assigned Branch");
        var customerId = await AddCustomerInBranchAsync("assigned@reporting.local", branchId);
        var agentId = await AddAgentAsync("counted.agent@reporting.local");

        // Open — counts.
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            assignedTo: agentId, status: TicketStatus.Open);

        // Cancelled — assigned to the same agent, but terminal, so it does NOT count.
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            assignedTo: agentId, status: TicketStatus.Cancelled);

        // Closed — resolved then closed by the same agent. Does NOT count as assigned, but DOES
        // count as resolved: the two columns measure different things.
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            assignedTo: agentId, resolvedAfterHours: 5, status: TicketStatus.Closed);

        var row = (await GetAsync($"?branchId={branchId}"))
            .GetProperty("agentPerformance").EnumerateArray()
            .Single(a => a.GetProperty("agent").GetProperty("id").GetGuid() == agentId);

        Assert.Equal(1, row.GetProperty("assignedCount").GetInt32());
        Assert.Equal(1, row.GetProperty("resolvedCount").GetInt32());
        Assert.Equal(5.0, row.GetProperty("averageResolutionHours").GetDouble(), 1);
    }

    /// <summary>
    /// Test 7 — <b>a priority with no tickets has <c>attainmentPercent: null</c>, not <c>100</c>.</b>
    /// Claiming perfect attainment on no evidence is the same error as a <c>0.0</c> average rating.
    /// </summary>
    [Fact]
    public async Task A_priority_with_no_tickets_has_a_null_attainment_percent()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Sparse Branch");
        var customerId = await AddCustomerInBranchAsync("sparse@reporting.local", branchId);

        // Exactly one Low ticket, so every other priority is empty in this selection.
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            priority: TicketPriority.Low);

        var rows = (await GetAsync($"?branchId={branchId}"))
            .GetProperty("slaAttainment").EnumerateArray().ToList();

        // Every priority is enumerated — a missing row would make "which priorities are healthy"
        // unanswerable.
        Assert.Equal(Enum.GetValues<TicketPriority>().Length, rows.Count);

        var urgent = rows.Single(r => r.GetProperty("priority").GetString() == "Urgent");
        Assert.Equal(0, urgent.GetProperty("met").GetInt32());
        Assert.Equal(0, urgent.GetProperty("breached").GetInt32());
        Assert.True(IsAbsentOrNull(urgent, "attainmentPercent"));

        // And the populated one does carry a number, so the null above is not vacuous.
        var low = rows.Single(r => r.GetProperty("priority").GetString() == "Low");
        Assert.Equal(100.0, low.GetProperty("attainmentPercent").GetDouble());
    }

    /// <summary>
    /// Test 8 — <b>a latched breach is still counted after the ticket's priority changed.</b>
    ///
    /// <para>
    /// Breach flags latch (data-model §5 constraint 13), so the ticket is counted as breached in
    /// whichever priority row it now sits in. That is what keeps this tile honest regardless of how
    /// OQ-2 was decided — nothing here depends on the answer.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_latched_breach_survives_a_later_priority_change()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Latched Branch");
        var customerId = await AddCustomerInBranchAsync("latched@reporting.local", branchId);

        var ticketId = await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
            priority: TicketPriority.Low, breached: true);

        // Escalate the priority AFTER the breach latched, exactly as an escalation would.
        await fixture.Factory.WithDbAsync(async db =>
        {
            var ticket = await db.Tickets.FindAsync(ticketId);

            // The same one-level raise escalation performs (Escalation.RaiseOneLevel, AP-7),
            // applied through the entity's own mutator rather than by writing the column.
            ticket!.ChangePriority(Escalation.RaiseOneLevel(ticket.Priority));

            return await db.SaveChangesAsync();
        });

        var rows = (await GetAsync($"?branchId={branchId}"))
            .GetProperty("slaAttainment").EnumerateArray().ToList();

        // It moved rows, and it is still breached — the flag did not reset.
        var medium = rows.Single(r => r.GetProperty("priority").GetString() == "Medium");
        Assert.Equal(1, medium.GetProperty("breached").GetInt32());
        Assert.Equal(0, medium.GetProperty("met").GetInt32());
        Assert.Equal(0.0, medium.GetProperty("attainmentPercent").GetDouble());

        var low = rows.Single(r => r.GetProperty("priority").GetString() == "Low");
        Assert.Equal(0, low.GetProperty("breached").GetInt32());
    }

    // ------------------------------------------------------------------ helpers

    private async Task<JsonElement> GetAsync(string query) =>
        await fixture.Factory.CreateClientFor(fixture.ManagerId)
            .GetFromJsonAsync<JsonElement>($"{Dashboard}{query}");

    private static int TotalTickets(JsonElement body) =>
        body.GetProperty("ticketCounts").GetProperty("byStatus").EnumerateArray()
            .Sum(e => e.GetProperty("count").GetInt32());

    /// <summary>
    /// A null is <b>omitted</b> from the payload rather than sent as <c>null</c> —
    /// <c>DefaultIgnoreCondition = WhenWritingNull</c> is set once on the MVC JSON options
    /// (docs/api-design.md §2). Absent and null mean the same thing, so a test must accept either.
    /// </summary>
    private static bool IsAbsentOrNull(JsonElement payload, string property) =>
        !payload.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    private Task<Guid> AddCustomerInBranchAsync(string email, Guid branchId) =>
        fixture.Factory.WithDbAsync(async db =>
        {
            var customer = SupportCrm.Domain.Modules.Customers.Customer.Create(
                Guid.NewGuid(), email, email, null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

    private Task<Guid> AddAgentAsync(string email) =>
        fixture.Factory.AddStaffUserAsync(
            SupportCrm.Domain.Modules.Identity.UserRole.Agent, email,
            departmentId: ReportingFixture.BillingDepartmentId);
}
