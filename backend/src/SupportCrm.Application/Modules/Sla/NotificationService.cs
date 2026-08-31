using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// The read side of A-13's in-app notifications — docs/api-design.md §5.10, §6.6.
///
/// <h3>Recipient-scoped, and that is the whole of the authorization</h3>
/// Every query filters on <c>ICurrentUser.Id</c>. There is no role check and there is no policy on
/// the controller beyond <c>[Authorize]</c>, because a notification's audience is one person rather
/// than a role — a Manager has no more right to another user's notifications than an Agent does.
/// Another user's row is <b><c>404</c>, not <c>403</c></b> (AP-4): a <c>403</c> would confirm the row
/// exists, which is the inference AP-4 exists to prevent.
///
/// <h3>There is no create method and no bulk method</h3>
/// Notifications are raised by the server through <c>INotificationPublisher</c>, never by a client
/// (§5.10). <b><c>POST /notifications/read-all</c> was removed as unrequested surface (AP-18)</b> and
/// is deliberately not reintroduced here as a convenience — the design decided that, not this file.
/// </summary>
public sealed class NotificationService(
    IApplicationDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    /// <summary>
    /// The caller's own notifications, newest first, plus their total unread count.
    /// </summary>
    public async Task<NotificationPage> ListAsync(
        bool unreadOnly, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();

        // Scoped first, so no later clause can widen it — the same discipline TicketScope applies.
        var mine = db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == currentUser.Id);

        // Counted from the scoped set and NOT from the filtered one: the badge is "how many unread
        // do I have", which does not change because the caller is looking at a filtered page.
        var unreadCount = await mine.CountAsync(n => n.ReadAt == null, ct);

        var filtered = unreadOnly ? mine.Where(n => n.ReadAt == null) : mine;

        // Newest first. There is no sort whitelist here because §5.10 publishes no `sort` parameter —
        // a notification list has one meaningful order, and offering others would be surface the
        // contract does not have.
        var rows = await filtered
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Join(
                db.Tickets.AsNoTracking(),
                n => n.TicketId,
                t => t.Id,
                (n, t) => new NotificationDto(
                    n.Id, n.Type.ToString(), n.TicketId, t.Subject, n.CreatedAt, n.ReadAt))
            .ToPagedResultAsync(pageNumber, pageSize, ct);

        return NotificationPage.From(rows, unreadCount);
    }

    /// <summary>
    /// Marks one of the caller's notifications read — <c>204</c>.
    ///
    /// <para>
    /// <b>A second read is a no-op, not a rewrite</b> (docs/data-model.md §5 constraint 22): the
    /// entity refuses to move an existing timestamp, so this method needs no check of its own and the
    /// endpoint answers <c>204</c> both times.
    /// </para>
    /// </summary>
    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        // Scoped in the same predicate that finds it — fetch-then-authorize, never
        // authorize-then-fetch-by-id. Another user's id is indistinguishable from a missing one.
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.RecipientUserId == currentUser.Id, ct)
            ?? throw new NotFoundException("Notification not found.");

        notification.MarkRead(clock.GetUtcNow());

        await db.SaveChangesAsync(ct);
    }
}
