using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>The one writer of <see cref="TicketActivity"/></b> (docs/architecture.md §2.5,
/// 00-implementation-plan §6). Every Application service that changes a ticket calls this
/// <b>on the same path</b> that performs the change, inside the same unit of work.
///
/// <para>
/// <b>It exposes no update and no delete method</b> — not merely no endpoint (§2.7). Ticket history
/// is append-only, and the way that is guaranteed is that there is nowhere to call. A future
/// <c>EditAsync</c> here would be the defect, which is why a test asserts this type's public
/// surface rather than trusting the comment. Same shape as <c>AuditRecorder</c> from Story 02.
/// </para>
///
/// <para>
/// <b>It does not commit.</b> <see cref="RecordAsync"/> adds to the change tracker and returns; the
/// caller's single <c>SaveChangesAsync</c> commits the change and its history entry together, so no
/// committed state has one without the other (docs/architecture.md §3). This is the same contract
/// <c>AuditRecorder</c> has, and for the same reason.
/// </para>
///
/// <para>
/// <b>This is not the audit log</b> (§2.14, AD-10). Business history and security history stay
/// separate and neither is derived from the other.
/// </para>
/// </summary>
public sealed class TicketActivityRecorder(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    /// <summary>
    /// Records one entry attributed to <b>the authenticated caller</b>.
    ///
    /// <para>
    /// The actor is resolved from <see cref="ICurrentUser"/> and is <b>not a parameter</b>: an
    /// activity actor is a server-derived field (docs/api-design.md §7), so accepting one would be
    /// the very thing AP-10 forbids. Two siblings exist for the cases where the caller is not the
    /// actor: <see cref="RecordBySystemAsync"/> for the SLA monitor's entries, and
    /// <see cref="RecordForActorAsync"/> for R-14's replying customer. <b>Neither accepts an actor
    /// from a request</b> — every caller passes an id the server itself resolved.
    /// </para>
    ///
    /// <para>
    /// <paramref name="oldValue"/> and <paramref name="newValue"/> are the before and after of a
    /// change type, and are left null for types that are not changes — <c>Created</c>, above all.
    /// </para>
    /// </summary>
    public Task RecordAsync(
        Guid ticketId,
        TicketActivityType activityType,
        string? oldValue = null,
        string? newValue = null,
        TicketActivityVisibility visibility = TicketActivityVisibility.CustomerVisible,
        CancellationToken ct = default)
    {
        var entry = TicketActivity.ByUser(
            Guid.NewGuid(),
            ticketId,
            activityType,
            currentUser.Id,
            clock.GetUtcNow(),
            oldValue,
            newValue,
            visibility);

        db.TicketActivities.Add(entry);

        // Deliberately not awaited on a commit: see the class remarks. Returning a completed task
        // keeps the call sites uniform with the async ones they sit beside.
        _ = ct;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Records one entry attributed to <b>the system</b> — <see cref="TicketActivity.BySystem"/>,
    /// so <c>ActorUserId</c> is null and <c>ActorKind</c> is <c>System</c>, the two halves of
    /// §2.7's invariant.
    ///
    /// <para>
    /// <b>Added by Story 06 because <c>EscalateAsync</c>'s signature requires it</b>, not because
    /// Story 06 has a system caller — it has none. The <c>ticket-lifecycle</c> plan fixes the
    /// escalation signature as usable by a system caller precisely so <b>Story 09's breach sweep
    /// reuses the escalation path rather than duplicating it</b>, and a parameter with no way to
    /// honour it would be decorative. This is the shared half; the SLA monitor that calls it is
    /// Story 09's and is not here.
    /// </para>
    ///
    /// <para>
    /// <b>The automatic <c>Pending → Open</c> does NOT come through here</b> (R-14). A customer
    /// reply is caused by the customer, and attributing it to the system would make ticket history
    /// less truthful — it uses <see cref="RecordForActorAsync"/> with the replying customer.
    /// </para>
    /// </summary>
    public Task RecordBySystemAsync(
        Guid ticketId,
        TicketActivityType activityType,
        string? oldValue = null,
        string? newValue = null,
        TicketActivityVisibility visibility = TicketActivityVisibility.CustomerVisible,
        CancellationToken ct = default)
    {
        db.TicketActivities.Add(TicketActivity.BySystem(
            Guid.NewGuid(), ticketId, activityType, clock.GetUtcNow(), oldValue, newValue, visibility));

        _ = ct;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Records one entry attributed to <b>a named user who is not necessarily the authenticated
    /// caller</b>.
    ///
    /// <para>
    /// <b>This exists for exactly one rule: R-14.</b> The automatic <c>Pending → Open</c> is
    /// attributed to the <b>replying customer</b>, and Story 07's portal message endpoint runs as
    /// that customer — so in practice the actor equals <c>ICurrentUser</c> today. It is a parameter
    /// anyway because the rule is *"the replying customer"*, not *"whoever is authenticated"*, and
    /// writing the rule as the latter would be correct by coincidence.
    /// </para>
    ///
    /// <para>
    /// <b>It does not weaken AP-10.</b> No endpoint accepts an actor; every caller of this method
    /// passes an id the server itself resolved.
    /// </para>
    /// </summary>
    public Task RecordForActorAsync(
        Guid ticketId,
        Guid actorUserId,
        TicketActivityType activityType,
        string? oldValue = null,
        string? newValue = null,
        TicketActivityVisibility visibility = TicketActivityVisibility.CustomerVisible,
        CancellationToken ct = default)
    {
        db.TicketActivities.Add(TicketActivity.ByUser(
            Guid.NewGuid(), ticketId, activityType, actorUserId, clock.GetUtcNow(),
            oldValue, newValue, visibility));

        _ = ct;

        return Task.CompletedTask;
    }
}
