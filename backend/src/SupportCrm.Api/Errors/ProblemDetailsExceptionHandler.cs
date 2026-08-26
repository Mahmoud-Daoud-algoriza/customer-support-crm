using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;

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
            Detail = exception is AppException ? exception.Message : null,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
        };

        if (exception is ValidationException { Errors.Count: > 0 } validation)
        {
            problem.Extensions["errors"] = validation.Errors;
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
        ValidationException e          => (StatusCodes.Status400BadRequest, e.ProblemType, "Invalid request"),
        UnprocessableException e       => (StatusCodes.Status422UnprocessableEntity, e.ProblemType, "Unprocessable"),
        PayloadTooLargeException e     => (StatusCodes.Status413PayloadTooLarge, e.ProblemType, "Payload too large"),
        SeamUnavailableException e     => (StatusCodes.Status503ServiceUnavailable, e.ProblemType, "Service unavailable"),
        _                              => (StatusCodes.Status500InternalServerError, "internal-error", "Internal server error"),
    };
}
