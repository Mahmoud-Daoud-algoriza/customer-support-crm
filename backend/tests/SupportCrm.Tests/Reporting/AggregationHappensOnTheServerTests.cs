using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Reporting;

/// <summary>
/// <b>The response shape does not grow with the database</b> — the intake's rule, verbatim:
/// <em>"Aggregate on the server; do not ship raw ticket lists to the browser to be counted there."</em>
///
/// <para>
/// <b>This is asserted by comparison, not by inspection.</b> Two dashboards are fetched — one over a
/// handful of tickets, one over many more — and their payloads must have the <b>same shape and the
/// same size class</b>. A future implementation that returned rows "just for the client to total up"
/// would pass every metric test in this folder and fail only here, which is why the test exists.
/// </para>
/// </summary>
public sealed class AggregationHappensOnTheServerTests(ReportingFixture fixture)
    : IClassFixture<ReportingFixture>
{
    private const string Dashboard = "/api/v1/reports/dashboard";

    /// <summary>
    /// Test 10 — <b>the payload for N tickets is independent of N.</b>
    /// </summary>
    [Fact]
    public async Task The_payload_does_not_grow_with_the_number_of_tickets()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Volume Branch");
        var customerId = await AddCustomerInBranchAsync("volume@reporting.local", branchId);

        await AddTicketsAsync(customerId, 3);
        var small = await RawAsync($"?branchId={branchId}");

        await AddTicketsAsync(customerId, 30);
        var large = await RawAsync($"?branchId={branchId}");

        // The counts really did change — otherwise the size comparison below proves nothing.
        Assert.Equal(3, TotalTickets(small));
        Assert.Equal(33, TotalTickets(large));

        // Ten times the tickets must not be ten times the payload. The only growth permitted is in
        // the digits of the counters themselves, so a generous allowance still catches a row dump.
        Assert.True(
            large.Length < small.Length + 200,
            $"Payload grew from {small.Length} to {large.Length} bytes for 10x the tickets — "
                + "the response is carrying rows rather than aggregates.");
    }

    /// <summary>
    /// The structural half: <b>no group in the response is a ticket array.</b> Asserted over the raw
    /// JSON, so a ticket list smuggled in under any property name is caught — not only one called
    /// "tickets".
    /// </summary>
    [Fact]
    public async Task No_group_in_the_response_carries_a_ticket()
    {
        var branchId = await fixture.Factory.EnsureBranchAsync("Shape Branch");
        var customerId = await AddCustomerInBranchAsync("shape@reporting.local", branchId);

        await AddTicketsAsync(customerId, 2);

        var raw = await RawAsync($"?branchId={branchId}");

        // Fields that only a ticket row would carry. None may appear anywhere in the payload.
        foreach (var leaked in new[] { "subject", "description", "ticketId", "categoryCode", "createdAt" })
        {
            Assert.DoesNotContain(leaked, raw, StringComparison.OrdinalIgnoreCase);
        }

        // And the four expected groups are all present, so the assertion above is not passing
        // because the response was empty.
        var body = JsonDocument.Parse(raw).RootElement;

        foreach (var group in new[] { "ticketCounts", "slaAttainment", "agentPerformance", "satisfaction" })
        {
            Assert.True(body.TryGetProperty(group, out _), $"Missing group '{group}'.");
        }
    }

    // ------------------------------------------------------------------ helpers

    private async Task<string> RawAsync(string query) =>
        await fixture.Factory.CreateClientFor(fixture.ManagerId)
            .GetStringAsync($"{Dashboard}{query}");

    private static int TotalTickets(string raw) =>
        JsonDocument.Parse(raw).RootElement
            .GetProperty("ticketCounts").GetProperty("byStatus").EnumerateArray()
            .Sum(e => e.GetProperty("count").GetInt32());

    private async Task AddTicketsAsync(Guid customerId, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await fixture.AddTicketAsync(
                ReportingFixture.BillingDepartmentId, customerId, fixture.BillingAgentId,
                priority: TicketPriority.Medium);
        }
    }

    private Task<Guid> AddCustomerInBranchAsync(string email, Guid branchId) =>
        fixture.Factory.WithDbAsync(async db =>
        {
            var customer = SupportCrm.Domain.Modules.Customers.Customer.Create(
                Guid.NewGuid(), email, email, null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });
}
