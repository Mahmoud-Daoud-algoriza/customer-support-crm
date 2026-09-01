using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Tickets;

namespace SupportCrm.Tests.Knowledge;

/// <summary>
/// Story 12 task 7, tests 8–12 — <b>keyword search and §7.4 retrieval</b>
/// (AD-13, AP-14, docs/api-design.md §5.9, §6.5).
///
/// <para>
/// It runs against <see cref="TicketApiFixture"/>'s two-department world because suggestions are
/// scoped through the ticket: the assertion that an out-of-department ticket is <c>404</c> needs an
/// agent who genuinely cannot see it, which one department cannot produce.
/// </para>
///
/// <para>
/// <b>Nothing here asserts a quality of match beyond the ranking AD-13 fixes</b> — title above body,
/// counted per term. Relevance tuning is excluded by the intake, so a test that demanded more would
/// be testing a feature the product does not have.
/// </para>
/// </summary>
public sealed class SearchAndSuggestionTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Articles = "/api/v1/kb/articles";

    /// <summary>
    /// <b>Test 8.</b> A keyword matches in the <b>title</b> and matches in the <b>body</b>, and the
    /// title match ranks higher (<c>ArticleSearch.TitleWeight</c> above <c>BodyWeight</c>).
    /// </summary>
    [Fact]
    public async Task Search_matches_title_and_body_and_a_title_match_ranks_higher()
    {
        var admin = await AdministratorAsync("rank");

        var bodyMatch = await AddArticleAsync(admin,
            "Unrelated heading for the ranking check",
            "The word thistledown appears only in this body text.");

        var titleMatch = await AddArticleAsync(admin,
            "Thistledown in the title of this article",
            "A body with no other marker in it.");

        var response = await Agent().GetAsync($"{Articles}?q=thistledown");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = await IdsOf(response);

        // Both are found: matching covers title AND body (AD-13).
        Assert.Contains(titleMatch, ids);
        Assert.Contains(bodyMatch, ids);

        // And the title match leads, which is the whole of the ranking.
        Assert.Equal(titleMatch, ids[0]);
        Assert.True(ids.IndexOf(titleMatch) < ids.IndexOf(bodyMatch));
    }

    /// <summary>
    /// <b>Test 9.</b> A search with no matches is a <b>clean empty page</b>, not an error — an empty
    /// state is never an error (docs/ui-design.md §9).
    /// </summary>
    [Fact]
    public async Task A_search_with_no_matches_returns_an_empty_page()
    {
        var response = await Agent().GetAsync($"{Articles}?q=nonexistentkeywordxyzzy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, body.GetProperty("totalPages").GetInt32());
    }

    /// <summary>
    /// <b>Test 10.</b> <c>GET /tickets/{id}/suggested-articles</c> returns articles whose text
    /// overlaps the ticket's <b>subject and description</b>, each carrying a <c>matchScore</c>
    /// (docs/api-design.md §6.5).
    /// </summary>
    [Fact]
    public async Task Suggested_articles_retrieve_matches_for_the_tickets_own_text()
    {
        var admin = await AdministratorAsync("suggest");

        var relevant = await AddArticleAsync(admin,
            "Sprocket calibration for the reporting export",
            "A sprocket that drifts out of calibration makes the reporting export fail.");

        var irrelevant = await AddArticleAsync(admin,
            "Choosing a stationery supplier",
            "Nothing in this article has anything to do with the ticket under test.");

        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            subject: "Sprocket calibration keeps failing");

        var response = await Agent().GetAsync($"/api/v1/tickets/{ticketId}/suggested-articles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();

        var suggestion = Assert.Single(items, item => item.GetProperty("id").GetGuid() == relevant);

        Assert.True(suggestion.GetProperty("matchScore").GetInt32() > 0);
        Assert.DoesNotContain(items, item => item.GetProperty("id").GetGuid() == irrelevant);

        // **Retrieval, not generation.** The payload is `{ id, title, type, matchScore }` and carries
        // no `generatedBy` — the field every AI response does carry (§6.10). AP-14 is the reason.
        var keys = suggestion.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(["id", "matchScore", "title", "type"], keys);
    }

    /// <summary>
    /// <b>Test 11.</b> The same endpoint on an <b>out-of-department</b> ticket is <c>404</c> — the
    /// ticket is scoped before any article is read (AD-5, AP-4).
    /// </summary>
    [Fact]
    public async Task Suggested_articles_on_an_out_of_department_ticket_is_404()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            subject: "A billing ticket a technical agent may not see");

        var response = await fixture.Factory.CreateClientFor(fixture.TechnicalAgentId)
            .GetAsync($"/api/v1/tickets/{ticketId}/suggested-articles");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// <b>Test 12.</b> <b>No <c>/ai</c> route serves suggested solutions</b> (AP-14). Suggested
    /// solutions retrieve rather than generate, so they are a Knowledge endpoint; publishing them
    /// under <c>/ai</c> would move a keyword search behind a provider call.
    /// <para>
    /// Asserted twice: the path is not routable over HTTP, and no registered endpoint anywhere in
    /// the application has a route template mentioning it. The second check is what catches the
    /// route being added under a different verb or a different <c>/ai</c> prefix.
    /// </para>
    /// </summary>
    [Fact]
    public async Task No_ai_route_serves_suggested_solutions()
    {
        var response = await Agent().PostAsync("/api/v1/ai/suggested-solutions", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var templates = fixture.Factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(templates, template =>
            template.Contains("suggested-solutions", StringComparison.OrdinalIgnoreCase));

        // The endpoint that *does* serve §7.4 is on the ticket, under Knowledge — not under /ai.
        Assert.Contains(templates, template =>
            template.Contains("suggested-articles", StringComparison.OrdinalIgnoreCase) &&
            !template.Contains("/ai/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <b>No search engine and no vector store is reachable from this endpoint</b> (architecture §8,
    /// AD-13): the whole of matching is <c>LIKE</c> in one file, so a search runs with nothing but a
    /// database — which is what the hermetic SQLite host proves by answering at all.
    /// </summary>
    [Fact]
    public async Task Search_runs_against_the_database_alone()
    {
        var admin = await AdministratorAsync("nosearchengine");

        await AddArticleAsync(admin, "Filigree handbook", "A body mentioning filigree twice: filigree.");

        var response = await Agent().GetAsync($"{Articles}?q=filigree");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
    }

    /// <summary>
    /// <b>AP-15.</b> §5.9 publishes no sort field for this endpoint, so any <c>sort</c> value is a
    /// <c>400</c> rather than being silently ignored.
    /// </summary>
    [Fact]
    public async Task An_unknown_sort_field_is_400()
    {
        var response = await Agent().GetAsync($"{Articles}?sort=title:asc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>An unknown filter value names what is allowed rather than defaulting silently.</summary>
    [Fact]
    public async Task An_unknown_type_filter_is_400()
    {
        var response = await Agent().GetAsync($"{Articles}?type=Novel");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient Agent() => fixture.Factory.CreateClientFor(fixture.BillingAgentId);

    private async Task<HttpClient> AdministratorAsync(string discriminator)
    {
        var id = await fixture.Factory.AddStaffUserAsync(
            UserRole.Administrator,
            $"kb.{discriminator}.admin@knowledge.local",
            departmentId: TicketApiFixture.BillingDepartmentId);

        return fixture.Factory.CreateClientFor(id);
    }

    /// <summary>Authored and published through the real endpoints, as an Administrator (A-4).</summary>
    private static async Task<Guid> AddArticleAsync(HttpClient administrator, string title, string body)
    {
        var created = await administrator.PostAsJsonAsync(
            Articles, new { title, body, type = "HelpArticle", visibility = "Public" });

        created.EnsureSuccessStatusCode();

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await administrator.PostAsync($"{Articles}/{id}/publish", null)).EnsureSuccessStatusCode();

        return id;
    }

    private static async Task<List<Guid>> IdsOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())];
    }
}
