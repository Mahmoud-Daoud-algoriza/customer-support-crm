using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SupportCrm.Tests.Tickets;

namespace SupportCrm.Tests.Portal;

/// <summary>
/// <b>The permission demonstration Story 13 carries</b> — plan tests 1–6.
///
/// <para>
/// The portal is the second actor surface, and product-scope calls it out as carrying <em>"much of
/// the permission demonstration"</em>. Three separate rules are proven here, and each is proven in
/// the form that would catch a real regression:
/// </para>
/// <list type="number">
///   <item><description><b>A customer sees only their own tickets</b> — asserted <b>through the API and past the UI entirely</b>, because a screen that renders the right thing proves nothing about the server.</description></item>
///   <item><description><b>Another customer's id is <c>404</c>, on every action</b> (<b>AP-4</b>) — and the stronger form: <b>indistinguishable</b> from a genuinely missing id, same <c>type</c> and same <c>detail</c>. A <c>403</c> would confirm the ticket exists.</description></item>
///   <item><description><b>No staff field leaks</b> — asserted on the <b>raw JSON</b>, not on the DTO type (AP-16, UI-11). A type is a promise; the payload is what a customer receives, and only one of the two is what a client can read.</description></item>
/// </list>
///
/// <para>
/// <b>Internal notes are unreachable by path, not by filter</b> (T2-C, AP-5): there is no route under
/// <c>/portal</c> that reaches them, which is why test 6 asserts on <em>routing</em> rather than on
/// a response body. The complementary check — that an internal note an agent wrote never appears in a
/// portal thread — lives in <c>Tickets/InternalNotesAreUnreachableTests</c> and belongs to
/// <b>Story 14</b>, which owns the entity.
/// </para>
/// </summary>
public sealed class PortalIsolationTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Portal = "/api/v1/portal/tickets";
    private const string StaffTickets = "/api/v1/tickets";

    /// <summary>
    /// Test 1 — <c>GET /portal/tickets</c> returns <b>only</b> the caller's own requests.
    ///
    /// <para>
    /// Two customers, one ticket each, and the assertion is made <b>both ways</b>: the caller's own
    /// id is present, and the other customer's is absent. Asserting only the first would pass even if
    /// the endpoint returned everything.
    /// </para>
    ///
    /// <para>
    /// <b>There is no <c>customerId</c> parameter to get wrong.</b> Ownership is
    /// <c>TicketScope.ForCaller</c>'s, applied before any filter — so this test is about the scope,
    /// not about a query string.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_customer_sees_only_their_own_requests()
    {
        var mine = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId, subject: "Mine");

        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.NorthCustomerId,
            fixture.BillingAgentId, subject: "Theirs");

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.GetAsync($"{Portal}?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToList();

        Assert.Contains(mine, ids);
        Assert.DoesNotContain(theirs, ids);
    }

    /// <summary>
    /// Test 2 — <c>GET /portal/tickets/{id}</c> for <b>another customer's</b> ticket is <c>404</c>,
    /// and <b>identical</b> to a missing one (<b>AP-4</b>).
    ///
    /// <para>
    /// The comparison is the test. A <c>404</c> alone would still leak if its wording differed
    /// between "does not exist" and "not yours" — so both responses are read and their <c>type</c>
    /// and <c>detail</c> compared. <c>TicketScope.NotFound</c> is one constant precisely so this
    /// assertion can hold.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Another_customers_request_is_indistinguishable_from_one_that_does_not_exist()
    {
        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.NorthCustomerId, fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var foreign = await client.GetAsync($"{Portal}/{theirs}");
        var missing = await client.GetAsync($"{Portal}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var foreignProblem = await foreign.Content.ReadFromJsonAsync<JsonElement>();
        var missingProblem = await missing.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            missingProblem.GetProperty("type").GetString(),
            foreignProblem.GetProperty("type").GetString());

        Assert.Equal(
            missingProblem.GetProperty("detail").GetString(),
            foreignProblem.GetProperty("detail").GetString());
    }

    /// <summary>
    /// Test 3 — <b>every other portal action</b> answers <c>404</c> on another customer's ticket
    /// too: the thread read and reply, the transition, both attachment actions, and feedback.
    ///
    /// <para>
    /// <b>Enumerated rather than sampled.</b> AP-4 is a property of the path space, not of one
    /// endpoint, and the failure mode this guards against is a <em>new</em> action that forgets to
    /// compose <c>TicketScope</c>. Listing all of them means the list is what a reviewer checks
    /// against §5.7.
    /// </para>
    ///
    /// <para>
    /// <b>Feedback is <c>404</c> here even though the ticket is not <c>Resolved</c></b> — which is
    /// the point of the ordering in <c>CustomerFeedbackService</c>: scope is checked <em>before</em>
    /// the precondition, so a customer never learns from a <c>409</c> that someone else's ticket
    /// exists.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Every_portal_action_on_another_customers_request_is_404()
    {
        var theirs = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId,
            fixture.TechnicalAgentId, categoryCode: "technical");

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var responses = new List<(string Action, HttpResponseMessage Response)>
        {
            ("GET messages", await client.GetAsync($"{Portal}/{theirs}/messages")),
            ("POST messages", await client.PostAsJsonAsync(
                $"{Portal}/{theirs}/messages", new { body = "Not my ticket." })),
            ("POST transition", await client.PostAsJsonAsync(
                $"{Portal}/{theirs}/transition", new { targetStatus = "Cancelled" })),
            ("GET attachments", await client.GetAsync($"{Portal}/{theirs}/attachments")),
            ("POST attachments", await client.PostAsync(
                $"{Portal}/{theirs}/attachments", OneFile())),
            ("POST feedback", await client.PostAsJsonAsync(
                $"{Portal}/{theirs}/feedback", new { rating = 1 })),
        };

        foreach (var (action, response) in responses)
        {
            // The action is named in the failure message: "one of six answered 200" is otherwise a
            // hunt through six identical assertion failures.
            Assert.True(
                response.StatusCode == HttpStatusCode.NotFound,
                $"{action} answered {(int)response.StatusCode}, not 404.");
        }
    }

    /// <summary>
    /// Test 4 — a customer calling a <b>staff</b> ticket endpoint gets <c>403</c>, not <c>404</c>.
    ///
    /// <para>
    /// <b>And <c>403</c> is correct here, which is not a contradiction of AP-4.</b> AP-4 governs
    /// <em>resource</em> reachability: telling a customer that <em>this ticket</em> exists is a leak.
    /// This is a <em>capability</em> denial the caller can infer from their own role — they know they
    /// are a customer, and no id is being confirmed, because the class policy refuses before any
    /// ticket is loaded (docs/api-design.md §4.2, AP-5).
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_customer_on_a_staff_ticket_endpoint_is_403()
    {
        var mine = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        // Their OWN ticket, on the staff route. The refusal is about the route, not about ownership.
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{StaffTickets}/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(StaffTickets)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{StaffTickets}/{mine}/messages")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(
                $"{StaffTickets}/{mine}/transition", new { targetStatus = "Closed" })).StatusCode);
    }

    /// <summary>
    /// Test 5 — <b>the serialized portal payload carries no staff field</b> (AP-16, UI-11).
    ///
    /// <para>
    /// <b>Asserted on the raw JSON, deliberately.</b> <c>PortalTicketDto</c> has no such
    /// member, so a test against the type would pass by construction and would keep passing if a
    /// later story returned a different type from one of these actions. What matters is the bytes a
    /// customer receives — <em>"the UI cannot show what the contract does not return."</em>
    /// </para>
    ///
    /// <para>
    /// All three shapes are checked: the <b>detail</b>, a <b>list row</b> (§6.4 defines them as one
    /// shape, and this proves it) and the <b>transition response</b>, which is the payload most
    /// likely to be answered with the staff DTO by accident — the lifecycle service it delegates to
    /// returns exactly that.
    /// </para>
    /// </summary>
    [Fact]
    public async Task No_portal_payload_carries_an_assignee_department_priority_or_sla_field()
    {
        // Assigned on purpose: an unassigned ticket cannot prove that `assignee` is omitted rather
        // than merely null (A-18 — assignment does not change the status, so it stays New).
        var mine = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId, assignedTo: fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var detail = await ReadJsonAsync(client, $"{Portal}/{mine}");

        var row = (await ReadJsonAsync(client, $"{Portal}?pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == mine);

        var transitioned = await client.PostAsJsonAsync(
            $"{Portal}/{mine}/transition", new { targetStatus = "Cancelled" });

        Assert.Equal(HttpStatusCode.OK, transitioned.StatusCode);

        var afterTransition = await transitioned.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var payload in new[] { detail, row, afterTransition })
        {
            foreach (var forbidden in StaffOnlyMembers)
            {
                Assert.False(
                    payload.TryGetProperty(forbidden, out _),
                    $"The portal payload exposed '{forbidden}'.");
            }

            // The members §6.4 DOES define, asserted so this test cannot pass by returning {}.
            Assert.True(payload.TryGetProperty("status", out _));
            Assert.True(payload.TryGetProperty("categoryCode", out _));
            Assert.True(payload.TryGetProperty("hasFeedback", out _));
        }
    }

    /// <summary>
    /// Test 6 — <b><c>/portal/tickets/{id}/internal-notes</c> is not routable</b> (T2-C, AP-5).
    ///
    /// <para>
    /// <b>This asserts on routing, and that is the whole rule.</b> Internal notes are excluded from
    /// the portal because <em>no route reaches them</em> — not because a filter removes them from a
    /// merged list. There is nothing to forget, and a <c>404</c> from the router (rather than from a
    /// scope check) is what proves it: the action does not exist to be called.
    /// </para>
    ///
    /// <para>
    /// It is asserted for <b>the caller's own ticket</b>, so a <c>404</c> cannot be explained away as
    /// an ownership refusal.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_portal_path_space_has_no_internal_notes_route()
    {
        var mine = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"{Portal}/{mine}/internal-notes")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(
                $"{Portal}/{mine}/internal-notes", new { body = "Should not be routable." })).StatusCode);
    }

    /// <summary>
    /// The staff vocabulary the portal does not speak, exactly as the plan enumerates it (AP-16,
    /// UI-11). <b>Adding a member to the portal DTO that appears in this list is a contract change</b>,
    /// and this array is where the refusal is written down.
    /// </summary>
    private static readonly string[] StaffOnlyMembers =
    [
        "assignee",
        "departmentId",
        "priority",
        "firstResponseDueAt",
        "resolutionDueAt",
        "firstResponseBreached",
        "resolutionBreached",
    ];

    private static async Task<JsonElement> ReadJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// One tiny <c>multipart/form-data</c> body. The bytes are irrelevant: the assertion is that the
    /// request never reaches storage, because the ticket is unreachable.
    /// </summary>
    private static MultipartFormDataContent OneFile()
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not my ticket"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        return new MultipartFormDataContent { { file, "file", "note.txt" } };
    }
}
