using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportCrm.Application.Modules.Ai;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Ai;

/// <summary>
/// The three assist endpoints of docs/api-design.md §5.8, and the guardrails A-8 requires of them.
///
/// <para>
/// <b>Every test here runs against the deterministic offline fake with no credentials configured</b> —
/// which is the point of Story 10's default, and the reason this suite needs no provider account.
/// </para>
/// </summary>
public sealed class AiAssistEndpointTests(AiAssistFixture fixture) : IClassFixture<AiAssistFixture>
{
    /// <summary>
    /// <b>Both thread assists work, and both carry the label A-8 requires.</b> `generatedBy: "ai"` is
    /// what lets a screen label the result without inferring anything from which endpoint it called.
    /// </summary>
    [Fact]
    public async Task Summary_and_suggested_reply_return_ai_labelled_results()
    {
        var ticketId = await fixture.AddTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.AgentId);

        var summary = await PostAsync(agent, $"/api/v1/tickets/{ticketId}/ai/summary");
        var reply = await PostAsync(agent, $"/api/v1/tickets/{ticketId}/ai/suggested-reply");

        Assert.Equal("ai", summary.GetProperty("generatedBy").GetString());
        Assert.False(string.IsNullOrWhiteSpace(summary.GetProperty("summary").GetString()));

