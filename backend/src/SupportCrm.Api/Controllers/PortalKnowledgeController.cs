using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Knowledge;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The customer's knowledge path space — the two portal rows of docs/api-design.md §5.9, under the
/// same <c>api/v1/portal</c> prefix as <see cref="PortalTicketsController"/>.
///
/// <para>
/// <b>A sibling controller rather than more actions on the ticket one</b>, because these are a
/// different resource; the prefix is what AP-5 separates, not the class. <b>Do not create a second
/// portal prefix.</b>
/// </para>
///
/// <para>
/// <b>Public and published only</b> (§5.9). The rule is not written here — it is
/// <c>PortalArticleService.PortalVisible</c>, the single implementation of docs/data-model.md §5
/// constraint 19 — so this controller cannot forget it and no future action here can bypass it.
/// </para>
///
/// <para>
/// <b>An <c>Internal</c> or unpublished article is <c>404</c> on these paths, never <c>403</c></b>
/// (<b>AP-4</b>): <c>403</c> would confirm that the article exists. The service cannot distinguish
/// the cases, so neither can this controller, and the front end renders one wording for all three
/// (docs/ui-design.md §9).
/// </para>
///
/// <para>
/// <b>The payload is <see cref="PortalArticleDto"/>, not the staff shape</b> — no <c>visibility</c>,
/// no <c>isPublished</c>, no author (§6.5). <b>There is no write action here of any kind</b>:
/// authoring is Administrator-only (A-4), and a customer surface with a publish route would be a
/// contradiction, not an oversight.
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireCustomer)]
[Route("api/v1/portal/kb/articles")]
public sealed class PortalKnowledgeController(PortalArticleService articles) : ApiControllerBase
{
    /// <summary>
    /// <c>GET /portal/kb/articles</c> — the customer's search over public, published articles.
    /// <c>q</c> is the same keyword match staff search uses (AD-13).
    /// <para>
    /// <b>An internal article never appears in these results</b>, and no matches is a clean empty
    /// page rather than an error (docs/ui-design.md §9).
    /// </para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PortalArticleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<PortalArticleDto>>> Search(
        [FromQuery] PortalArticleListFilter filter,
        [FromQuery] PageQuery paging,
        CancellationToken ct) =>
        Ok(await articles.SearchAsync(filter, paging, ct));

    /// <summary>
    /// <c>GET /portal/kb/articles/{id}</c>. Missing, internal and unpublished are <b>one answer</b>:
    /// <c>404</c> (AP-4).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PortalArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalArticleDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await articles.GetAsync(id, ct));
}
