using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Scalar.AspNetCore;
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

// The remaining configuration keys of architecture §6.3 — categories, category-to-department map,
// default branch, priorities, SLA targets, quick replies, feedback rating scale — are defined and
// validated by Story 16 Part A. This story delivers the mechanism only.

builder.Services.AddInfrastructure(builder.Configuration);

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
app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can reach the composition root from the test project.
public partial class Program;
