using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Modules.Integrations;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Infrastructure.Seams.Channels;

namespace SupportCrm.Tests.Integrations;

/// <summary>
/// <b>The channel seam, tested as the claim it makes</b> — docs/architecture.md §5.2:
/// <em>"adding email, WhatsApp or SMS later means writing an adapter — not a second message concept,
/// a second thread model, or a second ingestion path. <b>That is the whole claim this seam makes, and
/// it is the claim a reviewer should test.</b>"</em>
///
/// <para>
/// These tests are therefore not about the log adapter's behaviour for its own sake. They are about
/// the four properties that make the claim true, each of which fails the build when broken: the
/// adapter attempts no external call, the send is recorded through the <em>one</em> ingestion
/// service, <b>the adapter is the only channel-specific code in the codebase</b>, and nothing
/// branches on the channel.
/// </para>
///
/// <para>
/// Every test here runs in a host whose <b>every outbound HTTP request throws</b> — see
/// <see cref="IntegrationSeamFixture"/>.
/// </para>
/// </summary>
public sealed class ChannelSeamTests(IntegrationSeamFixture fixture) : IClassFixture<IntegrationSeamFixture>
{
    /// <summary>
    /// <b>Test 1 — the adapter accepts, and attempts no external call.</b>
    ///
    /// <para>
    /// The "no external call" half is carried by the fixture: the send below runs in a host where any
    /// outbound request throws, so a passing assertion <em>is</em> the proof that nothing reached the
    /// network. <see cref="The_console_adapter_has_no_http_dependency"/> adds the structural half.
    /// </para>
    ///
    /// <para>
    /// The reference is deterministic — SHA-256 over the message, no <c>Random</c>, no
    /// <c>Guid.NewGuid</c>, no clock — so a demo repeats and a test can assert it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Sending_is_accepted_and_returns_a_deterministic_reference()
    {
        var ticketId = await fixture.AddTicketAsync();
        var message = new OutboundMessage(ticketId, "customer@example.test", "Your ticket has been updated.");

        var first = await fixture.SendAsync(message);
        var second = await fixture.SendAsync(message);

        Assert.True(first.Accepted);
        Assert.False(string.IsNullOrWhiteSpace(first.AdapterReference));
        Assert.Equal(first.AdapterReference, second.AdapterReference);
    }

    /// <summary>
    /// <b>Test 2 — the send is recorded against the ticket.</b> The intake's acceptance criterion is
    /// <em>"produces a log entry <b>and is recorded against the ticket</b>, with no external call
    /// attempted"</em>.
    ///
    /// <para>
    /// <b>The record is an ordinary message with its one linked activity row</b>, written by
    /// <c>TicketMessageService.PostAsync</c> — the same method the staff reply endpoint and the
    /// portal call. That is what makes *"not a second message concept"* true rather than stated, and
    /// it is why the assertions below read the <em>ticket's</em> tables: <b>DM-6 permits no
    /// integration table at all</b>.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_send_is_recorded_as_a_message_and_exactly_one_linked_activity_row()
    {
        var ticketId = await fixture.AddTicketAsync();

        await fixture.SendAsync(new OutboundMessage(ticketId, "customer@example.test", "Recorded send."));

        var (messages, activities) = await fixture.Factory.WithDbAsync(async db =>
        (
            await db.TicketMessages.Where(m => m.TicketId == ticketId).ToListAsync(),
            await db.TicketActivities.Where(a => a.TicketId == ticketId).ToListAsync()
        ));

        var posted = Assert.Single(messages);

        Assert.Equal("Recorded send.", posted.Body);

        // Outbound, because direction is derived from the caller's role (PF-7) — the adapter never
        // passes one. The caller is an Agent.
        Assert.Equal(MessageDirection.Outbound, posted.Direction);

        // The channel is stored, not branched on. It is the adapter's declared channel.
        Assert.Equal(MessageChannel.Portal, posted.Channel);

        // §5 constraint 17 / DM-4: exactly one activity row per message, linked to it.
        var messagePosted = Assert.Single(activities, a => a.ActivityType == TicketActivityType.MessagePosted);

        Assert.Equal(posted.Id, messagePosted.MessageId);

        // The body lives once: the activity row points at the message rather than copying it.
        Assert.Null(messagePosted.OldValue);
        Assert.Null(messagePosted.NewValue);
    }