        Assert.Equal("ai", reply.GetProperty("generatedBy").GetString());
        Assert.False(string.IsNullOrWhiteSpace(reply.GetProperty("draft").GetString()));
    }

    /// <summary>
    /// <b>Determinism through the whole stack</b>, not just in the fake's unit test: the same ticket
    /// asked twice gives the same answer, so a demo is repeatable and an agent who reloads does not
    /// get a different suggestion.
    /// </summary>
    [Fact]
    public async Task The_same_ticket_produces_the_same_summary_twice()
    {
        var ticketId = await fixture.AddTicketAsync();

        var agent = fixture.Factory.CreateClientFor(fixture.AgentId);

        var first = await PostAsync(agent, $"/api/v1/tickets/{ticketId}/ai/summary");
        var second = await PostAsync(agent, $"/api/v1/tickets/{ticketId}/ai/summary");

        Assert.Equal(
            first.GetProperty("summary").GetString(),
            second.GetProperty("summary").GetString());
    }

    /// <summary>
    /// <b>Classification needs no ticket</b> — it is callable before one exists, which is what
    /// "suggest at creation" requires — and the code it returns is <b>in the configured list</b>.
    /// </summary>
    [Fact]
    public async Task Classification_works_without_a_ticket_and_returns_a_configured_category()
    {
        var agent = fixture.Factory.CreateClientFor(fixture.AgentId);

        var response = await agent.PostAsJsonAsync("/api/v1/ai/classification-suggestion", new
        {
            subject = "Card declined at checkout",
            description = "Every payment attempt is declined at the final step.",
            isUrgent = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("ai", body.GetProperty("generatedBy").GetString());

        var configured = await fixture.ConfiguredCategoryCodesAsync();

        Assert.Contains(body.GetProperty("categoryCode").GetString(), configured);

        // The priority is one of A-6's four, and it arrives as the string the ticket endpoints accept.
        Assert.Contains(
            body.GetProperty("priority").GetString(),
            Enum.GetNames<TicketPriority>());
    }

    /// <summary>
    /// <b>A Customer gets <c>403</c> on every assist.</b> A-8 excludes customer-facing generation
    /// entirely, and there is no portal variant of any of these endpoints.
    /// </summary>
    [Fact]
    public async Task A_customer_is_forbidden_from_every_assist()
    {
        var ticketId = await fixture.AddTicketAsync();

        var customer = fixture.Factory.CreateClientFor(fixture.PortalUserId);

        var summary = await customer.PostAsync($"/api/v1/tickets/{ticketId}/ai/summary", null);
        var reply = await customer.PostAsync($"/api/v1/tickets/{ticketId}/ai/suggested-reply", null);

        var classification = await customer.PostAsJsonAsync(
            "/api/v1/ai/classification-suggestion", new { subject = "s", description = "d" });

        Assert.Equal(HttpStatusCode.Forbidden, summary.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, reply.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, classification.StatusCode);
    }

    /// <summary>
    /// <b>No AI endpoint changes any ticket field</b> — the snapshot assertion the plan asks for, and
    /// the checkable form of AD-12. Every column that could plausibly drift is compared.
    /// </summary>
    [Fact]
    public async Task No_assist_changes_any_ticket_field()
    {
        var ticketId = await fixture.AddTicketAsync();

        var before = await fixture.SnapshotAsync(ticketId);

        // The fixture seeds a two-message thread so a summary has something to summarize, so the claim
        // is that the count is UNCHANGED — not that it is zero.
        var messagesBefore = await MessageCountAsync(ticketId);

        var agent = fixture.Factory.CreateClientFor(fixture.AgentId);

        await PostAsync(agent, $"/api/v1/tickets/{ticketId}/ai/summary");
        await PostAsync(agent, $"/api/v1/tickets/{ticketId}/ai/suggested-reply");

        var after = await fixture.SnapshotAsync(ticketId);

        Assert.Equal(before, after);

        // A suggested reply is a draft, never a send: no message row appeared.
        Assert.Equal(messagesBefore, await MessageCountAsync(ticketId));

        // Nor any activity row: DM-5 persists nothing for a summary or a draft.
        var activity = await fixture.Factory.WithDbAsync(async db =>
            await db.TicketActivities.CountAsync(a =>
                a.TicketId == ticketId && a.ActivityType == TicketActivityType.AiSuggestionOffered));

        Assert.Equal(0, activity);
    }

    private Task<int> MessageCountAsync(Guid ticketId) =>
        fixture.Factory.WithDbAsync(async db =>
            await db.TicketMessages.CountAsync(m => m.TicketId == ticketId));

    private static async Task<JsonElement> PostAsync(HttpClient client, string url)
    {
        var response = await client.PostAsync(url, content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

/// <summary>
/// <b>Scoping happens before any provider call</b> — the security property, asserted with a recording
/// stub so "never invoked" is observed rather than assumed.
///
/// <para>
/// It needs its own host because it replaces the AI seam, so it cannot share the class fixture with
/// the tests above.
/// </para>
/// </summary>
public sealed class AiScopingHappensBeforeTheProviderTests : IAsyncLifetime
{
    private readonly RecordingAiService _recorder = new();

    private AiAssistFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = new AiAssistFixture(services =>
        {
            services.RemoveAll<IAiAssistService>();
            services.AddSingleton<IAiAssistService>(_recorder);
        });

        await _fixture.InitializeAsync();
    }

    public Task DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>
    /// An Agent asking for a summary of <b>another department's</b> ticket gets <c>404</c>, and
    /// <b>the seam was never called</b> — so no customer content left the process for a ticket the
    /// caller may not read (architecture §4.3 point 2).
    /// </summary>
    [Fact]
    public async Task An_out_of_department_ticket_is_404_before_the_provider_is_touched()
    {
        var theirs = await _fixture.AddTicketAsync(otherDepartment: true);

        var agent = _fixture.Factory.CreateClientFor(_fixture.AgentId);

        var summary = await agent.PostAsync($"/api/v1/tickets/{theirs}/ai/summary", null);
        var reply = await agent.PostAsync($"/api/v1/tickets/{theirs}/ai/suggested-reply", null);

        Assert.Equal(HttpStatusCode.NotFound, summary.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reply.StatusCode);

        // The assertion that gives the test its meaning.
        Assert.Equal(0, _recorder.Calls);
    }

    /// <summary>
    /// The comparison that makes the above meaningful: an <b>in-department</b> ticket does reach the
    /// seam. Without this, a service that never called the provider at all would pass.
    /// </summary>
    [Fact]
    public async Task An_in_department_ticket_does_reach_the_provider()
    {
        var mine = await _fixture.AddTicketAsync();

        var agent = _fixture.Factory.CreateClientFor(_fixture.AgentId);

        var response = await agent.PostAsync($"/api/v1/tickets/{mine}/ai/summary", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(_recorder.Calls > 0);
    }

    /// <summary>Records that it was called, and returns something harmless.</summary>
    private sealed class RecordingAiService : IAiAssistService
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);

            return Task.FromResult(new AiSummary("recorded", DateTimeOffset.UnixEpoch));
        }

        public Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);

            return Task.FromResult(new AiSuggestedReply("recorded", DateTimeOffset.UnixEpoch));
        }

        public Task<AiClassification> SuggestClassificationAsync(
            AiClassificationRequest request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);

            return Task.FromResult(
                new AiClassification("billing", TicketPriority.Medium, DateTimeOffset.UnixEpoch));
        }
    }
}

