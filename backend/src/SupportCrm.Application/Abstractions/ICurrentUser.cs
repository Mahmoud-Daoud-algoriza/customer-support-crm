using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Abstractions;

/// <summary>
/// The request-scoped identity, <b>resolved per request from the authoritative <c>User</c> row</b> —
/// never from a token claim (AD-15, docs/architecture.md §4.1.1).
/// <para>
/// <b>This is the only source of caller identity in the Application layer.</b> Application services
/// never read claims and never accept a caller-supplied user id, department id or role
/// (docs/architecture.md §4.3 point 1). That rule is enforced by this being the only way to ask.
/// </para>
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// False on an anonymous request. Every other member throws when this is false.
    /// <para>
    /// It exists because <c>AuditRecorder</c> has to record a <b>failed sign-in</b>, which by
    /// definition has no resolved actor — the entry carries <c>actorDescriptor</c> instead
    /// (docs/data-model.md §2.14). Asking is the alternative to catching.
    /// </para>
    /// </summary>
    bool IsAuthenticated { get; }

    Guid Id { get; }

    UserRole Role { get; }

    /// <summary>Set for a staff role, null for <c>Customer</c> (DM-1).</summary>
    Guid? DepartmentId { get; }

    /// <summary>Set for <c>Customer</c>, null for a staff role (DM-1).</summary>
    Guid? CustomerId { get; }

    string DisplayName { get; }

    string Email { get; }

    /// <summary>
    /// The A-4 hierarchy check. An endpoint marked <c>Agent</c> is also reachable by Manager and
    /// Administrator (docs/api-design.md §4.2).
    /// </summary>
    bool IsInRoleAtLeast(UserRole minimum);
}

/// <summary>
/// Thrown when an Application service asks for the caller on an unauthenticated request. It means a
/// wiring mistake — an endpoint that should carry an authorization policy and does not — so it is
/// deliberately not a Problem Details case.
/// </summary>
public sealed class NoCurrentUserException()
    : InvalidOperationException(
        "No authenticated user is present on this request. An Application service asked for the " +
        "caller on an endpoint that is anonymous or missing an authorization policy.");
