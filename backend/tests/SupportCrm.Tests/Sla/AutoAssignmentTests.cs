using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Sla;

/// <summary>
/// Round-robin automatic assignment — T2-D's second line, asserted <b>through
/// <c>POST /tickets</c></b> rather than against the policy in isolation.
///
/// <para>
/// That matters: the policy runs at a seam inside <c>CreateAsync</c>, and the property T2-D actually
/// promises is about what happens when an agent creates a ticket. A unit test of the policy would
/// pass even if the seam were never called.
/// </para>
/// </summary>
public sealed class AutoAssignmentTests(SlaFixture fixture) : IClassFixture<SlaFixture>
{
    private const string Tickets = "/api/v1/tickets";

    /// <summary>
    /// <b>Assigned automatically, to an agent in the ticket's own department</b> — and the department
    /// is derived from the category (A-14), not supplied.
    /// </summary>
    [Fact]
    public async Task A_new_ticket_is_auto_assigned_to_an_active_agent_in_its_own_department()
    {
        var ticket = await CreateTicketAsync("billing");

        Assert.NotNull(ticket.Assignee);

        var assigneeId = ticket.Assignee!.Value;

        var department = await fixture.Factory.WithDbAsync(async db =>
            await db.Users.AsNoTracking()
                .Where(u => u.Id == assigneeId)
                .Select(u => u.DepartmentId)
                .FirstAsync());

        Assert.Equal(SlaFixture.BillingDepartmentId, department);
    }

    /// <summary>
    /// <b>The property T2-D names in as many words</b>: <em>"successive tickets go to different
    /// agents"</em>. Two Billing agents exist, so two consecutive tickets must not land on one.
    /// </summary>
    [Fact]
    public async Task Successive_tickets_in_one_department_go_to_different_agents()
    {
        var first = await CreateTicketAsync("billing");
        var second = await CreateTicketAsync("billing");

        Assert.NotNull(first.Assignee);
        Assert.NotNull(second.Assignee);
        Assert.NotEqual(first.Assignee, second.Assignee);
    }

    /// <summary>
    /// <b>Neither a deactivated agent nor another department's agent is ever selected.</b> Asserted
    /// together because they are the two ways the candidate query can be too wide, and both would look
    /// like a working feature until someone checked who got the ticket.
    /// </summary>
    [Fact]
    public async Task A_deactivated_agent_and_an_out_of_department_agent_are_never_selected()
    {
        // A Technical ticket must never reach a Billing agent, and vice versa.
        var technical = await CreateTicketAsync("technical");

        Assert.NotNull(technical.Assignee);
        Assert.Equal(fixture.TechnicalAgentId, technical.Assignee);

        // Now remove the only Technical agent and confirm the ticket is left unassigned rather than
        // handed to whoever is left in another department.
        await fixture.DeactivateAsync(fixture.TechnicalAgentId);

        var afterDeactivation = await CreateTicketAsync("technical");

        Assert.Null(afterDeactivation.Assignee);
    }

    /// <summary>
    /// <b>A-18, and this is the assertion the plan insists on making about the status rather than the
    /// assignee.</b> Automatic assignment leaves the ticket <c>New</c> — the customer's cancellation
    /// window depends on it (A-16).
    /// </summary>
    [Fact]
    public async Task The_auto_assigned_tickets_status_is_still_new()
    {
        var ticket = await CreateTicketAsync("billing");

        Assert.NotNull(ticket.Assignee);
        Assert.Equal(nameof(TicketStatus.New), ticket.Status);
    }

    /// <summary>
    /// <b>Manual assignment overrides the automatic one and is recorded.</b> The history row is the
    /// half that matters — the intake requires every assignment change to be traceable.
    /// </summary>
    [Fact]
    public async Task A_manual_reassignment_overrides_the_automatic_one_and_is_recorded()
    {
        var ticket = await CreateTicketAsync("billing");

        var automatic = ticket.Assignee!.Value;

        // Whichever Billing agent the rotation did NOT pick.
        var target = automatic == fixture.BillingAgentId
            ? fixture.BillingAgentTwoId
            : fixture.BillingAgentId;

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticket.Id}/assignment", new { assignedUserId = target });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await fixture.ReloadAsync(ticket.Id);

        Assert.Equal(target, after.AssignedUserId);

        // Two Assigned rows: the automatic one at creation and the manual override.
        var assignedRows = (await fixture.ActivityForAsync(ticket.Id))
            .Where(a => a.ActivityType == TicketActivityType.Assigned)
            .ToList();

        Assert.Equal(2, assignedRows.Count);
    }

    /// <summary>
    /// Creates a ticket through the API as a Billing agent and returns the fields these tests assert
    /// on. The department is never supplied — A-14 derives it from the category.
    /// </summary>
    private async Task<(Guid Id, string Status, Guid? Assignee)> CreateTicketAsync(string categoryCode)
    {
        var response = await fixture.Factory.CreateClientFor(fixture.BillingManagerId)
            .PostAsJsonAsync(Tickets, new
            {
                customerId = fixture.CustomerId,
                subject = $"Auto-assignment probe {Guid.NewGuid():N}",
                description = "Created to observe the round-robin policy.",
                categoryCode,
                priority = nameof(TicketPriority.Medium),
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var assignee = body.TryGetProperty("assignee", out var node) && node.ValueKind == JsonValueKind.Object
            ? node.GetProperty("id").GetGuid()
            : (Guid?)null;

        return (body.GetProperty("id").GetGuid(), body.GetProperty("status").GetString()!, assignee);
    }
}
