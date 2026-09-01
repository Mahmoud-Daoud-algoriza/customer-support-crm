using System.ComponentModel.DataAnnotations;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Knowledge;

namespace SupportCrm.Application.Modules.Knowledge;

/// <summary>
/// <c>Article</c> — docs/api-design.md §6.5, the shape of <c>GET /kb/articles/{id}</c>.
/// <para>
/// <see cref="Type"/> and <see cref="Visibility"/> are <b>stable string codes, never integers</b>
/// (docs/api-design.md §2), the same choice every other enum in this contract makes.
/// </para>
/// </summary>
public sealed record ArticleDto(
    Guid Id,
    string Title,
    string Body,
    string Type,
    string Visibility,
    bool IsPublished,
    UserSummaryDto Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// <c>ArticleListItem</c> — docs/api-design.md §6.5. <b>No body</b>, so a list does not ship every
/// article's full text; and no author, which §6.5 gives to the detail payload alone.
/// </summary>
public sealed record ArticleListItemDto(
    Guid Id,
    string Title,
    string Type,
    string Visibility,
    bool IsPublished,
    DateTimeOffset UpdatedAt);

/// <summary>
/// <c>Portal article</c> — docs/api-design.md §6.5. <b>A different shape, deliberately</b>:
/// no <c>visibility</c>, no <c>isPublished</c>, no author.
/// <para>
/// §6.5's reason, verbatim: <em>"the portal only ever receives public, published articles, so
/// returning those fields would state the obvious and leak the taxonomy."</em> It is not
/// <see cref="ArticleDto"/> with members omitted — sharing one type across the two path spaces
/// (AP-5) would make the narrowing an optional field a component could read anyway.
/// </para>
/// </summary>
public sealed record PortalArticleDto(
    Guid Id,
    string Title,
    string Body,
    string Type,
    DateTimeOffset UpdatedAt);

/// <summary>
/// <c>SuggestedArticle</c> — <c>GET /tickets/{id}/suggested-articles</c>, docs/api-design.md §6.5.
/// <para>
/// <b><see cref="MatchScore"/> is the database's own text-match ranking (AD-13), exposed so a screen
/// can order results — a query artefact, not a stored field.</b> Nothing persists it, and no
/// endpoint accepts it.
/// </para>
/// <para>
/// <b>There is no <c>generatedBy</c> here, and there must never be one.</b> These are existing
/// articles retrieved by keyword, not generated text (AP-14, docs/architecture.md §5.1).
/// </para>
/// </summary>
public sealed record SuggestedArticleDto(Guid Id, string Title, string Type, int MatchScore);

/// <summary>
/// <c>POST /kb/articles</c> — docs/api-design.md §6.11.
/// <para>
/// <b><c>author</c> is absent</b>: it is the authenticated Administrator and is never supplied
/// (§6.11, §7). So are <c>id</c>, <c>createdAt</c> and <c>updatedAt</c> — a request carrying any of
/// them is a <c>400</c> rather than accepted and ignored, because
/// <c>UnmappedMemberHandling.Disallow</c> is set once on the MVC JSON options (AP-10, finding I-9).
/// </para>
/// </summary>
public sealed record CreateArticleRequest
{
    [Required, MaxLength(200)] public string Title { get; init; } = default!;

    [Required] public string Body { get; init; } = default!;

    /// <summary>A stable string code — <c>Faq</c>, <c>HelpArticle</c> or <c>SolutionGuide</c>.</summary>
    [Required] public string Type { get; init; } = default!;

    /// <summary>A stable string code — <c>Public</c> or <c>Internal</c>.</summary>
    [Required] public string Visibility { get; init; } = default!;

    /// <summary>
    /// Optional, and <b>false when omitted</b> (docs/api-design.md §6.11), so an article is drafted
    /// before it is visible.
    /// </summary>
    public bool? IsPublished { get; init; }
}

/// <summary>
/// <c>PATCH /kb/articles/{id}</c> — docs/api-design.md §6.11. <b>Exactly four patchable fields.</b>
/// Every property is nullable because absent means "leave unchanged".
/// <para>
/// <b><c>isPublished</c> is not here, and its absence is the contract.</b> §6.11: <em>"publishing is
/// the dedicated <c>/publish</c> and <c>/unpublish</c> action pair (AP-1), so publication state
/// changes through one path only."</em> A body carrying <c>isPublished</c> is a <c>400</c>, not a
/// silent no-op — <c>UnmappedMemberHandling.Disallow</c> makes that real (finding I-9), and
/// <c>KnowledgeArticle.Update</c> could not apply it in any case.
/// </para>
/// </summary>
public sealed record PatchArticleRequest
{
    [MaxLength(200)] public string? Title { get; init; }

    public string? Body { get; init; }

    public string? Type { get; init; }

    public string? Visibility { get; init; }
}

/// <summary>
/// Filters for <c>GET /kb/articles</c> — docs/api-design.md §5.9: <c>q</c>, <c>type</c>,
/// <c>visibility</c>, <c>isPublished</c>, and nothing else. Different parameters AND together (§2.1).
/// <para>
/// <b>This is the staff filter set, and <c>visibility</c> belongs in it</b>: staff see internal and
/// public articles alike, so narrowing to one is a filter, not scoping. The portal has no
/// <c>visibility</c> filter because it has no choice to make (§5.9).
/// </para>
/// </summary>
public sealed record ArticleListFilter
{
    /// <summary>Keyword search over title and body, using the database's own text matching (AD-13).</summary>
    public string? Q { get; init; }

    public string? Type { get; init; }

    public string? Visibility { get; init; }

    public bool? IsPublished { get; init; }
}

/// <summary>
/// Filters for <c>GET /portal/kb/articles</c> — <c>q</c> alone (docs/api-design.md §5.9).
/// <para>
/// <b>There is no <c>visibility</c> or <c>isPublished</c> filter here, and there must never be
/// one.</b> Both are fixed by <c>PortalArticleService.PortalVisible</c>; a filter would imply a
/// customer could ask for something else.
/// </para>
/// </summary>
public sealed record PortalArticleListFilter
{
    public string? Q { get; init; }
}

/// <summary>
/// Maps an article <c>type</c> string onto <see cref="ArticleType"/>, and refuses anything else.
/// An unknown value is a <c>400</c> naming what is allowed, never a silent default — the same rule
/// <c>TicketPriorityParser</c> applies, for the same reason (docs/api-design.md §2.2).
/// </summary>
public static class ArticleTypeParser
{
    public static ArticleType Parse(string? value) =>
        ParseOptional(value)
        ?? throw new Abstractions.ValidationException(
            $"An article type is required. Allowed values: {string.Join(", ", Enum.GetNames<ArticleType>())}.");

    /// <summary>Null and whitespace mean "no filter", which is not an error.</summary>
    public static ArticleType? ParseOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<ArticleType>(value, ignoreCase: true, out var type) && Enum.IsDefined(type))
        {
            return type;
        }

        throw new Abstractions.ValidationException(
            $"Unknown article type '{value}'. Allowed values: {string.Join(", ", Enum.GetNames<ArticleType>())}.");
    }
}

/// <inheritdoc cref="ArticleTypeParser"/>
public static class ArticleVisibilityParser
{
    public static ArticleVisibility Parse(string? value) =>
        ParseOptional(value)
        ?? throw new Abstractions.ValidationException(
            "An article visibility is required. Allowed values: " +
            $"{string.Join(", ", Enum.GetNames<ArticleVisibility>())}.");

    /// <summary>Null and whitespace mean "no filter", which is not an error.</summary>
    public static ArticleVisibility? ParseOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<ArticleVisibility>(value, ignoreCase: true, out var visibility) &&
            Enum.IsDefined(visibility))
        {
            return visibility;
        }

        throw new Abstractions.ValidationException(
            $"Unknown article visibility '{value}'. Allowed values: " +
            $"{string.Join(", ", Enum.GetNames<ArticleVisibility>())}.");
    }
}
