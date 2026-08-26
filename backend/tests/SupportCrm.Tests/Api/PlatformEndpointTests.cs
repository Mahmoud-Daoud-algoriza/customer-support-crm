using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SupportCrm.Tests.Api;

/// <summary>
/// The two platform endpoints of docs/api-design.md §5.1. Coverage is targeted, not exhaustive
/// (product-scope §8).
/// </summary>
public sealed class PlatformEndpointTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    [Fact]
    public async Task Health_returns_200_and_reports_a_reachable_database()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("reachable", body.GetProperty("database").GetString());
        Assert.EndsWith("Z", body.GetProperty("utcNow").GetString());
    }

    [Fact]
    public async Task Bootstrap_config_returns_200_with_branding_and_both_languages()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/config/bootstrap");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("productName").GetString()));

        var languages = body.GetProperty("languages")
            .EnumerateArray().Select(l => l.GetString()).ToArray();
        Assert.Contains("en", languages);
        Assert.Contains("ar", languages);

        Assert.Equal("en", body.GetProperty("defaultLanguage").GetString());
    }
}
