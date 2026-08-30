namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The portal read filter (T2-C, docs/data-model.md §2.7).
/// <para>
/// <b><see cref="Internal"/> entries never reach a customer-facing read</b> — not the portal, and
/// not the customer interaction timeline of Story 04, which already excludes them in the
/// Application layer, once (docs/architecture.md §2.5).
/// </para>
/// </summary>
public enum TicketActivityVisibility
{
    CustomerVisible,
    Internal,
}
