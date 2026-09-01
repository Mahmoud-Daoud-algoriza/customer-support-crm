using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Knowledge;

namespace SupportCrm.Application.Modules.Knowledge;

/// <summary>
/// <b>Keyword search — the one implementation of AD-13</b> (.squad/plans/00-implementation-plan.md
/// §6 names this file as its single home). Staff search, portal search and suggested articles all
/// compose the expressions built here; none of them expresses matching a second time.
///
/// <para>
/// <b>AD-13: the database's own text matching. No search engine, no vector index, no relevance
/// tuning.</b> Matching is <c>LIKE</c> over <see cref="KnowledgeArticle.Title"/> and
/// <see cref="KnowledgeArticle.Body"/> — a scan, which docs/data-model.md §6 states is sufficient at
/// assessment data volumes and needs no special index. SQL Server full-text indexing on those two
/// columns is the available upgrade if match quality proves inadequate; it is a database feature,
/// not a new component, and <b>this story does not take it</b>.
/// </para>
///
/// <para>
/// <b>Relevance is the weighted count of matched terms, a title match above a body match — and that
/// is the whole of it.</b> The intake excludes tuning, synonyms and semantic search, so there is no
/// boosting beyond these two constants, no term frequency, no recency decay and no usage feedback.
/// </para>
///
/// <para>
/// <b>Why expression trees rather than a fold over <c>Where</c>.</b> The score has to be one SQL
/// expression so the database can filter and order by it in a single statement; summing in C# would
/// mean materializing every article first. The trees below are built per query from the parsed terms
/// and handed to EF, which translates them to a sum of <c>CASE WHEN ... LIKE ... THEN n ELSE 0
/// END</c>.
/// </para>
/// </summary>
public static class ArticleSearch
{
    /// <summary>A title match outranks a body match. The whole of the ranking (AD-13).</summary>
    public const int TitleWeight = 2;

    /// <inheritdoc cref="TitleWeight"/>
    public const int BodyWeight = 1;

    /// <summary>
    /// A ceiling on how many terms reach the database, so a pathological query cannot build an
    /// arbitrarily large predicate. <b>Implementation choice, not a documented rule</b> — no
    /// approved document fixes a term limit.
    /// </summary>
    public const int MaxTerms = 12;

