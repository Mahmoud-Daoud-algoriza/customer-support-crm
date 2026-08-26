using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// Sign-in and the resolved-identity read — docs/api-design.md §5.2.
/// <para>
/// Controllers are thin: bind, delegate to one Application service, return the result. No business
/// rule and no <c>try</c>/<c>catch</c> lives here (docs/architecture.md §2.1) — the exception family
/// is translated centrally by <c>ProblemDetailsExceptionHandler</c>.
/// </para>
/// <c>POST /auth/register</c> is <b>not</b> here: it creates a <c>Customer</c> with the configured
/// default branch (A-15), and neither exists until Stories 04 and 16 Part A. It is delivered by
/// Story 04 — finding S9-7.
/// </summary>
public sealed class AuthController(AuthService auth) : ApiControllerBase
{
    /// <summary>
    /// Exchanges credentials for a token. Anonymous — one of the four anonymous endpoints
    /// (docs/api-design.md §4.1).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthTokenDto>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    /// <summary>
    /// The per-request resolved identity (AP-9) — <b>not a decoded token</b>. It reflects a role or
    /// department change made by an Administrator immediately, without a new token being issued.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<IdentityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IdentityDto>> Me(CancellationToken ct) =>
        Ok(await auth.GetMeAsync(ct));
}
