using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SupportCrm.Tests.Knowledge;

/// <summary>
/// Story 12 task 7, tests 1–7 — <b>who may see and who may write an article</b>
/// (docs/api-design.md §5.9, docs/data-model.md §5 constraint 19, A-4, AP-4).
///
/// <para>
/// The two rules under test are different in kind, and the suite keeps them apart. <b>Role</b> is a
/// capability the caller can infer from their own role, so a refusal is <c>403</c> (§4.2).
/// <b>Portal visibility</b> is an existence question, so a refusal is <c>404</c> — <c>403</c> would
/// confirm that the article exists, which is exactly what AP-4 forbids.
/// </para>
/// </summary>
public sealed class KnowledgeVisibilityTests(KnowledgeFixture fixture) : IClassFixture<KnowledgeFixture>
{
    /// <summary>
    /// <b>Test 1.</b> An Agent's search returns <b>both</b> public and internal articles: internal
    /// articles exist for staff, and no visibility predicate narrows a staff read (§5.9).
    /// </summary>
    [Fact]
    public async Task An_agent_search_returns_public_and_internal_articles()
    {
        var publicId = await fixture.AddArticleAsync(
            "Zephyrine public guidance", "Zephyrine is the shared marker for this test.");

        var internalId = await fixture.AddArticleAsync(
            "Zephyrine internal runbook", "Zephyrine is the shared marker for this test.",
            visibility: "Internal");

        var response = await fixture.Agent.GetAsync($"{KnowledgeFixture.Articles}?q=zephyrine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");

        var ids = items.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

        Assert.Contains(publicId, ids);
        Assert.Contains(internalId, ids);

        // And the internal one is *labelled* internal, which is what the staff badge renders
        // (docs/ui-design.md §5.6).
        var internalItem = items.EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == internalId);

