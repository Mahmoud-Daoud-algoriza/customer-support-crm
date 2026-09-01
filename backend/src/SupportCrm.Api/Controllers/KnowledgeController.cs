using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Knowledge;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The staff and administrator knowledge endpoints of docs/api-design.md §5.9 — six of the nine
/// rows. The seventh, <c>GET /tickets/{id}/suggested-articles</c>, hangs off a ticket and lives in
/// <see cref="TicketsController"/>; the two portal rows are
/// <see cref="PortalKnowledgeController"/>.
///
/// <para>
/// <b>Two gates, because §5.9 has two audiences.</b> The class carries <c>RequireAgent</c> so every
/// read is open to staff; the four write actions carry <c>RequireAdministrator</c> on the method,
/// because <b>authoring is an Administrator capability</b> (A-4). A Customer calling any of these
/// gets <c>403</c> — a capability denial they can infer from their own role, so AP-4's <c>404</c>
/// rule does not apply (§4.2). Customers have their own path space.
/// </para>
///
/// <para>
/// <b>Staff reads are not narrowed by visibility.</b> An Agent's search returns internal and public
/// articles alike; internal ones exist for staff. The narrowing belongs to the portal, and it is
/// <c>PortalArticleService.PortalVisible</c>, once.
/// </para>
///
/// <para>
/// <b>Publish and unpublish are actions, not a patchable field</b> (AP-1, §6.11). <c>PATCH</c>
/// carries four fields and <c>isPublished</c> is not one of them — a body containing it is a
/// <c>400</c>, so publication state changes through one path only.
/// </para>
///
/// <para>
/// <b>There is no delete endpoint here or anywhere else</b> (T2-E, docs/ui-design.md §6), and no
/// versioning, review-workflow or scheduled-publishing route. None of them exists server-side, so
/// none may be added client-first.
/// </para>
///
/// <para>
/// Controllers are thin: bind, delegate to one Application service, return the result. Errors are
/// translated centrally by <c>ProblemDetailsExceptionHandler</c> (docs/architecture.md §2.1).
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAgent)]
[Route("api/v1/kb/articles")]
public sealed class KnowledgeController(KnowledgeArticleService articles) : ApiControllerBase
{
    /// <summary>
    /// <c>GET /kb/articles</c> — search and browse <b>all</b> articles, paged. Filters <c>q</c>,
    /// <c>type</c>, <c>visibility</c> and <c>isPublished</c>; <c>q</c> matches title and body using
    /// the database's own text matching (AD-13).
    /// <para>
    /// <b>No matches is an empty page, not an error</b> (docs/ui-design.md §9).
    /// </para>
    /// </summary>
    /// <remarks>
    /// The paging parameter is named <c>paging</c>, not <c>page</c>, for the reason
    /// <see cref="UsersController.List"/> records: a complex query parameter whose name matches an
    /// incoming query key switches the model binder to prefix mode and silently leaves
    /// <c>pageSize</c> unbound.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<ArticleListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<ArticleListItemDto>>> Search(
        [FromQuery] ArticleListFilter filter, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await articles.SearchAsync(filter, paging, ct));

    /// <summary>
    /// <c>GET /kb/articles/{id}</c> — the full article with its author (docs/api-design.md §6.5).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await articles.GetAsync(id, ct));

    /// <summary>
    /// <c>POST /kb/articles</c> — <b>Administrator only</b> (A-4).
    /// <para>
    /// <c>isPublished</c> is optional and <b>false when omitted</b>, so an article is drafted before
    /// it is visible; <c>author</c> is the authenticated Administrator and is never accepted from a
    /// client (§6.11, §7, AP-10).
    /// </para>
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ArticleDto>> Create(CreateArticleRequest request, CancellationToken ct)
    {
        var created = await articles.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// <c>PATCH /kb/articles/{id}</c> — <b>Administrator only</b>. Any of <c>title</c>, <c>body</c>,
    /// <c>type</c>, <c>visibility</c>.
    /// <para>
    /// <b><c>isPublished</c> is not patchable</b> (§6.11): it is absent from
    /// <see cref="PatchArticleRequest"/>, so a body carrying it is a <c>400</c> rather than accepted
    /// and ignored (AP-10, finding I-9).
    /// </para>
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> Patch(
        Guid id, PatchArticleRequest request, CancellationToken ct) =>
        Ok(await articles.UpdateAsync(id, request, ct));

    /// <summary>
    /// <c>POST /kb/articles/{id}/publish</c> — <b>Administrator only</b>. One half of the action pair
    /// that is the only way publication state changes (AP-1, §6.11).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> Publish(Guid id, CancellationToken ct) =>
        Ok(await articles.PublishAsync(id, ct));

    /// <inheritdoc cref="Publish"/>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
    [ProducesResponseType<ArticleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleDto>> Unpublish(Guid id, CancellationToken ct) =>
        Ok(await articles.UnpublishAsync(id, ct));
}
