using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Identity;

/// <summary>
/// The server-side validation rules of docs/api-design.md §5.3, exercised through the endpoint.
/// The front end re-implements some of them for immediate feedback; these tests prove the server
/// does not rely on it (T1-D).
/// </summary>
public sealed class UserAdminValidationTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    private async Task<HttpClient> AdministratorClientAsync(string email) =>
        factory.CreateClientFor(await factory.AddStaffUserAsync(UserRole.Administrator, email));

    [Fact]
    public async Task A_staff_role_without_a_department_is_rejected()
    {
        var client = await AdministratorClientAsync("admin.nodept@test.local");

        var response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "agent.nodept@test.local",
            password = "Passw0rd!",
            displayName = "No Department",
            role = "Agent",
            // departmentId deliberately omitted
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// DM-1: the <c>Customer</c> role cannot be created here at all — customers arrive through
    /// registration or by an agent creating a profile.
    /// </summary>
    [Fact]
    public async Task The_Customer_role_is_rejected_outright()
    {
        var client = await AdministratorClientAsync("admin.nocustomer@test.local");
        var departmentId = await factory.EnsureDepartmentAsync("Customer Rejection Department");

        var response = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "customer.viauseradmin@test.local",
            password = "Passw0rd!",
            displayName = "Should Not Exist",
            role = "Customer",
            departmentId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_duplicate_email_is_409_user_already_exists()
    {
        var client = await AdministratorClientAsync("admin.duplicate@test.local");
        var departmentId = await factory.EnsureDepartmentAsync("Duplicate Department");

        var request = new
        {
            email = "duplicate.agent@test.local",
            password = "Passw0rd!",
            displayName = "First Occupant",
            role = "Agent",
            departmentId,
        };

        var first = await client.PostAsJsonAsync("/api/v1/users", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/users", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var problem = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user-already-exists", problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task A_created_user_never_exposes_its_password_hash()
    {
        var client = await AdministratorClientAsync("admin.nohash@test.local");
        var departmentId = await factory.EnsureDepartmentAsync("Hash Department");

        var created = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email = "nohash.agent@test.local",
            password = "Passw0rd!",
            displayName = "No Hash Please",
            role = "Agent",
            departmentId,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", createdBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Passw0rd!", createdBody, StringComparison.Ordinal);

        // The same must hold for both read paths, which share one projection.
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.DoesNotContain("passwordHash",
            await (await client.GetAsync($"/api/v1/users/{id}")).Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("passwordHash",
            await (await client.GetAsync("/api/v1/users")).Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_created_user_can_sign_in_and_a_deactivated_one_cannot()
    {
        var client = await AdministratorClientAsync("admin.lifecycle@test.local");
        var departmentId = await factory.EnsureDepartmentAsync("Lifecycle Department");

        const string email = "lifecycle.agent@test.local";
        const string password = "Lifecycl3Passw0rd!";

        var created = await client.PostAsJsonAsync("/api/v1/users", new
        {
            email, password, displayName = "Lifecycle Agent", role = "Agent", departmentId,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();

        var signedIn = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        var token = await signedIn.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("accessToken").GetString()));
        Assert.EndsWith("Z", token.GetProperty("expiresAt").GetString());
        Assert.Equal(email, token.GetProperty("user").GetProperty("email").GetString());

        var deactivated = await client.PostAsync($"/api/v1/users/{id}/deactivate", content: null);
        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);

        var afterDeactivation = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivation.StatusCode);
    }

    /// <summary>
    /// AP-15: an unknown sort field is a <c>400</c>, never silently ignored.
    /// </summary>
    [Fact]
    public async Task An_unknown_sort_field_is_400()
    {
        var client = await AdministratorClientAsync("admin.sort@test.local");

        var unknown = await client.GetAsync("/api/v1/users?sort=passwordHash:asc");
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var known = await client.GetAsync("/api/v1/users?sort=email:desc");
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
    }

    [Fact]
    public async Task The_list_returns_the_paged_envelope()
    {
        var client = await AdministratorClientAsync("admin.paging@test.local");

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/users?page=1&pageSize=1");

        // AP-3, docs/api-design.md §2.1 — the exact five keys every collection endpoint returns.
        foreach (var key in new[] { "items", "page", "pageSize", "totalItems", "totalPages" })
        {
            Assert.True(body.TryGetProperty(key, out _), $"missing '{key}'");
        }

        Assert.Equal(1, body.GetProperty("pageSize").GetInt32());
    }
}
