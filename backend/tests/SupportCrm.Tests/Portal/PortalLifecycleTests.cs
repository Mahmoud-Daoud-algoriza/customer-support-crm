using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Tickets;

namespace SupportCrm.Tests.Portal;

/// <summary>
/// <b>A-16's customer column, now over HTTP</b> — plan tests 7–11.
///
/// <para>
/// <b>These close finding I-23.</b> Story 06 asserted the customer column against
/// <c>TransitionAuthority.MayInvoke</c> directly, because <em>no endpoint existed that a Customer
/// could call</em>: the staff controller is <c>RequireAgent</c>, and
/// <c>POST /portal/tickets/{id}/transition</c> was Story 13's. It exists now, so the same rules are
/// asserted <b>through the endpoint a customer actually uses</b> — which is what I-23 said was
/// deferred rather than unproven.
/// </para>
///
/// <para>
/// <b>The two refusals stay distinguishable</b>, and that is half of what is proven here: cancelling
/// an <c>Open</c> request is <c>403 transition-not-permitted</c> — the edge is perfectly legal and an
/// agent may take it, the <em>caller</em> may not — while an edge outside A-5's graph would be
/// <c>409 illegal-transition</c>. Collapsing them would tell two different mistakes the same story.
/// </para>
/// </summary>
public sealed class PortalLifecycleTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Portal = "/api/v1/portal/tickets";
    private const string StaffTickets = "/api/v1/tickets";

    /// <summary>
    /// Test 7 — a customer cancels their own <c>New</c> request, <b>including one that has already
    /// been auto-assigned</b> (<b>A-18</b>).
    ///
    /// <para>
    /// <b>The assignee is asserted to exist before the cancel.</b> That is the whole test: A-18 says
    /// assignment is <em>not</em> the start of work, so an assigned ticket is still <c>New</c> and
    /// the A-16 cancel window is still open. A test on an unassigned ticket would pass under an
    /// implementation that (wrongly) treated an assignee as "work has started" — this one would not.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_customer_cancels_their_own_New_request_even_when_it_is_already_assigned()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId, assignedTo: fixture.BillingAgentId);

        // The precondition A-18 is about: an assignee IS set and the status is STILL New.
        var before = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.NotNull(before.AssignedUserId);
        Assert.Equal(TicketStatus.New, before.Status);

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/transition", new { targetStatus = "Cancelled" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", body.GetProperty("status").GetString());

        var after = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.Equal(TicketStatus.Cancelled, after.Status);

        // Cancelling does not un-assign: A-18 keeps the two facts independent in both directions.
        Assert.Equal(before.AssignedUserId, after.AssignedUserId);
    }

    /// <summary>
    /// Test 8 — once the request is <c>Open</c>, the same call is <c>403
    /// transition-not-permitted</c> (<b>A-16</b>).
    ///
    /// <para>
    /// <b><c>403</c>, not <c>409</c>, and the slug is asserted.</b> <c>Open → Cancelled</c> is in
    /// A-5's graph — an agent may cancel an open ticket — so this is an <em>authority</em> refusal.
    /// Answering <c>409 illegal-transition</c> would claim the edge does not exist, which is false.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_customer_cancelling_their_own_Open_request_is_refused()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        await MoveAsync(ticketId, fixture.BillingAgentId, "Open");

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/transition", new { targetStatus = "Cancelled" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("transition-not-permitted", problem.GetProperty("type").GetString());

        // The edge itself is legal — which is exactly why this had to be a 403.
        Assert.True(TicketLifecycle.IsLegal(TicketStatus.Open, TicketStatus.Cancelled));
    }

    /// <summary>
    /// Test 9 — a customer reopens their own <c>Resolved</c> request, <b>and the reopen appears in
    /// ticket history</b>.
    ///
    /// <para>
    /// <b>The history assertion is read through the staff activity endpoint</b>, as an agent — not
    /// from the table. What matters is that an agent picking the request back up can <em>see</em> who
    /// reopened it and from what, so the assertion is made where a human would look: the
    /// <c>StatusChanged</c> row carries <c>oldValue: "Resolved"</c>, <c>newValue: "Open"</c> and the
    /// <b>customer</b> as actor with <c>actorKind: "User"</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_customer_reopens_their_own_Resolved_request_and_the_reopen_is_in_history()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        await MoveAsync(ticketId, fixture.BillingAgentId, "Open");
        await MoveAsync(ticketId, fixture.BillingAgentId, "Resolved");

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/transition", new { targetStatus = "Open" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Open", body.GetProperty("status").GetString());

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var history = await agent.GetFromJsonAsync<JsonElement>(
            $"{StaffTickets}/{ticketId}/activity?pageSize=100");

        var reopen = history.GetProperty("items").EnumerateArray()
            .Single(entry =>
                entry.GetProperty("activityType").GetString() == "StatusChanged" &&
                entry.GetProperty("oldValue").GetString() == "Resolved" &&
                entry.GetProperty("newValue").GetString() == "Open");

        // Attributed to the customer who asked for it, not to the system.
        Assert.Equal("User", reopen.GetProperty("actorKind").GetString());
        Assert.Equal(
            fixture.HeadOfficePortalUserId,
            reopen.GetProperty("actor").GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Test 10 — a customer attempting <c>→ Closed</c> is refused (<b>A-16</b>).
    ///
    /// <para>
    /// <b>Customers cannot close</b>, and A-16 names that as a deliberate consequence rather than an
    /// oversight: closure is manual and staff-only, and there is no timer and no automatic closure
    /// anywhere in this design. It is asserted from <c>Resolved</c> — where <c>→ Closed</c> is
    /// <em>legal</em> — so the refusal can only be about authority.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_customer_may_not_close_their_own_request()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        await MoveAsync(ticketId, fixture.BillingAgentId, "Open");
        await MoveAsync(ticketId, fixture.BillingAgentId, "Resolved");

        Assert.True(TicketLifecycle.IsLegal(TicketStatus.Resolved, TicketStatus.Closed));

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/transition", new { targetStatus = "Closed" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("transition-not-permitted", problem.GetProperty("type").GetString());

        var after = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.Equal(TicketStatus.Resolved, after.Status);
    }

    /// <summary>
    /// Test 11 — replying to a <c>Pending</c> request returns it to <c>Open</c>, and the reply's own
    /// response says so: <c>statusChanged: true</c>, <c>ticketStatus: "Open"</c> (<b>R-13</b>).
    ///
    /// <para>
    /// <b>This is what lets the portal show its "reopened" cue in place</b> (docs/ui-design.md §7.3)
    /// <em>without a re-fetch and without guessing</em> — so the envelope is asserted, not just the
    /// stored status. Story 07 proved the transition; this proves the <b>client-visible</b> half the
    /// screen depends on, and that the request stays <c>Open</c> afterwards.
    /// </para>
    ///
    /// <para>
    /// <b>And the customer was never offered a manual reopen from <c>Pending</c></b> — A-16 gives
    /// them none, which is asserted here too: the same target through the transition endpoint is
    /// <c>403</c>. §7.3's <em>"the UI must not offer a manual reopen for a <c>Pending</c>
    /// request"</em> is therefore backed by the server, not only by the screen.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Replying_to_a_Pending_request_reopens_it_and_the_response_says_so()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        await MoveAsync(ticketId, fixture.BillingAgentId, "Open");
        await MoveAsync(ticketId, fixture.BillingAgentId, "Pending");

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        // A-16 offers a customer no Pending → Open, because R-13 does it automatically.
        var manual = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/transition", new { targetStatus = "Open" });

        Assert.Equal(HttpStatusCode.Forbidden, manual.StatusCode);

        var reply = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/messages", new { body = "Here is the transfer reference." });

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        var envelope = await reply.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(envelope.GetProperty("statusChanged").GetBoolean());
        Assert.Equal("Open", envelope.GetProperty("ticketStatus").GetString());

        // The portal message shape omits channel and authorRole (§6.4), and the envelope carries it.
        var message = envelope.GetProperty("message");
        Assert.False(message.TryGetProperty("channel", out _));
        Assert.False(message.TryGetProperty("authorRole", out _));

        var after = await fixture.Factory.WithDbAsync(db =>
            db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId));

        Assert.Equal(TicketStatus.Open, after.Status);
    }

    /// <summary>
    /// Moves a ticket through the <b>staff</b> transition endpoint, as an agent — establishing a
    /// precondition through the real path rather than by writing the column, so every seeded status
    /// in this file is one A-5 actually permits and every one carries its history row.
    /// </summary>
    private async Task MoveAsync(Guid ticketId, Guid agentUserId, string targetStatus)
    {
        var agent = fixture.Factory.CreateClientFor(agentUserId);

        var response = await agent.PostAsJsonAsync(
            $"{StaffTickets}/{ticketId}/transition", new { targetStatus });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
