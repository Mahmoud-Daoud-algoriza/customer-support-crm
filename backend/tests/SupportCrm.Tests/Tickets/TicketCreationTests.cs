using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// Story 05 plan task 10, items 7–13 — creation, the A-14 derivation, and assignment.
/// <para>
/// Every assertion is made through the API. Where a rule is about what was <b>written</b>, the row
/// is re-read afterwards: a status code is not evidence that the right thing happened.
/// </para>
/// </summary>
public sealed class TicketCreationTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";

    // ------------------------------------------------- 7 & 8. A-14 department derivation

    /// <summary>
    /// <b>Omitting <c>departmentId</c> derives it from the category map</b> (A-14): a `billing`
    /// ticket lands in the Billing department without the client naming one.
    /// </summary>
    [Fact]
    public async Task Omitting_the_department_derives_it_from_the_category_map()
    {
        var created = await CreateAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Derived department",
            description = "The category should decide the department.",
            categoryCode = "billing",
            priority = "Medium",
        });

        Assert.Equal(TicketApiFixture.BillingDepartmentId, created.GetProperty("departmentId").GetGuid());
    }

    /// <summary>
    /// <b>An agent supplying <c>departmentId</c> overrides the map</b> — A-14 makes the mapping a
    /// default and <em>not a cage for agents</em>. The category stays `billing` while the department
    /// is Technical, which is precisely the state the override exists to allow.
    /// </summary>
    [Fact]
    public async Task A_supplied_department_overrides_the_category_map()
    {
        var created = await CreateAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Overridden department",
            description = "The agent knows better than the map.",
            categoryCode = "billing",
            priority = "Medium",
            departmentId = TicketApiFixture.TechnicalDepartmentId,
        }, callerId: fixture.ManagerId);

        Assert.Equal(TicketApiFixture.TechnicalDepartmentId, created.GetProperty("departmentId").GetGuid());
        Assert.Equal("billing", created.GetProperty("categoryCode").GetString());
    }

    // ------------------------------------------------------- 9 & 10. Rejected input

    /// <summary>An unknown <c>categoryCode</c> is rejected, never defaulted (A-6, §5 constraint 11).</summary>
    [Fact]
    public async Task An_unknown_category_is_400()
    {
        var response = await PostAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Unknown category",
            description = "There is no such category.",
            categoryCode = "not-a-category",
            priority = "Medium",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>An unknown priority is rejected too — the four levels of A-6 and no others.</summary>
    [Fact]
    public async Task An_unknown_priority_is_400()
    {
        var response = await PostAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Unknown priority",
            description = "There is no such priority.",
            categoryCode = "billing",
            priority = "Catastrophic",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// <b><c>isUrgent</c> in a staff create body is a `400`</b> — <em>not</em> accepted and ignored
    /// (A-17, AP-10). It is customer input only, so the request model has no such property and
    /// <c>UnmappedMemberHandling.Disallow</c> refuses the body (finding I-9).
    /// <para>
    /// The second half matters as much as the status: <b>nothing was written</b>. A `400` that had
    /// already created the ticket would be worse than accepting the field.
    /// </para>
    /// </summary>
    [Fact]
    public async Task IsUrgent_on_a_staff_create_is_400_and_writes_nothing()
    {
        const string subject = "Urgent flag rejected";

        var response = await PostAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject,
            description = "isUrgent is customer input only.",
            categoryCode = "billing",
            priority = "Medium",
            isUrgent = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var written = await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.AnyAsync(t => t.Subject == subject));

        Assert.False(written);
    }

    /// <summary>
    /// <c>status</c> is server-derived and never a create field (AP-1, docs/api-design.md §7), so a
    /// body carrying it is refused rather than honoured.
    /// </summary>
    [Fact]
    public async Task Status_on_a_create_is_400()
    {
        var response = await PostAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Status rejected",
            description = "Status is not a create field.",
            categoryCode = "billing",
            priority = "Medium",
            status = "Resolved",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------ 11. The SLA clock

    /// <summary>
    /// <b>Both due timestamps are non-null on the created ticket and match the configured hours.</b>
    /// The configured targets for `High` are 4 and 24 hours (appsettings), measured from
    /// <c>createdAt</c> on a 24/7 clock (A-3).
    /// </summary>
    [Fact]
    public async Task Both_due_timestamps_are_set_from_the_configured_hours()
    {
        var created = await CreateAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "SLA clock",
            description = "Both deadlines should be computed at creation.",
            categoryCode = "billing",
            priority = "High",
        });

        var createdAt = created.GetProperty("createdAt").GetDateTimeOffset();
        var firstResponseDueAt = created.GetProperty("firstResponseDueAt").GetDateTimeOffset();
        var resolutionDueAt = created.GetProperty("resolutionDueAt").GetDateTimeOffset();

        Assert.Equal(createdAt.AddHours(4), firstResponseDueAt);
        Assert.Equal(createdAt.AddHours(24), resolutionDueAt);
    }

    /// <summary>
    /// <b>A-20 — the due timestamps freeze.</b> Changing the priority after creation leaves both
    /// exactly where they were, and this test is what would fail if
    /// <c>SlaClock.OnPriorityChanged</c> were ever "fixed" into recomputing.
    /// <para>
    /// The plan does not name this test. It is added because A-20's implementation is an empty
    /// method, and an empty method with no test is the one thing a later contributor removes
    /// believing it dead. Recorded as finding I-15.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Changing_priority_does_not_move_the_due_timestamps()
    {
        var created = await CreateAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "A-20 freeze",
            description = "Escalating must not move the deadline.",
            categoryCode = "billing",
            priority = "Low",
        });

        var id = created.GetProperty("id").GetGuid();
        var firstResponseDueAt = created.GetProperty("firstResponseDueAt").GetDateTimeOffset();
        var resolutionDueAt = created.GetProperty("resolutionDueAt").GetDateTimeOffset();

        // Low -> Urgent is the largest possible tightening, so recompute would be unmissable.
        var patched = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PatchAsJsonAsync($"{Tickets}/{id}", new { priority = "Urgent" });

        patched.EnsureSuccessStatusCode();

        var after = await patched.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Urgent", after.GetProperty("priority").GetString());
        Assert.Equal(firstResponseDueAt, after.GetProperty("firstResponseDueAt").GetDateTimeOffset());
        Assert.Equal(resolutionDueAt, after.GetProperty("resolutionDueAt").GetDateTimeOffset());
    }

    // --------------------------------------------------------- 12 & 13. Assignment

    /// <summary>
    /// <b>Assigning a `New` ticket leaves <c>status = New</c></b> (A-18) — asserted explicitly,
    /// because this is the detail most likely to be got wrong. Assignment is not the start of work.
    /// </summary>
    [Fact]
    public async Task Assigning_a_new_ticket_leaves_the_status_new()
    {
        var created = await CreateAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Assignment does not start work",
            description = "The status must stay New.",
            categoryCode = "billing",
            priority = "Medium",
        });

        var id = created.GetProperty("id").GetGuid();

        Assert.Equal("New", created.GetProperty("status").GetString());

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{id}/assignment", new { assignedUserId = fixture.BillingAgentId });

        response.EnsureSuccessStatusCode();

        var assigned = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Two independent facts: an assignee is present AND the status is untouched.
        Assert.Equal(fixture.BillingAgentId, assigned.GetProperty("assignee").GetProperty("id").GetGuid());
        Assert.Equal("New", assigned.GetProperty("status").GetString());

        // Re-read the row, not just the response: the response could be right while the row is not.
        var row = await fixture.Factory.WithDbAsync(async db => await db.Tickets.FindAsync(id));

        Assert.Equal(TicketStatus.New, row!.Status);
        Assert.Equal(fixture.BillingAgentId, row.AssignedUserId);
    }

    /// <summary>
    /// <b>An out-of-department assignee is <c>422 assignee-out-of-department</c></b> (§5
    /// constraint 10) — so a ticket can never be assigned to someone who could not then see it.
    /// </summary>
    [Fact]
    public async Task Assigning_an_out_of_department_agent_is_422()
    {
        var id = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{id}/assignment", new { assignedUserId = fixture.TechnicalAgentId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("assignee-out-of-department", problem.GetProperty("type").GetString());

        // Nothing was written.
        var row = await fixture.Factory.WithDbAsync(async db => await db.Tickets.FindAsync(id));

        Assert.Null(row!.AssignedUserId);
    }

    /// <summary>
    /// An inactive user in the right department is refused with the <b>same slug</b> — one answer
    /// for every way an assignee is unsuitable, so a caller cannot map the difference back to which
    /// staff accounts exist.
    /// </summary>
    [Fact]
    public async Task Assigning_a_deactivated_agent_is_422()
    {
        var deactivatedId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Agent, $"deactivated.{Guid.NewGuid():N}@test.local",
            departmentId: TicketApiFixture.BillingDepartmentId);

        await fixture.Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(deactivatedId);
            user!.Deactivate();

            return await db.SaveChangesAsync();
        });

        var id = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{id}/assignment", new { assignedUserId = deactivatedId });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>
    /// <b>Every assignment change is recorded as a <c>TicketActivity</c> row</b> — the intake's
    /// acceptance criterion, and the reason the entity is introduced in this story rather than in
    /// Story 06.
    /// </summary>
    [Fact]
    public async Task An_assignment_writes_an_activity_row()
    {
        var id = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{id}/assignment", new { assignedUserId = fixture.BillingAgentId });

        response.EnsureSuccessStatusCode();

        var entries = await fixture.Factory.WithDbAsync(async db =>
            await db.TicketActivities
                .Where(a => a.TicketId == id && a.ActivityType == TicketActivityType.Assigned)
                .ToListAsync());

        var entry = Assert.Single(entries);

        Assert.Equal(TicketActorKind.User, entry.ActorKind);
        Assert.Equal(fixture.BillingAgentId, entry.ActorUserId);
    }

    /// <summary>
    /// A <c>Created</c> activity row is written on the same path as the creation, in the same unit
    /// of work — so no committed state has a ticket without its first history entry.
    /// </summary>
    [Fact]
    public async Task Creating_a_ticket_writes_a_created_activity_row()
    {
        var created = await CreateAsync(new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "History spine starts here",
            description = "Creation is itself an activity.",
            categoryCode = "billing",
            priority = "Medium",
        });

        var id = created.GetProperty("id").GetGuid();

        var entries = await fixture.Factory.WithDbAsync(async db =>
            await db.TicketActivities.Where(a => a.TicketId == id).ToListAsync());

        var entry = Assert.Single(entries);

        Assert.Equal(TicketActivityType.Created, entry.ActivityType);
        Assert.Null(entry.OldValue);
        Assert.Null(entry.NewValue);
    }

    // -------------------------------------------------------------------- helpers

    private async Task<JsonElement> CreateAsync(object body, Guid? callerId = null)
    {
        var response = await PostAsync(body, callerId);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> PostAsync(object body, Guid? callerId = null) =>
        fixture.Factory.CreateClientFor(callerId ?? fixture.BillingAgentId)
            .PostAsJsonAsync(Tickets, body);
}
