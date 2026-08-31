using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Ai;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Seams.Ai;

/// <summary>
/// <b>The real-provider adapter</b> — the other side of the seam (docs/architecture.md §5.1). It is
/// selected only when <c>SupportCrm:Ai:Provider = Provider</c>; the fake is the default (A-7).
///
/// <h3>No provider is named, here or anywhere in the contract</h3>
/// The endpoint and model are <b>opaque configuration</b>. Which provider they point at — and whether
/// data-residency limits apply to sending customer content — is <b>product-scope §9 question 1 and
/// stays open</b>. This file is where provider specifics would live, and it deliberately contains
/// none: the request shape below is the widely-used chat-completions form rather than any one
/// vendor's extension.
///
/// <h3>Failure is contained, and only one exception type crosses the seam</h3>
/// A timeout, a non-success status, an unreadable body or a missing field all become
/// <see cref="AiUnavailableException"/>. <b>No provider or transport exception bubbles into
/// Application</b> — the layer above learns "AI is unavailable" and nothing about how, which is what
/// keeps the provider question open and keeps vendor exception types out of the contract.
///
/// <h3>A missing credential is a construction failure, not a runtime surprise</h3>
/// The constructor throws if the key or endpoint is absent. Calling a provider anonymously would
/// produce a confusing <c>401</c> on the first agent who pressed <em>Summarize</em>; failing at
/// startup produces a sentence an operator can act on.
///
/// <h3>Logging records what happened, not what was said</h3>
/// The capability, the outcome and the duration are logged. <b>The ticket body is never logged</b> —
/// lengths and message counts are, which is enough to diagnose a bad request without writing customer
/// content into a log file that has a different audience and a different retention (intake AC).
/// </summary>
public sealed class ProviderAiService : IAiAssistService
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProviderAiService> _logger;

    public ProviderAiService(
        HttpClient http,
        IOptions<AiOptions> options,
        TimeProvider clock,
        ILogger<ProviderAiService> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        // Fail to construct rather than call anonymously. Startup validation catches this first in
        // the composition root; this is the guarantee for anyone who constructs the type directly.
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            throw new InvalidOperationException(
                $"{AiOptions.SectionName}:Endpoint is required when the AI provider is enabled.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                $"{AiOptions.SectionName}:ApiKey is required when the AI provider is enabled. " +
                "Supply it through the environment; it is never committed.");
        }

        _http = http;
        _clock = clock;
        _logger = logger;

        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = await CompleteAsync(
            "summarize",
            "Summarize this support ticket thread in two or three sentences for an agent.",
            Render(context),
            ct);

        return new AiSummary(text, _clock.GetUtcNow());
    }

    public async Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = await CompleteAsync(
            "suggest-reply",
            "Draft a reply an agent will review and edit before sending. Do not promise dates.",
            Render(context),
            ct);

        return new AiSuggestedReply(text, _clock.GetUtcNow());
    }

    public async Task<AiClassification> SuggestClassificationAsync(
        AiClassificationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var text = await CompleteAsync(
            "classify",
            "Reply with a category code and a priority (Low, Medium, High, Urgent) separated by a "
            + "single space, and nothing else.",
            $"Subject: {request.Subject}\nDescription: {request.Description}\n"
            + $"Customer marked urgent: {request.IsUrgent}",
            ct);

        var parts = text.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        // A provider that answers in an unexpected shape is an unavailable capability, not a
        // half-applied suggestion — the alternative is guessing a category the agent then has to
        // notice and undo.
        if (parts.Length < 2 || !Enum.TryParse<TicketPriority>(parts[1], ignoreCase: true, out var priority))
        {
            throw new AiUnavailableException(
                "The AI provider returned a classification that could not be interpreted.");
        }

        return new AiClassification(parts[0], priority, _clock.GetUtcNow());
    }

    /// <summary>
    /// One provider call, with every failure mode funnelled into
    /// <see cref="AiUnavailableException"/>.
    /// </summary>
    private async Task<string> CompleteAsync(
        string capability, string instruction, string content, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            using var response = await _http.PostAsJsonAsync(
                _options.Endpoint,
                new
                {
                    model = _options.Model,
                    messages = new[]
                    {
                        new { role = "system", content = instruction },
                        new { role = "user", content },
                    },
                },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                // The status is logged; the body is not, because a provider error body can echo the
                // request — which is the customer content this must not write to a log.
                _logger.LogWarning(
                    "AI {Capability} failed with status {Status} after {ElapsedMs}ms.",
                    capability, (int)response.StatusCode, ElapsedMs(started));

                throw new AiUnavailableException(
                    $"The AI provider answered {(int)response.StatusCode}.");
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            var text = body
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiUnavailableException("The AI provider returned an empty response.");
            }

            // Lengths, not content.
            _logger.LogInformation(
                "AI {Capability} succeeded in {ElapsedMs}ms; {PromptChars} chars in, {ResultChars} out.",
                capability, ElapsedMs(started), content.Length, text.Length);

            return text.Trim();
        }
        catch (AiUnavailableException)
        {
            // Already the seam's own type. Re-thrown rather than re-wrapped so the message survives.
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller gave up — the request was abandoned, not the provider failing. This must
            // stay a cancellation, or a user navigating away would be reported as an AI outage.
            throw;
        }
        catch (Exception ex)
        {
            // Everything else — timeout, DNS, TLS, malformed JSON, a missing field. One type crosses
            // the seam, and the provider's exception type is not it.
            _logger.LogWarning(
                ex, "AI {Capability} failed after {ElapsedMs}ms.", capability, ElapsedMs(started));

            throw new AiUnavailableException("The AI provider could not be reached.", ex);
        }
    }

    private static long ElapsedMs(long startedAt) =>
        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    /// <summary>
    /// The thread as prompt text. <b>Roles, not names</b> — the context carries no identity, so none
    /// can be sent.
    /// </summary>
    private static string Render(AiThreadContext context)
    {
        var lines = context.Messages.Select(m => $"{m.AuthorRole}: {m.Body}");

        return $"Subject: {context.Subject}\nDescription: {context.Description}\n"
            + $"Customer marked urgent: {context.IsUrgent}\n"
            + string.Join("\n", lines);
    }
}
