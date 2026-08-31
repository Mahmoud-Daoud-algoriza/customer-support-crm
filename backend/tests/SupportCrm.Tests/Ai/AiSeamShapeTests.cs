using System.Net;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Ai;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Ai;

/// <summary>
/// <b>The tests that make A-8's guardrails checkable rather than remembered.</b>
///
/// <para>
/// AD-12 is the reasoning: <em>"a general 'AI action' interface would make autonomous behaviour a
/// one-line mistake away."</em> The first test below is what turns that from a review comment into a
/// build failure.
/// </para>
/// </summary>
public sealed class AiSeamShapeTests
{
    /// <summary>The only return types A-8 permits: suggestions, and nothing that performs anything.</summary>
    private static readonly Type[] SuggestionTypes =
        [typeof(AiSummary), typeof(AiSuggestedReply), typeof(AiClassification)];

    /// <summary>
    /// <b>The AD-12 test.</b> Every method on the seam returns one of the three suggestion records,
    /// and no method name suggests an action. Adding <c>SendReplyAsync</c> — or a ticket id parameter
    /// to write back to — fails here.
    /// </summary>
    [Fact]
    public void The_seam_exposes_no_method_that_could_take_an_action()
    {
        var methods = typeof(IAiAssistService).GetMethods();

        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            // Every method is async and returns Task<one of the three suggestion records>.
            Assert.True(
                method.ReturnType.IsGenericType
                && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>),
                $"{method.Name} must return a Task<T> carrying a suggestion.");

            var payload = method.ReturnType.GetGenericArguments()[0];

            Assert.Contains(payload, SuggestionTypes);

            // No action verbs. A method that could send, assign or transition would be the mistake
            // AD-12 names, and it is unspellable if this assertion holds.
            Assert.DoesNotContain(
                method.Name,
                new[] { "Send", "Assign", "Transition", "Update", "Apply", "Post", "Create", "Delete" },
                StringComparer.OrdinalIgnoreCase);

            foreach (var forbidden in new[] { "Send", "Assign", "Transition", "Update", "Apply", "Post" })
            {
                Assert.False(
                    method.Name.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                    $"{method.Name} looks like an action. The AI seam may only return suggestions (A-8, AD-12).");
            }