        Assert.Equal("Internal", internalItem.GetProperty("visibility").GetString());
    }

    /// <summary>
    /// <b>Test 2.</b> A Customer calling the <b>staff</b> path is <c>403</c>, not <c>404</c>: the
    /// staff path space is role-gated, and a role denial is one the caller can infer from their own
    /// role (§4.2). Customers have their own path space.
    /// </summary>
    [Fact]
    public async Task A_customer_is_refused_the_staff_search()
    {
        var response = await fixture.Customer.GetAsync(KnowledgeFixture.Articles);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// <b>Test 3.</b> The portal search returns <b>only</b> articles that are public <em>and</em>
    /// published — the two independent facts of constraint 19.
    /// </summary>
    [Fact]
    public async Task The_portal_search_returns_only_public_published_articles()
    {
        var visibleId = await fixture.AddArticleAsync(
            "Quaraline published help", "Quaraline is the shared marker for this test.");

        var internalId = await fixture.AddArticleAsync(
            "Quaraline internal notes", "Quaraline is the shared marker for this test.",
            visibility: "Internal");

        var draftId = await fixture.AddArticleAsync(
            "Quaraline draft", "Quaraline is the shared marker for this test.", publish: false);

        var response = await fixture.Customer.GetAsync($"{KnowledgeFixture.PortalArticles}?q=quaraline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = await IdsOf(response);

        Assert.Contains(visibleId, ids);
        Assert.DoesNotContain(internalId, ids);
        Assert.DoesNotContain(draftId, ids);
    }

    /// <summary>
    /// <b>Test 4.</b> An internal article read from the portal is <c>404</c>, <b>not <c>403</c></b>
    /// (AP-4). A <c>403</c> would confirm that the id names a real article.
    /// </summary>
    [Fact]
    public async Task An_internal_article_is_404_on_the_portal_never_403()
    {
        var internalId = await fixture.AddArticleAsync(
            "Vermillion internal escalation path", "Staff-only body text.", visibility: "Internal");

        var response = await fixture.Customer.GetAsync($"{KnowledgeFixture.PortalArticles}/{internalId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        // And the wording is the same one a missing id produces, so the response body cannot
        // distinguish them either (docs/ui-design.md §9).
        var missing = await fixture.Customer.GetAsync($"{KnowledgeFixture.PortalArticles}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await DetailOf(missing), await DetailOf(response));
    }

    /// <summary>
    /// <b>Test 5.</b> A <b>public but unpublished</b> article is equally unreachable — proving
    /// <c>isPublished</c> is enforced separately from <c>visibility</c>.
    /// </summary>
    [Fact]
    public async Task A_public_but_unpublished_article_is_404_on_the_portal()
    {
        var draftId = await fixture.AddArticleAsync(
            "Marlowe upcoming changes", "A public article that has not been published.", publish: false);

        var response = await fixture.Customer.GetAsync($"{KnowledgeFixture.PortalArticles}/{draftId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Unpublishing is reversible, and the portal follows it: the same id becomes reachable once
        // published, which is what makes the refusal a rule rather than an accident.
        (await fixture.Administrator.PostAsync($"{KnowledgeFixture.Articles}/{draftId}/publish", null))
            .EnsureSuccessStatusCode();

        var afterPublish = await fixture.Customer.GetAsync($"{KnowledgeFixture.PortalArticles}/{draftId}");

        Assert.Equal(HttpStatusCode.OK, afterPublish.StatusCode);

        // The portal payload is the narrow shape of §6.5 — no visibility, no isPublished, no author.
        var body = await afterPublish.Content.ReadFromJsonAsync<JsonElement>();
        var keys = body.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal);

        Assert.Equal(["body", "id", "title", "type", "updatedAt"], keys);
    }

    /// <summary>
    /// <b>Test 6.</b> Authoring is Administrator-only (A-4): an Agent is <c>403</c>, an Administrator
    /// gets <c>201</c>, and the created article is <b>unpublished</b> — drafted before it is visible
    /// (docs/api-design.md §6.11).
    /// </summary>
    [Fact]
    public async Task Only_an_administrator_may_author_and_a_new_article_is_unpublished()
    {
        var refused = await fixture.Agent.PostAsJsonAsync(
            KnowledgeFixture.Articles,
            new { title = "Agent attempt", body = "Body.", type = "Faq", visibility = "Public" });

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var created = await fixture.Administrator.PostAsJsonAsync(
            KnowledgeFixture.Articles,
            new { title = "Administrator draft", body = "Body.", type = "Faq", visibility = "Public" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("isPublished").GetBoolean());

        // The author is the authenticated Administrator, never a supplied value (§6.11, §7).
        Assert.Equal(fixture.AdministratorId, body.GetProperty("author").GetProperty("id").GetGuid());
    }

    /// <summary>
    /// <b>Test 7.</b> <c>PATCH</c> carrying <c>isPublished</c> is <c>400</c> — publication changes
    /// only through <c>/publish</c> and <c>/unpublish</c> (§6.11, AP-1, AP-10).
    /// <para>
    /// The refusal is real rather than aspirational: the field is absent from
    /// <c>PatchArticleRequest</c>, and <c>UnmappedMemberHandling.Disallow</c> turns an unmapped
    /// member into a <c>400</c> instead of a silent no-op (finding I-9).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Patching_isPublished_is_400_and_the_action_pair_still_works()
    {
        var id = await fixture.AddArticleAsync(
            "Ashgrove publication rules", "Body text.", publish: false);

        var refused = await fixture.Administrator.PatchAsJsonAsync(
            $"{KnowledgeFixture.Articles}/{id}", new { isPublished = true });

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // Still unpublished — the rejected request changed nothing.
        var afterPatch = await fixture.Administrator.GetFromJsonAsync<JsonElement>(
            $"{KnowledgeFixture.Articles}/{id}");

        Assert.False(afterPatch.GetProperty("isPublished").GetBoolean());

        // The one path that does work.
        (await fixture.Administrator.PostAsync($"{KnowledgeFixture.Articles}/{id}/publish", null))
            .EnsureSuccessStatusCode();

        var published = await fixture.Administrator.GetFromJsonAsync<JsonElement>(
            $"{KnowledgeFixture.Articles}/{id}");

        Assert.True(published.GetProperty("isPublished").GetBoolean());

        (await fixture.Administrator.PostAsync($"{KnowledgeFixture.Articles}/{id}/unpublish", null))
            .EnsureSuccessStatusCode();

        var unpublished = await fixture.Administrator.GetFromJsonAsync<JsonElement>(
            $"{KnowledgeFixture.Articles}/{id}");

        Assert.False(unpublished.GetProperty("isPublished").GetBoolean());
    }

    /// <summary>
    /// <b>No delete path exists anywhere</b> (T2-E, docs/ui-design.md §6) — not for an
    /// Administrator, and not on the portal. The absence is the contract, so it is asserted rather
    /// than assumed.
    /// </summary>
    [Fact]
    public async Task There_is_no_delete_endpoint()
    {
        var id = await fixture.AddArticleAsync("Deletion is not modelled", "Body text.");

        var staff = await fixture.Administrator.DeleteAsync($"{KnowledgeFixture.Articles}/{id}");
        var portal = await fixture.Customer.DeleteAsync($"{KnowledgeFixture.PortalArticles}/{id}");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, staff.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, portal.StatusCode);
    }

    private static async Task<IReadOnlyList<Guid>> IdsOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return [.. body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())];
    }

    private static async Task<string?> DetailOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        return body.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
    }
}
