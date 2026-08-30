using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Tickets;

namespace SupportCrm.Tests.Organization;

/// <summary>
/// <b>The acceptance criterion that matters most</b>, from the `departments-branches` intake:
/// <em>"Branch is demonstrably NOT a permission boundary: an agent can see in-department tickets
/// regardless of the customer's branch."</em>
/// <para>
/// It was delivered in two halves, and <b>Story 05 completes both</b>:
/// </para>
/// <list type="number">
///   <item>
///     <b>The structural half</b> — <c>Ticket</c> has no branch member at all (docs/data-model.md
///     §2.3). Story 03 wrote it and skipped it, because <c>Ticket</c> did not exist; Story 05
///     task 10 removes the <c>Skip</c>, which is done below.
///   </item>
///   <item>
///     <b>The behavioural half</b> — an agent sees an in-department ticket whose customer is in a
///     different branch. Added here by Story 05 task 10.
///   </item>
/// </list>
/// </summary>
public sealed class BranchIsNotABoundaryTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    /// <summary>
    /// The structural half. <b>Story 05 replaced the assembly-qualified lookup with
    /// <c>typeof(Ticket)</c></b>, exactly as Story 03's comment anticipated: the type exists now, so
    /// a reflective string lookup that could silently resolve to null is no longer the honest form.
    /// </summary>
    [Fact]
    public void Ticket_has_no_branch_member()
    {
        // Ticket deliberately has NO branchId (docs/data-model.md §2.3). A ticket's branch is
        // derived Ticket -> Customer -> Branch, and the absence of the column is what makes misuse
        // impossible rather than merely discouraged: a branch value within reach of the ticket
        // scoping helper is a value A-2 forbids that helper to read.
        //
        // Should a branch-level access rule ever be required, that contradicts A-2 and is a SCOPE
        // CHANGE to be raised against docs/product-scope.md first — not a fix to this test.
        var branchMembers = typeof(Ticket)
            .GetMembers()
            .Where(m => m.Name.Contains("Branch", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(branchMembers);
    }

    /// <summary>
    /// The behavioural half, and the intake's sentence made executable: <b>an agent reaches an
    /// in-department ticket whose customer belongs to a different branch</b>.
    /// <para>
    /// The agent has no branch of their own and the customer is in North Branch, so if branch were
    /// a boundary anywhere in the read path this would return <c>404</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_agent_sees_an_in_department_ticket_whose_customer_is_in_another_branch()
    {
        var id = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.NorthCustomerId,
            fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        // The detail read.
        var detail = await client.GetAsync($"/api/v1/tickets/{id}");

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);

        // And the list, because a boundary could plausibly be applied in one and not the other.
        var page = await client.GetFromJsonAsync<JsonElement>("/api/v1/tickets?pageSize=100");

        var ids = page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .ToList();

        Assert.Contains(id, ids);
    }

    /// <summary>
    /// The converse, so the test above cannot pass by the scope being absent altogether: the same
    /// customer's ticket in <b>another department</b> is unreachable. Branch does not gate;
    /// department does.
    /// </summary>
    [Fact]
    public async Task The_same_customers_ticket_in_another_department_is_not_reachable()
    {
        var id = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId,
            fixture.NorthCustomerId,
            fixture.TechnicalAgentId,
            categoryCode: "technical");

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .GetAsync($"/api/v1/tickets/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
