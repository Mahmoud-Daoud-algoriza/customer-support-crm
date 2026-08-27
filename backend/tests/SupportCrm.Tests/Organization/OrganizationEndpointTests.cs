using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Organization;

/// <summary>
/// The two read endpoints of docs/api-design.md §5.4, proven <b>through the API as each role</b> —
/// T1-D requires permissions to be proven server-side, not assumed from the front end.
/// </summary>
public sealed class OrganizationEndpointTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    [Fact]
    public async Task Departments_as_Agent_is_200_with_at_least_two_rows()
    {
        // At least two departments, so department scoping is demonstrable in Story 05 — the same
        // shape the seeder produces (Billing, Technical).
        var first = await factory.EnsureDepartmentAsync("Endpoint Billing");
        var second = await factory.EnsureDepartmentAsync("Endpoint Technical");

        var agentId = await factory.AddStaffUserAsync(
            UserRole.Agent, "agent.departments@test.local", departmentId: first);

        var response = await factory.CreateClientFor(agentId).GetAsync("/api/v1/departments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        Assert.True(items.Count >= 2, $"Expected at least two departments, got {items.Count}.");

        var ids = items.Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(first, ids);
        Assert.Contains(second, ids);

        // The paged envelope, even for a short list (AP-3, docs/api-design.md §2.1).
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(25, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalItems").GetInt32() >= 2);
    }

    [Fact]
    public async Task Departments_as_Customer_is_403()
    {
        var customerId = await factory.AddCustomerRoleUserAsync("customer.departments@test.local");

        var response = await factory.CreateClientFor(customerId).GetAsync("/api/v1/departments");

        // 403, not 404: a Customer can infer this denial from their own role, so AP-4's 404 rule
        // does not apply (docs/api-design.md §4.2). Customers never call this endpoint — under A-14
        // they choose a category, never a department — so no /portal variant exists to redirect to.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// docs/api-design.md §6.2: <c>managerUserId</c> "may be absent". Combined with §2 — nulls are
    /// omitted rather than sent — absent means <b>the key is not there</b>, not that it is
    /// <c>null</c>. A client that distinguishes the two would be reading a value the contract never
    /// promised, and <b>OQ-3</b> is precisely the question of what a manager-less department means.
    /// </summary>
    [Fact]
    public async Task A_department_with_no_manager_omits_the_managerUserId_key_entirely()
    {
        var departmentId = await factory.EnsureDepartmentAsync("Endpoint Unmanaged");

        var agentId = await factory.AddStaffUserAsync(
            UserRole.Agent, "agent.unmanaged@test.local", departmentId: departmentId);

        var response = await factory.CreateClientFor(agentId).GetAsync("/api/v1/departments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var row = body.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == departmentId);

        Assert.False(
            row.TryGetProperty("managerUserId", out var manager),
            $"managerUserId must be absent, not present as {manager.ValueKind}.");

        // And explicitly not serialized as null — the failure this test exists to catch is a JSON
        // configuration change that starts emitting nulls.
        Assert.DoesNotContain("managerUserId", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Branches_as_Agent_is_200_with_at_least_two_rows()
    {
        var head = await factory.EnsureBranchAsync("Endpoint Head Office");
        var north = await factory.EnsureBranchAsync("Endpoint North Branch");

        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "agent.branches@test.local");

        var response = await factory.CreateClientFor(agentId).GetAsync("/api/v1/branches");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items").EnumerateArray().ToList();

        var ids = items.Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(head, ids);
        Assert.Contains(north, ids);

        // Exactly two fields, and no more: branch is a reporting attribute, so §6.2 publishes
        // { id, name } and nothing else.
        foreach (var item in items)
        {
            Assert.Equal(2, item.EnumerateObject().Count());
        }
    }

    [Fact]
    public async Task Branches_as_Customer_is_403()
    {
        var customerId = await factory.AddCustomerRoleUserAsync("customer.branches@test.local");

        var response = await factory.CreateClientFor(customerId).GetAsync("/api/v1/branches");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Departments and branches are <b>seeded and configured</b>, not managed through an admin UI
    /// (T2-I, docs/api-design.md §5.4). This locks the absence: a future story that adds a write
    /// action to either controller breaks here, which is the point.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/departments")]
    [InlineData("/api/v1/branches")]
    public async Task No_write_endpoint_exists(string path)
    {
        var adminId = await factory.AddStaffUserAsync(
            UserRole.Administrator, $"admin.nowrite{path.GetHashCode()}@test.local");

        var client = factory.CreateClientFor(adminId);

        // Administrator — the highest role there is. This is not a permission result; the verb
        // simply does not exist.
        var post = await client.PostAsJsonAsync(path, new { name = "Invented" });
        var patch = await client.PatchAsJsonAsync(path, new { name = "Invented" });
        var delete = await client.DeleteAsync(path);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, patch.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    /// <summary>
    /// docs/api-design.md §2.1: an unknown sort field is a <c>400</c>, never silently ignored
    /// (AP-15).
    /// </summary>
    [Fact]
    public async Task An_unknown_sort_field_is_400()
    {
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "agent.sort@test.local");

        var response = await factory.CreateClientFor(agentId)
            .GetAsync("/api/v1/departments?sort=managerUserId:asc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