    /// <summary>
    /// The terms of a <b>caller-typed</b> query (<c>q</c>). Split on whitespace and punctuation,
    /// de-duplicated, and stripped of the <c>LIKE</c> wildcards so a percent sign typed into a search
    /// box matches a literal percent sign rather than everything.
    /// <para>
    /// Nothing else is removed: a caller who types a short or common word meant to search for it.
    /// <see cref="KeywordsFrom"/> is the variant that prunes, and it exists for a different job.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> Terms(string? query) =>
        (query ?? string.Empty)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Sanitize)
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();

    /// <summary>
    /// The terms of a <b>ticket's own text</b> — the keyword extraction §7.4's retrieval needs
    /// (<c>SuggestedArticleService</c>). Same splitting as <see cref="Terms"/>, then two prunes:
    /// words shorter than <see cref="MinKeywordLength"/>, and the stop-word list below.
    /// <para>
    /// <b>Implementation choice, not a documented rule.</b> No approved document defines keyword
    /// extraction; the plan asks only that keywords come from the subject and description. Unpruned,
    /// "the" and "is" would match every article and the ranking would be noise — which would fail the
    /// intake's own criterion that suggestions be relevant. The list is deliberately tiny: a larger
    /// one would be relevance tuning, which the intake excludes.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> KeywordsFrom(params string?[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        return texts
            .SelectMany(Terms)
            .Where(term => term.Length >= MinKeywordLength && !StopWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTerms)
            .ToList();
    }

    /// <summary>
    /// Narrows a query to the articles matching <b>any</b> of <paramref name="terms"/> in title or
    /// body. An empty term list leaves the query untouched — <em>no keyword</em> means <em>no keyword
    /// filter</em>, which is browse, not "match nothing".
    /// </summary>
    public static IQueryable<KnowledgeArticle> MatchingKeywords(
        this IQueryable<KnowledgeArticle> query, IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(terms);

        return terms.Count == 0 ? query : query.Where(MatchPredicate(terms));
    }

    /// <inheritdoc cref="MatchingKeywords(IQueryable{KnowledgeArticle}, IReadOnlyList{string})"/>
    public static IQueryable<KnowledgeArticle> MatchingKeywords(
        this IQueryable<KnowledgeArticle> query, string? terms) =>
        query.MatchingKeywords(Terms(terms));

    /// <summary>
    /// The match score as one SQL expression: the weighted count of matched terms, title above body.
    /// It orders every search, and <c>GET /tickets/{id}/suggested-articles</c> projects it into the
    /// payload — <b>a query artefact, not a stored field</b> (docs/api-design.md §6.5).
    /// <para>
    /// With no terms the score is a constant zero, so an unfiltered browse orders by recency alone.
    /// </para>
    /// </summary>
    public static Expression<Func<KnowledgeArticle, int>> ScoreExpression(IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var article = Expression.Parameter(typeof(KnowledgeArticle), "a");

        Expression score = Expression.Constant(0);

        foreach (var term in terms)
        {
            score = Expression.Add(
                score, Weighted(Like(article, nameof(KnowledgeArticle.Title), term), TitleWeight));

            score = Expression.Add(
                score, Weighted(Like(article, nameof(KnowledgeArticle.Body), term), BodyWeight));
        }

        return Expression.Lambda<Func<KnowledgeArticle, int>>(score, article);
    }

    /// <summary>
    /// <b>Filter, then rank — the order every knowledge read uses.</b> Staff search, portal search
    /// and suggested articles all start here, so "what matches and in what order" is expressed once.
    /// <para>
    /// Best match first, then recency (docs/data-model.md §2.13: the list shows recency, which is
    /// also the whole ordering when no keyword was given), then id — a stable tiebreak, without
    /// which paging within one score is not meaningful.
    /// </para>
    /// </summary>
    public static IOrderedQueryable<KnowledgeArticle> RankedByRelevance(
        this IQueryable<KnowledgeArticle> query, IReadOnlyList<string> terms) =>
        query.MatchingKeywords(terms)
            .OrderByDescending(ScoreExpression(terms))
            .ThenByDescending(a => a.UpdatedAt)
            .ThenBy(a => a.Id);

    /// <summary>
    /// <see cref="RankedByRelevance"/> with the score <b>projected</b>, computed in the database by
    /// the same expression that ordered the rows — so a published
    /// <see cref="ScoredArticle.MatchScore"/> can never disagree with the ordering it came from.
    /// <para>
    /// <c>GET /tickets/{id}/suggested-articles</c> is its only caller: that payload is the one place
    /// the score leaves the server (docs/api-design.md §6.5). The two search endpoints rank by the
    /// same expression and never publish the number.
    /// </para>
    /// <para>
    /// The projection is applied <b>after</b> the ordering rather than before it: EF orders by a
    /// column expression, not by a member of a projected type, so ranking first is what keeps the
    /// whole query translatable to one statement.
    /// </para>
    /// </summary>
    public static IQueryable<ScoredArticle> RankedByKeywords(
        this IQueryable<KnowledgeArticle> query, IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(query);

        var score = ScoreExpression(terms);

        // A template, so the projection reads as ordinary C# and only the score — which has to be
        // built per query — is spliced in. Substituting into a template beats hand-assembling an
        // Expression.New: the shape stays legible to the next reader.
        Expression<Func<KnowledgeArticle, int, ScoredArticle>> template =
            (a, s) => new ScoredArticle(a.Id, a.Title, a.Type, a.UpdatedAt, s);

        var body = new SubstituteVisitor(
                template.Parameters[0], score.Parameters[0], template.Parameters[1], score.Body)
            .Visit(template.Body);

        var projection = Expression.Lambda<Func<KnowledgeArticle, ScoredArticle>>(
            body, score.Parameters[0]);

        return query.RankedByRelevance(terms).Select(projection);
    }

    /// <summary>Matched in title <b>or</b> body, for any one of the terms.</summary>
    private static Expression<Func<KnowledgeArticle, bool>> MatchPredicate(IReadOnlyList<string> terms)
    {
        var article = Expression.Parameter(typeof(KnowledgeArticle), "a");

        Expression matched = Expression.Constant(false);

        foreach (var term in terms)
        {
            matched = Expression.OrElse(matched, Like(article, nameof(KnowledgeArticle.Title), term));
            matched = Expression.OrElse(matched, Like(article, nameof(KnowledgeArticle.Body), term));
        }

        return Expression.Lambda<Func<KnowledgeArticle, bool>>(matched, article);
    }

    private static Expression Weighted(Expression condition, int weight) =>
        Expression.Condition(condition, Expression.Constant(weight), Expression.Constant(0));

    /// <summary><c>EF.Functions.Like(a.&lt;property&gt;, "%term%")</c>, as an expression node.</summary>
    private static Expression Like(ParameterExpression article, string propertyName, string term) =>
        Expression.Call(
            LikeMethod,
            Expression.Constant(EF.Functions),
            Expression.Property(article, propertyName),
            Expression.Constant(Wrap(term)));

    private static string Wrap(string term) => string.Concat("%", term, "%");

    private static readonly MethodInfo LikeMethod =
        typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException("EF.Functions.Like(string, string) was not found.");

    /// <summary>
    /// Wildcards are removed rather than escaped: an escape clause differs by provider, and a keyword
    /// search has no use for a caller-supplied pattern — the requirement is words, not globs.
    /// </summary>
    private static string Sanitize(string term) =>
        new([.. term.Where(c => !Wildcards.Contains(c))]);

    private static readonly char[] Separators =
    [
        ' ', '\t', '\r', '\n', ',', ';', ':', '.', '!', '?', '(', ')', '"', '\'', '/', '\\',
        '<', '>', '#', '*', '|', '`', '’',
    ];

    private static readonly char[] Wildcards = ['%', '_', '[', ']', '^'];

    private const int MinKeywordLength = 3;

    /// <inheritdoc cref="KeywordsFrom"/>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "was", "are", "but", "not", "you", "your",
        "our", "their", "them", "they", "has", "have", "had", "were", "been", "from", "into",
        "when", "what", "which", "would", "could", "should", "there", "here", "any", "all",
        "can", "will", "does", "did", "doing", "after", "before", "every", "some", "than",
        "then", "these", "those", "about", "still", "also", "just", "only", "very", "please",
    };

    /// <summary>Replaces two parameters of a template lambda with supplied expressions.</summary>
    private sealed class SubstituteVisitor(
        ParameterExpression article,
        Expression articleReplacement,
        ParameterExpression score,
        Expression scoreReplacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == article ? articleReplacement
            : node == score ? scoreReplacement
            : base.VisitParameter(node);
    }
}

/// <summary>
/// One matching article and the score the database gave it (AD-13).
/// <para>
/// <b>The score is a query artefact, not a stored field</b> (docs/api-design.md §6.5). This type is a
/// query result shape, never an entity and never persisted — <c>KnowledgeArticle</c> has no score
/// column, and adding one would turn a computed ranking into stale data.
/// </para>
/// </summary>
public sealed record ScoredArticle(
    Guid Id,
    string Title,
    ArticleType Type,
    DateTimeOffset UpdatedAt,
    int MatchScore);