    /// <summary>
    /// <b>The send writes no integration row.</b> DM-6 permits exactly one persisted field across
    /// every seam, and it belongs to the customer — so <c>AdapterReference</c> is returned and
    /// <em>never</em> stored (§2.16: no adapter-configuration, credential, webhook or
    /// delivery-receipt storage exists).
    /// </summary>
    [Fact]
    public async Task The_adapter_reference_is_returned_and_never_persisted()
    {
        var ticketId = await fixture.AddTicketAsync();

        var result = await fixture.SendAsync(
            new OutboundMessage(ticketId, "customer@example.test", "Nothing stores this reference."));

        var reference = result.AdapterReference!;

        var (bodies, values) = await fixture.Factory.WithDbAsync(async db =>
        (
            await db.TicketMessages.Where(m => m.TicketId == ticketId).Select(m => m.Body).ToListAsync(),
            await db.TicketActivities.Where(a => a.TicketId == ticketId)
                .Select(a => (a.OldValue ?? string.Empty) + (a.NewValue ?? string.Empty)).ToListAsync()
        ));

        Assert.DoesNotContain(bodies, body => body.Contains(reference, StringComparison.Ordinal));
        Assert.DoesNotContain(values, value => value.Contains(reference, StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>Test 3 — the adapter is the only channel-specific code.</b> The reflection half of
    /// *"adding a hypothetical channel requires implementing the adapter only"* (intake AC 4).
    ///
    /// <para>
    /// A second implementation appearing here would mean a second channel-specific code path exists,
    /// which is the thing the seam promises will never be needed. When a real provider adapter is
    /// added this count changes deliberately, in one place, and nothing else moves.
    /// </para>
    /// </summary>
    [Fact]
    public void Exactly_one_outbound_channel_adapter_implementation_exists()
    {
        var implementations = new[]
            {
                typeof(IOutboundChannelAdapter).Assembly,
                typeof(ConsoleChannelAdapter).Assembly,
            }
            .Distinct()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && typeof(IOutboundChannelAdapter).IsAssignableFrom(type))
            .ToList();

        var only = Assert.Single(implementations);

        Assert.Equal(typeof(ConsoleChannelAdapter), only);
    }

    /// <summary>
    /// <b>Test 3b — nothing branches on the channel.</b> Channel origin is <b>data, never a code
    /// path</b> (docs/data-model.md §2.8): <c>PostAsync</c> takes the channel as a parameter and
    /// stores it.
    ///
    /// <para>
    /// The plan asks for a grep over <c>Modules/Tickets</c>. Doing it as a test rather than as a
    /// one-off verification step means it keeps holding after this story ships — a
    /// <c>switch (message.Channel)</c> added in a year fails here.
    /// </para>
    /// </summary>
    [Fact]
    public void Nothing_in_the_tickets_module_switches_on_the_message_channel()
    {
        var ticketsModule = Path.Combine(
            RepositoryRoot(), "backend", "src", "SupportCrm.Application", "Modules", "Tickets");

        Assert.True(Directory.Exists(ticketsModule), $"Expected the Tickets module at {ticketsModule}.");

        var offenders = Directory.EnumerateFiles(ticketsModule, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, text: File.ReadAllText(path)))
            .Where(file => BranchesOnChannel(file.text))
            .Select(file => Path.GetFileName(file.path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Channel origin must be data, never a code path (architecture §5.2, data-model §2.8). " +
            $"These files branch on MessageChannel: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// <b>Test 4 — <c>MessageChannel</c> gained no new value in this story.</b>
    ///
    /// <para>
    /// DM-6 fixes the set at the two <em>real</em> channels: <em>"present from day one with the two
    /// real values, which is what makes T3-A a seam."</em> <b>A new value arrives with the adapter
    /// that implements it</b> — so the console adapter, which implements no provider, adds none. It
    /// declares <c>Portal</c>, because it is a transport fake standing in for a future provider
    /// rather than a new channel of its own.
    /// </para>
    /// </summary>
    [Fact]
    public void The_message_channel_enum_still_has_only_the_two_real_values()
    {
        Assert.Equal(
            new[] { MessageChannel.WebForm, MessageChannel.Portal },
            Enum.GetValues<MessageChannel>());

        // Stored as a stable string code, never an ordinal (api-design §2), so this list is a
        // contract rather than an implementation detail.
        Assert.Equal(["WebForm", "Portal"], Enum.GetNames<MessageChannel>());
    }

    /// <summary>
    /// The shipped adapter is what configuration selects with <b>nothing configured</b> — which is
    /// what *"selected by configuration"* has to mean when the whole point is that no configuration
    /// is required (product-scope §10 item 5). Neither <c>SupportCrm:Channels</c> nor
    /// <c>SupportCrm:Erp</c> appears in <c>appsettings.json</c>.
    /// </summary>
    [Fact]
    public async Task The_console_adapter_is_what_resolves_with_no_configuration()
    {
        var adapter = await fixture.WithAgentScopeAsync(provider =>
            Task.FromResult(provider.GetRequiredService<IOutboundChannelAdapter>()));

        Assert.IsType<ConsoleChannelAdapter>(adapter);
        Assert.Equal(MessageChannel.Portal, adapter.Channel);
    }

    /// <summary>
    /// The adapter cannot make a network call, because it holds nothing that could. Structural, so it
    /// stays true even for a code path no test exercises.
    /// </summary>
    [Fact]
    public void The_console_adapter_has_no_http_dependency()
    {
        var parameters = typeof(ConsoleChannelAdapter)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(HttpClient), parameters);
        Assert.DoesNotContain(typeof(IHttpClientFactory), parameters);
    }

    /// <summary>
    /// A <c>switch</c> or comparison whose subject is a message channel. Deliberately narrow: it
    /// looks for the channel <em>being branched on</em>, not for the word appearing in prose.
    /// </summary>
    private static bool BranchesOnChannel(string source)
    {
        string[] branches =
        [
            "switch (channel",
            "switch (message.Channel",
            "Channel switch",
            "channel switch",
            "Channel is MessageChannel.",
            "channel is MessageChannel.",
            "Channel == MessageChannel.",
            "channel == MessageChannel.",
            "Channel != MessageChannel.",
            "channel != MessageChannel.",
        ];

        return branches.Any(branch => source.Contains(branch, StringComparison.Ordinal));
    }

    /// <summary>
    /// Walks up from the test binary to the repository root — the same technique
    /// <c>ModelStateProblemDetailsTests</c> uses to reach the shipped i18n dictionaries.
    /// </summary>
    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "backend", "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The repository root was not found above the test binary.");
    }
}
