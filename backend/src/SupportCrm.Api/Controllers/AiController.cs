using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Modules.Ai;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The three AI assist endpoints of docs/api-design.md §5.8 — and <b>only</b> those three.
///
/// <h3>All three are <c>POST</c>, and none of them mutates a ticket</h3>
/// <c>POST</c> <b>because they perform work</b>, not because they change anything (§5.8). There is no
/// <c>PATCH</c> here, no persistence call, and no side effect —
/// <c>AiAssistEndpointTests</c> snapshots the ticket row before and after each call to prove it. If a
/// future change needs an AI path that writes, that is a contract decision, not a controller edit
/// (AD-12).
///
/// <h3><c>503 ai-unavailable</c> comes from the one handler</h3>
/// Nothing here catches <c>AiUnavailableException</c>. The Story 01 Problem Details handler maps it,
/// so there is a single mapping and no per-endpoint code (AP-12) — and <c>503</c> is the only place in
/// this API that status appears.
///
/// <h3>Two routes on a ticket, one that needs none</h3>
/// Summary and suggested reply hang off <c>/tickets/{id}</c> because they read a thread.
/// <c>classification-suggestion</c> **takes no ticket id and is callable before a ticket exists** —
/// which is exactly what "suggest at creation" requires, so it sits at the root of <c>/ai</c>.
///
/// <h3>What must never be added here</h3>
/// <b>Suggested solutions (§7.4) do not belong under <c>/ai</c></b> — <b>AP-14</b> puts them under
/// Knowledge as <c>GET /tickets/{id}/suggested-articles</c> (Story 12), because they <em>retrieve</em>
/// rather than generate. Adding <c>/ai/suggested-solutions</c> would move a keyword search behind a
/// provider call and a running cost.
///
/// <h3>Staff only</h3>
/// <c>RequireAgent</c>: a Customer gets <c>403</c>, a capability denial they can infer from their own
/// role, so AP-4's <c>404</c> rule does not apply (§4.2). <b>A-8 excludes customer-facing generation
/// entirely</b> — there is no portal variant of any of these, and there must not be one.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize(Policy = AuthorizationPolicies.RequireAgent)]
public sealed class AiController(TicketAiAssistService assists) : ControllerBase
{
    /// <summary>
    /// §7.1 — a summary of the ticket's thread. <b>Scoped first</b>: an out-of-department ticket is
    /// <c>404</c> before any provider call, so customer content never leaves the process for a ticket
    /// the caller may not read.
    /// </summary>
    [HttpPost("tickets/{id:guid}/ai/summary")]
    [ProducesResponseType<AiSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiSummaryDto>> Summarize(Guid id, CancellationToken ct) =>
        Ok(await assists.SummarizeAsync(id, ct));

    /// <summary>
    /// §7.2 — a draft reply. <b>It creates no message</b>: the draft goes to the agent's composer, and
    /// sending remains <c>POST /tickets/{id}/messages</c> pressed by a person (A-8, UI-7).
    /// </summary>
    [HttpPost("tickets/{id:guid}/ai/suggested-reply")]
    [ProducesResponseType<AiSuggestedReplyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiSuggestedReplyDto>> SuggestReply(Guid id, CancellationToken ct) =>
        Ok(await assists.SuggestReplyAsync(id, ct));

    /// <summary>
    /// §7.3 — a suggested category and priority, <b>before the ticket exists</b>. The suggestion is
    /// advisory: the agent may override both, and a category outside the configured list is corrected
    /// server-side before the response leaves (§5.8).
    /// </summary>
    [HttpPost("ai/classification-suggestion")]
    [ProducesResponseType<AiClassificationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AiClassificationDto>> SuggestClassification(
        [FromBody] AiClassificationSuggestionRequest request, CancellationToken ct) =>
        Ok(await assists.SuggestClassificationAsync(request, ct));
}
