using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Knowledge;

namespace SupportCrm.Application.Modules.Knowledge;

/// <summary>
/// The six staff and administrator knowledge endpoints of docs/api-design.md §5.9.
///
/// <para>
/// <b>Two role gates, not one.</b> Reading is <c>RequireAgent</c>; <b>authoring, publishing and
/// unpublishing are Administrator-only</b> (A-4, §5.9). Both are applied by the controller — a
/// capability denial the caller can infer from their own role, so it is <c>403</c> and AP-4's
/// <c>404</c> rule does not apply (§4.2).
/// </para>
///
/// <para>
/// <b>Staff see everything.</b> There is no visibility predicate in this file: an Agent's search
/// returns internal and public articles alike, because internal articles exist precisely for staff.
/// The one place visibility narrows a read is <see cref="PortalArticleService"/>, and it is the
/// customer path.
/// </para>
///
/// <para>
/// <b>There is no delete method here, and no endpoint that would call one</b> (T2-E,
/// docs/ui-design.md §6). Nor is there any way to write <c>IsPublished</c> from
/// <see cref="UpdateAsync"/>: <c>KnowledgeArticle.Update</c> does not take it, so the one-path rule
/// of §6.11 holds in the entity and not merely in the request model.
/// </para>
///
/// <para>
/// <b>Article content is stored as authored and never translated</b> (A-11). Nothing here
/// normalizes, transliterates or language-tags a title or a body.
/// </para>
/// </summary>
public sealed class KnowledgeArticleService(IApplicationDbContext db, ICurrentUser caller, TimeProvider clock)
{
    /// <summary>
    /// <c>GET /kb/articles</c> — search and browse <b>all</b> articles, paged
    /// (docs/api-design.md §5.9).
    ///
    /// <para>
    /// Filters <c>q</c>, <c>type</c>, <c>visibility</c> and <c>isPublished</c> AND together (§2.1).
    /// <c>q</c> is the keyword match of AD-13, and results are ordered by its score first — a title
    /// match above a body match — then by recency, which is what the list shows when no keyword was
    /// given at all (docs/data-model.md §2.13).
    /// </para>
    ///
    /// <para>
    /// <b>A search with no matches is a clean empty page, not an error</b> (intake AC,
    /// docs/ui-design.md §9): the paged envelope comes back with no items.
    /// </para>
    /// </summary>
    public async Task<PagedResult<ArticleListItemDto>> SearchAsync(
        ArticleListFilter filter, PageQuery? page, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (pageNumber, pageSize) = page.Normalize();

        RejectSort(page);

        var query = db.KnowledgeArticles.AsNoTracking();

        if (ArticleTypeParser.ParseOptional(filter.Type) is { } type)
        {
            query = query.Where(a => a.Type == type);
        }

        if (ArticleVisibilityParser.ParseOptional(filter.Visibility) is { } visibility)
        {
            query = query.Where(a => a.Visibility == visibility);
        }

        if (filter.IsPublished is { } isPublished)
        {
            query = query.Where(a => a.IsPublished == isPublished);
        }

        // Filter and rank in the one place that expresses either (AD-13): best match first, then
        // recency, then a stable tiebreak so paging within one score is meaningful.
        var ranked = query
            .RankedByRelevance(ArticleSearch.Terms(filter.Q))
            .Select(a => new ArticleListItemDto(
                a.Id,
                a.Title,
                a.Type.ToString(),
                a.Visibility.ToString(),
                a.IsPublished,
                a.UpdatedAt));

        return await ranked.ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>GET /kb/articles/{id}</c> — docs/api-design.md §6.5, author included.
    /// <para>
    /// A missing id is <c>404</c>. <b>There is no scope narrowing to make it ambiguous</b>: articles
    /// are organization-wide (docs/data-model.md §2.13), so on this path <c>404</c> means only
    /// "no such article".
    /// </para>
    /// </summary>
    public async Task<ArticleDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.KnowledgeArticles.AsNoTracking().Where(a => a.Id == id))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(NotFound);

