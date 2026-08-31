using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Modules.Ai;

/// <summary>
/// <c>POST /tickets/{id}/ai/summary</c> — docs/api-design.md §6.10.
/// <c>{ summary, generatedBy, generatedAt }</c>.
/// </summary>
public sealed record AiSummaryDto(string Summary, string GeneratedBy, DateTimeOffset GeneratedAt);

/// <summary>
/// <c>POST /tickets/{id}/ai/suggested-reply</c> — §6.10.
/// <c>{ draft, generatedBy, generatedAt }</c>.
///
/// <para>
/// <b>It is called <c>draft</c>, not <c>message</c> or <c>body</c></b>, and the name is doing work:
/// no endpoint accepts this shape, so there is nothing a client could post it back to. The agent's
/// composer sends ordinary text through <c>POST /tickets/{id}/messages</c> (A-8, UI-7).
/// </para>
/// </summary>
public sealed record AiSuggestedReplyDto(string Draft, string GeneratedBy, DateTimeOffset GeneratedAt);

/// <summary>
/// <c>POST /ai/classification-suggestion</c> — §6.10.
/// <c>{ categoryCode, priority, generatedBy, generatedAt }</c>.
///
/// <para>
/// <c>priority</c> is a <b>string</b> because that is what the contract publishes and what the ticket
/// endpoints accept — the client pre-selects it in the same selector an agent would use, and the four
/// values are fixed by A-6.
/// </para>
/// </summary>
public sealed record AiClassificationDto(
    string CategoryCode, string Priority, string GeneratedBy, DateTimeOffset GeneratedAt);

/// <summary>
/// The request body of <c>POST /ai/classification-suggestion</c> — docs/api-design.md §6.11.
///
/// <para>
/// <b>There is no ticket id, and that is the point</b>: the endpoint is callable <em>before a ticket
/// exists</em>, which is what "suggest at creation" requires (§5.8).
/// </para>
///
/// <para>
/// <b><c>isUrgent</c> is optional and is an input, never an outcome</b> (A-17). It may inform the
/// suggested priority; it does not set a priority, and the ticket endpoints still refuse it on a staff
/// creation body (AP-10).
/// </para>
/// </summary>
public sealed record AiClassificationSuggestionRequest
{
    [Required, MaxLength(200)]
    public string? Subject { get; init; }

    [Required, MaxLength(5000)]
    public string? Description { get; init; }

    public bool? IsUrgent { get; init; }
}
