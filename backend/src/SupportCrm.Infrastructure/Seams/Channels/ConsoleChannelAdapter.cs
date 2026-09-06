using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Modules.Integrations;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Seams.Channels;

/// <summary>
/// <b>The shipped outbound channel adapter</b> — docs/architecture.md §5.2: <em>"one outbound
/// adapter interface, with a console/log adapter as the shipped implementation. Sending produces a
/// log entry and a recorded ticket activity, and attempts no external call."</em>
///
/// <h3>It lives in the application, not in test-only code</h3>
/// The intake is explicit: <em>"keep the fakes in the application, not in test-only code — the demo
/// runs against them."</em> This type is registered in the composition root and selected by
/// <c>SupportCrm:Channels:Outbound</c>, whose default is the only value there is.
///
/// <h3>It attempts no external call, and that is checkable</h3>
/// The constructor takes <b>no HTTP dependency of any kind</b>. A test asserts that by reflection
/// and then sends through a host whose <c>HttpMessageHandler</c> fails every request — so "no
/// external call" is a build failure when broken, not a comment.
///
/// <h3>The record of a send is an ordinary message, not an integration table</h3>
/// Sending goes through <see cref="TicketMessageService"/> — <b>the same method the staff reply
/// endpoint and the portal call</b>. That is what produces the <c>MessagePosted</c> activity row the
/// acceptance criterion asks for, and it is why no activity row is written here directly: DM-4 and
/// §5 constraint 17 require exactly one activity row per message, linked to it, and there is one
/// place that holds. <b>DM-6 permits no adapter, receipt or delivery table</b>, so the adapter
/// reference below reaches the log line and nothing else.
///
/// <h3>Why <see cref="Channel"/> is <c>Portal</c></h3>
/// <b><see cref="MessageChannel"/> gains no new value in this story</b>, and a test asserts that: a
/// new value arrives with the adapter that implements it (DM-6 fixes the set at the two <em>real</em>
/// channels). This adapter is a <b>transport fake standing in for a future provider</b>, not a new
/// channel of its own — so it declares the one real outbound channel the product has. An email
/// adapter would add <c>Email</c> here and to the enum together, which is precisely the one-step
/// change §5.2 promises.
///
/// <h3>Deterministic, so the demo repeats</h3>
/// The adapter reference is SHA-256 over the message, exactly as the AI fake derives its output —
/// no <c>Random</c>, no <c>Guid.NewGuid</c>, no clock in the value. The same send produces the same
/// reference on every run and every machine.
/// </summary>
public sealed class ConsoleChannelAdapter(
    TicketMessageService messages,
    ILogger<ConsoleChannelAdapter> logger) : IOutboundChannelAdapter
{
    /// <summary>Hex characters of the digest kept. Long enough to be distinct in a demo log.</summary>
    private const int ReferenceLength = 16;

    /// <summary>See the "Why <c>Portal</c>" note on the type. Nothing branches on this value.</summary>
    public MessageChannel Channel => MessageChannel.Portal;

    public async Task<OutboundDeliveryResult> SendAsync(OutboundMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        var reference = DeriveReference(message);

        // The record of the send: one TicketMessage and its one linked activity row, written by the
        // service every other caller uses. Direction is derived from the caller's role there (PF-7)
        // and is never passed in from here.
        await messages.PostAsync(message.TicketId, message.Body, Channel, ct);

        // **The body is not logged.** Its length is. A ticket message is customer content, and a log
        // is the one place it would leak to somewhere with different retention and different
        // readers than the database it belongs in. The recipient is logged because a demo has to
        // show where the message would have gone — that is the whole point of the fake.
        logger.LogInformation(
            "Outbound channel send: adapter={Adapter} channel={Channel} ticket={TicketId} " +
            "recipient={Recipient} bodyLength={BodyLength} reference={AdapterReference} " +
            "externalCallAttempted={ExternalCallAttempted}",
            nameof(ConsoleChannelAdapter),
            Channel,
            message.TicketId,
            message.RecipientAddress,
            message.Body.Length,
            reference,
            false);

        return new OutboundDeliveryResult(Accepted: true, AdapterReference: reference);
    }

    /// <summary>
    /// A stable reference derived from the message itself. It stands where a provider's own message
    /// id would go — and it is <b>returned, never persisted</b> (DM-6).
    /// </summary>
    private static string DeriveReference(OutboundMessage message)
    {
        var material = $"{message.TicketId:N}|{message.RecipientAddress}|{message.Body}";
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return Convert.ToHexStringLower(digest)[..ReferenceLength];
    }
}
