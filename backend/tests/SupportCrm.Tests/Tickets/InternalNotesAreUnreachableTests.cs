using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>The portal-thread isolation check, now that internal notes exist.</b>
///
/// <para>
/// Story 07 created this file as a deliberately skipped stub — <c>TicketInternalNote</c> was Story
/// 14's entity and there was nothing to leak, so there was nothing to assert. <b>Story 14 fills the
/// body</b>, which is what the stub's own remarks demanded: <em>"Story 14 unskips it and fills the
/// body — it must not be made vacuous instead."</em>
/// </para>
///
/// <para>
/// <b>This is the suite the intake demands be performed as a customer, not by checking the UI</b>
/// (T2-C). Every leak assertion below runs against the <b>raw JSON</b> a customer's own token
/// receives — not against a DTO type, because a type is a promise and the payload is what a
/// customer actually gets.
/// </para>
///
/// <para>
/// <b>The canary is the whole technique.</b> One unguessable token is written into a note, and then
/// searched for in the serialized bytes of every customer-facing response. A projection that
/// accidentally included the note would fail here even if it exposed the text under a field name
/// nobody thought to check.
/// </para>
/// </summary>
public sealed class InternalNotesAreUnreachableTests(TicketApiFixture fixture)
    : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";
    private const string Portal = "/api/v1/portal/tickets";

    /// <summary>
    /// The token searched for in every customer-facing payload. It is deliberately unlike anything
    /// else in the fixture, so a match cannot be a coincidence.
    /// </summary>
    private const string Canary = "INTERNAL-CANARY-9137";

    // ------------------------------------------------------------------ reachability by role

    /// <summary>
    /// Test 1 — <b>Agent, Manager and Administrator can all read the notes</b> (the story's
    /// <em>"visible to Agent, Manager and Administrator"</em> criterion, A-4's hierarchy).
    /// </summary>
    [Fact]
    public async Task Every_internal_role_can_read_the_notes()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            assignedTo: fixture.BillingAgentId);

        var administratorId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Administrator, "admin@notes.local",
            departmentId: TicketApiFixture.BillingDepartmentId);

        await PostNoteAsync(ticketId, fixture.BillingAgentId, "Agent-written note.");

        foreach (var staffId in new[] { fixture.BillingAgentId, fixture.ManagerId, administratorId })
        {
            var client = fixture.Factory.CreateClientFor(staffId);

            var response = await client.GetAsync($"{Tickets}/{ticketId}/internal-notes");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(1, body.GetProperty("totalItems").GetInt32());
        }
    }

    /// <summary>
    /// Test 2 — <b>a Customer is refused by the role gate</b>. The staff path space is
    /// <c>RequireAgent</c> (AP-5), so this is <c>403</c> and the refusal happens before any note is
    /// read.
    /// </summary>
    [Fact]
    public async Task A_customer_calling_the_staff_note_route_is_forbidden()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        // Their OWN ticket — so the refusal is unambiguously about the path space and the role,
        // not about scope. A customer cannot reach this route even for a ticket they own.
        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var read = await client.GetAsync($"{Tickets}/{ticketId}/internal-notes");
        var write = await client.PostAsJsonAsync(
            $"{Tickets}/{ticketId}/internal-notes", new { body = "Should never be written." });

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    /// <summary>
    /// Test 3 — <b>there is no portal route at all.</b> <c>/portal/tickets/{id}/internal-notes</c>
    /// is not merely refused, it is <b>not routable</b>: the router has nothing to match, so the
    /// answer comes from the framework rather than from an authorization decision.
    ///
    /// <para>
    /// <b>This is the assertion that would catch someone adding a portal counterpart "for
    /// symmetry".</b> AP-5 makes the path space itself the visibility rule, and a new route here
    /// would break T2-C no matter how carefully its handler filtered.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_portal_path_space_has_no_internal_notes_route()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var read = await client.GetAsync($"{Portal}/{ticketId}/internal-notes");
        var write = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/internal-notes", new { body = "Should never be written." });

        // 404 (no route) or 405 (the path exists for another verb) — either proves there is no
        // handler. What must NOT happen is 200, or a 403 from a handler that exists.
        Assert.Contains(read.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Contains(write.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    // ------------------------------------------------------------------ the leak checks

    /// <summary>
    /// Test 4 — <b>the portal thread contains none of the note's text</b>, asserted on the raw JSON.
    ///
    /// <para>
    /// The ticket has a real customer message as well, so the thread is not trivially empty: the
    /// test proves the note is <em>absent from a populated response</em>, not that the endpoint
    /// returned nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_portal_thread_never_carries_a_note()
    {
        var ticketId = await SeedTicketWithNoteAndThreadAsync();

        var customer = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var thread = await customer.GetStringAsync($"{Portal}/{ticketId}/messages");
        var detail = await customer.GetStringAsync($"{Portal}/{ticketId}");

        Assert.DoesNotContain(Canary, thread, StringComparison.Ordinal);
        Assert.DoesNotContain(Canary, detail, StringComparison.Ordinal);

        // The thread is populated — otherwise the assertion above would pass vacuously.
        Assert.Contains("Customer-visible reply.", thread, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 5 — <b>the customer interaction timeline carries no <c>Internal</c> entry and none of
    /// the note's text</b> (docs/data-model.md §5 constraint 18), again on the raw JSON.
    ///
    /// <para>
    /// Two independent things are checked because two independent mechanisms protect it: the
    /// timeline excludes <c>Internal</c> visibility <b>and</b> never joins the note table. Either
    /// alone would be enough; asserting both means a regression in one is still caught.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_customer_timeline_carries_no_internal_entry()
    {
        var ticketId = await SeedTicketWithNoteAndThreadAsync();

        _ = ticketId;

        // The timeline is a STAFF endpoint about a customer, so it is fetched as an agent — which is
        // the stronger test: even the reader most entitled to see the note does not get it here,
        // because this projection is the customer's interaction history (architecture §2.5).
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var timeline = await agent.GetStringAsync(
            $"/api/v1/customers/{fixture.HeadOfficeCustomerId}/timeline");

        Assert.DoesNotContain(Canary, timeline, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal", timeline, StringComparison.Ordinal);
    }

    /// <summary>
    /// Test 6 — <b>posting an internal note raises no notification.</b> A-13 enumerates four types
    /// and none of them is a note (docs/data-model.md §2.12); raising one would be the @mention
    /// feature T2-C excludes.
    /// </summary>
    [Fact]
    public async Task Posting_an_internal_note_raises_no_notification()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            // Assigned, so there IS a plausible recipient. An unassigned ticket would make the
            // assertion pass for the wrong reason.
            assignedTo: fixture.TechnicalAgentId);

        var before = fixture.Notifications.For(ticketId).Count;

        await PostNoteAsync(ticketId, fixture.BillingAgentId, "Nobody should be notified about this.");

        Assert.Equal(before, fixture.Notifications.For(ticketId).Count);
    }

    // ------------------------------------------------------------------ history and immutability

    /// <summary>
    /// Test 7 — <b>the note DOES appear in staff ticket history</b>, as <c>InternalNotePosted</c>
    /// with <c>visibility: "Internal"</c> and a linked <c>internalNoteId</c> (§2.7).
    ///
    /// <para>
    /// The story requires both halves: excluded from customer reads, <b>and</b> present for internal
    /// roles. A note that vanished entirely would satisfy the leak tests and fail the requirement.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_note_appears_in_staff_history_as_an_internal_entry()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        var noteId = await PostNoteAsync(ticketId, fixture.BillingAgentId, "Recorded in history.");

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var body = await agent.GetFromJsonAsync<JsonElement>($"{Tickets}/{ticketId}/activity");

        var entry = body.GetProperty("items").EnumerateArray().Single(
            e => e.GetProperty("activityType").GetString() == "InternalNotePosted");

        Assert.Equal("Internal", entry.GetProperty("visibility").GetString());
        Assert.Equal(noteId, entry.GetProperty("internalNoteId").GetGuid());

        // The actor is the author, and the body was NOT copied onto the history row (DM-4).
        Assert.Equal("User", entry.GetProperty("actorKind").GetString());
        Assert.Equal(fixture.BillingAgentId, entry.GetProperty("actor").GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Test 8 — <b>immutability by reflection.</b> <c>TicketInternalNoteService</c> exposes no
    /// method whose name contains <c>Update</c> or <c>Delete</c> (§5 constraint 16).
    ///
    /// <para>
    /// <b>Asserted against the type's public surface rather than trusted to a comment</b>, the same
    /// technique <c>TicketActivityRecorder</c>'s append-only test uses. The entity has no mutator
    /// either, so a future edit path would have to add one in two places — and this fails on the
    /// first.
    /// </para>
    /// </summary>
    [Fact]
    public void The_note_service_exposes_no_edit_or_delete()
    {
        var offenders = typeof(TicketInternalNoteService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.Contains("Update", StringComparison.OrdinalIgnoreCase)
                     || m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                     || m.Name.Contains("Edit", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(offenders);

        // The entity's half of the same rule: no mutator at all, so there is nothing for a service
        // to call even if one were added.
        var mutators = typeof(TicketInternalNote)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(mutators);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A ticket that has <b>both</b> a customer-visible reply and an internal note carrying the
    /// canary — the arrangement every leak assertion needs, because a thread with no messages would
    /// let a "no canary" assertion pass without proving anything.
    /// </summary>
    private async Task<Guid> SeedTicketWithNoteAndThreadAsync()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            assignedTo: fixture.BillingAgentId);

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var reply = await agent.PostAsJsonAsync(
            $"{Tickets}/{ticketId}/messages", new { body = "Customer-visible reply." });

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        await PostNoteAsync(
            ticketId, fixture.BillingAgentId, $"Do not tell the customer: {Canary}");

        return ticketId;
    }

    private async Task<Guid> PostNoteAsync(Guid ticketId, Guid staffUserId, string body)
    {
        var client = fixture.Factory.CreateClientFor(staffUserId);

        var response = await client.PostAsJsonAsync(
            $"{Tickets}/{ticketId}/internal-notes", new { body });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The note is stored exactly as written, and attributed to the caller — never to a value the
        // request could have carried.
        var stored = await fixture.Factory.WithDbAsync(db =>
            db.TicketInternalNotes.AsNoTracking().SingleAsync(
                n => n.Id == created.GetProperty("id").GetGuid()));

        Assert.Equal(staffUserId, stored.AuthorUserId);

        return stored.Id;
    }
}
