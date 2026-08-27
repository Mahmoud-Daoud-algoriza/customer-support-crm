using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The customer-satisfaction rating scale (docs/architecture.md §6.3, closing PF-3/B-1), published
/// by <c>GET /config</c> and validated server-side by Story 13's feedback endpoint.
///
/// <para>
/// <b>⚠ OQ-1 IS OPEN. THE KEY IS APPROVED; THE VALUES ARE NOT.</b>
/// </para>
/// <para>
/// docs/architecture.md §6.3: *"The boundary values are deliberately not decided here — <b>OQ-1 is
/// open</b> (product-scope §9, data-model §8). The key exists so the contract has a home for the
/// answer; inventing the answer is out of scope."* docs/api-design.md §6.9 says the same of the
/// values it shows: *"illustrative placeholders, not a decision."*
/// </para>
/// <para>
/// <b>What that means for code:</b> whatever <c>appsettings.json</c> holds is a <b>placeholder</b>,
/// commented as such at the key. Startup validation checks only that <c>Min &lt; Max</c> — a
/// structural check that <b>asserts nothing about which values are correct</b>. Story 13 renders the
/// control from these values (an ordinal range is a rating scale; a binary scale is two buttons —
/// docs/ui-design.md §11 forbids hardcoding either).
/// </para>
/// <para>
/// <b>No <c>min</c> or <c>max</c> constant may appear anywhere else in the codebase</b> — not in a
/// schema, not in the domain, not in a service, not in the UI. Everything reads it from here, so
/// answering OQ-1 is a configuration edit rather than a hunt.
/// </para>
/// </summary>
public sealed class FeedbackOptions
{
    public const string SectionName = "SupportCrm:Feedback:RatingScale";

    /// <summary>Lowest selectable rating. <b>Placeholder — OQ-1.</b></summary>
    public int Min { get; init; }

    /// <summary>Highest selectable rating. <b>Placeholder — OQ-1.</b></summary>
    public int Max { get; init; }
}
