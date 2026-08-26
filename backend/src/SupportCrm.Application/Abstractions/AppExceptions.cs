namespace SupportCrm.Application.Abstractions;

/// <summary>
/// The exception family the API translates into RFC 9457 Problem Details.
/// Each carries its own stable <c>type</c> slug (docs/api-design.md §6.12) so the handler
/// never invents one, and no controller ever needs a try/catch (docs/architecture.md §2.1).
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string problemType, string message, Exception? inner = null)
        : base(message, inner) => ProblemType = problemType;

    /// <summary>The stable slug published as Problem Details <c>type</c>.</summary>
    public string ProblemType { get; }
}

/// <summary>404 — the resource does not exist <em>or is outside the caller's scope</em> (AP-4).</summary>
public sealed class NotFoundException(string message = "Resource not found.")
    : AppException("not-found", message);

/// <summary>
/// 401 — no valid identity. Sign-in with a wrong password <b>and</b> sign-in to a deactivated
/// account both use this with <c>invalid-credentials</c>: distinguishing them would confirm which
/// emails have accounts (docs/api-design.md §6.11).
/// </summary>
public sealed class UnauthorizedException(string problemType, string message)
    : AppException(problemType, message);

/// <summary>403 — authenticated, role known, and the role may not use this capability.</summary>
public sealed class ForbiddenException(string problemType, string message)
    : AppException(problemType, message);

/// <summary>409 — a state conflict: illegal transition, duplicate email, second feedback.</summary>
public sealed class ConflictException(string problemType, string message)
    : AppException(problemType, message);

/// <summary>400 — malformed input, or an unknown filter or sort field (never silently ignored).</summary>
public sealed class ValidationException(string message, IDictionary<string, string[]>? errors = null)
    : AppException("validation-failed", message)
{
    public IDictionary<string, string[]> Errors { get; } =
        errors ?? new Dictionary<string, string[]>();
}

/// <summary>422 — well-formed but semantically invalid, e.g. <c>assignee-out-of-department</c>.</summary>
public sealed class UnprocessableException(string problemType, string message)
    : AppException(problemType, message);

/// <summary>413 — the upload exceeds the configured size cap (<c>attachment-too-large</c>).</summary>
public sealed class PayloadTooLargeException(string message = "Attachment exceeds the configured size cap.")
    : AppException("attachment-too-large", message);

/// <summary>
/// 503 — an integration seam is unavailable. Used by the AI endpoints <em>only</em>
/// (<c>ai-unavailable</c>, docs/api-design.md §2.2).
/// </summary>
public sealed class SeamUnavailableException(string message = "The AI service is unavailable.", Exception? inner = null)
    : AppException("ai-unavailable", message, inner);
