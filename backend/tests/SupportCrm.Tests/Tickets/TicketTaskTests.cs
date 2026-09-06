using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>Ticket-attached tasks</b> — Story 14, requirements §4.3, T2-C.
///
/// <para>
/// Two rules carry the weight here and neither can be proven by reading the code: that
/// <c>completedAt</c> moves <b>exactly</b> with <c>isDone</c> in both directions (docs/data-model.md
/// §5 constraint 23), and that a client cannot set it (<b>AP-10</b>). The rest is scope and
/// assignee eligibility, which reuse rules Story 05 already proved for ticket assignment.
/// </para>
/// </summary>
public sealed class TicketTaskTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";

    /// <summary>
    /// Test 9 — a task is created with a due date and an assignee, and appears on the ticket.
    /// </summary>
    [Fact]
    public async Task A_task_is_created_with_a_due_date_and_an_assignee_and_appears_on_the_ticket()
    {
        var ticketId = await NewBillingTicketAsync();

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var dueAt = DateTimeOffset.UtcNow.AddDays(2);

        var response = await client.PostAsJsonAsync($"{Tickets}/{ticketId}/tasks", new
        {
            title = "Call the acquirer",
            dueAt,
            assignedUserId = fixture.BillingAgentId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Call the acquirer", created.GetProperty("title").GetString());
        Assert.Equal(fixture.BillingAgentId, created.GetProperty("assignee").GetProperty("id").GetGuid());

        // Created not-done, so the completion half of constraint 23 starts null.
        Assert.False(created.GetProperty("isDone").GetBoolean());
        Assert.True(IsAbsentOrNull(created, "completedAt"));

        // Actor and timestamp are recorded — the story's "recorded with actor and timestamp".
        Assert.Equal(fixture.BillingAgentId, created.GetProperty("createdBy").GetProperty("id").GetGuid());

        var list = await client.GetFromJsonAsync<JsonElement>($"{Tickets}/{ticketId}/tasks");

        Assert.Equal(1, list.GetProperty("totalItems").GetInt32());
    }

    /// <summary>
    /// Test 10 — <b>constraint 23 in both directions.</b> <c>PATCH { isDone: true }</c> sets
    /// <c>completedAt</c>; <c>PATCH { isDone: false }</c> <b>clears</b> it.
    ///
    /// <para>
    /// The un-done half is the one a nullable column invites getting wrong — a stale timestamp on a
    /// re-opened task would satisfy "set when done" and still break the invariant.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Completed_at_is_set_exactly_when_the_task_is_done()
    {
        var ticketId = await NewBillingTicketAsync();
        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var taskId = await CreateTaskAsync(client, ticketId);

        var done = await client.PatchAsJsonAsync(
            $"{Tickets}/{ticketId}/tasks/{taskId}", new { isDone = true });

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);

        var afterDone = await done.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(afterDone.GetProperty("isDone").GetBoolean());

        // Present and a real timestamp — the "set" half of constraint 23.
        Assert.True(afterDone.TryGetProperty("completedAt", out var completedAt));
        Assert.Equal(JsonValueKind.String, completedAt.ValueKind);

        var undone = await client.PatchAsJsonAsync(
            $"{Tickets}/{ticketId}/tasks/{taskId}", new { isDone = false });

        var afterUndone = await undone.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(afterUndone.GetProperty("isDone").GetBoolean());
        Assert.True(IsAbsentOrNull(afterUndone, "completedAt"));

        // And at the row, not only in the payload: the column is what constraint 23 is about.
        var stored = await fixture.Factory.WithDbAsync(db =>
            db.TicketTasks.AsNoTracking().SingleAsync(t => t.Id == taskId));

        Assert.False(stored.IsDone);
        Assert.Null(stored.CompletedAt);
    }

    /// <summary>
    /// Test 11 — <b>AP-10.</b> <c>completedAt</c> is server-derived (docs/api-design.md §5.6, §7), so
    /// a body carrying it is a <c>400</c> — never accepted and ignored, which would let a client
    /// believe it had set the timestamp.
    /// </summary>
    [Fact]
    public async Task A_patch_carrying_completed_at_is_refused()
    {
        var ticketId = await NewBillingTicketAsync();
        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var taskId = await CreateTaskAsync(client, ticketId);

        var json = """{ "isDone": true, "completedAt": "2026-01-01T00:00:00+00:00" }""";

        var response = await client.PatchAsync(
            $"{Tickets}/{ticketId}/tasks/{taskId}",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Refused whole: the task is untouched, not half-applied.
        var stored = await fixture.Factory.WithDbAsync(db =>
            db.TicketTasks.AsNoTracking().SingleAsync(t => t.Id == taskId));

        Assert.False(stored.IsDone);
        Assert.Null(stored.CompletedAt);
    }

    /// <summary>
    /// Test 12 — a task on an <b>out-of-department</b> ticket is <c>404</c> for an Agent, worded
    /// identically to a ticket that does not exist (AP-4). Scope is the ticket's, inherited.
    /// </summary>
    [Fact]
    public async Task A_task_on_an_out_of_department_ticket_is_not_found()
    {
        // A Technical ticket, reached by a Billing agent.
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.TechnicalAgentId,
            categoryCode: "technical");

        var intruder = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var list = await intruder.GetAsync($"{Tickets}/{ticketId}/tasks");

        var create = await intruder.PostAsJsonAsync($"{Tickets}/{ticketId}/tasks", new
        {
            title = "Should not be creatable",
            dueAt = DateTimeOffset.UtcNow.AddDays(1),
            assignedUserId = fixture.TechnicalAgentId,
        });

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
    }

    /// <summary>
    /// Test 13 — an assignee who is <b>deactivated</b> or <b>in another department</b> is
    /// <c>422 assignee-out-of-department</c> — the same slug and the same message ticket assignment
    /// uses (docs/data-model.md §5 constraint 10), because it is the same rule.
    /// </summary>
    [Fact]
    public async Task An_ineligible_assignee_is_refused()
    {
        var ticketId = await NewBillingTicketAsync();
        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var deactivated = await fixture.Factory.AddStaffUserAsync(
            UserRole.Agent, "deactivated@tasks.local",
            departmentId: TicketApiFixture.BillingDepartmentId);

        await fixture.Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == deactivated);
            user.Deactivate();

            return await db.SaveChangesAsync();
        });

        // In the right department, but not active.
        var inactive = await client.PostAsJsonAsync($"{Tickets}/{ticketId}/tasks", new
        {
            title = "Assigned to a deactivated agent",
            dueAt = DateTimeOffset.UtcNow.AddDays(1),
            assignedUserId = deactivated,
        });

        // Active, but in another department.
        var wrongDepartment = await client.PostAsJsonAsync($"{Tickets}/{ticketId}/tasks", new
        {
            title = "Assigned across departments",
            dueAt = DateTimeOffset.UtcNow.AddDays(1),
            assignedUserId = fixture.TechnicalAgentId,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongDepartment.StatusCode);

        // One slug for both, so neither answer reveals which staff accounts exist.
        foreach (var response in new[] { inactive, wrongDepartment })
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Contains(
                "assignee-out-of-department",
                problem.GetProperty("type").GetString(),
                StringComparison.Ordinal);
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A null <c>DateTimeOffset?</c> is <b>omitted</b> from the payload, not sent as
    /// <c>null</c> — <c>DefaultIgnoreCondition = WhenWritingNull</c> is set once on the MVC JSON
    /// options (docs/api-design.md §2), and nothing in this contract opts back in. The same idiom
    /// <c>NotificationTests</c> uses for <c>readAt</c> and <c>MessagingTests</c> for
    /// <c>firstRespondedAt</c>: absent and null mean the same thing, and a test must accept either
    /// or it is asserting the serializer's settings rather than the rule.
    /// </summary>
    private static bool IsAbsentOrNull(JsonElement payload, string property) =>
        !payload.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null;

    private Task<Guid> NewBillingTicketAsync() =>
        fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            assignedTo: fixture.BillingAgentId);

    private async Task<Guid> CreateTaskAsync(HttpClient client, Guid ticketId)
    {
        var response = await client.PostAsJsonAsync($"{Tickets}/{ticketId}/tasks", new
        {
            title = "Seeded task",
            dueAt = DateTimeOffset.UtcNow.AddDays(1),
            assignedUserId = fixture.BillingAgentId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        return created.GetProperty("id").GetGuid();
    }
}
