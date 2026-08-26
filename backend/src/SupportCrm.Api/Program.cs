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

// The remaining configuration keys of architecture §6.3 — categories, category-to-department map,
// default branch, priorities, SLA targets, quick replies, feedback rating scale — are defined and
// validated by Story 16 Part A. This story delivers the mechanism only.

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
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

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

app.Run();

// Exposed so WebApplicationFactory<Program> can reach the composition root from the test project.
public partial class Program;
