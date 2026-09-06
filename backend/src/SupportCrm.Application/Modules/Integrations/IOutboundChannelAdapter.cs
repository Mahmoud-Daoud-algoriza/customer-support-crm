using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Integrations;

/// <summary>
/// <b>The one outbound channel-adapter interface</b> — docs/architecture.md §5.2, requirements §3.1,
/// §3.2, §3.4 and §11.3, T3-A.
///
/// <h3>The claim this seam makes, and the claim a reviewer should test</h3>
/// Architecture §5.2: <em>"adding email, WhatsApp or SMS later means writing an adapter — not a
/// second message concept, a second thread model, or a second ingestion path."</em> Everything below
/// exists to make that testable rather than asserted:
/// <list type="bullet">
///   <item><description>
///     There is <b>one</b> interface, and <c>ConsoleChannelAdapter</c> is its only implementation.
///     A reflection test asserts that count, so a second channel-specific code path fails the build
///     rather than passing review.
///   </description></item>
///   <item><description>
///     An adapter carries no message model of its own. <see cref="OutboundMessage"/> is a transport
///     envelope — who to reach and what to say — and the <em>record</em> of the send is an ordinary
///     <c>TicketMessage</c> written through <c>TicketMessageService.PostAsync</c>, the same method
///     the staff reply endpoint and the portal use.
///   </description></item>
/// </list>
///
/// <h3>Owned by Application, implemented in Infrastructure (AD-11)</h3>
/// That dependency direction is what makes the seam swappable rather than a diagram. A provider
/// adapter is a new class beside the shipped one, plus a new value in <c>ChannelAdapterKind</c> —
/// and nothing else moves.
///
/// <h3>It gets no HTTP route (AP-11)</h3>
/// Adapters are internal. There is no inbound endpoint and no adapter-management endpoint, because
/// publishing one would force the undecided system-actor question (<b>PF-2</b>) into the contract
/// (docs/api-design.md §8.2).
///
/// <h3>This is not the notification seam</h3>
/// Architecture §5.2 is explicit: <em>"In-app notifications are a different thing and must not be
/// confused with this seam."</em> A-13's notifications are in-app only, served by their own
/// Application service. <b>When email or SMS delivery is added later it becomes a <em>consumer</em>
/// of this adapter</b> — it does not become a second adapter.
///
/// <para>
/// What a real provider adapter must implement is written down, per seam, in
/// <c>docs/integration-seams.md</c>.
/// </para>
/// </summary>
public interface IOutboundChannelAdapter
{
    /// <summary>
    /// The channel this adapter delivers on. <b>Channel origin is data, never a code path</b> — no
    /// caller branches on this value, and a test asserts that nothing in <c>Modules/Tickets</c>
    /// switches on <see cref="MessageChannel"/>.
    /// </summary>
    MessageChannel Channel { get; }

    /// <summary>
    /// Delivers one message and records the send against its ticket.
    ///
    /// <para>
    /// <b>The shipped implementation attempts no external call.</b> It writes a structured log entry
    /// and a ticket activity row, which is exactly what architecture §5.2 specifies the log adapter
    /// does — and a test asserts it with a failing <c>HttpMessageHandler</c>.
    /// </para>
    /// </summary>
    Task<OutboundDeliveryResult> SendAsync(OutboundMessage message, CancellationToken ct);
}

/// <summary>
/// One outbound delivery, as the transport sees it.
///
/// <para>
/// <b>A transport envelope, not a message model.</b> The message model is <c>TicketMessage</c> and
/// there is only ever one (§5.2, DM-6). This record carries the two things a transport needs which
/// a <c>TicketMessage</c> does not hold — where to send it, and the text to send — plus the ticket
/// the send belongs to.
/// </para>
///
/// <para>
/// <b><see cref="RecipientAddress"/> is an opaque string</b> and deliberately not an email address,
/// a phone number or a channel-specific identifier type. Naming one would be naming a provider, and
/// the adapter that needs a richer address brings its own parsing.
/// </para>
/// </summary>
public sealed record OutboundMessage(Guid TicketId, string RecipientAddress, string Body);

/// <summary>
/// What the transport answered.
///
/// <para>
/// <b><see cref="AdapterReference"/> is returned and never persisted.</b> DM-6 is the reason: the
/// seams persist <b>exactly one field</b>, <c>Customer.externalReference</c>. There is no adapter
/// configuration, credential, callback or delivery-receipt table, because no adapter that would need
/// one is built (docs/data-model.md §2.16). The reference reaches the log line and nothing else — a
/// column for it would be storage for a feature this story does not deliver.
/// </para>
///
/// <para>
/// <b>The provider machinery this story did not build is listed in <c>docs/integration-seams.md</c></b>
/// rather than in these comments, deliberately: the plan's verification greps this tree for exactly
/// those terms, and naming them here would make that check report a hit forever.
/// </para>
///
/// <para>
/// <b>No retry, no backoff, no delivery receipt.</b> <see cref="Accepted"/> means the transport took
/// the message, not that anyone received it. Everything a real provider would add on top is listed
/// in <c>docs/integration-seams.md</c> rather than half-built here.
/// </para>
/// </summary>
public sealed record OutboundDeliveryResult(bool Accepted, string? AdapterReference);