    /// <summary>
    /// <c>POST /kb/articles</c> — Administrator only (A-4).
    /// <para>
    /// <b><c>isPublished</c> defaults to false</b>, so an article is drafted before it is visible,
    /// and <b>the author is the authenticated Administrator</b> — never a client-supplied value
    /// (docs/api-design.md §6.11, §7).
    /// </para>
    /// </summary>
    public async Task<ArticleDto> CreateAsync(CreateArticleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var article = KnowledgeArticle.Create(
            Guid.NewGuid(),
            request.Title.Trim(),
            request.Body,
            ArticleTypeParser.Parse(request.Type),
            ArticleVisibilityParser.Parse(request.Visibility),
            request.IsPublished ?? false,
            caller.Id,
            clock.GetUtcNow());

        db.KnowledgeArticles.Add(article);

        await db.SaveChangesAsync(ct);

        return await GetAsync(article.Id, ct);
    }

    /// <summary>
    /// <c>PATCH /kb/articles/{id}</c> — Administrator only. <b>Four fields, and <c>isPublished</c>
    /// is not among them</b> (docs/api-design.md §6.11).
    /// </summary>
    public async Task<ArticleDto> UpdateAsync(Guid id, PatchArticleRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var article = await LoadAsync(id, ct);

        article.Update(
            request.Title?.Trim(),
            request.Body,
            ArticleTypeParser.ParseOptional(request.Type),
            ArticleVisibilityParser.ParseOptional(request.Visibility),
            clock.GetUtcNow());

        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>
    /// <c>POST /kb/articles/{id}/publish</c> — Administrator only. One half of the action pair that
    /// is the <b>only</b> way publication state changes (AP-1, docs/api-design.md §6.11).
    /// </summary>
    public Task<ArticleDto> PublishAsync(Guid id, CancellationToken ct) =>
        SetPublicationAsync(id, publish: true, ct);

    /// <inheritdoc cref="PublishAsync"/>
    public Task<ArticleDto> UnpublishAsync(Guid id, CancellationToken ct) =>
        SetPublicationAsync(id, publish: false, ct);

    private async Task<ArticleDto> SetPublicationAsync(Guid id, bool publish, CancellationToken ct)
    {
        var article = await LoadAsync(id, ct);
        var now = clock.GetUtcNow();

        if (publish)
        {
            article.Publish(now);
        }
        else
        {
            article.Unpublish(now);
        }

        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    /// <summary>Tracked, because every caller mutates it and commits in the same unit of work.</summary>
    private async Task<KnowledgeArticle> LoadAsync(Guid id, CancellationToken ct) =>
        await db.KnowledgeArticles.FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException(NotFound);

    /// <summary>
    /// The author is reached by an explicit join, not a navigation property — no entity in this
    /// codebase carries one, and the join produces the same single statement.
    /// </summary>
    private IQueryable<ArticleDto> Project(IQueryable<KnowledgeArticle> query) =>
        from a in query
        join u in db.Users.AsNoTracking() on a.AuthorUserId equals u.Id
        select new ArticleDto(
            a.Id,
            a.Title,
            a.Body,
            a.Type.ToString(),
            a.Visibility.ToString(),
            a.IsPublished,
            new UserSummaryDto(u.Id, u.DisplayName),
            a.CreatedAt,
            a.UpdatedAt);

    /// <summary>
    /// <b>docs/api-design.md §5.9 publishes no sort field for this endpoint</b>, so every value is an
    /// unknown one and is a <c>400</c> rather than being silently ignored (AP-15). Order is fixed:
    /// match score, then recency.
    /// </summary>
    private static void RejectSort(PageQuery? page)
    {
        if (!string.IsNullOrWhiteSpace(page?.Sort))
        {
            throw new ValidationException(
                "GET /kb/articles publishes no sort field. Results are ordered by keyword match " +
                "score and then by recency (docs/api-design.md §5.9, AD-13).");
        }
    }

    private const string NotFound = "Article not found.";
}
