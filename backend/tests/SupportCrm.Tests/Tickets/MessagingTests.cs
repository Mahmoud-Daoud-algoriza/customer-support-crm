using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>Web form intake and in-portal messaging</b> — Story 07, the thirteen tests its plan names.
///
/// <para>
/// Three things are proven here that nothing else can prove: that <c>direction</c> and
/// <c>channel</c> are <b>server-derived</b> and refused in a body (<b>PF-7</b>, AP-10); that a
/// customer reply on a <c>Pending</c> ticket reopens it <b>in one transaction</b>, attributed to
/// the replying customer (<b>R-13</b>, <b>R-14</b>); and that the reopen raises <b>no</b>
/// notification while the reply itself raises exactly one (A-13).
/// </para>
///
/// <para>
/// <b>Nothing here polls, and nothing here is chat</b> (T3-B). Every call is ordinary
/// request/response.
/// </para>
/// </summary>
public sealed class MessagingTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";
    private const string Portal = "/api/v1/portal/tickets";

    // ------------------------------------------------------------------ the web form (§3.5)

    /// <summary>
    /// Test 1 — an <b>authenticated</b> customer submits, the ticket is linked to <b>that</b>
    /// customer, and the department comes from the <b>category</b> (A-14), which the body never
    /// carried.
    /// </summary>
    [Fact]
    public async Task An_authenticated_customer_submits_and_the_category_chooses_the_department()
    {
        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.PostAsJsonAsync(Portal, new
        {
            subject = "My card keeps failing",
            description = "Every attempt to pay is declined at the last step.",
            categoryCode = "payments",
            isUrgent = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ticketId = body.GetProperty("id").GetGuid();

        // The portal payload of §6.4: no department, no priority, no assignee, no SLA fields.
        Assert.Equal("New", body.GetProperty("status").GetString());
        Assert.True(body.GetProperty("isUrgent").GetBoolean());
        Assert.False(body.TryGetProperty("departmentId", out _));
        Assert.False(body.TryGetProperty("priority", out _));
        Assert.False(body.TryGetProperty("assignee", out _));
        Assert.False(body.TryGetProperty("firstResponseDueAt", out _));

        var stored = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        // Linked to the CALLER'S OWN profile — the body could not say who, and did not.
        Assert.Equal(fixture.HeadOfficeCustomerId, stored.CustomerId);

        // A-14: "payments" maps to Billing in the configured map, and the customer never named it.
        Assert.Equal(TicketApiFixture.BillingDepartmentId, stored.DepartmentId);

        // A-17: isUrgent is stored and does NOT set priority.
        Assert.True(stored.IsUrgent);
        Assert.Equal(TicketPriority.Medium, stored.Priority);

        // A-3: both due timestamps exist at creation, as §2.6 requires them to.
        Assert.True(stored.FirstResponseDueAt > stored.CreatedAt);
        Assert.True(stored.ResolutionDueAt > stored.CreatedAt);
    }

    /// <summary>Test 2 — <b>A-9: no anonymous submission.</b> There is no unauthenticated variant.</summary>
    [Fact]
    public async Task An_unauthenticated_submission_is_refused()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(Portal, new
        {
            subject = "Anonymous",
            description = "Submitted with no token at all.",
            categoryCode = "billing",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Test 3 — <b>AP-10.</b> <c>priority</c> and <c>departmentId</c> are server-derived on this
    /// path (docs/api-design.md §5.7, §7), so a body carrying either is a <c>400</c> — never
    /// accepted and ignored, which would let a client believe it had set them.
    /// </summary>
    [Theory]
    [InlineData("priority", "\"High\"")]
    [InlineData("departmentId", "\"11111111-1111-1111-1111-111111111102\"")]
    [InlineData("customerId", "\"11111111-1111-1111-1111-111111111199\"")]
    public async Task A_portal_submission_carrying_a_server_derived_field_is_refused(
        string member, string value)
    {
        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var json =
            $$"""
              { "subject": "Rejected", "description": "Carries a field it may not set.",
                "categoryCode": "billing", "{{member}}": {{value}} }
              """;

        var response = await client.PostAsync(
            Portal, new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("validation-failed", problem.GetProperty("type").GetString());
    }

    // ------------------------------------------------------------------ the thread (§3.3)

    /// <summary>
    /// Test 4 — customer and agent exchange replies, and <b>both</b> read the same thread in the
    /// same order. Two path spaces (AP-5), one message model.
    /// </summary>
    [Fact]
    public async Task Customer_and_agent_exchange_replies_and_both_read_the_thread_in_order()
    {
        var ticketId = await OpenTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        await PostStaffReplyAsync(agent, ticketId, "Could you send the reference?");
        await PostCustomerReplyAsync(customer, ticketId, "Reference is TRF-9182.");
        await PostStaffReplyAsync(agent, ticketId, "Found it — matching it now.");

        var staffThread = await ReadArrayAsync(agent, $"{Tickets}/{ticketId}/messages");
        var portalThread = await ReadArrayAsync(customer, $"{Portal}/{ticketId}/messages");

        Assert.Equal(
            ["Could you send the reference?", "Reference is TRF-9182.", "Found it — matching it now."],
            staffThread.Select(m => m.GetProperty("body").GetString()));

        // The same rows, the same order, through the other path space.
        Assert.Equal(
            staffThread.Select(m => m.GetProperty("id").GetGuid()),
            portalThread.Select(m => m.GetProperty("id").GetGuid()));

        // §6.4: the portal variant omits channel and authorRole, and keeps direction.
        Assert.All(portalThread, m =>
        {
            Assert.False(m.TryGetProperty("channel", out _));
            Assert.False(m.TryGetProperty("authorRole", out _));
            Assert.NotNull(m.GetProperty("direction").GetString());
        });

        // Every message records its channel of origin, author and timestamp (the intake's AC).
        Assert.All(staffThread, m =>
        {
            Assert.Equal(nameof(MessageChannel.Portal), m.GetProperty("channel").GetString());
            Assert.NotEqual(Guid.Empty, m.GetProperty("author").GetProperty("id").GetGuid());
            Assert.True(m.GetProperty("postedAt").GetDateTimeOffset() > DateTimeOffset.MinValue);
        });
    }

    /// <summary>
    /// Test 5 — <b>PF-7.</b> Direction follows the author's role with <b>no client input at all</b>,
    /// and a request that tries to supply it is a <c>400</c> (AP-10).
    /// </summary>
    [Fact]
    public async Task Direction_is_derived_from_the_author_and_never_accepted_from_the_body()
    {
        var ticketId = await OpenTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var fromAgent = await PostStaffReplyAsync(agent, ticketId, "From the agent.");
        var fromCustomer = await PostCustomerReplyAsync(customer, ticketId, "From the customer.");

        Assert.Equal(
            nameof(MessageDirection.Outbound), fromAgent.GetProperty("direction").GetString());
        Assert.Equal(
            nameof(MessageDirection.Inbound),
            fromCustomer.GetProperty("message").GetProperty("direction").GetString());

        // The agent's role rides on the staff payload; direction is derived from it, not sent.
        Assert.Equal("Agent", fromAgent.GetProperty("authorRole").GetString());

        foreach (var (client, path) in new[]
                 {
                     (agent, $"{Tickets}/{ticketId}/messages"),
                     (customer, $"{Portal}/{ticketId}/messages"),
                 })
        {
            var refused = await client.PostAsync(
                path,
                new StringContent(
                    """{ "body": "Trying to pick a side.", "direction": "Outbound" }""",
                    System.Text.Encoding.UTF8,
                    "application/json"));

            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }

        // channel is refused on the same grounds (docs/api-design.md §7).
        var channelRefused = await agent.PostAsync(
            $"{Tickets}/{ticketId}/messages",
            new StringContent(
                """{ "body": "Trying to pick a channel.", "channel": "WebForm" }""",
                System.Text.Encoding.UTF8,
                "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, channelRefused.StatusCode);
    }

    /// <summary>
    /// Test 6 — <b>§5 constraint 17.</b> Exactly one <c>MessagePosted</c> activity row per message,
    /// each pointing at its own message, and <b>the body is not copied onto the row</b> (DM-4).
    /// </summary>
    [Fact]
    public async Task Every_message_has_exactly_one_message_posted_activity_row()
    {
        var ticketId = await OpenTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        await PostStaffReplyAsync(agent, ticketId, "One.");
        await PostCustomerReplyAsync(customer, ticketId, "Two.");
        await PostStaffReplyAsync(agent, ticketId, "Three.");

        var messages = await ReadArrayAsync(agent, $"{Tickets}/{ticketId}/messages");
        var activity = await ReadArrayAsync(agent, $"{Tickets}/{ticketId}/activity");

        var posted = activity
            .Where(a => a.GetProperty("activityType").GetString() == nameof(TicketActivityType.MessagePosted))
            .ToArray();

        Assert.Equal(3, posted.Length);

        // One row per message, and no row without a message: the two id sets are equal.
        Assert.Equal(
            messages.Select(m => m.GetProperty("id").GetGuid()).OrderBy(id => id),
            posted.Select(a => a.GetProperty("messageId").GetGuid()).OrderBy(id => id));

        // Content lives once (DM-4). The row carries no before/after, because posting is not a
        // change to a field — the API omits nulls, so "absent" is the shape.
        Assert.All(posted, a =>
        {
            Assert.Equal("CustomerVisible", a.GetProperty("visibility").GetString());
            Assert.False(a.TryGetProperty("oldValue", out _));
            Assert.False(a.TryGetProperty("newValue", out _));
        });
    }

    /// <summary>
    /// Test 7 — <b>A-5 / §5 constraint 8.</b> <c>Closed</c> and <c>Cancelled</c> are terminal: no
    /// further messages, from either side.
    /// </summary>
    [Theory]
    [InlineData("Closed")]
    [InlineData("Cancelled")]
    public async Task A_reply_on_a_terminal_ticket_is_refused(string terminalStatus)
    {
        var ticketId = await CreateTicketAsync();
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        foreach (var step in terminalStatus == "Closed"
                     ? new[] { "Open", "Resolved", "Closed" }
                     : ["Cancelled"])
        {
            var moved = await agent.PostAsJsonAsync(
                $"{Tickets}/{ticketId}/transition", new { targetStatus = step });

            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        }

        var staffReply = await agent.PostAsJsonAsync(
            $"{Tickets}/{ticketId}/messages", new { body = "Anything else?" });

        Assert.Equal(HttpStatusCode.Conflict, staffReply.StatusCode);

        var problem = await staffReply.Content.ReadFromJsonAsync<JsonElement>();

        // NOT illegal-transition: nothing was transitioned, and reusing that slug would make the
        // front end render a lifecycle message for a refused reply.
        Assert.Equal("ticket-terminal", problem.GetProperty("type").GetString());

        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var customerReply = await customer.PostAsJsonAsync(
            $"{Portal}/{ticketId}/messages", new { body = "One more thing." });

        Assert.Equal(HttpStatusCode.Conflict, customerReply.StatusCode);

        // And nothing was written: a refused reply leaves no message and no activity row.
        var stored = await fixture.Factory.WithDbAsync(db =>
            db.TicketMessages.AsNoTracking().CountAsync(m => m.TicketId == ticketId));

        Assert.Equal(0, stored);
    }

    /// <summary>
    /// Test 8 — <b>§2.8.</b> The <b>first</b> outbound message sets <c>firstRespondedAt</c>; the
    /// second does not move it. Inbound messages never set it at all.
    /// </summary>
    [Fact]
    public async Task The_first_outbound_message_sets_first_responded_at_and_the_second_does_not()
    {
        var ticketId = await OpenTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        Assert.Null(await FirstRespondedAtAsync(agent, ticketId));

        // A customer reply is not a response TO the customer, so it must not stamp it.
        await PostCustomerReplyAsync(customer, ticketId, "Still waiting.");

        Assert.Null(await FirstRespondedAtAsync(agent, ticketId));

        await PostStaffReplyAsync(agent, ticketId, "Looking into it now.");

        var first = await FirstRespondedAtAsync(agent, ticketId);

        Assert.NotNull(first);

        await PostStaffReplyAsync(agent, ticketId, "And an update.");

        Assert.Equal(first, await FirstRespondedAtAsync(agent, ticketId));
    }

    // ------------------------------------------------------- R-13 / R-14, the one side effect

    /// <summary>
    /// Test 9 — <b>the rule this story exists to deliver.</b> A customer reply on a <c>Pending</c>
    /// ticket returns it to <c>Open</c>, in the same transaction, with <b>both</b> history entries
    /// written and the <c>StatusChanged</c> row attributed to <b>the replying customer</b> with
    /// <c>actorKind: "User"</c> (<b>R-14</b>).
    /// </summary>
    [Fact]
    public async Task A_customer_reply_on_a_pending_ticket_reopens_it_and_says_so_in_the_response()
    {
        var ticketId = await OpenTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        await agent.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Pending" });

        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var envelope = await PostCustomerReplyAsync(customer, ticketId, "Here is the reference.");

        // The client never has to guess and never has to re-fetch (§6.4).
        Assert.True(envelope.GetProperty("statusChanged").GetBoolean());
        Assert.Equal("Open", envelope.GetProperty("ticketStatus").GetString());

        var activity = await ReadArrayAsync(agent, $"{Tickets}/{ticketId}/activity");

        var messagePosted = Assert.Single(
            activity,
            a => a.GetProperty("activityType").GetString() == nameof(TicketActivityType.MessagePosted));

        // BOTH entries, and the automatic one is the last StatusChanged on the trail.
        var statusChanged = activity
            .Where(a => a.GetProperty("activityType").GetString() == nameof(TicketActivityType.StatusChanged))
            .Last();

        Assert.Equal("Pending", statusChanged.GetProperty("oldValue").GetString());
        Assert.Equal("Open", statusChanged.GetProperty("newValue").GetString());

        // R-14 — the actor is the REPLYING CUSTOMER, and the kind is User, never System.
        Assert.Equal("User", statusChanged.GetProperty("actorKind").GetString());
        Assert.Equal(
            fixture.HeadOfficePortalUserId,
            statusChanged.GetProperty("actor").GetProperty("id").GetGuid());

        // Same transaction: the message and the reopen are both committed, or neither is.
        Assert.Equal(
            envelope.GetProperty("message").GetProperty("id").GetGuid(),
            messagePosted.GetProperty("messageId").GetGuid());

        var stored = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.Equal(TicketStatus.Open, stored.Status);
    }

    /// <summary>
    /// Test 10 — <b>from <c>Pending</c> only.</b> A reply on a <c>New</c> ticket leaves it
    /// <c>New</c> (replying is not an agent starting work, A-18), and a reply on a <c>Resolved</c>
    /// one does <b>not</b> reopen it — reopening <c>Resolved</c> stays the customer's explicit
    /// transition under A-16.
    /// </summary>
    [Theory]
    [InlineData("New", new string[0])]
    [InlineData("Resolved", new[] { "Open", "Resolved" })]
    public async Task A_customer_reply_does_not_transition_a_ticket_that_is_not_pending(
        string expected, string[] steps)
    {
        var ticketId = await CreateTicketAsync();
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        foreach (var step in steps)
        {
            await agent.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = step });
        }

        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var envelope = await PostCustomerReplyAsync(customer, ticketId, "Adding a note to this.");

        Assert.False(envelope.GetProperty("statusChanged").GetBoolean());
        Assert.Equal(expected, envelope.GetProperty("ticketStatus").GetString());

        var stored = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.Equal(expected, stored.Status.ToString());
    }

    /// <summary>
    /// Test 11 — <b>an agent reply never transitions the ticket.</b> The rule is about an
    /// <c>Inbound</c> message, and an agent's is <c>Outbound</c> whatever the status.
    /// </summary>
    [Fact]
    public async Task An_agent_reply_on_a_pending_ticket_does_not_transition_it()
    {
        var ticketId = await OpenTicketAsync();
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        await agent.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Pending" });

        await PostStaffReplyAsync(agent, ticketId, "Chasing this internally.");

        var stored = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.Equal(TicketStatus.Pending, stored.Status);

        // No StatusChanged row was written by the reply either. The only two on the trail are the
        // agent's own moves — New -> Open and Open -> Pending — and the last of them is still the
        // move INTO Pending, so nothing followed the reply.
        var activity = await ReadArrayAsync(agent, $"{Tickets}/{ticketId}/activity");

        var statusChanges = activity
            .Where(a => a.GetProperty("activityType").GetString() == nameof(TicketActivityType.StatusChanged))
            .ToArray();

        Assert.Equal(2, statusChanges.Length);
        Assert.Equal("Pending", statusChanges[^1].GetProperty("newValue").GetString());
    }

    /// <summary>
    /// Test 12 — <b>AP-4.</b> Another customer's ticket is <c>404</c>, worded identically to one
    /// that does not exist. A <c>403</c> would confirm it exists.
    /// </summary>
    [Fact]
    public async Task A_customer_replying_to_another_customers_ticket_is_not_found()
    {
        var ticketId = await OpenTicketAsync();

        var stranger = fixture.Factory.CreateClientFor(fixture.NorthPortalUserId);

        var reply = await stranger.PostAsJsonAsync(
            $"{Portal}/{ticketId}/messages", new { body = "Not mine, but let me try." });

        Assert.Equal(HttpStatusCode.NotFound, reply.StatusCode);

        var thread = await stranger.GetAsync($"{Portal}/{ticketId}/messages");

        Assert.Equal(HttpStatusCode.NotFound, thread.StatusCode);

        // Identical to a ticket that genuinely does not exist — the comparison IS the AP-4 proof.
        var missing = await stranger.PostAsJsonAsync(
            $"{Portal}/{Guid.NewGuid()}/messages", new { body = "No such ticket." });

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var refused = await reply.Content.ReadFromJsonAsync<JsonElement>();
        var absent = await missing.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(absent.GetProperty("type").GetString(), refused.GetProperty("type").GetString());
        Assert.Equal(absent.GetProperty("detail").GetString(), refused.GetProperty("detail").GetString());
    }

    /// <summary>
    /// Test 13 — <b>A-13.</b> A customer reply on an <b>assigned</b> ticket raises exactly one
    /// <c>CustomerReplied</c>, to the assignee. <b>The automatic status change raises none</b>:
    /// A-13 defines four notification types and none of them is a status change.
    /// </summary>
    [Fact]
    public async Task A_customer_reply_on_an_assigned_ticket_raises_exactly_one_notification()
    {
        var ticketId = await OpenTicketAsync(assign: true);
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        await agent.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Pending" });

        var before = fixture.Notifications.For(ticketId).Count;

        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var envelope = await PostCustomerReplyAsync(customer, ticketId, "Replying, which reopens this.");

        // The reopen definitely happened, so "none for the status change" is a real assertion.
        Assert.True(envelope.GetProperty("statusChanged").GetBoolean());

        var raised = fixture.Notifications.For(ticketId).Skip(before).ToArray();

        var only = Assert.Single(raised);

        Assert.Equal(NotificationType.CustomerReplied, only.Type);
        Assert.Equal(fixture.BillingAgentId, only.RecipientUserId);
    }

    /// <summary>
    /// The other half of A-13's rule, and the one an implementation drifts on: an <b>unassigned</b>
    /// ticket has nobody on the hook, so no notification is raised at all.
    /// </summary>
    [Fact]
    public async Task A_customer_reply_on_an_unassigned_ticket_raises_no_notification()
    {
        var ticketId = await OpenTicketAsync();

        // **Story 09 made this precondition something the test has to establish.** Round-robin
        // auto-assignment now assigns at creation (T2-D), so a ticket opened through the endpoint is
        // no longer unassigned by default and the rule below would be untested.
        //
        // The unassigned state is still genuinely reachable in production — the policy returns null
        // when a department has no active agent — but no endpoint clears an assignee (a ticket is
        // reassigned, never un-assigned), so there is no API call that produces it. Reaching the
        // column through EF is the honest way to set up a state the contract does not expose.
        await fixture.Factory.WithDbAsync(async db =>
        {
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);

            db.Entry(ticket).Property(nameof(Ticket.AssignedUserId)).CurrentValue = null;

            return await db.SaveChangesAsync();
        });

        var before = fixture.Notifications.For(ticketId).Count;

        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        await PostCustomerReplyAsync(customer, ticketId, "Nobody is assigned to this yet.");

        Assert.Equal(before, fixture.Notifications.For(ticketId).Count);
    }

    // ------------------------------------------------------------------------------ helpers

    /// <summary>
    /// A ticket for the fixture's Head Office customer, created through the real staff endpoint so
    /// the preconditions are the ones an endpoint produces.
    /// </summary>
    private async Task<Guid> CreateTicketAsync()
    {
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var created = await agent.PostAsJsonAsync(Tickets, new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "Thread subject",
            description = "The originating text — not a first message (§2.6).",
            categoryCode = "billing",
            priority = "Medium",
        });

        created.EnsureSuccessStatusCode();

        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>A ticket an agent has started work on — <c>New → Open</c> (A-18).</summary>
    private async Task<Guid> OpenTicketAsync(bool assign = false)
    {
        var ticketId = await CreateTicketAsync();
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        if (assign)
        {
            await agent.PostAsJsonAsync(
                $"{Tickets}/{ticketId}/assignment", new { assignedUserId = fixture.BillingAgentId });
        }

        await agent.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });

        return ticketId;
    }

    private static async Task<JsonElement> PostStaffReplyAsync(
        HttpClient client, Guid ticketId, string body)
    {
        var response = await client.PostAsJsonAsync($"{Tickets}/{ticketId}/messages", new { body });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> PostCustomerReplyAsync(
        HttpClient client, Guid ticketId, string body)
    {
        var response = await client.PostAsJsonAsync($"{Portal}/{ticketId}/messages", new { body });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement[]> ReadArrayAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync($"{path}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. page.GetProperty("items").EnumerateArray()];
    }

    private static async Task<DateTimeOffset?> FirstRespondedAtAsync(HttpClient client, Guid ticketId)
    {
        var response = await client.GetAsync($"{Tickets}/{ticketId}");

        response.EnsureSuccessStatusCode();

        var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Nulls are omitted rather than sent (docs/api-design.md §2), so "absent" is "not yet".
        return ticket.TryGetProperty("firstRespondedAt", out var value)
            ? value.GetDateTimeOffset()
            : null;
    }
}
