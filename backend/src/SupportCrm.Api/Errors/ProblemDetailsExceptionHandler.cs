using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Api.Errors;

/// <summary>
/// The single translation point from application exceptions to RFC 9457 Problem Details.
/// There is no try/catch in any controller (docs/architecture.md §2.1), and the <c>type</c> slug
/// is taken from the exception rather than invented here (docs/api-design.md §6.12).
/// </summary>
public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, slug, title) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}.",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request refused on {Method} {Path}: {Slug} ({Status}).",
                httpContext.Request.Method, httpContext.Request.Path, slug, status);
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Type = slug,
            Title = title,
            Status = status,
            // §6.12 lists `detail` in the envelope; only `errors` is optional. IllegalTransitionException
            // is a DOMAIN exception rather than an AppException (AD-4 leaves Domain unable to name
            // one), so it is admitted here explicitly — its message names the two statuses and no
            // internals. Everything else stays excluded: an unexpected exception's message must never
            // reach a client.
            Detail = exception is AppException or IllegalTransitionException
                ? exception.Message
                : null,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
        };

        if (exception is ValidationException { Errors.Count: > 0 } validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        // docs/api-design.md §5.6 requires the legal targets INSIDE the 409 problem detail — the
        // only place the contract publishes that set today (finding F-1). The set is carried on the
        // exception rather than recomputed here, so what the caller is told is provably the set the
        // refusal was made against. It is empty for a terminal ticket, which is the truthful answer
        // rather than an omission.
        if (exception is IllegalTransitionException illegalTransition)
        {
            problem.Extensions["allowedTransitions"] =
                illegalTransition.AllowedTransitions.Select(s => s.ToString()).ToArray();
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }

    private static (int Status, string Slug, string Title) Map(Exception exception) => exception switch
    {
        UnauthorizedException e        => (StatusCodes.Status401Unauthorized, e.ProblemType, "Unauthorized"),
        NotFoundException e            => (StatusCodes.Status404NotFound, e.ProblemType, "Not found"),
        ForbiddenException e           => (StatusCodes.Status403Forbidden, e.ProblemType, "Forbidden"),
        ConflictException e            => (StatusCodes.Status409Conflict, e.ProblemType, "Conflict"),

        // An A-5 legality violation. The Domain cannot name AppException (AD-4: zero references),
        // so the slug is attached here — at the one translation point that already exists, rather
        // than by wrapping the exception at every call site.
        IllegalTransitionException      => (StatusCodes.Status409Conflict, "illegal-transition", "Conflict"),
        ValidationException e          => (StatusCodes.Status400BadRequest, e.ProblemType, "Invalid request"),
        UnprocessableException e       => (StatusCodes.Status422UnprocessableEntity, e.ProblemType, "Unprocessable"),
        PayloadTooLargeException e     => (StatusCodes.Status413PayloadTooLarge, e.ProblemType, "Payload too large"),
        SeamUnavailableException e     => (StatusCodes.Status503ServiceUnavailable, e.ProblemType, "Service unavailable"),
        _                              => (StatusCodes.Status500InternalServerError, "internal-error", "Internal server error"),
    };
}
