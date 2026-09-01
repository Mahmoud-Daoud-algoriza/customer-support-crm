using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using SupportCrm.Api.Auth;
using SupportCrm.Api.Errors;
using SupportCrm.Api.Routing;
using SupportCrm.Api.Serialization;
using SupportCrm.Application.Configuration;
using SupportCrm.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------------
// Configuration — appsettings.json -> appsettings.{Environment}.json -> environment variables,
// bound to strongly typed options and validated at startup so invalid configuration fails fast
// (docs/architecture.md §6.3). No secret and no branding value is hardcoded.
// ---------------------------------------------------------------------------------------------
builder.Services.AddOptions<BrandingOptions>()
    .Bind(builder.Configuration.GetSection(BrandingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<LocalizationOptions>()
    .Bind(builder.Configuration.GetSection(LocalizationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<LocalizationOptions>, LocalizationOptionsValidator>();

// The signing key comes from the environment only; appsettings.json carries no value for it, so a
// missing key fails at startup rather than falling back to something guessable
// (docs/architecture.md §4.1, §6.3).
builder.Services.AddOptions<SeedOptions>()
    .Bind(builder.Configuration.GetSection(SeedOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// The remaining configuration keys of architecture §6.3 — Story 16 Part A.
//
// Every one is bound, data-annotation validated and ValidateOnStart, exactly like the three
// above: invalid configuration must fail fast at startup with a clear message rather than
// degrade silently at runtime (architecture §6.3, the audit-configuration intake).
//
// The rules data annotations cannot express are IValidateOptions implementations in
// ConfigurationValidator.cs. The two that read ROWS — every category maps to an existing
// department (A-14), and DefaultBranchId is an existing branch (A-15) — cannot run here at all:
// they run in DatabaseInitializer, after migrations and seeding have created those rows.
builder.Services.AddOptions<CategoryOptions>()
    .Bind(builder.Configuration.GetSection(CategoryOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<CategoryOptions>, CategoryOptionsValidator>();

builder.Services.AddOptions<PriorityOptions>()
    .Bind(builder.Configuration.GetSection(PriorityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<PriorityOptions>, PriorityOptionsValidator>();

builder.Services.AddOptions<SlaTargetOptions>()
    .Bind(builder.Configuration.GetSection(SlaTargetOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<SlaTargetOptions>, SlaTargetOptionsValidator>();

// Story 10 — the AI seam. **The default is the fake**, so this binds cleanly with no configuration
// at all (A-7, product-scope §10 item 5); the validator only speaks when a provider is selected.
builder.Services.AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

// Story 09 — the sweep interval (AD-6). Same section as the targets: one operator concern.
builder.Services.AddOptions<SlaMonitorOptions>()
    .Bind(builder.Configuration.GetSection(SlaMonitorOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<QuickReplyOptions>()
    .Bind(builder.Configuration.GetSection(QuickReplyOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<QuickReplyOptions>, QuickReplyOptionsValidator>();

builder.Services.AddOptions<RegistrationOptions>()
    .Bind(builder.Configuration.GetSection(RegistrationOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AttachmentOptions>()
    .Bind(builder.Configuration.GetSection(AttachmentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AttachmentOptions>, AttachmentOptionsValidator>();

// The KEY is approved (architecture §6.3); the VALUES are not — OQ-1 is open. The validator
// checks Min < Max and nothing more, so it cannot accidentally answer the question.
builder.Services.AddOptions<FeedbackOptions>()
    .Bind(builder.Configuration.GetSection(FeedbackOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<FeedbackOptions>, FeedbackOptionsValidator>();

// Story 12 — the one knowledge-base setting: how many suggested articles a ticket view receives
// (§7.4). Presentation volume, not a product rule, and published to no client.
builder.Services.AddOptions<KnowledgeOptions>()
    .Bind(builder.Configuration.GetSection(KnowledgeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------------------------
// Authentication and authorization (AD-7, AD-15, docs/architecture.md §4.1, §4.1.1, §4.2)
// ---------------------------------------------------------------------------------------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Validating the signature, issuer, audience and lifetime establishes only WHO the caller
        // claims to be. Everything an authorization decision needs is read from the database
        // afterwards, by CurrentUserMiddleware.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt?.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt?.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt?.SigningKey ?? new string('.', JwtOptions.MinimumSigningKeyLength))),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            // The token carries no role claim to map, and mapping one would be the very staleness
            // AD-15 removes. RoleClaimType is set only so the framework never invents a default.
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
        };
    });

builder.Services.AddAuthorizationBuilder().AddSupportCrmPolicies();

// One instance per request, filled by CurrentUserMiddleware and read by Application services as
// ICurrentUser. Scoped, never singleton: it holds this request's resolved identity.
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddScoped<SupportCrm.Application.Abstractions.ICurrentUser>(
    sp => sp.GetRequiredService<CurrentUserAccessor>());

// Story 09 — the periodic in-process SLA breach sweep (AD-6). It holds no business logic: each tick
// creates a scope and calls SlaEvaluationService. No queue, no broker, no external scheduler.
builder.Services.AddHostedService<SupportCrm.Api.BackgroundServices.SlaMonitorHostedService>();

// ---------------------------------------------------------------------------------------------
// Cross-cutting API concerns (docs/api-design.md §2)
// ---------------------------------------------------------------------------------------------
builder.Services.AddControllers(options =>
    {
        // Route tokens become lower-case slugs, so the served path and the OpenAPI document both
        // read /api/v1/health, exactly as docs/api-design.md specifies.
        options.Conventions.Add(new RouteTokenTransformerConvention(new SlugifyParameterTransformer()));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        // Nulls are omitted rather than sent, except where null is meaningful — those properties
        // opt back in per-member (docs/api-design.md §2).
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        // Enums travel as stable string codes, never integers (docs/api-design.md §2), so
        // renumbering an enum can never change the wire contract.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());

        // AP-10, enforced here and nowhere else. docs/api-design.md §7 states that a server-derived
        // field is "never accepted in a request body" and that "a request containing one is 400, so
        // a client is never misled into thinking it worked" — with "accepting and ignoring them"
        // named as the REJECTED alternative in AP-10's own row.
        //
        // Omitting the property from the request model is only half of that rule: it makes the
        // field UNREACHABLE, but System.Text.Json's default is to skip a JSON member that maps to
        // nothing, so the request is still ACCEPTED. Disallow turns the skip into a JsonException,
        // which the MVC input formatter records as a model-state error, which [ApiController] turns
        // into the 400 the contract promises. Every request model therefore enforces AP-10 by the
        // shape it already has — no attribute, no filter and no per-endpoint check (finding I-9).
        //
        // It is set once, on the options every controller body is bound with, because AP-10 is a
        // property of the contract rather than of any one endpoint. A type that must accept unknown
        // members would opt out with [JsonUnmappedMemberHandling]; none does, and none should.
        //
        // Reads only. Serialization is untouched, so no response shape changes. Query strings are
        // model-bound rather than deserialized, so the AP-15 filter and sort whitelists are
        // unaffected and keep raising their own 400s; multipart uploads are unaffected likewise.
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;

        // The refusal this produces is SHAPED by AddModelStateProblemDetails below, which also
        // switches off AllowInputFormatterExceptionMessages on these same options — that flag is
        // what stops System.Text.Json's message naming .NET internals to a client (finding I-10).
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

// The other half of the error contract. AddExceptionHandler above covers everything raised as an
// AppException; it never sees a MODEL-STATE failure, because [ApiController] answers those itself
// before the action runs — with the framework's shape, not §6.12's. This makes the two agree on one
// envelope and one `validation-failed` slug, and stops System.Text.Json's messages naming .NET
// internals to a client (finding I-10). See ModelStateProblemDetails for the whole reasoning.
builder.Services.AddModelStateProblemDetails();

builder.Services.AddOpenApi();

const string CorsPolicy = "SupportCrmSpa";
var allowedOrigins = builder.Configuration
    .GetSection("SupportCrm:Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors(CorsPolicy);

app.UseAuthentication();

// The per-request resolution step of AD-15. It must sit AFTER authentication (it needs the
// validated token's subject) and BEFORE authorization (the policies read the role it writes).
app.UseMiddleware<CurrentUserMiddleware>();

app.UseAuthorization();

app.MapControllers();

// Story 10 — record which AI implementation is live, once, at startup. **The plan verifies this by
// reading the log**: with no credentials configured the line must name the offline fake, which is the
// observable form of "the whole system runs with no external accounts" (A-7, product-scope §10 item 5).
// The provider is never named, only the kind (api-design §8.1, AP-12).
{
    var aiOptions = app.Services.GetRequiredService<IOptions<AiOptions>>().Value;

    app.Logger.LogInformation(
        "AI seam: {Implementation} implementation selected (SupportCrm:Ai:Provider = {Provider}).",
        aiOptions.Provider == AiProviderKind.Provider ? "real provider" : "deterministic offline fake",
        aiOptions.Provider);
}

app.Run();

// Exposed so WebApplicationFactory<Program> can reach the composition root from the test project.
public partial class Program;
