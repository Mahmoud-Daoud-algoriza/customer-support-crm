using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>The suite product-scope T1-D demands</b>, and the one docs/architecture.md §4.3 opens by
/// asking for: <em>"the rule most likely to be got wrong, so it gets one implementation and one
/// test suite."</em>
///
/// <para>
/// Every assertion is made <b>through the API, as each role, bypassing the UI</b> (§4.3 point 5,
/// required explicitly by the <c>ticket-core</c> intake). A guard that only the client honours is
/// not a guard, so nothing here goes through <c>TicketScope</c> directly.
/// </para>
/// </summary>
public sealed class TicketScopingTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";

    // ------------------------------------------------------------- 1. Reads are scoped

    /// <summary>
    /// <b>With no filter applied at all</b>, an Agent's list is already narrowed to their own
    /// department. The scope is not something a caller opts into.
    /// </summary>
    [Fact]
    public async Task An_agent_list_returns_only_their_own_department()
    {
        var mine = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical");

        var ids = await ListIdsAsync(fixture.BillingAgentId);

        Assert.Contains(mine, ids);
        Assert.DoesNotContain(theirs, ids);
    }

    /// <summary>
    /// <b>`404`, not `403`</b> (AP-4). A `403` would confirm the ticket exists somewhere the caller
    /// cannot see, which is the inference AP-4 exists to prevent.
    /// </summary>
    [Fact]
    public async Task An_agent_reading_an_out_of_department_ticket_gets_404_not_403()
    {
        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical");

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .GetAsync($"{Tickets}/{theirs}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// <b>Proof by comparison, which is the only way to prove AP-4.</b> An out-of-department ticket
    /// and an id that exists nowhere must produce the <b>same status and the same body</b> — asserted
    /// against each other rather than separately, because two independently-correct assertions would
    /// still pass if the bodies differed.
    /// </summary>
    [Fact]
    public async Task Out_of_scope_and_nonexistent_are_indistinguishable()
    {
        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical");

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var outOfScope = await client.GetAsync($"{Tickets}/{theirs}");
        var nonexistent = await client.GetAsync($"{Tickets}/{Guid.NewGuid()}");

        Assert.Equal(outOfScope.StatusCode, nonexistent.StatusCode);

        var a = await outOfScope.Content.ReadFromJsonAsync<JsonElement>();
        var b = await nonexistent.Content.ReadFromJsonAsync<JsonElement>();

        // Every discriminating field, compared against the other response rather than against a
        // literal. `instance` is deliberately excluded: it echoes the URL the caller just requested
        // (`GET /api/v1/tickets/{id}`), so it necessarily differs and tells them nothing they did
        // not already know. Everything that could reveal whether the ticket exists is here.
        foreach (var field in new[] { "type", "title", "detail" })
        {
            Assert.Equal(a.GetProperty(field).GetString(), b.GetProperty(field).GetString());
        }

        Assert.Equal(a.GetProperty("status").GetInt32(), b.GetProperty("status").GetInt32());
    }

    // ------------------------------------------------ 2. Writes re-check on load

    /// <summary>
    /// <b>The write path re-checks on load</b> (§4.3 point 3), so a guessed identifier is refused —
    /// fetch-then-authorize, never authorize-then-fetch-by-id. Both write routes, not just the read.
    /// </summary>
    [Fact]
    public async Task Writes_to_an_out_of_department_ticket_are_refused_with_404()
    {
        // Assigned at creation, so "nothing was written" is a claim with something to compare
        // against on both fields rather than only on the priority.
        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical", assignedTo: fixture.TechnicalAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var patch = await client.PatchAsJsonAsync(
            $"{Tickets}/{theirs}", new { priority = "Urgent" });

        var assign = await client.PostAsJsonAsync(
            $"{Tickets}/{theirs}/assignment", new { assignedUserId = fixture.BillingAgentId });

        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, assign.StatusCode);

        // And nothing was written: the ticket still carries its original priority and no assignee.
        var after = await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.FindAsync(theirs));

        Assert.Equal(TicketPriority.Medium, after!.Priority);
        Assert.Equal(fixture.TechnicalAgentId, after.AssignedUserId);
    }

    // ------------------------------------------------------ 3. Manager sees all

    /// <summary>A Manager sees tickets from both departments (A-4).</summary>
    [Fact]
    public async Task A_manager_sees_tickets_from_every_department()
    {
        var billing = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var technical = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical");

        var ids = await ListIdsAsync(fixture.ManagerId);

        Assert.Contains(billing, ids);
        Assert.Contains(technical, ids);
    }

    // -------------------------------------- 4. A filter narrows, it never widens

    /// <summary>
    /// <b>An empty page, not an error and not another department's rows.</b> A <c>departmentId</c>
    /// filter narrows within what the caller may already see; supplying another department's id
    /// <em>simply matches nothing</em> (docs/api-design.md §4.3).
    /// </summary>
    [Fact]
    public async Task An_agent_filtering_by_another_department_gets_an_empty_page()
    {
        await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical");

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .GetAsync($"{Tickets}?departmentId={TicketApiFixture.TechnicalDepartmentId}");

        // Not a 400, not a 403 — a successful, empty answer.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, page.GetProperty("totalItems").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    // ------------------------------------------------- 5. The staff path is role-gated

    /// <summary>
    /// A <c>Customer</c> calling any staff ticket route gets <c>403</c> — the staff path space is
    /// role-gated, and the portal has its own routes (Story 07, AP-16). This is the role gate, a
    /// different thing from the department scoping above.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("?status=New")]
    public async Task A_customer_cannot_reach_the_staff_ticket_list(string query)
    {
        var customerUserId = await fixture.Factory.AddCustomerRoleUserAsync(
            $"scope.customer.{Guid.NewGuid():N}@test.local");

        var response = await fixture.Factory.CreateClientFor(customerUserId)
            .GetAsync($"{Tickets}{query}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Anonymous is `401`, not `403` — a different failure, and the contract distinguishes them.</summary>
    [Fact]
    public async Task An_anonymous_caller_gets_401()
    {
        var response = await fixture.Factory.CreateClient().GetAsync(Tickets);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Every staff role satisfies <c>RequireAgent</c> — the A-4 hierarchy.</summary>
    [Fact]
    public async Task Every_staff_role_reaches_the_list()
    {
        foreach (var userId in new[] { fixture.BillingAgentId, fixture.ManagerId })
        {
            var response = await fixture.Factory.CreateClientFor(userId).GetAsync(Tickets);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var adminId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Administrator, $"scope.admin.{Guid.NewGuid():N}@test.local",
            departmentId: TicketApiFixture.BillingDepartmentId);

        Assert.Equal(
            HttpStatusCode.OK,
            (await fixture.Factory.CreateClientFor(adminId).GetAsync(Tickets)).StatusCode);
    }

    private async Task<List<Guid>> ListIdsAsync(Guid callerId)
    {
        var page = await fixture.Factory.CreateClientFor(callerId)
            .GetFromJsonAsync<JsonElement>($"{Tickets}?pageSize=100");

        return [.. page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())];
    }
}
