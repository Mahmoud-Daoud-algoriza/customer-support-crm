using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Ai;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Seams.Ai;

/// <summary>
/// <b>The deterministic offline fake</b> — and it is a hard requirement, not a convenience. A-7
/// requires the AI integration to degrade to an offline fake; product-scope §10 item 5 requires the
/// whole system to run with <b>no external accounts or credentials</b>. The intake puts it plainly:
/// <em>"if the AI provider is unavailable on demo day, the fake IS the demo, and that is by
/// design."</em>
///
/// <h3>Deterministic by construction, not by luck</h3>
/// Every output is derived from <b>SHA-256 of the input text</b>. There is no <c>Random</c>, no
/// <c>DateTime.Now</c> in any decision, no <c>Guid.NewGuid</c> and no ambient state — so the same
/// ticket produces the same suggestion on every run, on every machine, in every process. That is what
/// makes a demo repeatable and a test assertable.
///
/// <para>
/// <b>The one non-deterministic field is <c>GeneratedAt</c></b>, which is a timestamp rather than part
/// of the suggestion. It comes from an injected <see cref="TimeProvider"/>, so even that is
/// controllable in a test.
/// </para>
///
/// <h3>It makes no network call and reads no credential</h3>
/// This type's constructor takes <b>no HTTP dependency of any kind</b>, and a test asserts that by
/// reflection — the guarantee is checkable rather than promised. The type name is not written out
/// here: the plan's verification greps this folder for it and expects only the provider adapter to
/// match, so naming it in a comment would make that check report a hit forever.
///
/// <h3>Obviously extractive, which is the honest thing for a fake to be</h3>
/// The summary quotes the description and counts the thread; it does not pretend to be generated
/// prose. A reviewer should be able to tell at a glance that no model ran — a fake that imitated a
/// model's voice would be a more impressive demo and a worse one.
/// </summary>
public sealed class DeterministicFakeAiService(
    IOptions<CategoryOptions> categories, TimeProvider clock) : IAiAssistService
{
    /// <summary>How much of the description the summary quotes. Long enough to be useful in a demo.</summary>
    private const int SummaryExcerptLength = 180;

    /// <summary>
    /// Neutral drafts, chosen by hash. <b>Each is a starting point an agent edits</b>, which is why
    /// none of them promises anything, commits to a date, or admits fault — a canned draft that did
    /// would be a liability the moment an agent sent it unread.
    /// </summary>
    private static readonly string[] ReplyTemplates =
    [
        "Thank you for the details. I have reviewed the information on this ticket and am looking into it now. I will follow up as soon as I have something concrete to share.",
        "Thanks for getting in touch about this. I can see what you have described and I am investigating. Could you confirm whether the problem is still happening at your end?",
        "I appreciate your patience on this one. I have gone through the history on this ticket and am picking it up now. I will update you with what I find.",
        "Thank you for reporting this. I have the details you provided and am checking them against our records. I will come back to you shortly with next steps.",
    ];

    public Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        _ = ct;

        var excerpt = Truncate(context.Description, SummaryExcerptLength);

        var lastAuthor = context.Messages.Count > 0
            ? context.Messages[^1].AuthorRole
            : "nobody yet";

        // Extractive and plainly so: the subject, an excerpt, the thread size and who spoke last.
        var summary =
            $"\"{context.Subject}\" — {excerpt} " +
            $"[{context.Messages.Count} message(s) on the thread; last from {lastAuthor}" +
            (context.IsUrgent ? "; the customer marked this urgent" : string.Empty) +
            "]";

        return Task.FromResult(new AiSummary(summary, clock.GetUtcNow()));
    }

    public Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        _ = ct;

        // The template is chosen by the hash of the thread, so the same ticket always offers the same
        // draft — an agent who reloads the screen does not get a different suggestion.
        var index = (int)(HashOf(context.Subject, context.Description) % (uint)ReplyTemplates.Length);

        return Task.FromResult(new AiSuggestedReply(ReplyTemplates[index], clock.GetUtcNow()));
    }

    public Task<AiClassification> SuggestClassificationAsync(
        AiClassificationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = ct;

        // **From the CONFIGURED list** (A-6): a code this seam invented would produce a ticket no
        // department maps to (A-14). If configuration somehow holds none, there is nothing honest to
        // suggest and the capability reports unavailable rather than guessing a code.
        var configured = categories.Value.Items;

        if (configured.Count == 0)
        {
            throw new AiUnavailableException(
                "No categories are configured, so no classification can be suggested.");
        }

        var hash = HashOf(request.Subject, request.Description);

        var category = configured[(int)(hash % (uint)configured.Count)].Code;

        // **`isUrgent` is one input to the suggested priority, and that is exactly what A-17
        // permits** — the customer's indication may inform a suggestion; it does not set a priority.
        // An urgent flag lifts the suggestion into the top two levels; otherwise the hash picks
        // across the lower two. The agent overrides either way.
        var priority = request.IsUrgent
            ? (hash % 2 == 0 ? TicketPriority.High : TicketPriority.Urgent)
            : (hash % 2 == 0 ? TicketPriority.Low : TicketPriority.Medium);

        return Task.FromResult(new AiClassification(category, priority, clock.GetUtcNow()));
    }

    /// <summary>
    /// A stable hash of the input text. <b>SHA-256 rather than <c>string.GetHashCode</c></b>: the
    /// latter is randomized per process by default, which would make this fake non-deterministic
    /// across runs in precisely the way that is hardest to notice.
    /// </summary>
    private static uint HashOf(string first, string second)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{first}{second}"));

        return BitConverter.ToUInt32(bytes, 0);
    }

    private static string Truncate(string value, int length)
    {
        var collapsed = value.ReplaceLineEndings(" ").Trim();

        return collapsed.Length <= length ? collapsed : collapsed[..length].TrimEnd() + "…";
    }
}
