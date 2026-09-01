namespace SupportCrm.Domain.Modules.Knowledge;

/// <summary>
/// <b>T2-E: one concept with a type, not three subsystems.</b> Requirements §6 asks for FAQs, help
/// articles and solution guides; docs/data-model.md §2.13 models them as a single
/// <c>KnowledgeArticle</c> distinguished by this field, and docs/product-scope.md T2-E records that
/// simplification as a decision rather than an omission.
/// <para>
/// Persisted as a stable string code, never an ordinal (docs/api-design.md §2), so reordering these
/// members cannot silently reinterpret stored rows.
/// </para>
/// </summary>
public enum ArticleType
{
    Faq,
    HelpArticle,
    SolutionGuide,
}
