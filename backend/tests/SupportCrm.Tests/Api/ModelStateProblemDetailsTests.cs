using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Tests.Api;

/// <summary>
/// <b>AP-2, for the <c>400</c> that <c>[ApiController]</c> produces</b> — docs/api-design.md §3
/// (AP-2) and §6.12: Problem Details for every error, "with a stable <c>type</c> slug per error
/// class", in the envelope <c>{ type, title, status, detail, instance, errors? }</c>.
///
/// <para>
/// <b>Why this suite exists.</b> The API had <em>two</em> shapes for one status code. Everything
/// raised as an <c>AppException</c> went through <c>ProblemDetailsExceptionHandler</c> and came out
/// conformant. A <b>model-state</b> failure never reaches that handler — <c>[ApiController]</c>
/// short-circuits it before the action runs — so it came out as the framework's default instead:
/// <c>type</c> the RFC URL rather than a slug, no <c>detail</c>, no <c>instance</c>, and
/// <c>errors</c> messages naming .NET internals. Finding <b>I-10</b>.
/// </para>
///
/// <para>
/// <b>The expected shape was not invented here.</b> The front end's own
/// <c>error.interceptor.spec.ts</c> already pins a validation <c>400</c> as
/// <c>{ type: 'validation-failed', errors: { email: [...] } }</c>, and both i18n dictionaries
/// already ship an <c>errors.validation-failed</c> string. These tests assert the server now
/// matches what the rest of the system was already built against.
/// </para>
/// </summary>
public sealed class ModelStateProblemDetailsTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    private const string Customers = "/api/v1/customers";
    private const string Users = "/api/v1/users";
    private const string Slug = "validation-failed";

    // ---------------------------------------------------------------- The envelope

    /// <summary>
    /// <b>The whole of §6.12, on one response.</b> Every member the envelope names is present and
    /// correct — not merely the <c>type</c>.
    /// </summary>
    [Fact]
    public async Task A_model_state_400_carries_the_whole_contract_envelope()
    {
        var client = await AdministratorAsync("envelope");

        var response = await client.PostAsJsonAsync(Users, new { email = "not-an-email", password = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(Slug, problem.GetProperty("type").GetString());
        Assert.Equal("Invalid request", problem.GetProperty("title").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());

        // detail is diagnostic, never rendered raw (T2-J) — but §6.12 names it, so it is present.
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));

        // instance is METHOD + path, exactly as ProblemDetailsExceptionHandler writes it.
        Assert.Equal($"POST {Users}", problem.GetProperty("instance").GetString());

        // The per-field dictionary the front end needs to render a 400 "inline on the offending
        // field" (docs/ui-design.md §9).
        var errors = problem.GetProperty("errors");
        Assert.Equal("The Email field is not a valid e-mail address.",
            errors.GetProperty("email")[0].GetString());
        Assert.True(errors.TryGetProperty("displayName", out _));
        Assert.True(errors.TryGetProperty("role", out _));
    }

    /// <summary>
    /// <b>The slug is stable across every producer of a model-state <c>400</c>.</b> Five distinct
    /// code paths inside ASP.NET Core reach this response — data annotations, the JSON reader in
    /// three different failure modes, and query-string model binding — and a client must not have to
    /// know which one refused it.
    /// </summary>
    [Theory]
    [MemberData(nameof(ModelStateProducers))]
    public async Task Every_model_state_producer_uses_the_validation_failed_slug(string label)
    {
        var response = await InvokeAsync(label);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(Slug, problem.GetProperty("type").GetString());
        Assert.Equal("Invalid request", problem.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("instance").GetString()));
    }

    // ---------------------------------------------------------------- The keys

    /// <summary>
    /// <b>Keys are <c>camelCase</c> field names, whichever producer refused the request.</b> Data
    /// annotations and query binding key by property name; the JSON reader keys by path
    /// (<c>$.email</c>). Both are normalized to the name the client actually sent, so a form can
    /// attach the message to its input without knowing the difference.
    /// </summary>
    [Theory]
    [InlineData("unmapped-member", "email")]
    [InlineData("type-mismatch", "branchId")]
    [InlineData("malformed-json", "fullName")]
    [InlineData("query-binding", "branchId")]
    public async Task Error_keys_are_camel_case_field_names(string label, string field)
    {
        var problem = await ProblemAsync(label);
        var errors = problem.GetProperty("errors");

        Assert.True(
            errors.TryGetProperty(field, out _),
            $"expected the error to be keyed '{field}'; got {errors.GetRawText()}");

        // The JSON path form is gone, not merely duplicated.
        Assert.False(errors.TryGetProperty($"$.{field}", out _));
    }

    /// <summary>
    /// <b>The action's own parameter name never reaches a client.</b> When a body fails to
    /// deserialize the parameter binds to null and MVC adds a second error keyed <c>request</c> —
    /// a C# identifier the client never sent, restating a failure already reported against the
    /// field. It is removed.
    /// </summary>
    [Theory]
    [InlineData("unmapped-member")]
    [InlineData("type-mismatch")]
    [InlineData("malformed-json")]
    [InlineData("empty-body")]
    public async Task The_action_parameter_name_is_not_reported_as_a_field(string label)
    {
        var problem = await ProblemAsync(label);
        var errors = problem.GetProperty("errors");

        Assert.False(
            errors.TryGetProperty("request", out _),
            $"the action's parameter name leaked as a field: {errors.GetRawText()}");

        // Something is still reported — the entry was dropped, not the whole diagnosis.
        Assert.NotEmpty(errors.EnumerateObject());
    }

    // ---------------------------------------------------------------- The leak

    /// <summary>
    /// <b>Nothing internal reaches the client, on any producer.</b> Before the fix, a JSON reader
    /// failure returned the request model's fully qualified type name
    /// (<c>SupportCrm.Application.Modules.Identity.PatchUserRequest</c>), generic type notation
    /// (<c>System.Nullable`1[System.Guid]</c>) and byte offsets into the payload.
    /// <para>
    /// The assertion is over the <b>raw response text</b>, not a parsed field, so a leak anywhere in
    /// the envelope fails it — including in a member this suite does not otherwise read.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ModelStateProducers))]
    public async Task No_internal_detail_reaches_the_client(string label)
    {
        var body = await (await InvokeAsync(label)).Content.ReadAsStringAsync();

        foreach (var forbidden in new[]
                 {
                     "SupportCrm.",              // any application type name
                     "System.",                  // any framework type name
                     "`",                        // generic arity, as in Nullable`1
                     "LineNumber",               // System.Text.Json parser internals
                     "BytePositionInLine",       // ... and its byte offsets
                     "tools.ietf.org",           // the framework's RFC-URL type
                 })
        {
            Assert.False(
                body.Contains(forbidden, StringComparison.Ordinal),
                $"'{forbidden}' leaked into the {label} response: {body}");
        }
    }

    // ---------------------------------------------------------------- The two paths agree

    /// <summary>
    /// <b>The point of the whole fix.</b> A <c>400</c> raised as a <c>ValidationException</c> and a
    /// <c>400</c> produced by model-state validation are the same error class, and now say so: the
    /// same slug, the same title, the same envelope. A client writes one branch, not two.
    /// <para>
    /// <c>errors</c> is deliberately <b>not</b> compared — §6.12 marks it optional, and the
    /// exception path has no per-field dictionary to offer.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_exception_path_and_the_model_state_path_are_the_same_error_class()
    {
        var modelState = await ProblemAsync("data-annotations");
        var exception = await ProblemAsync("validation-exception");

        Assert.Equal(Slug, modelState.GetProperty("type").GetString());
        Assert.Equal(Slug, exception.GetProperty("type").GetString());

        Assert.Equal(
            modelState.GetProperty("title").GetString(),
            exception.GetProperty("title").GetString());

        Assert.Equal(
            modelState.GetProperty("status").GetInt32(),
            exception.GetProperty("status").GetInt32());

        // Both name the request they refused, in the same form.
        foreach (var problem in new[] { modelState, exception })
        {
            Assert.Matches(@"^[A-Z]+ /api/v1/", problem.GetProperty("instance").GetString()!);
        }
    }

    /// <summary>
    /// <b>The slug resolves to a real translation.</b> AP-2 exists so the front end can localize by
    /// <c>type</c> (T2-J); a slug with no dictionary entry renders as the raw key, which is the
    /// user-visible half of I-10. Both shipped dictionaries carry this key, and this test fails if
    /// the server ever emits one they do not.
    /// </summary>
    [Fact]
    public async Task The_slug_matches_a_key_the_shipped_dictionaries_carry()
    {
        var problem = await ProblemAsync("data-annotations");
        var slug = problem.GetProperty("type").GetString();

        foreach (var language in new[] { "en", "ar" })
        {
            var dictionary = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(FrontendI18nDirectory, $"{language}.json")));

            Assert.True(
                dictionary.RootElement.GetProperty("errors").TryGetProperty(slug!, out var text),
                $"the {language} dictionary has no errors.{slug} string");

            Assert.False(string.IsNullOrWhiteSpace(text.GetString()));
        }
    }

    // ---------------------------------------------------------------- Nothing else moved

    /// <summary>
    /// <b>A valid request is untouched.</b> The fix shapes a failure; it must not shape a success,
    /// and no status code moves.
    /// </summary>
    [Fact]
    public async Task A_valid_request_is_unaffected()
    {
        var branchId = await factory.EnsureBranchAsync("Envelope Branch");
        var client = await AgentAsync("valid");

        var response = await client.PostAsJsonAsync(
            Customers, new { fullName = "Still Fine", email = "envelope.ok@test.local", branchId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// <b>Other error classes keep their own slugs.</b> The fix normalizes the <c>400</c> that had
    /// none; it must not flatten the ones §6.12 names individually.
    /// </summary>
    [Fact]
    public async Task Other_problem_slugs_are_untouched()
    {
        var branchId = await factory.EnsureBranchAsync("Slug Branch");
        var client = await AgentAsync("slugs");

        var first = await client.PostAsJsonAsync(
            Customers, new { fullName = "Dup", email = "slug.dup@test.local", branchId });
        first.EnsureSuccessStatusCode();

        var duplicate = await client.PostAsJsonAsync(
            Customers, new { fullName = "Dup", email = "slug.dup@test.local", branchId });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            "customer-email-in-use",
            (await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());

        var missing = await client.GetAsync($"{Customers}/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(
            "not-found",
            (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString());
    }

    // ---------------------------------------------------------------- Harness

    /// <summary>
    /// Every distinct ASP.NET Core code path that produces a model-state <c>400</c>, plus the
    /// exception path it now agrees with.
    /// </summary>
    public static TheoryData<string> ModelStateProducers =>
    [
        "data-annotations",
        "unmapped-member",
        "type-mismatch",
        "malformed-json",
        "root-garbage",
        "empty-body",
        "query-binding",
    ];

    private static readonly string FrontendI18nDirectory = FindFrontendI18n();

    /// <summary>
    /// Walks up from the test binary to the repository root. The dictionaries are the front end's,
    /// deliberately: the point of that test is that the two halves agree, so reading a copy would
    /// prove nothing.
    /// </summary>
    private static string FindFrontendI18n()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "frontend", "src", "assets", "i18n");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("frontend/src/assets/i18n was not found above the test binary.");
    }

    private async Task<JsonElement> ProblemAsync(string label) =>
        await (await InvokeAsync(label)).Content.ReadFromJsonAsync<JsonElement>();

    private async Task<HttpResponseMessage> InvokeAsync(string label)
    {
        var slug = $"mspd.{label}";

        switch (label)
        {
            case "data-annotations":
            {
                var client = await AdministratorAsync(slug);

                // No displayName, no role, and an unparseable email.
                return await client.PostAsJsonAsync(Users, new { email = "not-an-email", password = "x" });
            }

            case "unmapped-member":
            {
                var client = await AdministratorAsync(slug);
                var subjectId = await factory.AddStaffUserAsync(
                    UserRole.Agent, $"{slug}.subject.{Unique()}@test.local");

                // email is not on PatchUserRequest — the AP-10 refusal (finding I-9).
                return await client.PatchAsJsonAsync($"{Users}/{subjectId}", new { email = "x@y.local" });
            }

            case "type-mismatch":
            {
                var client = await AgentAsync(slug);

                return await client.PostAsJsonAsync(
                    Customers,
                    new { fullName = "X", email = $"{slug}.{Unique()}@test.local", branchId = "not-a-guid" });
            }

            case "malformed-json":
            {
                var client = await AgentAsync(slug);

                return await client.PostAsync(Customers, Json("{\"fullName\":"));
            }

            case "root-garbage":
            {
                var client = await AgentAsync(slug);

                return await client.PostAsync(Customers, Json("not json at all"));
            }

            case "empty-body":
            {
                var client = await AgentAsync(slug);

                return await client.PostAsync(Customers, Json(string.Empty));
            }

            case "query-binding":
            {
                var client = await AgentAsync(slug);

                return await client.GetAsync($"{Customers}?branchId=not-a-guid");
            }

            case "validation-exception":
            {
                var client = await AgentAsync(slug);

                // AP-15's sort whitelist, raised as a ValidationException from the Application layer.
                return await client.GetAsync($"{Customers}?sort=email");
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(label), label, "Unknown producer.");
        }
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    /// <summary>
    /// A caller of its own, every time. Several tests drive the same producer through
    /// <see cref="InvokeAsync"/>, and user email is unique across the table (A-10), so the slug
    /// alone would collide on the second call. The slug stays in the address so a failure still
    /// names the producer that caused it.
    /// </summary>
    private async Task<HttpClient> AdministratorAsync(string slug) =>
        factory.CreateClientFor(await factory.AddStaffUserAsync(
            UserRole.Administrator, $"{slug}.admin.{Unique()}@test.local"));

    /// <inheritdoc cref="AdministratorAsync"/>
    private async Task<HttpClient> AgentAsync(string slug) =>
        factory.CreateClientFor(await factory.AddStaffUserAsync(
            UserRole.Agent, $"{slug}.agent.{Unique()}@test.local"));

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];
}
