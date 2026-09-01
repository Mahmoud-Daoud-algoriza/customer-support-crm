using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Knowledge;

namespace SupportCrm.Application.Modules.Knowledge;

/// <summary>
/// The customer-facing knowledge read — <c>GET /portal/kb/articles</c> and
/// <c>/portal/kb/articles/{id}</c> (docs/api-design.md §5.9, AP-5).
///
/// <para>
/// <b>This is the single place the portal visibility rule lives</b>
/// (.squad/plans/00-implementation-plan.md §6 names <see cref="PortalVisible"/> as its one
/// implementation). docs/data-model.md §5 constraint 19: <em>"<c>Internal</c> or unpublished
/// knowledge articles never reach a portal read."</em> Every read below composes
/// <see cref="PortalVisible"/> before anything else, so no query here can forget it.
/// </para>
///
/// <para>
/// <b>An id that exists but is <c>Internal</c> or unpublished is <c>404</c>, never <c>403</c></b>
/// (<b>AP-4</b>, §5.9). <c>403</c> would confirm that the article exists, which is precisely what a
/// customer must not learn — so this service <em>cannot tell the two cases apart</em>: both fall out
/// of the same filtered query as "no row", and both carry <see cref="NotFound"/>, one message.
/// </para>
///
/// <para>
/// <b>The payload is a different shape from the staff one, deliberately</b> —
/// <see cref="PortalArticleDto"/> carries no <c>visibility</c>, no <c>isPublished</c> and no author
/// (docs/api-design.md §6.5). Returning them would state the obvious and leak the taxonomy.
/// </para>
///
/// <para>
/// <b>Article content is never translated</b> (A-11): the body is returned as authored, whatever the
/// caller's interface language.
/// </para>
/// </summary>
public sealed class PortalArticleService(IApplicationDbContext db)
{
    /// <summary>
    /// <b>Constraint 19, enforced here, once, for every portal read.</b> Public <em>and</em>
    /// published — two independent facts, both required.
    /// </summary>
    private static IQueryable<KnowledgeArticle> PortalVisible(IQueryable<KnowledgeArticle> query) =>
        query.Where(a => a.Visibility == ArticleVisibility.Public && a.IsPublished);

    /// <summary>
    /// <c>GET /portal/kb/articles</c> — the customer's search, paged. <c>q</c> is the same keyword
    /// match staff search uses (AD-13); the difference between the two endpoints is
    /// <see cref="PortalVisible"/> and nothing else.
    /// <para>
    /// No matches is a clean empty page (docs/ui-design.md §9), not an error.
    /// </para>
    /// </summary>
    public async Task<PagedResult<PortalArticleDto>> SearchAsync(
        PortalArticleListFilter filter, PageQuery? page, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (pageNumber, pageSize) = page.Normalize();

        var ranked = PortalVisible(db.KnowledgeArticles.AsNoTracking())
            .RankedByRelevance(ArticleSearch.Terms(filter.Q))
            .Select(a => new PortalArticleDto(a.Id, a.Title, a.Body, a.Type.ToString(), a.UpdatedAt));

        return await ranked.ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>GET /portal/kb/articles/{id}</c>.
    /// <para>
    /// <b>Missing, internal and unpublished are one answer: <c>404</c>.</b> The filter runs before
    /// the id comparison, so an internal article is not "found and then refused" — it is not found.
    /// That is AP-4 made structural rather than remembered.
    /// </para>
    /// </summary>
    public async Task<PortalArticleDto> GetAsync(Guid id, CancellationToken ct) =>
        await PortalVisible(db.KnowledgeArticles.AsNoTracking())
            .Where(a => a.Id == id)
            .Select(a => new PortalArticleDto(a.Id, a.Title, a.Body, a.Type.ToString(), a.UpdatedAt))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(NotFound);

    /// <summary>
    /// One message for "no such article", "internal" and "unpublished". A distinct message per case
    /// would undo AP-4 at the Problem Details layer, after this service had got it right — the same
    /// discipline <c>TicketScope.NotFound</c> states.
    /// </summary>
    public const string NotFound = "Article not found.";
}
