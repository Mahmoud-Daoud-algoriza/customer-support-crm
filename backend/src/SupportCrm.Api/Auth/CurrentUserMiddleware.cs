using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Infrastructure.Persistence;

namespace SupportCrm.Api.Auth;

/// <summary>
/// <b>The per-request resolution step of AD-15 and docs/architecture.md §4.1.1.</b> Runs after
/// authentication and before authorization.
/// <para>
/// Why it exists: an Administrator can move an agent between departments, change their role, or
/// deactivate them at any moment, but a claim minted at sign-in keeps its value until the token
/// expires. Without this step a moved agent would go on reading their old department's tickets —
/// and be refused their new one's — for the remaining life of the token, while the server believed
/// it was enforcing §4.3 correctly. That is a confidentiality defect, and it fails silently.
/// </para>
/// </summary>
public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SupportCrmDbContext db, CurrentUserAccessor accessor)
    {
        // Anonymous request: nothing to resolve. Authorization refuses it if the endpoint needs a
        // policy; endpoints that are genuinely anonymous (health, bootstrap config, login) proceed.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // The user id is THE ONLY CLAIM ANY DECISION READS (AD-7). Role, department and active
        // status are deliberately absent from the token.
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(subject, out var userId))
        {
            await WriteUnauthenticatedAsync(context);
            return;
        }

        // The accepted cost: one indexed read per authenticated request, against the same database
        // the request is about to query anyway. No cache — a cache would reintroduce the staleness
        // AD-15 removes, with invalidation logic on top, and product-scope §8 excludes a caching
        // layer outright.
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id, u.Email, u.DisplayName, u.Role, u.DepartmentId, u.CustomerId, u.IsActive,
            })
            .SingleOrDefaultAsync(context.RequestAborted);

        // A user that no longer exists, or is deactivated, is refused HERE — 401, not 403. They have
        // no valid identity, regardless of what their token says (docs/api-design.md §4.1).
        // Authorization never runs on a stale principal.
        if (user is null || !user.IsActive)
        {
            await WriteUnauthenticatedAsync(context);
            return;
        }

        accessor.Set(user.Id, user.Email, user.DisplayName, user.Role, user.DepartmentId, user.CustomerId);

        // Role, department and active status are resolved together, because the §4.3 rule is a
        // function of all three. Refreshing one while trusting a stale other leaves the same defect
        // reachable through a different claim.
        //
        // The freshly read role also replaces the principal's role claim, so the coarse endpoint
        // gates of §4.2 and the row scoping of §4.3 read the same authoritative value rather than
        // two different vintages of it.
        var identity = new ClaimsIdentity(
            authenticationType: context.User.Identity!.AuthenticationType,
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));

        context.User = new ClaimsPrincipal(identity);

        await next(context);
    }

    /// <summary>
    /// A bare <c>401</c>. It deliberately carries no detail about whether the account is missing or
    /// deactivated — the same reasoning as `invalid-credentials` at sign-in: distinguishing them
    /// would confirm which accounts exist (docs/api-design.md §6.11).
    /// </summary>
    private static Task WriteUnauthenticatedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
