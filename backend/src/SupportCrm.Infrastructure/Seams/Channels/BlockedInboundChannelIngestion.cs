using SupportCrm.Application.Modules.Integrations;

namespace SupportCrm.Infrastructure.Seams.Channels;

/// <summary>
/// The inbound half of the channel seam, <b>refusing rather than guessing</b> — see
/// <see cref="IInboundChannelIngestion"/> for the full PF-2 / S9-10 statement.
///
/// <para>
/// It is registered in the composition root like any other seam implementation, so the wiring is
/// real and the gap is at exactly one line rather than spread across a missing registration, a
/// missing type and a missing test. Nothing in the application calls it.
/// </para>
///
/// <para>
/// <b>When PF-2 is decided, this class is what gets replaced</b> — and the replacement calls
/// <c>TicketMessageService.PostAsync</c>, the same method the web form and the portal use, from
/// in-process with no HTTP route (§5.2, AP-11).
/// </para>
/// </summary>
public sealed class BlockedInboundChannelIngestion : IInboundChannelIngestion
{
    /// <summary>
    /// The reason, in one sentence, so it reaches whoever hits it without them having to find the
    /// plan. The test that documents the shape this method will take is skipped with the same text.
    /// </summary>
    public const string BlockedReason =
        "PF-2 / S9-10: inbound actor attribution is undecided. Ticket.createdByUserId and " +
        "TicketMessage.authorUserId are required (data-model §2.6, §2.8) and actorKind = System is " +
        "reserved for the SLA monitor (§2.7, R-14). An inbound channel message has no human actor " +
        "and no approved document says who it is attributed to. Choose option A, B or C from " +
        ".squad/plans/integration-seams/18-story-channel-erp-adapters.md before implementing this.";

    public Task<Guid> IngestAsync(InboundChannelMessage message, CancellationToken ct)
    {
        // PF-2 / S9-10 — BLOCKED. Do NOT invent an attribution: picking one here would settle a
        // product decision in infrastructure code, which is the one place nobody would look for it.
        _ = message;
        _ = ct;

        throw new NotImplementedException(BlockedReason);
    }
}
