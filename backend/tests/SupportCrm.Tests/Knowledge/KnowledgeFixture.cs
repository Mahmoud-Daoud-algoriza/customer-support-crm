using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Knowledge;

/// <summary>
/// The world Story 12's API tests run against: an Administrator who authors, an Agent who reads, and
/// a Customer with a portal login.
///
/// <para>
/// <b>Articles are created through the real endpoint</b>, not written to the table, because
/// authoring <em>is</em> part of what these tests assert — the default of <c>isPublished</c>, the
/// Administrator gate and the author attribution all come from that path. The fixture only
/// establishes the three callers.
/// </para>
/// </summary>
public sealed class KnowledgeFixture : IAsyncLifetime
{
    public const string Articles = "/api/v1/kb/articles";

    public const string PortalArticles = "/api/v1/portal/kb/articles";

    public SupportCrmApiFactory Factory { get; } = new();

    public Guid AdministratorId { get; private set; }

    public Guid AgentId { get; private set; }

    public Guid CustomerUserId { get; private set; }

    public HttpClient Administrator => Factory.CreateClientFor(AdministratorId);

    public HttpClient Agent => Factory.CreateClientFor(AgentId);

    public HttpClient Customer => Factory.CreateClientFor(CustomerUserId);

    public async Task InitializeAsync()
    {
        AdministratorId = await Factory.AddStaffUserAsync(
            UserRole.Administrator, "kb.admin@knowledge.local");

        AgentId = await Factory.AddStaffUserAsync(UserRole.Agent, "kb.agent@knowledge.local");

        CustomerUserId = await Factory.AddCustomerRoleUserAsync("kb.customer@knowledge.local");
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Authors one article as the Administrator and, when asked, publishes it through the dedicated
    /// action — <b>never by writing a field</b>, because no such path exists (AP-1,
    /// docs/api-design.md §6.11).
    /// </summary>
    public async Task<Guid> AddArticleAsync(
        string title,
        string body,
        string type = "HelpArticle",
        string visibility = "Public",
        bool publish = true)
    {
        var client = Administrator;

        var created = await client.PostAsJsonAsync(
            Articles, new { title, body, type, visibility });

        created.EnsureSuccessStatusCode();

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        if (publish)
        {
            (await client.PostAsync($"{Articles}/{id}/publish", null)).EnsureSuccessStatusCode();
        }

        return id;
    }
}
