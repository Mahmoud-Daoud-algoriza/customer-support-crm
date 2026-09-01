using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Tests.Tickets;

namespace SupportCrm.Tests.Portal;

/// <summary>
/// <b>The sole CSAT input</b> — plan tests 12–16 (requirements §8.5, T2-F,
/// docs/data-model.md §2.15).
///
/// <para>
/// <b>⚠ Not one test in this file names a rating value that could be a scale (OQ-1).</b>
/// docs/data-model.md §2.15 forbids inferring a minimum, maximum or step <em>"into a validation
/// rule, a check constraint, or a UI control"</em> — and a test asserting <c>rating: 6 → 400</c>
/// would be exactly that, in the place it is least visible: it would keep passing, and it would
/// quietly become the schema's real specification. Every value here is <b>derived from the configured
/// range at run time</b> (see <see cref="Scale"/>), so the whole file survives whatever OQ-1 decides —
/// including a binary scale.
/// </para>
///
/// <para>
/// <b>Declining is a normal outcome, not a failure</b> (test 16). It is asserted as the absence of a
/// row, because that absence is what reporting reads as <em>"no response"</em> rather than as a zero.
/// </para>
/// </summary>
public sealed class FeedbackTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Portal = "/api/v1/portal/tickets";
    private const string StaffTickets = "/api/v1/tickets";

    /// <summary>
    /// Test 12 — feedback on a request that has <b>never reached <c>Resolved</c></b> is <c>409</c>.
    ///
    /// <para>
    /// T2-F offers the rating <em>when a request reaches <c>Resolved</c></em>, so a rating on one
    /// that never did is a state conflict rather than a malformed request — and it carries its own
    /// slug, <c>feedback-not-available</c>, so a client never renders "already submitted" for a
    /// request that was simply too early.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Feedback_before_a_request_has_been_resolved_is_refused()
    {
        var ticketId = await NewTicketAsync();

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var response = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/feedback", new { rating = Scale.Min });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("feedback-not-available", problem.GetProperty("type").GetString());

        var stored = await fixture.Factory.WithDbAsync(db =>
            db.CustomerFeedback.AsNoTracking().CountAsync(f => f.TicketId == ticketId));

        Assert.Equal(0, stored);
    }

    /// <summary>
    /// Test 13 — feedback on a <c>Resolved</c> request is <c>201</c>, and the request's
    /// <c>hasFeedback</c> becomes <c>true</c>.
    ///
    /// <para>
    /// <b><c>hasFeedback</c> is read back from the API, not from the row</b> — it is a response
    /// projection computed from the existence of the feedback row (docs/api-design.md §7, finding
    /// N-4), and this is the assertion that it is actually wired to the row rather than left at
    /// Story 07's constant <c>false</c>. It is checked on the <b>detail and the list row</b>, because
    /// §6.4 defines them as one shape and the portal detail screen decides whether to offer the
    /// control from exactly this field.
    /// </para>
    ///
    /// <para>
    /// The optional comment (T2-F) is sent here, and echoed back.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Feedback_on_a_Resolved_request_is_recorded_and_hasFeedback_becomes_true()
    {
        var ticketId = await ResolvedTicketAsync();

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        // Before: the control would be offered, because nothing has been submitted.
        var before = await client.GetFromJsonAsync<JsonElement>($"{Portal}/{ticketId}");
        Assert.False(before.GetProperty("hasFeedback").GetBoolean());

        var response = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/feedback",
            new { rating = Scale.Max, comment = "Sorted quickly, thank you." });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(ticketId, created.GetProperty("ticketId").GetGuid());
        Assert.Equal(Scale.Max, created.GetProperty("rating").GetInt32());
        Assert.Equal("Sorted quickly, thank you.", created.GetProperty("comment").GetString());
        Assert.True(created.TryGetProperty("submittedAt", out _));

        var after = await client.GetFromJsonAsync<JsonElement>($"{Portal}/{ticketId}");
        Assert.True(after.GetProperty("hasFeedback").GetBoolean());

        var row = (await client.GetFromJsonAsync<JsonElement>($"{Portal}?pageSize=100"))
            .GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == ticketId);

        Assert.True(row.GetProperty("hasFeedback").GetBoolean());

        // The history spine records it, like everything else that happens to a ticket (§2.7).
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);
        var history = await agent.GetFromJsonAsync<JsonElement>(
            $"{StaffTickets}/{ticketId}/activity?pageSize=100");

        Assert.Contains(
            history.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("activityType").GetString() == "FeedbackSubmitted");
    }

    /// <summary>
    /// Test 14 — a <b>second</b> submission is <c>409 feedback-already-submitted</c>.
    ///
    /// <para>
    /// <b>Write-once means write-once</b> (§2.15: <em>"not editable, not resubmittable"</em>). The
    /// second call sends a <em>different</em> rating and a different comment, so the assertion also
    /// proves the first row was not quietly overwritten — a service that updated instead of refusing
    /// would pass a test that only checked the status code.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_second_submission_is_refused_and_the_first_rating_stands()
    {
        var ticketId = await ResolvedTicketAsync();

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        var first = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/feedback", new { rating = Scale.Min, comment = "The first answer." });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            $"{Portal}/{ticketId}/feedback", new { rating = Scale.Max, comment = "A changed mind." });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("feedback-already-submitted", problem.GetProperty("type").GetString());

        var rows = await fixture.Factory.WithDbAsync(db =>
            db.CustomerFeedback.AsNoTracking().Where(f => f.TicketId == ticketId).ToListAsync());

        var only = Assert.Single(rows);
        Assert.Equal(Scale.Min, only.Rating);
        Assert.Equal("The first answer.", only.Comment);
    }

    /// <summary>
    /// Test 15 — a rating <b>outside the configured range</b> is <c>400</c>.
    ///
    /// <para>
    /// <b>Written against the configured range, never against literal numbers</b>, exactly as the
    /// plan requires — so it survives whatever OQ-1 decides. Both ends are tried, from configuration:
    /// one below <c>min</c> and one above <c>max</c>. Under a binary scale those are still the two
    /// values immediately outside it, so the test remains meaningful rather than merely compiling.
    /// </para>
    ///
    /// <para>
    /// <b>The refusal must be a <c>400</c>, not a clamp.</b> Storing the nearest permitted value
    /// would record an answer the customer did not give, and the assertion that no row was written is
    /// what proves it did not happen.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_rating_outside_the_configured_range_is_refused()
    {
        var ticketId = await ResolvedTicketAsync();

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        foreach (var outOfRange in new[] { Scale.Min - 1, Scale.Max + 1 })
        {
            var response = await client.PostAsJsonAsync(
                $"{Portal}/{ticketId}/feedback", new { rating = outOfRange });

            Assert.True(
                response.StatusCode == HttpStatusCode.BadRequest,
                $"A rating of {outOfRange} answered {(int)response.StatusCode}, not 400. " +
                $"The configured range is {Scale.Min}–{Scale.Max}.");
        }

        // Nothing was stored, so the request is still ratable — a clamp would have consumed the one
        // submission this ticket is allowed.
        var stored = await fixture.Factory.WithDbAsync(db =>
            db.CustomerFeedback.AsNoTracking().CountAsync(f => f.TicketId == ticketId));

        Assert.Equal(0, stored);

        Assert.False(
            (await client.GetFromJsonAsync<JsonElement>($"{Portal}/{ticketId}"))
                .GetProperty("hasFeedback").GetBoolean());
    }

    /// <summary>
    /// Test 16 — <b>not submitting is not an error.</b>
    ///
    /// <para>
    /// §2.15: <em>"declining is a normal outcome, so the absence of a row is meaningful and reporting
    /// must treat it as 'no response', not as a zero."</em> So this test does the one thing a
    /// declining customer does — <b>nothing</b> — and asserts that the request stays entirely usable:
    /// it reads back, it reports <c>hasFeedback: false</c>, the thread still works, and <b>no row
    /// exists</b> for a report to average in.
    /// </para>
    ///
    /// <para>
    /// <b>There is no endpoint to call to decline</b>, which is the other half of the rule: an
    /// endpoint would create a "declined" state the model does not have.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Declining_is_a_normal_outcome_and_leaves_no_row()
    {
        var ticketId = await ResolvedTicketAsync();

        var client = fixture.Factory.CreateClientFor(fixture.HeadOfficePortalUserId);

        // The customer simply never calls the feedback endpoint.
        var ticket = await client.GetFromJsonAsync<JsonElement>($"{Portal}/{ticketId}");

        Assert.Equal("Resolved", ticket.GetProperty("status").GetString());
        Assert.False(ticket.GetProperty("hasFeedback").GetBoolean());

        // Still a working request in every other respect.
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"{Portal}/{ticketId}/messages")).StatusCode);

        var rows = await fixture.Factory.WithDbAsync(db =>
            db.CustomerFeedback.AsNoTracking().CountAsync(f => f.TicketId == ticketId));

        Assert.Equal(0, rows);
    }

    /// <summary>
    /// The <b>configured</b> rating scale, read from the running host's options — the same
    /// <c>Feedback rating scale</c> key <c>GET /config</c> publishes and the service validates
    /// against (docs/architecture.md §6.3).
    ///
    /// <para>
    /// <b>This is why no test in this file contains a scale.</b> The values are read, never assumed,
    /// so answering OQ-1 is a configuration edit and this suite needs no change at all.
    /// </para>
    /// </summary>
    private FeedbackOptions Scale =>
        fixture.Factory.Services.GetRequiredService<IOptions<FeedbackOptions>>().Value;

    private Task<Guid> NewTicketAsync() => fixture.AddTicketAsync(
        TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

    /// <summary>
    /// A request the caller owns, taken to <c>Resolved</c> <b>through the staff transition
    /// endpoint</b> — so <c>ResolvedAt</c> is stamped by the entity exactly as it is in production,
    /// which is the field the feedback precondition reads.
    /// </summary>
    private async Task<Guid> ResolvedTicketAsync()
    {
        var ticketId = await NewTicketAsync();
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        foreach (var target in new[] { "Open", "Resolved" })
        {
            var response = await agent.PostAsJsonAsync(
                $"{StaffTickets}/{ticketId}/transition", new { targetStatus = target });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        return ticketId;
    }
}
