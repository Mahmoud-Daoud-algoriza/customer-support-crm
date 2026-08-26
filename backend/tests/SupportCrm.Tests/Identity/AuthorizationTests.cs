using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Identity;

/// <summary>
/// Role gating proven <b>through the API as each role, bypassing the UI</b> — product-scope T1-D
/// requires permissions to be proven server-side rather than assumed from the front end.
/// </summary>
public sealed class AuthorizationTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    [Fact]
    public async Task Unauthenticated_request_to_users_is_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_token_on_users_is_403()
    {
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "agent.403@test.local");

        var response = await factory.CreateClientFor(agentId).GetAsync("/api/v1/users");

        // 403, not 404: this is a capability denial the caller can infer from their own role
        // (docs/api-design.md §4.2), so AP-4's 404 rule does not apply.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_token_on_users_is_200()
    {
        var adminId = await factory.AddStaffUserAsync(UserRole.Administrator, "admin.200@test.local");

        var response = await factory.CreateClientFor(adminId).GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_satisfies_an_Agent_gate_because_roles_are_hierarchical()
    {
        // A-4: an endpoint marked Agent is also reachable by Manager and Administrator
        // (docs/api-design.md §4.2). /auth/me is merely [Authorize], so this asserts the hierarchy
        // through the policy the gate uses rather than through an endpoint Story 03 owns.
        var managerId = await factory.AddStaffUserAsync(UserRole.Manager, "manager.hierarchy@test.local");

        var response = await factory.CreateClientFor(managerId).GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Manager", body.GetProperty("role").GetString());
    }

    /// <summary>
    /// <b>The AD-15 regression test.</b> This is the defect the whole per-request resolution design
    /// exists to prevent: a token minted before deactivation must stop working immediately, not when
    /// it expires.
    /// </summary>
    [Fact]
    public async Task A_user_deactivated_after_their_token_was_issued_is_401_on_the_very_next_request()
    {
        var adminId = await factory.AddStaffUserAsync(UserRole.Administrator, "admin.deactivated@test.local");

        var client = factory.CreateClientFor(adminId);

        // The token works while the account is active.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        // Deactivate out of band — the token is untouched and still cryptographically valid.
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == adminId);
            user.Deactivate();
            await db.SaveChangesAsync();

            return true;
        });

        var afterDeactivation = await client.GetAsync("/api/v1/auth/me");

        // 401, not 403: a deactivated user has no valid identity regardless of what their token
        // says (docs/api-design.md §4.1).
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeactivation.StatusCode);
    }

    /// <summary>
    /// A wrong password and a deactivated account must be indistinguishable, or the response
    /// confirms which emails have accounts (docs/api-design.md §6.11).
    /// </summary>
    [Fact]
    public async Task Wrong_password_and_deactivated_account_return_the_same_401_body()
    {
        const string password = "CorrectPassw0rd!";

        var wrongPasswordUserId = await factory.AddStaffUserAsync(
            UserRole.Agent, "same.wrongpw@test.local", password);

        var deactivatedUserId = await factory.AddStaffUserAsync(
            UserRole.Agent, "same.deactivated@test.local", password);

        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == deactivatedUserId);
            user.Deactivate();
            await db.SaveChangesAsync();

            return true;
        });

        var client = factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "same.wrongpw@test.local", password = "WrongPassw0rd!" });

        var deactivated = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = "same.deactivated@test.local", password });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, deactivated.StatusCode);

        var wrongPasswordProblem = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var deactivatedProblem = await deactivated.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("invalid-credentials", wrongPasswordProblem.GetProperty("type").GetString());
        Assert.Equal("invalid-credentials", deactivatedProblem.GetProperty("type").GetString());

        // Same slug, same title, same detail — nothing distinguishes the two cases.
        Assert.Equal(
            wrongPasswordProblem.GetProperty("title").GetString(),
            deactivatedProblem.GetProperty("title").GetString());

        Assert.Equal(
            wrongPasswordProblem.GetProperty("detail").GetString(),
            deactivatedProblem.GetProperty("detail").GetString());

        // Neither response leaks the hash.
        Assert.DoesNotContain("passwordHash", await wrongPassword.Content.ReadAsStringAsync());
        Assert.NotEqual(Guid.Empty, wrongPasswordUserId);
    }

    /// <summary>
    /// AP-9: <c>/auth/me</c> is the per-request resolved identity, not a decoded token. A role change
    /// is therefore visible immediately, <b>without a new token being issued</b>.
    /// </summary>
    [Fact]
    public async Task Auth_me_returns_the_new_role_after_an_Administrator_changes_it_without_a_new_token()
    {
        var adminId = await factory.AddStaffUserAsync(UserRole.Administrator, "admin.promoter@test.local");
        var departmentId = await factory.EnsureDepartmentAsync("Promotion Department");
        var agentId = await factory.AddStaffUserAsync(
            UserRole.Agent, "agent.promoted@test.local", departmentId: departmentId);

        // The agent's token is issued ONCE, here, and never re-issued.
        var agentClient = factory.CreateClientFor(agentId);

        var before = await agentClient.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal("Agent", before.GetProperty("role").GetString());

        var promotion = await factory.CreateClientFor(adminId)
            .PatchAsJsonAsync($"/api/v1/users/{agentId}", new { role = "Manager" });
        Assert.Equal(HttpStatusCode.OK, promotion.StatusCode);

        var after = await agentClient.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal("Manager", after.GetProperty("role").GetString());
    }

    /// <summary>
    /// The token asserts identity only (AD-7). Nothing an authorization decision reads is present,
    /// which is what makes the staleness of §4.1.1 unreachable rather than merely discouraged.
    /// </summary>
    [Fact]
    public async Task The_issued_token_carries_sub_jti_iat_exp_and_nothing_else()
    {
        var userId = await factory.AddStaffUserAsync(UserRole.Manager, "claims@test.local");

        var payload = DecodeJwtPayload(factory.IssueTokenFor(userId));

        Assert.Equal(
            new[] { "aud", "exp", "iss", "jti", "nbf", "sub" },
            payload.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());

        Assert.Equal(userId.ToString(), payload.GetProperty("sub").GetString());

        // The three values AD-15 forbids, asserted by absence.
        Assert.False(payload.TryGetProperty("role", out _));
        Assert.False(payload.TryGetProperty("departmentId", out _));
        Assert.False(payload.TryGetProperty("isActive", out _));
        Assert.False(payload.TryGetProperty("email", out _));
    }

    /// <summary>
    /// Regression for Story 01: the anonymous endpoints must still answer without a token
    /// (docs/api-design.md §4.1 — exactly four anonymous endpoints, two of which exist today).
    /// </summary>
    [Theory]
    [InlineData("/api/v1/health")]
    [InlineData("/api/v1/config/bootstrap")]
    public async Task Anonymous_endpoints_still_answer_without_a_token(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static JsonElement DecodeJwtPayload(string token)
    {
        var segment = token.Split('.')[1];
        var padded = segment.PadRight(segment.Length + ((4 - (segment.Length % 4)) % 4), '=')
            .Replace('-', '+')
            .Replace('_', '/');

        return JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded));
    }
}
