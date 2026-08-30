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
    /// the very thing AP-10 forbids. The SLA monitor's <c>System</c> entries are Story 09's, and
    /// they go through <see cref="TicketActivity.BySystem"/> on a path that has no caller at all.
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
}
