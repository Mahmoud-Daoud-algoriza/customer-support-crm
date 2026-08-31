using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Ai;

/// <summary>
/// The thread as the AI seam sees it — <b>text the caller already had</b>, never a ticket id and
/// never a query. An implementation is handed this and can reach nothing else
/// (docs/architecture.md §5.1).
/// </summary>
/// <param name="Subject">The ticket subject.</param>
/// <param name="Description">The originating description (docs/data-model.md §2.6).</param>
/// <param name="Messages">The thread, oldest first.</param>
/// <param name="IsUrgent">
/// The customer's own urgency indication. <b>It is an input, not a priority</b> (A-17) — a seam may
/// weigh it when suggesting one, and nothing here sets a priority.
/// </param>
public sealed record AiThreadContext(
    string Subject,
    string Description,
    IReadOnlyList<AiMessage> Messages,
    bool IsUrgent);

/// <summary>
/// One message in the context. <b>The author is a role, not a person</b> — the seam needs to know
/// whether the customer or an agent spoke last, and it does not need a name or an id to do it. That
/// is the *"without logging customer content beyond what is necessary"* rule applied to the shape of
/// the input rather than only to the logging.
/// </summary>
public sealed record AiMessage(string AuthorRole, string Body, DateTimeOffset PostedAt);

/// <summary>
/// The input for a classification at creation — <b>no thread, because there is none yet</b> (§7.3).
/// </summary>
public sealed record AiClassificationRequest(string Subject, string Description, bool IsUrgent);

/// <summary>
/// A generated summary. <b>Not stored</b> (DM-5): there is no AI entity, and re-summarizing is
/// cheaper than a table that would drift from the thread it describes.
/// </summary>
public sealed record AiSummary(string Summary, DateTimeOffset GeneratedAt);

/// <summary>
/// A generated draft. <b>A draft, not a send</b> (A-8) — it reaches the agent's composer through the
/// one insertion point the quick replies use (UI-7), and the agent presses Send.
/// </summary>
public sealed record AiSuggestedReply(string Draft, DateTimeOffset GeneratedAt);

/// <summary>
/// A suggested category and priority. <b>Advisory</b>: the agent may override it, and the suggestion
/// plus its acceptance or override is what gets persisted — as ticket history, not as an AI row
/// (A-8, DM-5).
/// </summary>
/// <param name="CategoryCode">
/// A code from the <b>configured</b> category list (A-6). A seam that invented a code would produce
/// a ticket no department maps to (A-14), so an implementation must choose from configuration.
/// </param>
public sealed record AiClassification(
    string CategoryCode,
    TicketPriority Priority,
    DateTimeOffset GeneratedAt);
