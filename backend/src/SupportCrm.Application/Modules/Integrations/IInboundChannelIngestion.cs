namespace SupportCrm.Application.Modules.Integrations;

/// <summary>
/// The inbound half of the channel seam — docs/architecture.md §5.2.
///
/// <h3>⛔ BLOCKED — PF-2 / S9-10. The inbound path has no actor.</h3>
/// <para>
/// <c>Ticket.createdByUserId</c> and <c>TicketMessage.authorUserId</c> are <b>required</b>
/// (docs/data-model.md §2.6, §2.8), and <c>TicketActivity.actorKind = System</c> is reserved for
/// <b>the SLA monitor only</b> (§2.7, <b>R-14</b>). An inbound channel message has no human actor,
/// and <b>no approved document says who it is attributed to.</b>
/// </para>
///
/// <para>
/// Three options exist, each a product decision, and this story <b>chose none</b>:
/// </para>
/// <list type="table">
///   <item>
///     <term>A — the customer's linked portal <c>User</c></term>
///     <description>No schema change; fails for a customer with no portal login, which is most
///     agent-created profiles.</description>
///   </item>
///   <item>
///     <term>B — a seeded system/service account <c>User</c></term>
///     <description>No schema change; adds a second system actor, which R-14 deliberately avoided.</description>
///   </item>
///   <item>
///     <term>C — nullable author with <c>actorKind = System</c></term>
///     <description>A <b>data-model change</b>; widens <c>System</c> beyond the SLA monitor,
///     contradicting §2.7.</description>
///   </item>
/// </list>
///
/// <para>
/// <b>Do not invent an attribution.</b> The shipped implementation throws with this reason, and its
/// test is skipped with this reason. A skipped test is the honest artefact; a passing test built on
/// an invented rule is not.
/// </para>
///
/// <h3>Two things are already true and must survive the unblocking</h3>
/// <list type="number">
///   <item><description>
///     <b>Ingestion calls <c>TicketMessageService.PostAsync</c></b> — the same method the web form
///     and the portal use. It does <b>not</b> get a second ingestion path (§5.2).
///   </description></item>
///   <item><description>
///     <b>No HTTP route is added</b> (<b>AP-11</b>). The adapter calls this in-process. Publishing an
///     ingestion endpoint is exactly what AP-11 refused to do, and for this same reason.
///   </description></item>
/// </list>
/// </summary>
public interface IInboundChannelIngestion
{
    /// <summary>
    /// Delivers one normalized inbound message into the ticket thread it belongs to.
    ///
    /// <para>
    /// <b>Throws until PF-2 is decided.</b> See the type remarks: the attribution rule is a product
    /// decision, not an implementation detail.
    /// </para>
    /// </summary>
    /// <returns>The id of the ticket the message was recorded against.</returns>
    Task<Guid> IngestAsync(InboundChannelMessage message, CancellationToken ct);
}
