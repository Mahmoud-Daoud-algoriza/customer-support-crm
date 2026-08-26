using Microsoft.AspNetCore.Authorization;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Api.Auth;

/// <summary>
/// The coarse role gate — enforcement point 1 of docs/architecture.md §4.2.
/// <para>
/// Four policies, one per role, each satisfied by that role <b>or higher</b> (the A-4 hierarchy).
/// Controllers use <c>[Authorize(Policy = ...)]</c>; <b>no controller compares roles inline.</b>
/// There is no role editor, no per-field permission and no custom RBAC engine.
/// </para>
/// The role claim these policies read was replaced with the freshly resolved value by
/// <see cref="CurrentUserMiddleware"/>, so the gate and the row scoping agree.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireCustomer = nameof(RequireCustomer);
    public const string RequireAgent = nameof(RequireAgent);
    public const string RequireManager = nameof(RequireManager);
    public const string RequireAdministrator = nameof(RequireAdministrator);

    public static AuthorizationBuilder AddSupportCrmPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(RequireCustomer, p => p.RequireAssertion(AtLeast(UserRole.Customer)))
        .AddPolicy(RequireAgent, p => p.RequireAssertion(AtLeast(UserRole.Agent)))
        .AddPolicy(RequireManager, p => p.RequireAssertion(AtLeast(UserRole.Manager)))
        .AddPolicy(RequireAdministrator, p => p.RequireAssertion(AtLeast(UserRole.Administrator)));

    /// <summary>
    /// A Customer is <em>not</em> "at least an Agent", so `RequireAgent` refuses them — but a
    /// Manager satisfies `RequireAgent`, which is the hierarchy A-4 specifies.
    /// </summary>
    private static Func<AuthorizationHandlerContext, bool> AtLeast(UserRole minimum) => context =>
    {
        var claim = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Enum.TryParse<UserRole>(claim, ignoreCase: false, out var role)
            && role.RankAtLeast(minimum);
    };
}
