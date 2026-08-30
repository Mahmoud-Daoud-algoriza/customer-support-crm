using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Identity;

/// <summary>
/// Story 04 <b>slice 5</b> — plan task 7 and its route: <c>POST /auth/register</c>, deferred here
/// from Story 02 because it is the first point at which a <c>Customer</c>, a <c>Branch</c> and the
/// configured default branch all exist (A-15).
///
/// <para>
/// <b>The suite is organized around A-15's three outcomes</b> (docs/api-design.md §5.2), because
/// those three are the whole of the endpoint's behaviour and the plan says "and only these three".
/// Each is asserted at the row level, not merely by status code: a `201` that quietly wrote a second
/// customer would pass a status assertion and break A-10.
/// </para>
///
/// <para>
/// <b>What SQLite cannot verify here:</b> that <c>a@b.local</c> and <c>A@B.LOCAL</c> are the same
/// address. That is the SQL Server collation of docs/data-model.md §6.1, applied under a provider
/// guard, so the case-insensitive linking path is verified against real SQL Server in the slice's
/// live verification rather than in this suite — the same division
/// <see cref="SupportCrmApiFactory"/> already documents.
/// </para>
/// </summary>
public sealed class RegistrationTests(SupportCrmApiFactory factory) : IClassFixture<SupportCrmApiFactory>
{
    private const string Register = "/api/v1/auth/register";
    private const string Login = "/api/v1/auth/login";
    private const string Me = "/api/v1/auth/me";
    private const string Password = "PortalPassw0rd!";

    // ------------------------------------------------------------------ Outcome 1 of 3

