namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The channel a <see cref="TicketMessage"/> arrived on — docs/data-model.md §2.8.
///
/// <para>
/// <b>This enum is the seam</b> (docs/architecture.md §5.2). The web form and portal messaging are
/// the only <em>real</em> channels in this assessment, and they write into one normalized message
/// model from day one — so <b>a new value arrives with the adapter that implements it</b>, not with
/// a second message concept, a second thread model or a second ingestion path. That is the whole
/// claim the seam makes (T2-B real, T3-A future).
/// </para>
///
/// <para>
/// <b>Nothing branches on it.</b> <c>TicketMessageService.PostAsync</c> takes the channel as a
/// parameter and stores it: channel origin is <em>data</em>, never a code path. Story 18's log
/// adapter therefore adds a member here and calls the same method in-process — it gets no HTTP
/// route (<b>AP-11</b>), because publishing an ingestion endpoint would force the undecided
/// system-actor question (<b>PF-2</b>) into the contract.
/// </para>
///
/// <para>
/// <b>In-app notifications are a different thing and must not be confused with this seam</b>
/// (docs/architecture.md §5.2). Notifications are A-13's four types, served by their own seam.
/// </para>
///
/// Stored as a stable string code, never an ordinal (docs/api-design.md §2).
/// </summary>
public enum MessageChannel
{
    /// <summary>
    /// The web-form intake channel of requirements §3.5 (docs/api-design.md §7).
    /// <para>
    /// <b>No write path in Story 07 produces it</b>, and that is a consequence of the approved
    /// model rather than an omission: a portal submission stores its originating text as
    /// <c>Ticket.description</c> and <em>not</em> as a first <c>TicketMessage</c>
    /// (docs/data-model.md §2.6 — <em>"Replies are <c>TicketMessage</c> rows, not copies of
    /// this"</em>). Recorded as finding <b>I-27</b>.
    /// </para>
    /// </summary>
    WebForm,

    /// <summary>A reply exchanged in the portal thread — both directions (T2-B).</summary>
    Portal,
}
