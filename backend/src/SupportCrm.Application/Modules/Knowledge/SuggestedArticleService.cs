using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Tickets;

namespace SupportCrm.Application.Modules.Knowledge;

/// <summary>
/// Suggested solutions — requirements §7.4, <c>GET /tickets/{id}/suggested-articles</c>.
///
/// <para>
/// <b>This service does not reference <c>IAiAssistService</c> and must never be moved under
/// <c>/ai</c></b> (<b>AP-14</b>, docs/architecture.md §5.1). §5.1's closing line is explicit —
/// <em>"suggested solutions do not use this seam"</em> — and AP-14 gives the reason: putting them
/// under <c>/ai</c> would imply generation. What happens here is <b>retrieval</b>: the top keyword
/// matches for a ticket, from articles that already exist, scored by the database (AD-13). Routing
/// it through a provider would turn a <c>LIKE</c> into a running cost, for a worse answer.
/// </para>
///
/// <para>
/// <b>Nothing is stored.</b> docs/data-model.md §2.13 has no relationship between
/// <c>KnowledgeArticle</c> and <c>Ticket</c> on purpose: the suggestion set is computed at read time
/// and is never a saved link, so it follows the ticket's text and the article catalogue without a
/// maintenance step.
/// </para>
///
/// <para>
/// <b>Scoped first.</b> The ticket is loaded through <c>TicketScope.LoadScopedAsync</c> — the same
/// helper every ticket read composes (AD-5) — so an out-of-department ticket is <c>404</c> before a
/// single article is read (AP-4), and no customer text is searched on behalf of a caller who may not
/// see the ticket.
/// </para>
///
/// <para>
/// <b>Staff-visible articles, internal included.</b> The reader is an Agent and internal articles
/// exist for exactly this: <see cref="PortalArticleService"/>'s visibility rule is the customer path
/// and is not composed here.
/// </para>
/// </summary>
public sealed class SuggestedArticleService(
    IApplicationDbContext db,
    ICurrentUser caller,
    IOptions<KnowledgeOptions> options)
{
    /// <summary>
    /// The top matches for one ticket, best first.
    /// <list type="number">
    ///   <item>Scope the ticket — out of scope or missing is <c>404</c>.</item>
    ///   <item>Extract keywords from its <b>subject and description</b>.</item>
    ///   <item>Rank staff-visible articles by the one keyword expression (AD-13).</item>
    ///   <item>Take <c>SupportCrm:Knowledge:SuggestedArticleCount</c> of them (default 5).</item>
    /// </list>
    /// <para>
    /// <b>A ticket whose text yields no keywords, or whose keywords match nothing, returns an empty
    /// list — not an error.</b> An empty region is a normal state (docs/ui-design.md §9).
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<SuggestedArticleDto>> ForTicketAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking().LoadScopedAsync(ticketId, caller, ct);

        var keywords = ArticleSearch.KeywordsFrom(ticket.Subject, ticket.Description);

        if (keywords.Count == 0)
        {
            return [];
        }

        // The score is computed by the database and travels with the row, so the number in the
        // payload is the same number that ordered the query (docs/api-design.md §6.5, AD-13).
        var ranked = await db.KnowledgeArticles.AsNoTracking()
            .RankedByKeywords(keywords)
            .Take(options.Value.SuggestedArticleCount)
            .ToListAsync(ct);

        return [.. ranked.Select(a => new SuggestedArticleDto(
            a.Id, a.Title, a.Type.ToString(), a.MatchScore))];
    }
}
