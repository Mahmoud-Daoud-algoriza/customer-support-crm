namespace SupportCrm.Domain.Modules.Knowledge;

/// <summary>
/// Who an article is for — docs/data-model.md §2.13.
/// <para>
/// <b><see cref="Public"/> does not mean visible.</b> Visibility and publication are two independent
/// facts: a portal read requires <see cref="Public"/> <em>and</em>
/// <see cref="KnowledgeArticle.IsPublished"/> (§5 constraint 19). The pair is enforced in exactly one
/// place, <c>PortalArticleService.PortalVisible</c>.
/// </para>
/// </summary>
public enum ArticleVisibility
{
    /// <summary>Reachable from the customer portal, once published.</summary>
    Public,

    /// <summary>Staff only. Never leaves a staff surface, published or not.</summary>
    Internal,
}
