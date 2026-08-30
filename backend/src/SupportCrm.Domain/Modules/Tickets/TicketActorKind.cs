namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// Who caused an activity entry — so <b>a null actor is explicit, never ambiguous</b>
/// (docs/data-model.md §2.7).
/// <para>
/// <b><see cref="System"/> is used only by the SLA monitor</b> (Story 09). In particular the
/// automatic <c>Pending → Open</c> transition is <see cref="User"/>, attributed to the replying
/// customer — finding R-14: attributing a customer-caused change to the system would make ticket
/// history less truthful.
/// </para>
/// </summary>
public enum TicketActorKind
{
    User,
    System,
}
