using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>The ticket's append-only history</b> — <c>GET /tickets/{id}/activity</c>, the customer
/// timeline it feeds, and the AD-10 separation from the audit log.
/// </summary>
public sealed class TicketHistoryTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";

    /// <summary>
    /// Test 14 — status, assignment, priority and category changes each appear with <b>actor,
    /// timestamp and before/after values</b>, which is the intake's acceptance criterion word for
    /// word.
    /// </summary>
    [Fact]
    public async Task The_activity_read_shows_every_change_with_actor_and_before_and_after()
    {
        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var created = await client.PostAsJsonAsync($"{Tickets}", new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "History",
            description = "A ticket to change in four ways.",
            categoryCode = "billing",
            priority = "Low",
        });

        var createdBody = await created.Content.ReadFromJsonAsync<JsonElement>();

        var ticketId = createdBody.GetProperty("id").GetGuid();

        // **Story 09 assigns at creation, so the manual assignment must target someone else.**
        // Round-robin picks whichever Billing-department staff member is least recently assigned, and
        // `AssignAsync` deliberately writes no history row when the assignee is unchanged — so
        // re-assigning to whoever the policy just picked would produce one `Assigned` row on some runs
        // and two on others, depending on the order the suite happened to execute in. Reading the
        // automatic assignee and moving the ticket to the other one makes this a change every time.
        var automaticAssignee = createdBody.GetProperty("assignee").GetProperty("id").GetGuid();

        var manualAssignee = automaticAssignee == fixture.BillingAgentId
            ? fixture.ManagerId
            : fixture.BillingAgentId;

        await client.PostAsJsonAsync($"{Tickets}/{ticketId}/assignment",
            new { assignedUserId = manualAssignee });
        await client.PatchAsJsonAsync($"{Tickets}/{ticketId}", new { priority = "High" });
        await client.PatchAsJsonAsync($"{Tickets}/{ticketId}", new { categoryCode = "payments" });
        await client.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });

        var response = await client.GetAsync($"{Tickets}/{ticketId}/activity");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = page.GetProperty("items").EnumerateArray().ToArray();

        var types = entries
            .Select(e => e.GetProperty("activityType").GetString() ?? string.Empty).ToArray();

        // **Two `Assigned` rows, and both are real.** Story 09's round-robin assigns at creation
        // (T2-D), and the manual `POST /assignment` above then overrides it with a different agent —
        // which is exactly the behaviour T2-D requires to be recorded ("a manual reassignment
        // overrides the automatic one and is recorded in ticket history"). The trail is append-only,
        // so the superseded automatic assignment stays visible rather than being rewritten.
        Assert.Equal(
            ["Created", "Assigned", "Assigned", "PriorityChanged", "CategoryChanged", "StatusChanged"],
            types);

        // Every entry carries an actor and a timestamp; §6.4's shape, member for member.
        foreach (var entry in entries)
        {
            Assert.Equal("User", entry.GetProperty("actorKind").GetString());
            Assert.Equal(
                fixture.BillingAgentId, entry.GetProperty("actor").GetProperty("id").GetGuid());
            Assert.True(entry.GetProperty("occurredAt").GetDateTimeOffset() > DateTimeOffset.MinValue);
        }

        var status = entries.Single(e => e.GetProperty("activityType").GetString() == "StatusChanged");

        Assert.Equal("New", status.GetProperty("oldValue").GetString());
        Assert.Equal("Open", status.GetProperty("newValue").GetString());

        var priority = entries.Single(e => e.GetProperty("activityType").GetString() == "PriorityChanged");

        Assert.Equal("Low", priority.GetProperty("oldValue").GetString());
        Assert.Equal("High", priority.GetProperty("newValue").GetString());

        // Created is not a change, so it carries no before/after (§2.7). The API omits nulls
        // rather than emitting them (Program.cs, WhenWritingNull), so "absent" is the shape.
        var createdEntry = entries.Single(e => e.GetProperty("activityType").GetString() == "Created");

        Assert.False(createdEntry.TryGetProperty("oldValue", out _));
        Assert.False(createdEntry.TryGetProperty("newValue", out _));
    }

    /// <summary>
    /// Test 15 — <b>append-only, proven by reflection rather than asserted in a comment.</b> The one
    /// writer exposes no way to change or remove an entry, and the context has no path that removes
    /// one.
    /// </summary>
    [Fact]
    public void The_history_recorder_exposes_no_update_or_delete()
    {
        var methods = typeof(TicketActivityRecorder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.All(methods, name =>
        {
            Assert.DoesNotContain("Update", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Delete", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Remove", name, StringComparison.OrdinalIgnoreCase);
        });

        // The entity itself offers no mutator either: every property is externally read-only, so
        // there is nothing for a later caller to set even if it found the instance.
        var settable = typeof(TicketActivity)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        Assert.Empty(settable);
    }

    /// <summary>
    /// The other half of append-only: <b>no source file removes a <c>TicketActivity</c></b>. A
    /// reflection test cannot see a <c>Remove</c> call, so this reads the source the way a reviewer
    /// would — and would fail the moment one appeared.
    /// </summary>
    [Fact]
    public void No_source_path_removes_a_ticket_activity()
    {
        var root = SourceRoot();

        var offenders = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path =>
            {
                var text = File.ReadAllText(path);

                return text.Contains("TicketActivities.Remove", StringComparison.Ordinal)
                    || text.Contains("TicketActivities.RemoveRange", StringComparison.Ordinal)
                    || text.Contains("TicketActivities.ExecuteDelete", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Empty(offenders);
    }

    /// <summary>
    /// Test 16 — the customer's interaction timeline <b>now reflects these entries</b>, completing
    /// what Story 04 built against an empty ticket set, <b>and contains no <c>Internal</c> entry</b>.
    /// </summary>
    [Fact]
    public async Task The_customer_timeline_reflects_ticket_activity_and_excludes_internal_entries()
    {
        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var customerId = fixture.NorthCustomerId;

        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, customerId, fixture.TechnicalAgentId,
            categoryCode: "technical", subject: "Timeline subject");

        // One visible entry through the real path, and one Internal entry written directly —
        // Internal-visibility writers are Story 14's, so the row is created beneath them here for
        // the single purpose of proving the exclusion is real rather than vacuous.
        await fixture.Factory.CreateClientFor(fixture.TechnicalAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });

        await fixture.Factory.WithDbAsync(async db =>
        {
            db.TicketActivities.Add(TicketActivity.ByUser(
                Guid.NewGuid(), ticketId, TicketActivityType.InternalNotePosted,
                fixture.TechnicalAgentId, DateTimeOffset.UtcNow,
                visibility: TicketActivityVisibility.Internal));

            return await db.SaveChangesAsync();
        });

        var response = await client.GetAsync($"/api/v1/customers/{customerId}/timeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = page.GetProperty("items").EnumerateArray().ToArray();

        Assert.NotEmpty(entries);

        // The visible status change is there, with its ticket's subject resolved.
        var statusChange = entries.Single(e =>
            e.GetProperty("activityType").GetString() == "StatusChanged"
            && e.GetProperty("ticketId").GetGuid() == ticketId);

        Assert.Equal("Timeline subject", statusChange.GetProperty("ticketSubject").GetString());
        Assert.Equal("New", statusChange.GetProperty("oldValue").GetString());
        Assert.Equal("Open", statusChange.GetProperty("newValue").GetString());

        // Exclusion 1 — no Internal entry ever appears.
        Assert.DoesNotContain(entries, e =>
            e.GetProperty("activityType").GetString() == "InternalNotePosted");

        // Newest first (docs/api-design.md §5.5).
        var occurred = entries.Select(e => e.GetProperty("occurredAt").GetDateTimeOffset()).ToArray();

        Assert.Equal(occurred.OrderByDescending(x => x), occurred);
    }

    /// <summary>
    /// Story 04's acceptance criterion, <b>still met now that the projection is real</b>: a customer
    /// with no tickets gets an empty page, not an error. That is now a fact about the data rather
    /// than about the schema.
    /// </summary>
    [Fact]
    public async Task A_customer_with_no_tickets_still_gets_an_empty_page()
    {
        var customerId = await fixture.Factory.WithDbAsync(async db =>
        {
            var branchId = await db.Branches.Select(b => b.Id).FirstAsync();

            var customer = SupportCrm.Domain.Modules.Customers.Customer.Create(
                Guid.NewGuid(), "no.tickets@tickets.local", $"no.tickets.{Guid.NewGuid():N}@tickets.local",
                null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .GetAsync($"/api/v1/customers/{customerId}/timeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, page.GetProperty("totalPages").GetInt32());
    }

    /// <summary>
    /// Test 17 — <b>ticket history and the audit log are independently queryable and neither is
    /// derived from the other</b> (AD-10). A status change writes one of each, and they are
    /// different rows in different tables carrying different facts: the activity row has the
    /// before/after values, the audit row has none because <c>AuditEntry</c> has no value columns.
    /// </summary>
    [Fact]
    public async Task Ticket_history_and_the_audit_log_stay_independent()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });

        var (activityCount, auditEntry) = await fixture.Factory.WithDbAsync(async db =>
        {
            var activities = await db.TicketActivities.AsNoTracking()
                .CountAsync(a => a.TicketId == ticketId
                              && a.ActivityType == TicketActivityType.StatusChanged);

            var audit = await db.AuditEntries.AsNoTracking()
                .Where(a => a.Action == AuditAction.TicketStatusChanged && a.TargetId == ticketId)
                .SingleAsync();

            return (activities, audit);
        });

        Assert.Equal(1, activityCount);
        Assert.Equal(AuditTargetType.Ticket, auditEntry.TargetType);
        Assert.Equal(fixture.BillingAgentId, auditEntry.ActorUserId);

        // A user-administration audit entry has no ticket; a ticket activity row has no audit id.
        // Neither table references the other, which is the structural half of AD-10.
        var activityHasNoAuditReference = typeof(TicketActivity)
            .GetProperties()
            .All(p => !p.Name.Contains("Audit", StringComparison.OrdinalIgnoreCase));

        var auditHasNoTicketReference = typeof(AuditEntry)
            .GetProperties()
            .All(p => !p.Name.Contains("Ticket", StringComparison.OrdinalIgnoreCase));

        Assert.True(activityHasNoAuditReference);
        Assert.True(auditHasNoTicketReference);
    }

    /// <summary>
    /// <b>The <c>actor</c>/<c>actorKind</c> invariant of §2.7 and §6.4</b>: the actor is null exactly
    /// when the kind is <c>System</c>. Asserted over the projection, because that is where a wrong
    /// join would produce the one combination the model forbids.
    /// </summary>
    [Fact]
    public async Task The_projection_never_produces_a_system_entry_with_an_actor()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });

        // A System row — Story 09's SLA monitor writes these; here it stands in for one.
        await fixture.Factory.WithDbAsync(async db =>
        {
            db.TicketActivities.Add(TicketActivity.BySystem(
                Guid.NewGuid(), ticketId, TicketActivityType.SlaBreached,
                DateTimeOffset.UtcNow, "false", "true"));

            return await db.SaveChangesAsync();
        });

        var page = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .GetFromJsonAsync<JsonElement>($"{Tickets}/{ticketId}/activity");

        foreach (var entry in page.GetProperty("items").EnumerateArray())
        {
            var isSystem = entry.GetProperty("actorKind").GetString() == "System";

            // Absent, not null: the API omits nulls (Program.cs, WhenWritingNull). Either way the
            // invariant is the same one — no actor exactly when the kind is System (§2.7, §6.4).
            var hasActor = entry.TryGetProperty("actor", out var actor)
                        && actor.ValueKind != JsonValueKind.Null;

            Assert.Equal(isSystem, !hasActor);
        }
    }

    /// <summary>
    /// <b>The staff activity read includes internal entries</b> — §5.6 says so in as many words, and
    /// the route is staff-only by the class policy. This is the deliberate difference from the
    /// customer timeline, which excludes them.
    /// </summary>
    [Fact]
    public async Task The_staff_activity_read_includes_internal_entries()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        await fixture.Factory.WithDbAsync(async db =>
        {
            db.TicketActivities.Add(TicketActivity.ByUser(
                Guid.NewGuid(), ticketId, TicketActivityType.InternalNotePosted,
                fixture.BillingAgentId, DateTimeOffset.UtcNow,
                visibility: TicketActivityVisibility.Internal));

            return await db.SaveChangesAsync();
        });

        var page = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .GetFromJsonAsync<JsonElement>($"{Tickets}/{ticketId}/activity");

        Assert.Contains(page.GetProperty("items").EnumerateArray(), e =>
            e.GetProperty("visibility").GetString() == "Internal");
    }

    /// <summary>
    /// The <b>R-14</b> rule, exercised through the service Story 07 will call: the automatic
    /// <c>Pending → Open</c> is attributed to the <b>replying customer</b>, with
    /// <c>actorKind = User</c> — <em>not</em> <c>System</c>.
    /// </summary>
    [Fact]
    public async Task The_automatic_reply_transition_is_attributed_to_the_replying_customer()
    {
        var customerUserId = await fixture.Factory.AddCustomerRoleUserAsync(
            $"replier.{Guid.NewGuid():N}@tickets.local");

        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        await client.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });
        await client.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Pending" });

        var reopened = await InvokeAutomaticTransitionAsync(ticketId, customerUserId);

        Assert.True(reopened);

        var (status, entry) = await fixture.Factory.WithDbAsync(async db =>
        {
            var ticketStatus = await db.Tickets.AsNoTracking()
                .Where(t => t.Id == ticketId).Select(t => t.Status).FirstAsync();

            var automatic = await db.TicketActivities.AsNoTracking()
                .Where(a => a.TicketId == ticketId
                         && a.ActivityType == TicketActivityType.StatusChanged
                         && a.OldValue == nameof(TicketStatus.Pending))
                .SingleAsync();

            return (ticketStatus, automatic);
        });

        Assert.Equal(TicketStatus.Open, status);
        Assert.Equal(TicketActorKind.User, entry.ActorKind);
        Assert.Equal(customerUserId, entry.ActorUserId);
        Assert.Equal("Open", entry.NewValue);
    }

    /// <summary>
    /// The same rule's <b>negative</b> half: a reply on any status other than <c>Pending</c> changes
    /// nothing. Reopening a <c>Resolved</c> ticket stays the explicit transition A-16 gives the
    /// customer, and a reply on <c>New</c> is not an agent starting work (A-18).
    /// </summary>
    [Theory]
    [InlineData("New")]
    [InlineData("Open")]
    [InlineData("Resolved")]
    public async Task The_automatic_reply_transition_fires_only_from_Pending(string startingStatus)
    {
        var customerUserId = await fixture.Factory.AddCustomerRoleUserAsync(
            $"replier.{Guid.NewGuid():N}@tickets.local");

        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        foreach (var step in startingStatus switch
        {
            "New" => Array.Empty<string>(),
            "Open" => ["Open"],
            _ => new[] { "Open", "Resolved" },
        })
        {
            await client.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = step });
        }

        var reopened = await InvokeAutomaticTransitionAsync(ticketId, customerUserId);

        Assert.False(reopened);

        var status = await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.AsNoTracking().Where(t => t.Id == ticketId)
                .Select(t => t.Status).FirstAsync());

        Assert.Equal(Enum.Parse<TicketStatus>(startingStatus), status);
    }

    /// <summary>
    /// Calls the R-13/R-14 rule the way Story 07's portal message endpoint will: load the ticket,
    /// apply the transition, commit with the caller's own unit of work.
    /// </summary>
    private async Task<bool> InvokeAutomaticTransitionAsync(Guid ticketId, Guid replyingCustomerUserId)
    {
        using var scope = fixture.Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<SupportCrm.Infrastructure.Persistence.SupportCrmDbContext>();

        var lifecycle = scope.ServiceProvider.GetRequiredService<TicketLifecycleService>();

        var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);

        var reopened = await lifecycle.ApplyAutomaticCustomerReplyTransitionAsync(
            ticket, replyingCustomerUserId, CancellationToken.None);

        await db.SaveChangesAsync();

        return reopened;
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine(directory!.FullName, "src");
    }
}
