using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Ai;

/// <summary>
/// The three agent-facing assists of requirements §7.1–7.3, each consuming Story 10's seam
/// (docs/api-design.md §5.8, §6.10).
///
/// <h3>Nothing here mutates a ticket</h3>
/// <b>No method writes a column, creates a message, changes a status or assigns anyone.</b> All three
/// are <c>POST</c> because they perform work, not because they change anything (§5.8) — and
/// <c>AiAssistEndpointTests</c> snapshots the ticket row before and after each assist to prove it.
///
/// <h3>Scoping happens before any provider call</h3>
/// <b><see cref="TicketScope.LoadScopedAsync"/> first, always.</b> An out-of-department ticket is a
/// <c>404</c> <em>before</em> the seam is touched, so **customer content never leaves the process for a
/// ticket the caller may not read** (architecture §4.3 point 2). That ordering is the security
/// property, not an optimization, and a test asserts the seam was never invoked.
///
/// <h3>Nothing is persisted</h3>
/// <b>DM-5.</b> Summaries and drafts are generated on demand; there is no AI entity and no ticket
/// field pretending to hold authored content. Re-summarizing is cheaper than a table that would drift
/// from the thread it describes.
///
/// <h3><see cref="AiUnavailableException"/> is not caught here</h3>
/// It propagates to the Story 01 Problem Details handler, which maps it to <b><c>503
/// ai-unavailable</c></b> — one mapping, no per-endpoint code (AP-12). Catching it here to return a
/// friendlier shape would put a second answer to the same question in the codebase.
/// </summary>
public sealed class TicketAiAssistService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAiAssistService ai,
    IOptions<CategoryOptions> categories)
{
    /// <summary>§7.1 — a summary of the thread, on demand. Read-only, stored nowhere.</summary>
    public async Task<AiSummaryDto> SummarizeAsync(Guid ticketId, CancellationToken ct)
    {
        var context = await BuildContextAsync(ticketId, ct);

        var result = await ai.SummarizeThreadAsync(context, ct);

        return new AiSummaryDto(result.Summary, GeneratedByAi, result.GeneratedAt);
    }

    /// <summary>
    /// §7.2 — a draft for the agent to edit. <b>It does not create a <c>TicketMessage</c></b>: the
    /// draft reaches the composer and the agent presses Send (A-8, UI-7).
    /// </summary>
    public async Task<AiSuggestedReplyDto> SuggestReplyAsync(Guid ticketId, CancellationToken ct)
    {
        var context = await BuildContextAsync(ticketId, ct);

        var result = await ai.SuggestReplyAsync(context, ct);

        return new AiSuggestedReplyDto(result.Draft, GeneratedByAi, result.GeneratedAt);
    }

    /// <summary>
    /// §7.3 — a suggested category and priority, <b>callable before a ticket exists</b>, which is what
    /// "suggest at creation" requires. It takes no ticket id, so there is nothing to scope and nothing
    /// to mutate.
    ///
    /// <para>
    /// <b>An out-of-range suggestion never reaches the client</b> (§5.8): the category is checked
    /// against the configured list, and a miss falls back to the first configured category rather than
    /// returning a code no department maps to (A-14). The priority is validated by its own type — the
    /// seam returns a <see cref="TicketPriority"/>, so an invalid one is unrepresentable.
    /// </para>
    /// </summary>
    public async Task<AiClassificationDto> SuggestClassificationAsync(
        AiClassificationSuggestionRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await ai.SuggestClassificationAsync(
            new AiClassificationRequest(request.Subject!, request.Description!, request.IsUrgent ?? false),
            ct);

        var configured = categories.Value.Items;

        // Validated **server-side, before the response leaves the API**. A client must never receive a
        // suggestion it cannot act on — it would render a selector with a value not in its own options.
        var match = configured.FirstOrDefault(c =>
            string.Equals(c.Code, result.CategoryCode, StringComparison.OrdinalIgnoreCase));

        var categoryCode = match?.Code ?? configured[0].Code;

        return new AiClassificationDto(
            categoryCode, result.Priority.ToString(), GeneratedByAi, result.GeneratedAt);
    }

    /// <summary>
    /// The value every AI response carries (docs/api-design.md §6.10), so a client can label the
    /// result without inferring anything from which endpoint it called (A-8, UI-6).
    /// </summary>
    private const string GeneratedByAi = "ai";

    /// <summary>
    /// Loads the ticket <b>through the scope</b> and assembles the context from data already read —
    /// the seam never queries the database itself.
    /// </summary>
    private async Task<AiThreadContext> BuildContextAsync(Guid ticketId, CancellationToken ct)
    {
        // Scoped FIRST. Out of department is 404 here, before any provider interaction.
        var ticket = await db.Tickets.AsNoTracking().LoadScopedAsync(ticketId, currentUser, ct);

        // Roles, not names: the seam needs to know who spoke last, not who they are.
        var messages = await db.TicketMessages
            .AsNoTracking()
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.PostedAt)
            .Select(m => new AiMessage(m.Direction.ToString(), m.Body, m.PostedAt))
            .ToListAsync(ct);

        return new AiThreadContext(ticket.Subject, ticket.Description, messages, ticket.IsUrgent);
    }
}
