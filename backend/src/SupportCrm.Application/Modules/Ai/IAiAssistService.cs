namespace SupportCrm.Application.Modules.Ai;

/// <summary>
/// <b>The one AI seam</b> — every AI feature in requirements §7 sits behind this interface
/// (docs/architecture.md §5.1, T1-F). Declared in Application, implemented in Infrastructure
/// (AD-11), which is what makes the implementation swappable by configuration.
///
/// <h3>A-8 is a property of this file, not a discipline someone remembers</h3>
/// <b>Every method returns a suggestion record and nothing else.</b> There is no
/// <c>SendReplyAsync</c>, no <c>ApplyCategoryAsync</c>, no <c>AssignAsync</c>, and <b>no ticket id
/// parameter anywhere</b> — so there is nothing for an implementation to write back to even if it
/// wanted to. AD-12 states the reasoning: <em>"a general 'AI action' interface would make autonomous
/// behaviour a one-line mistake away."</em>
///
/// <para>
/// <c>AiSeamShapeTests</c> asserts this by reflection, so adding an action method fails the build
/// rather than passing review.
/// </para>
///
/// <h3>The seam never queries the database</h3>
/// <see cref="AiThreadContext"/> is <b>assembled by the caller from data it already has</b>. An
/// implementation is handed text; it cannot reach a ticket it was not given, cannot widen its own
/// scope, and cannot see another department's data through this interface.
///
/// <h3>Which provider is deliberately unanswered</h3>
/// Product-scope §9 question 1 — the provider, and whether data-residency limits apply to sending
/// customer content — is <b>open</b>. This seam is what lets it stay open: nothing here names a
/// provider, a model or a provider parameter (api-design §8.1, AP-12).
///
/// <h3>Failure is contained</h3>
/// An unavailable provider throws <see cref="AiUnavailableException"/>, which the Story 01 handler
/// maps to <c>503 ai-unavailable</c>. It degrades <b>one feature</b>: ticket creation, replies and
/// status changes continue, which <c>AiOutageDoesNotBlockWorkTests</c> proves at the seam.
///
/// <h3>What does not belong here</h3>
/// <b>Suggested solutions (§7.4) do not use this seam</b> — they are keyword retrieval in the
/// Knowledge module (AD-13, Story 12). The future chatbot (T3-C) would be a <b>consumer</b> of this
/// interface, not a new seam; see this module's README.
/// </summary>
public interface IAiAssistService
{
    /// <summary>A short summary of a ticket thread, generated on demand (§7.1).</summary>
    Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct);

    /// <summary>
    /// A draft reply for an agent to <b>edit before sending</b> (§7.2, A-8). It is a draft. Nothing
    /// in this interface can send it.
    /// </summary>
    Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct);

    /// <summary>
    /// A suggested category and priority at creation (§7.3), <b>overridable by the agent</b>. The
    /// suggestion and its acceptance or override are recorded in ticket history (A-8, DM-5) — by the
    /// caller, because this seam persists nothing.
    /// </summary>
    Task<AiClassification> SuggestClassificationAsync(
        AiClassificationRequest request, CancellationToken ct);
}
