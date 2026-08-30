using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Tests.Api;

/// <summary>
/// <b>AP-10, at the HTTP boundary</b> — docs/api-design.md §3 and §7: a server-derived field is
/// "never accepted in a request body", and "a request containing one is <c>400</c>, so a client is
/// never misled into thinking it worked". AP-10's own row names "accepting and ignoring them" as
/// the <b>rejected</b> alternative.
///
/// <para>
/// <b>Why this suite exists.</b> Every request model already enforces AP-10 by <em>omission</em> —
/// there is no <c>externalReference</c> on <c>PatchCustomerRequest</c>, no <c>email</c> on
/// <c>PatchUserRequest</c>. Omission makes the field <em>unreachable</em>, which is the safety half
/// of the rule. It does not make the request <em>refused</em>: <c>System.Text.Json</c> skips a
/// member that maps to nothing, so until <c>UnmappedMemberHandling.Disallow</c> was configured
/// these requests returned <c>200</c> and dropped the field silently — finding <b>I-9</b>. These
/// tests are the contract half, and they fail if that one setting is ever removed.
/// </para>
///
/// <para>
/// <b>The scope of the rule is request bodies, and this suite pins both edges.</b> Every endpoint
/// that binds a JSON body is covered — <c>POST /auth/login</c>, <c>POST /customers</c>,
/// <c>PATCH /customers/{id}</c>, <c>POST /customers/{id}/notes</c>, <c>POST /users</c> and
/// <c>PATCH /users/{id}</c>, which is all six of them. Query strings are model-bound rather than
/// deserialized and are deliberately unchanged, which
/// <see cref="Query_string_binding_is_not_affected"/> asserts so the boundary is not mistaken for
/// an oversight.
/// </para>
/// </summary>
public sealed class UnmappedRequestMemberTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    private const string Customers = "/api/v1/customers";
    private const string Users = "/api/v1/users";
    private const string Login = "/api/v1/auth/login";
    private const string Password = "TestPassw0rd!";

    // ------------------------------------------------------- The two cases named in the finding

    /// <summary>
    /// <b>I-9's first named case.</b> <c>externalReference</c> is settable through no endpoint
    /// (DM-6, docs/api-design.md §8.3), and a request carrying it is now refused rather than
    /// accepted and ignored.
    /// </summary>
    [Fact]
    public async Task Patching_a_customer_with_externalReference_is_400()
    {
        var (client, branchId) = await AgentAsync("ap10.extref");
        var customerId = await CreateCustomerAsync(client, "ap10.extref@test.local", branchId);

        var response = await client.PatchAsJsonAsync(
            $"{Customers}/{customerId}", new { externalReference = "ERP-123" });

        await AssertUnmappedMemberRefusedAsync(response, "externalReference");

        Assert.Null(await factory.WithDbAsync(db => db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId).Select(c => c.ExternalReference).SingleAsync()));
    }

    /// <summary>
    /// <b>I-9's second named case, and the one that has behaved this way since Story 02.</b>
    /// <c>User.email</c> is unpatchable through <c>PATCH /users/{id}</c> (docs/api-design.md §5.3),
    /// which <c>PatchUserRequest</c> expresses by having no <c>email</c> property — the same place
    /// AP-10 puts every such restriction, and the reason A-19's propagation needed
    /// <c>User.ChangeEmail</c> instead (finding I-3).
    /// <para>
    /// The address is asserted unchanged <em>and</em> the account is signed into with the original
    /// one afterwards, so the test proves the login the field controls did not move either.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Patching_a_user_with_email_is_400()
    {
        const string original = "ap10.subject@test.local";

        var client = await AdministratorAsync("ap10.email");
        var subjectId = await factory.AddStaffUserAsync(UserRole.Agent, original);

        var response = await client.PatchAsJsonAsync(
            $"{Users}/{subjectId}", new { email = "hijacked@test.local", displayName = "Renamed" });

        await AssertUnmappedMemberRefusedAsync(response, "email");

        var row = await factory.WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Id == subjectId)
            .Select(u => new { u.Email, u.DisplayName })
            .SingleAsync());

        Assert.Equal(original, row.Email);

        // The legitimate displayName sent alongside was not applied either: the body was refused
        // whole, so there is no partial write.
        Assert.NotEqual("Renamed", row.DisplayName);

        // And the sign-in the address controls is where it was.
        var signIn = await factory.CreateClient()
            .PostAsJsonAsync(Login, new { email = original, password = Password });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // ------------------------------------------------------- Every other body-binding endpoint

    /// <summary>
    /// <c>POST /customers</c> takes exactly four fields, and the ones a client may not send are
    /// absent from the model rather than accepted and ignored (docs/api-design.md §5.5).
    /// </summary>
    [Theory]
    [InlineData("externalReference")]
    [InlineData("id")]
    [InlineData("createdAt")]
    public async Task Creating_a_customer_with_a_server_derived_field_is_400(string member)
    {
        var (client, branchId) = await AgentAsync($"ap10.create.{member}");
        var email = $"ap10.create.{member}@test.local";

        var body = new Dictionary<string, object?>
        {
            ["fullName"] = "Refused",
            ["email"] = email,
            ["branchId"] = branchId,
            [member] = member == "createdAt" ? "2020-01-01T00:00:00Z" : "supplied-by-client",
        };

        await AssertUnmappedMemberRefusedAsync(await client.PostAsJsonAsync(Customers, body), member);

        // Nothing was written — the refusal happens before the action runs.
        Assert.False(await factory.WithDbAsync(db => db.Customers.AnyAsync(c => c.Email == email)));
    }

    /// <summary>
    /// <c>POST /users</c> — <c>isActive</c>, <c>customerId</c> and <c>createdAt</c> are the three
    /// <c>CreateUserRequest</c> names as unsendable (docs/api-design.md §5.3).
    /// </summary>
    [Theory]
    [InlineData("isActive")]
    [InlineData("customerId")]
    [InlineData("createdAt")]
    public async Task Creating_a_user_with_a_server_derived_field_is_400(string member)
    {
        var client = await AdministratorAsync($"ap10.newuser.{member}");
        var departmentId = await factory.EnsureDepartmentAsync("AP-10 Department");
        var email = $"ap10.newuser.{member}@test.local";

        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = Password,
            ["displayName"] = "Refused",
            ["role"] = "Agent",
            ["departmentId"] = departmentId,
            [member] = member == "isActive" ? true : "supplied-by-client",
        };

        await AssertUnmappedMemberRefusedAsync(await client.PostAsJsonAsync(Users, body), member);

        Assert.False(await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
    }

    /// <summary>
    /// <c>POST /customers/{id}/notes</c> carries a body and nothing else: the author is the
    /// authenticated caller and the customer is the route parameter, both of which
    /// docs/api-design.md §7 lists as server-derived.
    /// </summary>
    [Theory]
    [InlineData("authorUserId")]
    [InlineData("customerId")]
    [InlineData("createdAt")]
    public async Task Adding_a_note_with_a_server_derived_field_is_400(string member)
    {
        var (client, branchId) = await AgentAsync($"ap10.note.{member}");
        var customerId = await CreateCustomerAsync(client, $"ap10.note.{member}@test.local", branchId);

        var body = new Dictionary<string, object?>
        {
            ["body"] = "Refused.",
            [member] = member == "createdAt" ? "2020-01-01T00:00:00Z" : Guid.NewGuid().ToString(),
        };

        await AssertUnmappedMemberRefusedAsync(
            await client.PostAsJsonAsync($"{Customers}/{customerId}/notes", body), member);

        Assert.False(await factory.WithDbAsync(db => db.CustomerNotes
            .AnyAsync(n => n.CustomerId == customerId)));
    }

    /// <summary>
    /// <b>The rule is not authorization-gated.</b> <c>POST /auth/login</c> is anonymous — one of the
    /// four anonymous endpoints (docs/api-design.md §4.1) — and refuses an unmapped member exactly
    /// like the authenticated ones, because the setting is on the JSON options rather than on any
    /// endpoint.
    /// </summary>
    [Fact]
    public async Task Signing_in_with_an_unmapped_member_is_400()
    {
        const string email = "ap10.login@test.local";
        await factory.AddStaffUserAsync(UserRole.Agent, email);

        var response = await factory.CreateClient().PostAsJsonAsync(
            Login, new { email, password = Password, rememberMe = true });

        // 400, not 401: the body never deserialized, so no credential was ever checked.
        await AssertUnmappedMemberRefusedAsync(response, "rememberMe");
    }

    // ------------------------------------------------------- The change breaks nothing legitimate

    /// <summary>
    /// <b>The regression guard for the whole change.</b> Every endpoint that binds a JSON body,
    /// called with exactly the fields its request model publishes, still succeeds. If
    /// <c>Disallow</c> ever caught a legitimate field, this fails first.
    /// </summary>
    [Fact]
    public async Task Every_body_binding_endpoint_still_accepts_its_documented_fields()
    {
        var administrator = await AdministratorAsync("ap10.happy");
        var (agent, branchId) = await AgentAsync("ap10.happy");
        var departmentId = await factory.EnsureDepartmentAsync("AP-10 Happy Department");

        // POST /users — all six documented fields, branchId included.
        var createdUser = await administrator.PostAsJsonAsync(Users, new
        {
            email = "ap10.happy.subject@test.local",
            password = Password,
            displayName = "Happy Subject",
            role = "Agent",
            departmentId,
            branchId,
        });

        Assert.Equal(HttpStatusCode.Created, createdUser.StatusCode);

        var subjectId = (await createdUser.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        // PATCH /users/{id} — all four patchable fields.
        var patchedUser = await administrator.PatchAsJsonAsync($"{Users}/{subjectId}", new
        {
            displayName = "Happy Renamed",
            role = "Manager",
            departmentId,
            branchId,
        });

        Assert.Equal(HttpStatusCode.OK, patchedUser.StatusCode);

        // POST /auth/login — both fields.
        var signIn = await factory.CreateClient().PostAsJsonAsync(
            Login, new { email = "ap10.happy.subject@test.local", password = Password });

        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // POST /customers — all four fields, the optional phone included.
        var createdCustomer = await agent.PostAsJsonAsync(Customers, new
        {
            fullName = "Happy Customer",
            email = "ap10.happy.customer@test.local",
            phone = "+1 555 0100",
            branchId,
        });

        Assert.Equal(HttpStatusCode.Created, createdCustomer.StatusCode);

        var customerId = (await createdCustomer.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        // PATCH /customers/{id} — all four patchable fields, email included (A-19).
        var patchedCustomer = await agent.PatchAsJsonAsync($"{Customers}/{customerId}", new
        {
            fullName = "Happy Renamed",
            email = "ap10.happy.moved@test.local",
            phone = "+1 555 0199",
            branchId,
        });

        Assert.Equal(HttpStatusCode.OK, patchedCustomer.StatusCode);

        // POST /customers/{id}/notes — its one field.
        var createdNote = await agent.PostAsJsonAsync(
            $"{Customers}/{customerId}/notes", new { body = "Still accepted." });

        Assert.Equal(HttpStatusCode.Created, createdNote.StatusCode);
    }

    /// <summary>
    /// <b>Disallow rejects extra members, not missing ones.</b> A <c>PATCH</c> carries only the
    /// fields being changed (docs/api-design.md §2), so an empty body is a valid no-op patch and
    /// must stay one.
    /// </summary>
    [Fact]
    public async Task An_empty_patch_body_is_still_accepted()
    {
        var (client, branchId) = await AgentAsync("ap10.empty");
        var customerId = await CreateCustomerAsync(client, "ap10.empty@test.local", branchId);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PatchAsJsonAsync($"{Customers}/{customerId}", new { })).StatusCode);

        var administrator = await AdministratorAsync("ap10.empty");
        var subjectId = await factory.AddStaffUserAsync(UserRole.Agent, "ap10.empty.subject@test.local");

        Assert.Equal(
            HttpStatusCode.OK,
            (await administrator.PatchAsJsonAsync($"{Users}/{subjectId}", new { })).StatusCode);
    }

    /// <summary>
    /// <b>Matching is still case-insensitive.</b> The wire contract is <c>camelCase</c>
    /// (docs/api-design.md §2), but <c>JsonSerializerDefaults.Web</c> binds case-insensitively and
    /// <c>Disallow</c> does not narrow that — a member is "unmapped" only when no property matches
    /// under the <em>existing</em> rules. A client sending <c>PascalCase</c> worked before the
    /// change and still works, so the change cannot be mistaken for a casing tightening.
    /// </summary>
    [Fact]
    public async Task Case_insensitive_property_matching_is_unchanged()
    {
        var (client, branchId) = await AgentAsync("ap10.casing");

        var response = await client.PostAsJsonAsync(Customers, new
        {
            FullName = "Pascal Cased",
            Email = "ap10.casing@test.local",
            BranchId = branchId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// <b>The boundary of the change, pinned deliberately.</b> <c>UnmappedMemberHandling</c> governs
    /// JSON <em>deserialization</em>. Query strings are model-bound instead, so an unknown query key
    /// is still ignored — and the AP-15 sort whitelist still raises its own <c>400</c> from the
    /// Application layer, where it always did. Neither behaviour moved, and neither should: AP-10 is
    /// about request <em>bodies</em>.
    /// </summary>
    [Fact]
    public async Task Query_string_binding_is_not_affected()
    {
        var (client, _) = await AgentAsync("ap10.query");

        // An unknown query key is not part of AP-10 and is still tolerated by model binding.
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"{Customers}?page=1&pageSize=25&invented=true")).StatusCode);

        // AP-15's whitelist still refuses an unlisted sort field, from its own code path.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync($"{Customers}?sort=externalReference")).StatusCode);
    }

    // ---------------------------------------------------------------- Harness

    /// <summary>
    /// A <c>400</c> that <b>names the refused member</b>. The key assertion is what makes each test
    /// about AP-10 rather than about some other validation failure that also happens to return
    /// <c>400</c>.
    /// <para>
    /// <b>The key is the field name, not the JSON path.</b> It was <c>$.email</c> until finding
    /// <b>I-10</b> was closed; <c>ModelStateProblemDetails</c> now normalizes every producer of a
    /// model-state <c>400</c> to the <c>camelCase</c> name the client actually sent, so a form can
    /// attach the message to its input. <c>ModelStateProblemDetailsTests</c> owns that rule; this
    /// helper simply depends on it.
    /// </para>
    /// </summary>
    private static async Task AssertUnmappedMemberRefusedAsync(
        HttpResponseMessage response, string member)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // RFC 9457 with the contract's stable slug, like every other error (AP-2, §6.12).
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("validation-failed", problem.GetProperty("type").GetString());

        var errors = problem.GetProperty("errors");

        Assert.True(
            errors.TryGetProperty(member, out _),
            $"expected the refusal to name {member}; got {errors.GetRawText()}");
    }

    private async Task<HttpClient> AdministratorAsync(string slug) =>
        factory.CreateClientFor(
            await factory.AddStaffUserAsync(UserRole.Administrator, $"{slug}.admin@test.local"));

    private async Task<(HttpClient Client, Guid BranchId)> AgentAsync(string slug)
    {
        var branchId = await factory.EnsureBranchAsync($"{slug} branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, $"{slug}.agent@test.local");

        return (factory.CreateClientFor(agentId), branchId);
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string email, Guid branchId)
    {
        var response = await client.PostAsJsonAsync(
            Customers, new { fullName = email.Split('@')[0], email, branchId });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
