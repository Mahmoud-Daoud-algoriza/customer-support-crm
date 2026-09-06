using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Modules.Integrations;

namespace SupportCrm.Tests.Integrations;

/// <summary>
/// <b>The surface Story 18 did <em>not</em> add</b> — docs/api-design.md §8.2, §8.3, §8.4 and
/// <b>AP-11</b>.
///
/// <para>
/// An absence is the hardest thing to keep true, because nothing fails when it stops being one. So
/// it is asserted two ways: over the <b>route table</b> the running host publishes — which catches a
/// controller nobody remembered — and over live requests, which catches a route that exists but is
/// unreachable in the way that matters.
/// </para>
///
/// <para>
/// <b>Why AP-11 refuses an inbound endpoint at all:</b> publishing one would force the undecided
/// system-actor question (<b>PF-2</b>) into the contract. The refusal is not tidiness; it is how the
/// open question was kept open.
/// </para>
/// </summary>
public sealed class NoIntegrationSurfaceTests(IntegrationSeamFixture fixture)
    : IClassFixture<IntegrationSeamFixture>
{
    /// <summary>
    /// Every path fragment that would mean an integration surface leaked into the contract.
    /// <c>signalr</c> and <c>ws</c> cover T3-B's excluded real-time transport (§8.4).
    /// </summary>
    private static readonly string[] ForbiddenRouteFragments =
    [
        "channels", "webhook", "webhooks", "erp", "adapters", "adapter",
        "inbound", "ws", "websocket", "signalr", "hub", "poll", "longpoll",
    ];

    /// <summary>
    /// <b>Tests 7 and 8 — no route exists.</b> Read off the host's own <see cref="EndpointDataSource"/>,
    /// so it covers every controller and minimal-API endpoint the composition root publishes,
    /// including ones no test calls.
    /// </summary>
    [Fact]
    public void The_route_table_publishes_no_integration_endpoint()
    {
        var routes = fixture.Factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();

        Assert.NotEmpty(routes);

        foreach (var route in routes)
        {
            var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Where(segment => !segment.StartsWith('{'))
                .Select(segment => segment.ToLowerInvariant());

            foreach (var segment in segments)
            {
                Assert.False(
                    ForbiddenRouteFragments.Contains(segment),
                    $"Route '{route}' publishes integration surface. AP-11 and api-design §8.2–§8.4 " +
                    "forbid an inbound-channel, adapter-management, ERP, WebSocket or long-poll endpoint.");
            }
        }
    }

    /// <summary>
    /// The same absence, from the outside. A caller with a valid staff token gets <c>404</c>, which
    /// is what "there is nothing here" looks like over HTTP.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/channels/inbound")]
    [InlineData("/api/v1/channels")]
    [InlineData("/api/v1/webhooks/email")]
    [InlineData("/api/v1/webhooks")]
    [InlineData("/api/v1/erp/customers")]
    [InlineData("/api/v1/erp")]
    [InlineData("/api/v1/adapters")]
    [InlineData("/api/v1/adapters/channels")]
    [InlineData("/api/v1/hubs/chat")]
    public async Task No_integration_route_answers_a_request(string path)
    {
        using var client = fixture.Factory.CreateClientFor(fixture.AgentId);

        using var get = await client.GetAsync(path);
        using var post = await client.PostAsync(path, content: null);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        // A POST to a nonexistent path is 404; to an existing path with no POST it would be 405.
        // Asserting "not 405" is what distinguishes "no route" from "a route that refuses the verb".
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    /// <summary>
    /// <b>No WebSocket transport exists</b> (§8.4, T3-B). The host answers an upgrade request as an
    /// ordinary miss — there is no negotiate endpoint and no hub to reach.
    /// </summary>
    [Fact]
    public async Task No_websocket_or_negotiate_endpoint_exists()
    {
        using var client = fixture.Factory.CreateClientFor(fixture.AgentId);

        foreach (var path in new[] { "/ws", "/hubs/tickets", "/api/v1/hubs/tickets/negotiate" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.TryAddWithoutValidation("Connection", "Upgrade");
            request.Headers.TryAddWithoutValidation("Upgrade", "websocket");

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    /// <summary>
    /// <b>Test 9 — the application starts and serves with no integration credential configured.</b>
    ///
    /// <para>
    /// This is the T3 rule of engagement, made observable: the host in this fixture was built from
    /// the real composition root with <b>nothing</b> under <c>SupportCrm:Channels</c> or
    /// <c>SupportCrm:Erp</c> — neither section exists in any settings file — and with a poisoned
    /// network. It started, it resolves both seams, and it answers requests.
    /// </para>
    ///
    /// <para>
    /// The rest of the suite is the wider form of the same claim: <b>every prior story's tests run
    /// in hosts configured exactly this way.</b>
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_application_runs_with_no_integration_configuration_at_all()
    {
        // Both seams resolve to their fakes...
        var (adapter, gateway) = await fixture.WithAgentScopeAsync(provider => Task.FromResult((
            provider.GetRequiredService<IOutboundChannelAdapter>(),
            provider.GetRequiredService<IExternalSystemGateway>())));

        Assert.NotNull(adapter);
        Assert.NotNull(gateway);

        // ...and the application is genuinely serving, not merely constructed.
        using var client = fixture.Factory.CreateClientFor(fixture.AgentId);

        using var health = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    /// <summary>
    /// <b>No message broker or queue</b> carries integration traffic (product-scope §8). Asserted
    /// over the loaded assemblies rather than by grepping a file, so a transitive package reference
    /// is caught as well as a direct one.
    /// </summary>
    [Fact]
    public void No_message_broker_or_queue_library_is_loaded()
    {
        string[] forbidden = ["rabbit", "kafka", "azure.messaging", "amazon.sqs", "servicebus", "masstransit", "nservicebus"];

        var offenders = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name ?? string.Empty)
            .Where(name => forbidden.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"product-scope §8 excludes brokers and queues. Loaded: {string.Join(", ", offenders)}");
    }
}
