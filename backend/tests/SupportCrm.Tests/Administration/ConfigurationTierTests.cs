using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Administration;

/// <summary>
/// <b>AP-17 — configuration is split by audience — proven through the API as each role.</b>
/// <para>
/// This is the regression test for <b>B-2</b>: the first version of the contract returned quick
/// replies and SLA targets to every authenticated caller, Customers included. These tests fail if
/// the two endpoints are ever merged back into one, or if a staff-only value leaks into the
/// customer tier.
/// </para>
/// </summary>
public sealed class ConfigurationTierTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    /// <summary>
    /// Plan test 1. The assertion is on the <b>raw JSON keys</b>, not on a deserialized type: a DTO
    /// would happily ignore an extra property, and an extra property in this tier is the whole risk.
    /// </summary>
    [Fact]
    public async Task Customer_config_has_exactly_categories_and_feedback_and_nothing_else()
    {
        var customerId = await factory.AddCustomerRoleUserAsync("customer.config@test.local");

        var response = await factory.CreateClientFor(customerId).GetAsync("/api/v1/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var keys = body.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(["categories", "feedback"], keys);

        // The rating scale is present and structural. Its VALUES are not asserted — OQ-1 is open,
        // and a test that pinned 1..5 would answer it by accident (api-design §6.9).
        var scale = body.GetProperty("feedback").GetProperty("ratingScale");
        Assert.True(scale.GetProperty("min").GetInt32() < scale.GetProperty("max").GetInt32());

        Assert.NotEmpty(body.GetProperty("categories").EnumerateArray());
    }

    /// <summary>
    /// Plan test 2 — <b>the leak this tier exists to prevent.</b> Under A-14 a customer chooses a
    /// category and the server derives the department; the routing map is internal policy.
    /// </summary>
    [Fact]
    public async Task No_category_in_the_customer_tier_carries_a_departmentId()
    {
        var customerId = await factory.AddCustomerRoleUserAsync("customer.nomap@test.local");

        var response = await factory.CreateClientFor(customerId).GetAsync("/api/v1/config");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var category in body.GetProperty("categories").EnumerateArray())
        {
            Assert.Equal(["code", "name"], category.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        }

        // Belt and braces against a future shape change that nests the map somewhere else: the
        // string must not appear anywhere in the payload at all.
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("departmentId", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Plan test 3. <c>403</c>, not <c>404</c>: a capability denial the caller can infer from their
    /// own role, so AP-4's <c>404</c> rule does not apply (api-design §5.1, §4.2).
    /// </summary>
    [Fact]
    public async Task Staff_config_is_403_for_a_Customer()
    {
        var customerId = await factory.AddCustomerRoleUserAsync("customer.staffconfig@test.local");

        var response = await factory.CreateClientFor(customerId).GetAsync("/api/v1/config/staff");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Plan test 3, the other half — an Agent gets all four groups (api-design §6.9).</summary>
    [Fact]
    public async Task Staff_config_is_200_for_an_Agent_with_all_four_groups()
    {
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "agent.staffconfig@test.local");

        var response = await factory.CreateClientFor(agentId).GetAsync("/api/v1/config/staff");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var keys = body.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();

        Assert.Equal(["categoryDepartmentMap", "priorities", "quickReplies", "slaTargets"], keys);

        // A-6: four levels, in order. Configuration may not add a fifth.
        Assert.Equal(
            ["Low", "Medium", "High", "Urgent"],
            body.GetProperty("priorities").EnumerateArray().Select(p => p.GetString() ?? "").ToArray());

        // The routing map A-14 keeps away from customers is published here, and only here.
        Assert.NotEmpty(body.GetProperty("categoryDepartmentMap").EnumerateArray());
        foreach (var entry in body.GetProperty("categoryDepartmentMap").EnumerateArray())
        {
            Assert.NotEqual(Guid.Empty, entry.GetProperty("departmentId").GetGuid());
        }

        // Every priority carries a target — the same rule startup validation enforces (A-3).
        var targeted = body.GetProperty("slaTargets").EnumerateArray()
            .Select(t => t.GetProperty("priority").GetString())
            .ToArray();

        foreach (var level in new[] { "Low", "Medium", "High", "Urgent" })
        {
            Assert.Contains(level, targeted);
        }
    }

    /// <summary>The customer tier still requires authentication — it is not the anonymous tier.</summary>
    [Fact]
    public async Task Customer_config_is_401_when_anonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/config");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Plan test 4 — Story 01 regression. The public tier must stay <b>anonymous</b>: it is what
    /// renders the sign-in screen, so requiring a token would deadlock the front end (T3-E, T2-J).
    /// </summary>
    [Fact]
    public async Task Bootstrap_config_is_still_anonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/config/bootstrap");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("productName").GetString()));
    }

    /// <summary>
    /// <b>No endpoint in this API writes configuration</b> — changing it is a redeploy (T2-I). This
    /// locks the absence on all three tiers.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/config")]
    [InlineData("/api/v1/config/staff")]
    public async Task No_configuration_write_endpoint_exists(string path)
    {
        var adminId = await factory.AddStaffUserAsync(
            UserRole.Administrator, $"admin.confignowrite{path.GetHashCode()}@test.local");

        var client = factory.CreateClientFor(adminId);

        var post = await client.PostAsJsonAsync(path, new { invented = true });
        var patch = await client.PatchAsJsonAsync(path, new { invented = true });
        var put = await client.PutAsJsonAsync(path, new { invented = true });
        var delete = await client.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, patch.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }
}
