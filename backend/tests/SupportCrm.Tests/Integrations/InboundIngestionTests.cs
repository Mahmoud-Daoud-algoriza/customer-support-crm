using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Modules.Integrations;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Infrastructure.Seams.Channels;

namespace SupportCrm.Tests.Integrations;

/// <summary>
/// <b>⛔ The blocked half of the channel seam — PF-2 / S9-10.</b>
///
/// <para>
/// The acceptance criterion this file would satisfy is: <em>"an inbound message delivered through
/// the fake adapter creates or updates a ticket using the SAME message model as the web form and
/// portal, carrying its channel of origin."</em> <b>It is not satisfied, and it is not faked.</b>
/// </para>
///
/// <para>
/// <c>Ticket.createdByUserId</c> and <c>TicketMessage.authorUserId</c> are <b>required</b>
/// (docs/data-model.md §2.6, §2.8) and <c>actorKind = System</c> is reserved for the SLA monitor
/// (§2.7, <b>R-14</b>). An inbound channel message has no human actor, and no approved document says
/// who it is attributed to. Options A, B and C are set out in
/// <c>.squad/plans/integration-seams/18-story-channel-erp-adapters.md</c> and in
/// <c>docs/integration-seams.md</c> §1.3; <b>none was chosen</b>.
/// </para>
///
/// <para>
/// <b>A skipped test with a stated reason is the honest artefact here. A passing test built on an
/// invented attribution rule is not</b> — it would encode a product decision as a green check, in
/// the place least likely to be re-examined.
/// </para>
/// </summary>
public sealed class InboundIngestionTests(IntegrationSeamFixture fixture)
    : IClassFixture<IntegrationSeamFixture>
{
    /// <summary>
    /// <b>The test this becomes once PF-2 is decided.</b> Its body is written out rather than left
    /// empty so the shape is reviewable now: ingestion goes through
    /// <c>TicketMessageService.PostAsync</c> — <b>the same method the web form and the portal
    /// use</b>, not a second ingestion path (architecture §5.2) — and the resulting message carries
    /// its channel of origin.
    ///
    /// <para>
    /// Un-skipping it requires recording the attribution decision first. The one line that changes
    /// besides this attribute is <c>BlockedInboundChannelIngestion</c>.
    /// </para>
    /// </summary>
    [Fact(Skip =
        "BLOCKED — PF-2 / S9-10: inbound actor attribution is undecided. Ticket.createdByUserId and " +
        "TicketMessage.authorUserId are required (data-model §2.6, §2.8) and actorKind = System is " +
        "reserved for the SLA monitor (§2.7, R-14). An inbound channel message has no human actor " +
        "and no approved document says who it is attributed to. Choose option A, B or C from " +
        ".squad/plans/integration-seams/18-story-channel-erp-adapters.md — do not invent one.")]
    public async Task An_inbound_message_reaches_the_one_ingestion_service_and_carries_its_channel()
    {
        var ticketId = await fixture.AddTicketAsync();

        var inbound = new InboundChannelMessage(
            MessageChannel.Portal,
            "customer@example.test",
            Subject: null,
            Body: "Delivered through the fake inbound adapter.",
            ReceivedAt: DateTimeOffset.UtcNow);

        var recordedAgainst = await fixture.WithAgentScopeAsync(provider =>
            provider.GetRequiredService<IInboundChannelIngestion>()
                .IngestAsync(inbound, CancellationToken.None));

        Assert.Equal(ticketId, recordedAgainst);

        // The SAME message model as the web form and the portal — one row in TicketMessages, with
        // its one linked MessagePosted activity row (DM-4, §5 constraint 17) — carrying its channel
        // of origin. No second thread model and no second ingestion path (architecture §5.2).
    }

    /// <summary>
    /// <b>The blocker itself, asserted.</b> Skipping the criterion above proves nothing on its own;
    /// this proves the implementation <em>refuses</em> rather than quietly doing something.
    ///
    /// <para>
    /// If someone implements an attribution rule without recording the decision, this test fails —
    /// which is exactly the moment the decision should be written down.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Inbound_ingestion_refuses_with_the_reason_rather_than_inventing_an_attribution()
    {
        var inbound = new InboundChannelMessage(
            MessageChannel.Portal, "customer@example.test", null, "Body.", DateTimeOffset.UtcNow);

        var failure = await Assert.ThrowsAsync<NotImplementedException>(() =>
            fixture.WithAgentScopeAsync(provider =>
                provider.GetRequiredService<IInboundChannelIngestion>()
                    .IngestAsync(inbound, CancellationToken.None)));

        Assert.Contains("PF-2", failure.Message, StringComparison.Ordinal);
        Assert.Contains("S9-10", failure.Message, StringComparison.Ordinal);
        Assert.Equal(BlockedInboundChannelIngestion.BlockedReason, failure.Message);
    }

    /// <summary>
    /// <b>The normalized shape is not blocked and exists on its own merit.</b> It is what makes
    /// "adding a channel adds an adapter, not a second message concept" concrete: whatever an email
    /// or WhatsApp adapter receives, it normalizes to this before anything downstream sees it.
    ///
    /// <para>
    /// It carries <b>no actor</b> — deliberately. Adding one here would be choosing an option.
    /// </para>
    /// </summary>
    [Fact]
    public void The_normalized_inbound_shape_carries_no_actor()
    {
        var properties = typeof(InboundChannelMessage)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Equal(["Channel", "FromAddress", "Subject", "Body", "ReceivedAt"], properties);

        Assert.DoesNotContain(properties, name =>
            name.Contains("User", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Author", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Actor", StringComparison.OrdinalIgnoreCase));
    }
}