/// <summary>
/// <b>An out-of-list suggested category never reaches the client</b> (§5.8) — asserted with a stub
/// that deliberately returns one, because the shipped fake always chooses from configuration and
/// therefore cannot exercise this path.
/// </summary>
public sealed class AiOutOfListCategoryTests : IAsyncLifetime
{
    private AiAssistFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = new AiAssistFixture(services =>
        {
            services.RemoveAll<IAiAssistService>();
            services.AddSingleton<IAiAssistService, RogueCategoryAiService>();
        });

        await _fixture.InitializeAsync();
    }

    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task An_unconfigured_category_is_replaced_before_the_response_leaves()
    {
        var agent = _fixture.Factory.CreateClientFor(_fixture.AgentId);

        var response = await agent.PostAsJsonAsync("/api/v1/ai/classification-suggestion", new
        {
            subject = "Anything",
            description = "The stub will answer with a category that is not configured.",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var code = body.GetProperty("categoryCode").GetString();

        Assert.NotEqual("not-a-real-category", code);
        Assert.Contains(code, await _fixture.ConfiguredCategoryCodesAsync());
    }

    private sealed class RogueCategoryAiService : IAiAssistService
    {
        public Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct) =>
            Task.FromResult(new AiSummary("s", DateTimeOffset.UnixEpoch));

        public Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct) =>
            Task.FromResult(new AiSuggestedReply("d", DateTimeOffset.UnixEpoch));

        public Task<AiClassification> SuggestClassificationAsync(
            AiClassificationRequest request, CancellationToken ct) =>
            Task.FromResult(new AiClassification(
                "not-a-real-category", TicketPriority.Urgent, DateTimeOffset.UnixEpoch));
    }
}

/// <summary>
/// <b>The T1-F pairing, and it is the pairing that matters</b>: with the seam throwing, each assist
/// answers <c>503 ai-unavailable</c> <em>while</em> creating a ticket, replying and transitioning all
/// still succeed. Either half alone would prove much less.
/// </summary>
public sealed class AiUnavailableDegradesOneFeatureTests : IAsyncLifetime
{
    private AiAssistFixture _fixture = null!;

    public async Task InitializeAsync()
    {
        _fixture = new AiAssistFixture(services =>
        {
            services.RemoveAll<IAiAssistService>();
            services.AddSingleton<IAiAssistService, ThrowingAiService>();
        });

        await _fixture.InitializeAsync();
    }

    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Each_assist_returns_503_while_support_work_continues()
    {
        var ticketId = await _fixture.AddTicketAsync();

        var agent = _fixture.Factory.CreateClientFor(_fixture.AgentId);

        // --- the AI half: 503, with the contract's slug.
        var summary = await agent.PostAsync($"/api/v1/tickets/{ticketId}/ai/summary", null);
        var reply = await agent.PostAsync($"/api/v1/tickets/{ticketId}/ai/suggested-reply", null);

        var classification = await agent.PostAsJsonAsync(
            "/api/v1/ai/classification-suggestion", new { subject = "s", description = "d" });

        foreach (var response in new[] { summary, reply, classification })
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal("ai-unavailable", problem.GetProperty("type").GetString());
        }

        // --- the work half, in the same run: all three succeed.
        var created = await agent.PostAsJsonAsync("/api/v1/tickets", new
        {
            customerId = _fixture.CustomerId,
            subject = "Created while AI is down",
            description = "Support work must not depend on the AI seam.",
            categoryCode = "billing",
            priority = nameof(TicketPriority.Medium),
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var newTicketId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var message = await agent.PostAsJsonAsync(
            $"/api/v1/tickets/{newTicketId}/messages", new { body = "Written by a human." });

        Assert.Equal(HttpStatusCode.Created, message.StatusCode);

        var transition = await agent.PostAsJsonAsync(
            $"/api/v1/tickets/{newTicketId}/transition", new { targetStatus = nameof(TicketStatus.Open) });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);
    }

    private sealed class ThrowingAiService : IAiAssistService
    {
        public Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct) =>
            throw new AiUnavailableException();

        public Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct) =>
            throw new AiUnavailableException();

        public Task<AiClassification> SuggestClassificationAsync(
            AiClassificationRequest request, CancellationToken ct) =>
            throw new AiUnavailableException();
    }
}
