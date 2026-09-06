using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Integrations;

/// <summary>
/// <b>The normalized inbound shape</b> — docs/architecture.md §5.2: <em>"inbound arrives as a
/// normalized message into the same ingestion service the web form uses."</em>
///
/// <para>
/// <b>This shape is not blocked and is worth having on its own.</b> It is the thing that makes
/// "adding a channel adds an adapter, not a second message concept" concrete: whatever an email,
/// WhatsApp or SMS adapter receives, it normalizes to <em>this</em> before anything downstream sees
/// it — so the ingestion service never learns that a second channel exists.
/// </para>
///
/// <para>
/// <b>What consumes it is blocked on PF-2 / S9-10.</b> See
/// <see cref="IInboundChannelIngestion"/>: the shape exists, the interface exists, and the
/// implementation refuses rather than inventing an attribution rule.
/// </para>
///
/// <para>
/// <b><see cref="FromAddress"/> is opaque</b>, for the same reason <c>OutboundMessage</c>'s
/// recipient is: typing it would name a channel. <see cref="Subject"/> is nullable because most
/// channels have none — SMS and WhatsApp carry a body and nothing else.
/// </para>
///
/// <para>
/// <b><see cref="ReceivedAt"/> is the transport's timestamp, not the store's.</b> A message that sat
/// in a provider queue arrived when the provider says it arrived; the row's own <c>postedAt</c> is
/// still set by the ingestion service from <c>TimeProvider</c>, so the two are never confused.
/// </para>
/// </summary>
public sealed record InboundChannelMessage(
    MessageChannel Channel,
    string FromAddress,
    string? Subject,
    string Body,
    DateTimeOffset ReceivedAt);