    /// <summary>
    /// <b>A-15, row one:</b> no profile and no login for the email — both are created, and the
    /// customer lands in the <b>configured default branch</b> without ever being asked.
    /// <para>
    /// The DM-1 shape of the new login is asserted field by field, including the two that must be
    /// null. <c>User.branchId</c> is one of them — see
    /// <see cref="The_login_gets_no_branch_of_its_own"/> for why that is the contract rather than an
    /// omission.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_new_email_creates_the_customer_and_its_login()
    {
        const string email = "register.new@test.local";

        var response = await (await AnonymousAsync()).PostAsJsonAsync(Register, new
        {
            email,
            password = Password,
            fullName = "Nadia Faris",
            phone = "+20 100 555 0199",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await factory.WithDbAsync(db => db.Customers.AsNoTracking()
            .SingleAsync(c => c.Email == email));

        Assert.Equal("Nadia Faris", customer.FullName);
        Assert.Equal("+20 100 555 0199", customer.Phone);
        Assert.Equal(await ConfiguredDefaultBranchAsync(), customer.BranchId);

        // externalReference is the ERP seam field and no endpoint sets it (DM-6) — registration
        // included.
        Assert.Null(customer.ExternalReference);

        var user = await factory.WithDbAsync(db => db.Users.AsNoTracking()
            .SingleAsync(u => u.Email == email));

        Assert.Equal(UserRole.Customer, user.Role);
        Assert.Equal(customer.Id, user.CustomerId);
        Assert.Null(user.DepartmentId);
        Assert.True(user.IsActive);
        Assert.Equal("Nadia Faris", user.DisplayName);
    }

    /// <summary>
    /// <b>The resolution of finding I-5, asserted rather than assumed.</b> Slice 3 left open whether
    /// A-15's "configured default branch" also sets <c>User.branchId</c>. It does not: A-15 and
    /// docs/data-model.md §2.4 both attach the default to <c>Customer.branchId</c>, §2.1 calls
    /// <c>User.branchId</c> a "staff location, reporting attribute only", and
    /// docs/architecture.md §6.3 says the value is "assigned to self-registering <em>customers</em>".
    /// <para>
    /// The two are asserted <b>together</b>, so a future change that pushed the branch onto the login
    /// fails here with the reason attached.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_login_gets_no_branch_of_its_own()
    {
        const string email = "register.branch@test.local";

        await RegisterAsync(email, "Branchless Login");

        // Joined explicitly: the Domain carries no navigation properties (AD-4), so the link is
        // the CustomerId column and the query says so.
        var row = await factory.WithDbAsync(db =>
            (from u in db.Users.AsNoTracking()
             join c in db.Customers.AsNoTracking() on u.CustomerId equals c.Id
             where u.Email == email
             select new { LoginBranchId = u.BranchId, CustomerBranchId = c.BranchId })
            .SingleAsync());

        Assert.Null(row.LoginBranchId);
        Assert.Equal(await ConfiguredDefaultBranchAsync(), row.CustomerBranchId);
    }

    /// <summary>
    /// <b>The branch comes from configuration, not from a constant.</b> Asserted by pointing the
    /// configured value at a branch this test creates, on a host of its own — if the id were
    /// hardcoded or taken from the first branch found, the customer would land somewhere else.
    /// </summary>
    [Fact]
    public async Task The_default_branch_is_read_from_configuration()
    {
        var branchId = new Guid("0d1e2f3a-4b5c-6d7e-8f90-a1b2c3d4e5f6");

        using var configured = new SupportCrmApiFactory
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["SupportCrm:Registration:DefaultBranchId"] = branchId.ToString(),
            },
        };

        // The row has to exist before the foreign key is written. The startup check that would
        // normally prove it (ConfigurationValidator check 3) lives in DatabaseInitializer, which this
        // factory removes along with the seeders.
        await configured.WithDbAsync(async db =>
        {
            db.Branches.Add(Branch.Create(branchId, "Configured Default Branch"));

            return await db.SaveChangesAsync();
        });

        const string email = "register.configured@test.local";

        var response = await configured.CreateClient().PostAsJsonAsync(
            Register, new { email, password = Password, fullName = "Configured Branch" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal(branchId, await configured.WithDbAsync(db => db.Customers.AsNoTracking()
            .Where(c => c.Email == email).Select(c => c.BranchId).SingleAsync()));
    }

    // ------------------------------------------------------------------ Outcome 2 of 3

    /// <summary>
    /// <b>A-15, row two — the row that keeps A-10 true.</b> An agent created the profile earlier;
    /// registering with that email creates the login and <b>links it to the existing profile</b>.
    /// <para>
    /// The assertion that matters is the <b>count</b>: exactly one customer row for the address,
    /// with the id the agent created. A second profile would satisfy every status assertion and
    /// break "one customer per email address" silently.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_agent_created_profile_is_linked_not_duplicated()
    {
        const string email = "register.existing@test.local";

        var agent = await AgentClientAsync("register.existing");
        var branchId = await factory.EnsureBranchAsync("Register Existing Branch");

        var created = await agent.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Already Known",
            email,
            phone = "+1 555 0123",
            branchId,
        });

        created.EnsureSuccessStatusCode();

        var existingId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email, password = Password, fullName = "Already Known" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // One profile, still the agent's, still in the agent's branch — the default branch does NOT
        // overwrite a branch someone chose.
        var customers = await factory.WithDbAsync(db => db.Customers.AsNoTracking()
            .Where(c => c.Email == email).ToListAsync());

        Assert.Single(customers);
        Assert.Equal(existingId, customers[0].Id);
        Assert.Equal(branchId, customers[0].BranchId);
        Assert.Equal("+1 555 0123", customers[0].Phone);

        // And the new login points at it.
        Assert.Equal(existingId, await factory.WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Email == email).Select(u => u.CustomerId).SingleAsync()));
    }

    // ------------------------------------------------------------------ Outcome 3 of 3

    /// <summary>
    /// <b>A-15, row three — PF-6.</b> A login already exists for the email, so registration is a
    /// <c>409 user-already-exists</c>: the same slug <c>POST /users</c> raises for the same
    /// collision, because it is the same fact.
    /// <para>
    /// Nothing is written. Asserted by counting rows before and after, so a refusal that had already
    /// inserted a customer would fail here.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_second_registration_for_the_same_email_is_409()
    {
        const string email = "register.duplicate@test.local";

        await RegisterAsync(email, "First Time");

        var customersBefore = await factory.WithDbAsync(db => db.Customers.CountAsync());
        var usersBefore = await factory.WithDbAsync(db => db.Users.CountAsync());

        var second = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email, password = Password, fullName = "Second Time" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("user-already-exists", await ProblemTypeAsync(second));

        Assert.Equal(customersBefore, await factory.WithDbAsync(db => db.Customers.CountAsync()));
        Assert.Equal(usersBefore, await factory.WithDbAsync(db => db.Users.CountAsync()));
    }

    /// <summary>
    /// <b>A staff address is not a registration route.</b> The email belongs to an <c>Agent</c>, so
    /// the same <c>409</c> applies — the check is on <c>User.email</c>, which is unique across every
    /// role, and it must not be possible to attach a portal profile to a staff login.
    /// </summary>
    [Fact]
    public async Task Registering_a_staff_address_is_409_and_creates_no_customer()
    {
        const string email = "register.staff@test.local";

        await factory.AddStaffUserAsync(UserRole.Agent, email);

        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email, password = Password, fullName = "Impersonator" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user-already-exists", await ProblemTypeAsync(response));

        Assert.False(await factory.WithDbAsync(db => db.Customers.AnyAsync(c => c.Email == email)));
    }

    // ------------------------------------------------------------------ The contract around them

    /// <summary>
    /// <b>The request specifies no branch, no role and no customer id</b> — docs/api-design.md §5.2
    /// states it, and <c>RegisterRequest</c> enforces it by having no property for any of them
    /// (AP-10). Each is a <c>400</c>, never accepted-and-ignored.
    /// <para>
    /// <c>isActive</c> is included because it is the fourth thing a client might reasonably try, and
    /// docs/api-design.md §7 makes it server-derived too.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("branchId")]
    [InlineData("role")]
    [InlineData("customerId")]
    [InlineData("isActive")]
    public async Task The_request_cannot_specify_a_server_derived_field(string member)
    {
        var email = $"register.derived.{member}@test.local";

        var body = new Dictionary<string, object?>
        {
            ["email"] = email,
            ["password"] = Password,
            ["fullName"] = "Refused",
            [member] = member == "isActive" ? true : Guid.NewGuid().ToString(),
        };

        var response = await (await AnonymousAsync()).PostAsJsonAsync(Register, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await ProblemTypeAsync(response));

        // The refusal is whole: no profile, no login.
        Assert.False(await factory.WithDbAsync(db => db.Customers.AnyAsync(c => c.Email == email)));
        Assert.False(await factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == email)));
    }

    /// <summary>
    /// <b>The endpoint is anonymous</b> — one of the four of docs/api-design.md §4.1. Registration
    /// with no <c>Authorization</c> header at all must succeed, which every other test here relies
    /// on implicitly and this one states.
    /// </summary>
    [Fact]
    public async Task Registration_needs_no_token()
    {
        var client = await AnonymousAsync();

        Assert.Null(client.DefaultRequestHeaders.Authorization);

        var response = await client.PostAsJsonAsync(
            Register, new { email = "register.anon@test.local", password = Password, fullName = "Anon" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// <b>The response is an <c>AuthToken</c>, and the token works</b> (docs/api-design.md §6.1) —
    /// so a new customer is signed in rather than sent to the sign-in form. The embedded identity is
    /// the <c>Customer</c> shape of DM-1, and <c>GET /auth/me</c> agrees with it.
    /// </summary>
    [Fact]
    public async Task The_response_is_a_working_token_for_the_new_customer()
    {
        const string email = "register.token@test.local";

        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email, password = Password, fullName = "Tokened Customer" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var accessToken = body.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        var identity = body.GetProperty("user");
        Assert.Equal("Customer", identity.GetProperty("role").GetString());
        Assert.Equal(email, identity.GetProperty("email").GetString());
        Assert.True(identity.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(Guid.Empty, identity.GetProperty("customerId").GetGuid());

        // departmentId is omitted rather than null for a Customer (docs/api-design.md §2, DM-1).
        Assert.False(identity.TryGetProperty("departmentId", out _));

        // passwordHash reaches no response path, ever (docs/api-design.md §6).
        Assert.DoesNotContain("passwordHash", (await response.Content.ReadAsStringAsync()));

        // The token is real: it resolves through the same per-request path every other call uses.
        var authenticated = factory.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);

        var me = await authenticated.GetAsync(Me);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var resolved = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Customer", resolved.GetProperty("role").GetString());
        Assert.Equal(
            identity.GetProperty("customerId").GetGuid(),
            resolved.GetProperty("customerId").GetGuid());
    }

    /// <summary>
    /// <b><c>201</c> carries a <c>Location</c>, and it resolves for the caller who received it</b>
    /// (docs/api-design.md §2.2). It addresses <c>GET /auth/me</c> because that is the only route a
    /// <c>Customer</c>-role token may read the created identity through — recorded as a judgment
    /// call, on the same reasoning as the notes endpoint's collection <c>Location</c>.
    /// </summary>
    [Fact]
    public async Task The_created_response_points_at_a_location_the_new_customer_can_read()
    {
        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register,
            new { email = "register.location@test.local", password = Password, fullName = "Located" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.EndsWith(Me, location!.ToString());

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Not merely well-formed — followable by the recipient.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(location)).StatusCode);
    }

    /// <summary>
    /// <b>The password chosen at registration is the one that signs in.</b> Proves the hash was
    /// applied to the row rather than the sentinel being persisted.
    /// </summary>
    [Fact]
    public async Task The_registered_password_signs_in()
    {
        const string email = "register.signin@test.local";

        await RegisterAsync(email, "Signs In");

        var correct = await (await AnonymousAsync()).PostAsJsonAsync(Login, new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);

        var wrong = await (await AnonymousAsync()).PostAsJsonAsync(Login, new { email, password = "WrongPassw0rd!" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    /// <summary>
    /// <b>Registering grants the portal, not the workspace.</b> A-4's hierarchy puts
    /// <c>Customer</c> below every staff role, so the new token is refused by the customer directory
    /// exactly as any other portal login is. Without this, a mistake in the role assignment would be
    /// invisible until a customer browsed the CRM.
    /// </summary>
    [Fact]
    public async Task A_registered_customer_cannot_reach_the_staff_endpoints()
    {
        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register,
            new { email = "register.gate@test.local", password = Password, fullName = "Portal Only" });

        var token = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users")).StatusCode);
    }

    /// <summary>
    /// <b>One <c>UserCreated</c> entry, attributed to the person who registered.</b>
    /// <para>
    /// <c>actorUserId</c> is the new user for the reason a successful sign-in sets it: the endpoint
    /// is anonymous, so no request identity exists, while the actor is in fact known. Leaving it
    /// null would record "no user could be resolved", which docs/data-model.md §2.14 reserves for a
    /// failed sign-in.
    /// </para>
    /// <para>
    /// <b>The customer profile is not audited</b> — creating one is business data, not a security
    /// event (AD-10), which is why the count is exactly one and not two.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Registration_writes_exactly_one_audit_entry_for_the_new_login()
    {
        const string email = "register.audit@test.local";

        await RegisterAsync(email, "Audited");

        var userId = await factory.WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        var entries = await factory.WithDbAsync(db => db.AuditEntries.AsNoTracking()
            .Where(e => e.TargetId == userId)
            .ToListAsync());

        var entry = Assert.Single(entries);

        Assert.Equal(AuditAction.UserCreated, entry.Action);
        Assert.Equal(AuditOutcome.Success, entry.Outcome);
        Assert.Equal(AuditTargetType.User, entry.TargetType);
        Assert.Equal(userId, entry.ActorUserId);

        // The address is not copied into the append-only log (docs/data-model.md §2.14).
        Assert.Null(entry.ActorDescriptor);
    }

    /// <summary>
    /// <b>A refused registration writes nothing at all</b> — audit entry included. Only a failed
    /// <em>sign-in</em> is recorded as a <c>Failure</c> (docs/data-model.md §2.14); every other
    /// rejection is silent, the convention every user-administration call site already follows.
    /// </summary>
    [Fact]
    public async Task A_refused_registration_writes_no_audit_entry()
    {
        const string email = "register.noaudit@test.local";

        await RegisterAsync(email, "Only Once");

        var before = await factory.WithDbAsync(db => db.AuditEntries.CountAsync());

        var refused = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email, password = Password, fullName = "Again" });

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(before, await factory.WithDbAsync(db => db.AuditEntries.CountAsync()));
    }

    /// <summary>
    /// <b>A profile that already has a login is refused, not given a second one.</b>
    /// docs/data-model.md §5 constraint 3 allows at most one login per profile.
    /// <para>
    /// The state is built directly, because <b>no approved flow produces it</b> — A-19 keeps a
    /// profile's email and its login's in step, so a profile with a login always collides on the
    /// email check first. The guard exists so that if the invariant is ever broken elsewhere, this
    /// endpoint answers <c>409</c> rather than a unique-index violation surfacing as a <c>500</c>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_profile_that_already_has_a_login_is_refused()
    {
        const string profileEmail = "register.relinked@test.local";

        var branchId = await factory.EnsureBranchAsync("Relink Branch");

        // A profile, and a login pointing at it under a DIFFERENT address — the divergence A-19
        // prevents, constructed here on purpose.
        var customerId = await factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), "Relinked", profileEmail, null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        await factory.WithDbAsync(async db =>
        {
            var login = User.CreateCustomerUser(
                Guid.NewGuid(), "register.relinked.other@test.local", "!unhashed",
                "Relinked", customerId, DateTimeOffset.UtcNow);

            db.Users.Add(login);

            return await db.SaveChangesAsync();
        });

        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email = profileEmail, password = Password, fullName = "Relinked" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("user-already-exists", await ProblemTypeAsync(response));

        // Still one login for that profile.
        Assert.Equal(1, await factory.WithDbAsync(db => db.Users.CountAsync(u => u.CustomerId == customerId)));
    }

    /// <summary>
    /// <b>Validation is the server's, not the form's</b> (T1-D). The three required fields and the
    /// address format are checked here, through the endpoint.
    /// </summary>
    [Theory]
    [InlineData("{\"password\":\"PortalPassw0rd!\",\"fullName\":\"No Email\"}")]
    [InlineData("{\"email\":\"not-an-address\",\"password\":\"PortalPassw0rd!\",\"fullName\":\"Bad Email\"}")]
    [InlineData("{\"email\":\"register.nopassword@test.local\",\"fullName\":\"No Password\"}")]
    [InlineData("{\"email\":\"register.noname@test.local\",\"password\":\"PortalPassw0rd!\"}")]
    public async Task An_incomplete_registration_is_400(string body)
    {
        var response = await (await AnonymousAsync()).PostAsync(
            Register, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation-failed", await ProblemTypeAsync(response));
    }

    // ---------------------------------------------------------------- Harness

    /// <summary>
    /// An unauthenticated client, on a host where the configured default branch <b>exists as a
    /// row</b>.
    ///
    /// <para>
    /// <b>Why the row has to be arranged here.</b> <c>Customer.branchId</c> is a required foreign
    /// key, so registration cannot write a profile unless the configured branch is really there. In
    /// production two things guarantee it and neither runs in this host: <c>OrganizationSeeder</c>
    /// creates the branch, and <c>ConfigurationValidator</c> check 3 refuses to start the
    /// application if <c>DefaultBranchId</c> names a branch that does not exist (A-15). The test
    /// factory removes the seeders and the initializer that runs that check, by design — so the
    /// precondition those two provide is established here instead.
    /// </para>
    ///
    /// <para>
    /// <b>This is a precondition, not the behaviour under test.</b> That the configured value is
    /// what registration actually reads is proven separately, on a host of its own, by
    /// <see cref="The_default_branch_is_read_from_configuration"/>.
    /// </para>
    /// </summary>
    private async Task<HttpClient> AnonymousAsync()
    {
        var branchId = await ConfiguredDefaultBranchAsync();

        await factory.WithDbAsync(async db =>
        {
            if (!await db.Branches.AnyAsync(b => b.Id == branchId))
            {
                db.Branches.Add(Branch.Create(branchId, "Configured Default Branch"));
                await db.SaveChangesAsync();
            }

            return 0;
        });

        return factory.CreateClient();
    }

    private Task<Guid> ConfiguredDefaultBranchAsync()
    {
        using var scope = factory.Services.CreateScope();

        return Task.FromResult(scope.ServiceProvider
            .GetRequiredService<IOptions<RegistrationOptions>>().Value.DefaultBranchId);
    }

    private async Task RegisterAsync(string email, string fullName)
    {
        var response = await (await AnonymousAsync()).PostAsJsonAsync(
            Register, new { email, password = Password, fullName });

        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpClient> AgentClientAsync(string slug) =>
        factory.CreateClientFor(
            await factory.AddStaffUserAsync(UserRole.Agent, $"{slug}.agent@test.local"));

    private static async Task<string> ProblemTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString()!;
}