            // **No ticket id anywhere**: there is nothing for an implementation to write back to.
            foreach (var parameter in method.GetParameters())
            {
                Assert.False(
                    parameter.Name?.Contains("ticketId", StringComparison.OrdinalIgnoreCase) == true,
                    $"{method.Name} takes a ticket id. The seam is handed text, never a row to mutate.");
            }
        }
    }

    /// <summary>
    /// <b>The fake makes no network call, and this is the checkable form of that claim.</b> A test
    /// asserting the constructor takes no <c>HttpClient</c> is harder to defeat by accident than a
    /// comment saying it does not.
    /// </summary>
    [Fact]
    public void The_fake_has_no_http_dependency()
    {
        var parameters = typeof(SupportCrm.Infrastructure.Seams.Ai.DeterministicFakeAiService)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(HttpClient), parameters);
        Assert.DoesNotContain(typeof(IHttpClientFactory), parameters);
    }

    /// <summary>
    /// <b>Deterministic across separate instances, not merely within one.</b> Two service objects,
    /// called twice each, produce byte-identical suggestions — which is what makes a demo repeatable
    /// and rules out any cached or ambient state.
    /// </summary>
    [Fact]
    public async Task The_fake_is_byte_identical_for_identical_input()
    {
        var context = new AiThreadContext(
            "Card declined at checkout",
            "Every payment attempt is declined at the final step.",
            [new AiMessage("Customer", "It is still failing.", DateTimeOffset.UnixEpoch)],
            IsUrgent: true);

        var request = new AiClassificationRequest(context.Subject, context.Description, context.IsUrgent);

        var first = NewFake();
        var second = NewFake();

        var summaries = new List<string>();
        var replies = new List<string>();
        var classifications = new List<string>();

        foreach (var service in new[] { first, first, second, second })
        {
            summaries.Add((await service.SummarizeThreadAsync(context, default)).Summary);
            replies.Add((await service.SuggestReplyAsync(context, default)).Draft);

            var classification = await service.SuggestClassificationAsync(request, default);

            classifications.Add($"{classification.CategoryCode}|{classification.Priority}");
        }

        // One distinct value each, across four calls on two instances.
        Assert.Single(summaries.Distinct(StringComparer.Ordinal));
        Assert.Single(replies.Distinct(StringComparer.Ordinal));
        Assert.Single(classifications.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    /// Different input must actually produce different output — otherwise "deterministic" would be
    /// satisfied by a constant, and the test above would pass on a seam that ignored its input.
    /// </summary>
    [Fact]
    public async Task The_fake_distinguishes_different_input()
    {
        var fake = NewFake();

        var one = await fake.SummarizeThreadAsync(Context("Card declined"), default);
        var two = await fake.SummarizeThreadAsync(Context("Sign-in loop"), default);

        Assert.NotEqual(one.Summary, two.Summary);
    }

    /// <summary>
    /// <b>The suggested category always comes from configuration</b> (A-6): a code the seam invented
    /// would produce a ticket no department maps to (A-14).
    /// </summary>
    [Fact]
    public async Task The_fake_suggests_only_configured_categories()
    {
        var categories = Categories();
        var fake = NewFake();

        // Enough distinct inputs to exercise the hash across the configured list.
        foreach (var n in Enumerable.Range(0, 25))
        {
            var result = await fake.SuggestClassificationAsync(
                new AiClassificationRequest($"Subject {n}", $"Description {n}", n % 2 == 0), default);

            Assert.Contains(result.CategoryCode, categories.Value.Items.Select(c => c.Code));
        }
    }

    /// <summary>
    /// <b>A provider timeout becomes <c>AiUnavailableException</c></b> — not a
    /// <c>TaskCanceledException</c>, not an <c>HttpRequestException</c>. One type crosses the seam, so
    /// the layer above never learns how the provider failed (AP-12).
    /// </summary>
    [Fact]
    public async Task A_provider_timeout_surfaces_as_ai_unavailable()
    {
        var service = NewProvider(new ThrowingHandler(new TaskCanceledException("timed out")));

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => service.SummarizeThreadAsync(Context("Anything"), default));
    }

    /// <summary>A transport failure is contained in exactly the same way.</summary>
    [Fact]
    public async Task A_provider_transport_failure_surfaces_as_ai_unavailable()
    {
        var service = NewProvider(new ThrowingHandler(new HttpRequestException("no route to host")));

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => service.SuggestReplyAsync(Context("Anything"), default));
    }

    /// <summary>A non-success status is an unavailable capability, not a parsed empty answer.</summary>
    [Fact]
    public async Task A_provider_error_status_surfaces_as_ai_unavailable()
    {
        var service = NewProvider(new StatusHandler(HttpStatusCode.TooManyRequests));

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => service.SummarizeThreadAsync(Context("Anything"), default));
    }

    /// <summary>
    /// <b>A selected provider with no credential fails to construct</b> — a missing key is a
    /// configuration error at startup, not a <c>401</c> reaching the first agent who presses
    /// <em>Summarize</em>.
    /// </summary>
    [Fact]
    public void The_provider_refuses_to_construct_without_a_key()
    {
        var options = Options.Create(new AiOptions
        {
            Provider = AiProviderKind.Provider,
            Endpoint = "https://example.invalid/v1/chat/completions",
            ApiKey = null,
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new SupportCrm.Infrastructure.Seams.Ai.ProviderAiService(
                new HttpClient(), options, TimeProvider.System,
                NullLogger<SupportCrm.Infrastructure.Seams.Ai.ProviderAiService>.Instance));

        Assert.Contains("ApiKey", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>With nothing configured, the container resolves the fake.</b> This is the assertion behind
    /// "the whole application runs with no credentials" (A-7, product-scope §10 item 5).
    /// </summary>
    [Fact]
    public void With_no_configuration_the_container_resolves_the_fake()
    {
        using var factory = new SupportCrmApiFactory();

        using var scope = factory.Services.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IAiAssistService>();

        Assert.IsType<SupportCrm.Infrastructure.Seams.Ai.DeterministicFakeAiService>(resolved);
    }

    /// <summary>
    /// <b>Selecting a provider with no key fails startup validation with a message that says what to
    /// do.</b> Asserted through the real host, so it is the composition root's behaviour rather than
    /// the validator's in isolation.
    /// </summary>
    [Fact]
    public void Selecting_a_provider_without_a_key_fails_startup()
    {
        using var factory = new SupportCrmApiFactory
        {
            ConfigurationOverrides =
            {
                ["SupportCrm:Ai:Provider"] = "Provider",
                ["SupportCrm:Ai:Endpoint"] = "https://example.invalid/v1/chat/completions",
            },
        };

        var exception = Assert.ThrowsAny<Exception>(() => factory.Services.GetRequiredService<IAiAssistService>());

        Assert.Contains("ApiKey", Flatten(exception), StringComparison.Ordinal);
    }

    private static string Flatten(Exception exception) =>
        exception.InnerException is null
            ? exception.Message
            : $"{exception.Message} {Flatten(exception.InnerException)}";

    private static AiThreadContext Context(string subject) =>
        new(subject, $"Description for {subject}.", [], IsUrgent: false);

    private static IOptions<CategoryOptions> Categories() =>
        Options.Create(new CategoryOptions
        {
            Items =
            [
                new CategoryOption { Code = "billing", Name = "Billing", DepartmentId = Guid.NewGuid() },
                new CategoryOption { Code = "technical", Name = "Technical", DepartmentId = Guid.NewGuid() },
                new CategoryOption { Code = "account", Name = "Account", DepartmentId = Guid.NewGuid() },
            ],
        });

    private static SupportCrm.Infrastructure.Seams.Ai.DeterministicFakeAiService NewFake() =>
        new(Categories(), TimeProvider.System);

    private static SupportCrm.Infrastructure.Seams.Ai.ProviderAiService NewProvider(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            Options.Create(new AiOptions
            {
                Provider = AiProviderKind.Provider,
                Endpoint = "https://example.invalid/v1/chat/completions",
                Model = "test-model",
                ApiKey = "test-key",
                TimeoutSeconds = 1,
            }),
            TimeProvider.System,
            NullLogger<SupportCrm.Infrastructure.Seams.Ai.ProviderAiService>.Instance);

    /// <summary>A handler that always throws, standing in for a timeout or a transport failure.</summary>
    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    /// <summary>A handler that answers with a fixed non-success status.</summary>
    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }
}
