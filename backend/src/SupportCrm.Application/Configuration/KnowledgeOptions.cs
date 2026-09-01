using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The one knowledge-base setting: how many suggested articles a ticket view receives
/// (<c>GET /tickets/{id}/suggested-articles</c>, requirements §7.4).
///
/// <para>
/// <b>Why it is configuration.</b> The Story 12 plan (task 4) specifies the retrieval size as
/// <em>"the top N (default 5, configurable)"</em>. It is presentation volume, not a product rule: no
/// approved document fixes the number, and it is the kind of value that must not become a constant
/// inside a service. Changing it is a redeploy, like every other setting (T2-I).
/// </para>
///
/// <para>
/// <b>It is not published to any client.</b> No configuration tier returns it (AP-17,
/// docs/api-design.md §5.1) — a caller receives the suggestions, not the size of the list they came
/// from.
/// </para>
///
/// <para>
/// <b>Nothing here is a relevance parameter.</b> AD-13 excludes tuning, and there is deliberately no
/// weight, threshold or synonym key in this section — the ranking lives in
/// <c>ArticleSearch</c> as two constants, where it can be read rather than configured.
/// </para>
/// </summary>
public sealed class KnowledgeOptions
{
    public const string SectionName = "SupportCrm:Knowledge";

    /// <summary>How many articles <c>GET /tickets/{id}/suggested-articles</c> returns at most.</summary>
    [Range(1, 50)] public int SuggestedArticleCount { get; init; } = 5;
}
