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
/// <c>POST /auth/register</c> is here as of Story 04 <b>slice 5</b>, deferred from Story 02 because
/// it creates a <c>Customer</c> with the configured default branch (A-15) and neither the entity
/// nor the configuration key existed then — finding S9-7, now closed.
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
    /// Customer self-registration — docs/api-design.md §5.2, <b>A-15</b>. Anonymous: one of the
    /// four anonymous endpoints (§4.1).
    ///
    /// <para>
    /// <b>Three outcomes, and the controller decides none of them.</b> The two <c>201</c>s and the
    /// <c>409</c> live in <see cref="AuthService.RegisterAsync"/>; this action binds and delegates,
    /// like every other action here (docs/architecture.md §2.1).
    /// </para>
    ///
    /// <para>
    /// <b>On the <c>Location</c> header.</b> §2.2 pairs a <c>201</c> with one, and the created
    /// resource is the caller's own identity — so it addresses <c>GET /auth/me</c>, the only route
    /// that resolves for a <c>Customer</c>-role token. <c>GET /users/{id}</c> is Administrator-only
    /// and <c>GET /customers/{id}</c> is Agent-only, so either would be a <c>Location</c> the
    /// recipient is forbidden to follow; inventing a third would be contract surface no requirement
    /// asks for (AP-18). The same reasoning the notes endpoint records, and recorded again here as a
    /// judgment call.
    /// </para>
    ///
    /// <para>
    /// <b>The body is an <c>AuthToken</c></b>, so a new customer is signed in rather than bounced to
    /// the sign-in form — docs/api-design.md §6.1 names this endpoint alongside <c>/auth/login</c>
    /// for that payload.
    /// </para>
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthTokenDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthTokenDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var created = await auth.RegisterAsync(request, ct);

        return CreatedAtAction(nameof(Me), value: created);
    }

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
