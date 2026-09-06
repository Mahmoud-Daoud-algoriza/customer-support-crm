using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Reporting;

/// <summary>
/// <b>Who may read the dashboard, and what "Manager+" does NOT mean</b> — Story 15, T2-G, A-4,
/// docs/api-design.md §5.11.
///
/// <para>
/// Two rules are proven here and they pull in opposite directions. The endpoint is <b>closed</b> to
/// Agents and Customers — a <c>403</c>, not a <c>404</c>, because refusing a capability the caller
/// can infer from their own role reveals nothing about which records exist (AP-4 is about record
/// visibility, not about capability). And it is <b>open across departments</b> for those who pass:
/// a Manager's aggregate is never narrowed to their own department, which is the failure AD-5 cites
/// as its reason for rejecting global query filters.
/// </para>
/// </summary>
public sealed class DashboardAccessTests(ReportingFixture fixture) : IClassFixture<ReportingFixture>
{
    private const string Dashboard = "/api/v1/reports/dashboard";

    /// <summary>
    /// Test 1 — <b>Agent and Customer are refused server-side; Manager and Administrator are not.</b>
    /// The front-end guard hides the route, but this is the rule that actually protects it.
    /// </summary>
    [Fact]
    public async Task Only_manager_and_administrator_may_read_the_dashboard()
    {
        var refused = new[] { fixture.BillingAgentId, fixture.CustomerUserId };

        foreach (var userId in refused)
        {
            var response = await fixture.Factory.CreateClientFor(userId).GetAsync(Dashboard);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        foreach (var userId in new[] { fixture.ManagerId, fixture.AdministratorId })
        {
            var response = await fixture.Factory.CreateClientFor(userId).GetAsync(Dashboard);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>Anonymous is <c>401</c>, not <c>403</c> — no token is a different failure from the wrong role.</summary>
    [Fact]
    public async Task An_anonymous_caller_is_unauthorized()
    {
        var response = await fixture.Factory.CreateClient().GetAsync(Dashboard);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 2 — <b>the aggregate is NOT narrowed by the caller's own department.</b>
    ///
    /// <para>
    /// The Manager's own department is Billing. Their unfiltered response must still include the
    /// Technical department's tickets — because <c>DashboardReportService</c> deliberately does not
    /// compose <c>TicketScope.ForCaller</c> (architecture §4.3, AD-5). This is the test that fails
    /// the moment someone "fixes" that by adding the scope helper.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_managers_unfiltered_dashboard_spans_both_departments()
    {
        await fixture.AddTicketAsync(
            ReportingFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        await fixture.AddTicketAsync(
            ReportingFixture.TechnicalDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.TechnicalAgentId, categoryCode: "technical");

        var body = await fixture.Factory.CreateClientFor(fixture.ManagerId)
            .GetFromJsonAsync<JsonElement>(Dashboard);

        var departments = body
            .GetProperty("ticketCounts").GetProperty("byDepartment")
            .EnumerateArray()
            .Select(d => d.GetProperty("key").GetString())
            .ToList();

        Assert.Contains(ReportingFixture.BillingDepartmentId.ToString(), departments);
        Assert.Contains(ReportingFixture.TechnicalDepartmentId.ToString(), departments);
    }

    /// <summary>
    /// Test 9 — <b><c>?format=csv</c> is a <c>400</c>.</b> There is no export, and an unknown query
    /// parameter must be refused rather than ignored (AP-15): silently returning JSON would let a
    /// caller believe an export existed and had been produced.
    /// </summary>
    [Theory]
    [InlineData("format=csv")]
    [InlineData("export=true")]
    [InlineData("from=2026-01-01")]
    public async Task An_unknown_query_parameter_is_refused(string query)
    {
        var response = await fixture.Factory.CreateClientFor(fixture.ManagerId)
            .GetAsync($"{Dashboard}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>The two approved filters are of course accepted — the guard above must not over-refuse.</summary>
    [Fact]
    public async Task The_two_approved_filters_are_accepted()
    {
        var response = await fixture.Factory.CreateClientFor(fixture.ManagerId).GetAsync(
            $"{Dashboard}?departmentId={ReportingFixture.BillingDepartmentId}&branchId={fixture.HeadOfficeBranchId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// There is <b>no second reporting route</b> — no export, no per-metric endpoint (T2-G,
    /// product-scope §8). Asserted by the router rather than by reading the controller.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/reports")]
    [InlineData("/api/v1/reports/dashboard/export")]
    [InlineData("/api/v1/reports/tickets")]
    public async Task No_other_reporting_route_exists(string path)
    {
        var response = await fixture.Factory.CreateClientFor(fixture.ManagerId).GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>The dashboard accepts no write verb — it is a read surface (T2-G).</summary>
    [Fact]
    public async Task The_dashboard_accepts_no_write_verb()
    {
        var client = fixture.Factory.CreateClientFor(fixture.ManagerId);

        var post = await client.PostAsJsonAsync(Dashboard, new { });
        var delete = await client.DeleteAsync(Dashboard);

        foreach (var response in new[] { post, delete })
        {
            Assert.Contains(
                response.StatusCode,
                new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        }

        _ = TicketStatus.Open;
    }
}
