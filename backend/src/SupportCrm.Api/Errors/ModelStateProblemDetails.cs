using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SupportCrm.Api.Errors;

/// <summary>
/// The second translation point into RFC 9457 Problem Details, and the twin of
/// <see cref="ProblemDetailsExceptionHandler"/>.
///
/// <para>
/// The handler covers everything raised as an <c>AppException</c>. It never sees a
/// <b>model-state</b> failure, because <c>[ApiController]</c> short-circuits those before the
/// action runs and answers with its own response. That response is the framework's, not the
/// contract's: <c>type</c> is the RFC URL
/// <c>https://tools.ietf.org/html/rfc9110#section-15.5.1</c> rather than a stable slug, there is no
/// <c>detail</c> and no <c>instance</c>, and the <c>errors</c> messages name .NET internals. AP-2
/// requires a stable slug per error class precisely so the front end can localize by it (T2-J), and
/// [api-design.md](../../../../docs/api-design.md) §6.12 fixes the envelope as
/// <c>{ type, title, status, detail, instance, errors? }</c> — so the two paths were answering the
/// same status code with two different shapes. Recorded as finding <b>I-10</b>.
/// </para>
///
/// <para>
/// <b>This makes them one shape.</b> A model-state <c>400</c> now carries the same
/// <c>validation-failed</c> slug, the same <c>Invalid request</c> title and the same
/// <c>METHOD /path</c> instance that <c>ValidationException</c> already produced through the
/// handler. Nothing invents a slug: <c>validation-failed</c> is the one the Application layer has
/// used since Story 02, and both i18n dictionaries already ship a string for it.
/// </para>
///
/// <para>
/// <b>No status code moves, and no endpoint is touched.</b> This is a response <em>shape</em>, set
/// once, for every controller.
/// </para>
/// </summary>
public static class ModelStateProblemDetails
{
    /// <summary>
    /// The stable slug of [api-design.md](../../../../docs/api-design.md) §6.12, shared with
    /// <c>ValidationException</c> so a client handles one validation failure, not two.
    /// <b>An end-to-end test asserts the two paths agree</b> rather than a shared constant enforcing
    /// it, because the Application layer owns its own slug and must not depend on this assembly.
    /// </summary>
    public const string ProblemType = "validation-failed";

    /// <summary>The title <see cref="ProblemDetailsExceptionHandler"/> already gives a <c>400</c>.</summary>
    private const string ProblemTitle = "Invalid request";

    /// <summary>
    /// Stable prose. <c>detail</c> is diagnostic only — the front end renders a translated string
    /// chosen by <c>type</c> and never shows the server's words (T2-J, docs/ui-design.md §9) — so it
    /// says what happened without naming a single implementation detail.
    /// </summary>
    private const string ProblemDetail =
        "The request failed validation. See 'errors' for the fields concerned.";

    /// <summary>
    /// Registers both halves of the fix. Called once from <c>Program.cs</c>.
    /// </summary>
    public static IServiceCollection AddModelStateProblemDetails(this IServiceCollection services)
    {
        // Half one: stop the leak at its source.
        //
        // The framework's own documentation for this flag: "Error messages in the
        // ModelStateDictionary are often communicated to clients … this setting controls whether
        // clients can receive detailed error messages about submitted JSON data." Left at its
        // default of true, a JSON reader failure puts System.Text.Json's message into the response
        // — which named the request model's fully qualified type, generic type notation such as
        // System.Nullable`1[System.Guid], and byte offsets into the payload.
        //
        // With it false the framework substitutes one generic sentence and KEEPS THE KEY, which is
        // the half the contract actually needs: docs/ui-design.md §9 renders a 400 "inline on the
        // offending field", so the field is what has to survive, never the prose.
        //
        // It lives on the MVC JsonOptions — the same object Program.cs's AddJsonOptions block
        // configures — but it is registered here, with the response shape it exists to protect,
        // rather than among the settings that decide the wire format.
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(
            options => options.AllowInputFormatterExceptionMessages = false);

        // Half two: the envelope.
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = CreateResponse);

        // The factory resolves this per request. AddControllers registers it already; TryAdd keeps
        // the registration honest without displacing a customized one.
        services.TryAddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>();

        return services;
    }

    private static IActionResult CreateResponse(ActionContext context)
    {
        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();

        // Built through the framework's factory rather than by hand, so this response keeps whatever
        // the rest of the pipeline adds to a ProblemDetails — traceId today — and a 400 from here
        // stays indistinguishable in shape from a 400 raised as a ValidationException.
        var problem = factory.CreateValidationProblemDetails(
            context.HttpContext, Normalize(context), statusCode: StatusCodes.Status400BadRequest);

        // The factory fills these from ApiBehaviorOptions.ClientErrorMapping, which is where the RFC
        // URL came from. The contract's values replace them.
        problem.Type = ProblemType;
        problem.Title = ProblemTitle;
        problem.Detail = ProblemDetail;
        problem.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    /// <summary>
    /// Rewrites the raw <see cref="ModelStateDictionary"/> into the one the contract publishes:
    /// every key a <c>camelCase</c> field name, and nothing in it that is not a field.
    /// </summary>
    private static ModelStateDictionary Normalize(ActionContext context)
    {
        // When the body does not deserialize the parameter binds to null, and MVC adds an error
        // against it keyed by the ACTION's parameter name — "request". That is a C# identifier the
        // client never sent, restating a failure already reported against the field or the body.
        var bodyParameters = context.ActionDescriptor.Parameters
            .Where(parameter => parameter.BindingInfo?.BindingSource == BindingSource.Body)
            .Select(parameter => parameter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var normalized = new ModelStateDictionary();
        var parameterErrors = new List<string>();

        foreach (var (key, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                if (bodyParameters.Contains(key))
                {
                    parameterErrors.Add(Message(error));
                }
                else
                {
                    normalized.TryAddModelError(NormalizeKey(key), Message(error));
                }
            }
        }

        // Dropped whenever a real error stands beside it — which is every reachable case, because a
        // body that fails to parse always reports the field or the body itself as well. Kept only if
        // it would otherwise be the only thing wrong, and then under the general key, so a C#
        // identifier never reaches a client either way.
        if (normalized.ErrorCount == 0)
        {
            foreach (var message in parameterErrors)
            {
                normalized.TryAddModelError(string.Empty, message);
            }
        }

        return normalized;
    }

    /// <summary>
    /// <c>$.email</c> → <c>email</c>, <c>DisplayName</c> → <c>displayName</c>. The two producers
    /// disagreed: data annotations and query binding key by property name, the JSON reader keys by
    /// path. A client should not have to know which one refused it.
    /// </summary>
    private static string NormalizeKey(string key)
    {
        var path = key switch
        {
            "$" => string.Empty,
            _ when key.StartsWith("$.", StringComparison.Ordinal) => key[2..],
            _ => key,
        };

        // Segment by segment, so a nested path stays a path and only the names within it change.
        return string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }

    /// <summary>
    /// The message, or a stable sentence where there is none. An exception never reaches the client:
    /// with <c>AllowInputFormatterExceptionMessages</c> false the framework has already replaced the
    /// reader's prose, and this is the backstop for any other error carrying an exception instead of
    /// a message.
    /// </summary>
    private static string Message(ModelError error) =>
        string.IsNullOrWhiteSpace(error.ErrorMessage)
            ? "The value is not valid."
            : error.ErrorMessage;
}
