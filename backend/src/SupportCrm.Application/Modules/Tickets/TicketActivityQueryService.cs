using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <c>GET /tickets/{id}/activity</c> — the ticket's append-only history, paged and oldest-first
/// (docs/api-design.md §5.6, §6.4).
///
/// <para>
/// <b>Staff read: internal entries are included.</b> §5.6 words it exactly so — *"Full history,
/// internal entries included"* — because this endpoint sits behind <c>RequireAgent</c> and no portal
/// path reaches it (AP-5). <b>The customer-facing filter is somewhere else on purpose</b>:
/// <c>CustomerTimelineService</c>'s projection and Story 13's portal reads apply it. Filtering here
/// too would put the same rule in two places and hide which one is load-bearing.
/// </para>
///
/// <para>
/// <b>Chronological, not newest-first.</b> A history is read forwards — the timeline on the customer
/// screen is the newest-first view, and they are different reads for different questions. The
/// <c>TicketActivity(TicketId, OccurredAt)</c> index of docs/data-model.md §6 serves this one.
/// </para>
///
/// <para>
/// <b>Read-only, by construction.</b> This service has no writer, and the one writer that exists —
/// <c>TicketActivityRecorder</c> — has no update or delete method (§2.7, AD-10).
/// </para>
/// </summary>
public sealed class TicketActivityQueryService(IApplicationDbContext db, ICurrentUser currentUser)
{
    public async Task<PagedResult<TicketActivityDto>> ListAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        // Scope first: an out-of-department ticket's history is 404, worded identically to a ticket
        // that does not exist (AP-4). Reading history must not reveal what reading the ticket hides.
        _ = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        var (pageNumber, pageSize) = page.Normalize();

        var query = db.TicketActivities.AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.OccurredAt)
            .ThenBy(a => a.Id)   // a stable tiebreak, so paging is meaningful within one timestamp
            .Select(a => new TicketActivityDto(
                a.Id,
                a.OccurredAt,
                a.ActivityType.ToString(),
                a.ActorKind.ToString(),
                a.ActorUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == a.ActorUserId)
                        .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).FirstOrDefault(),
                a.OldValue,
                a.NewValue,
                a.Visibility.ToString(),
                a.MessageId,
                a.InternalNoteId));

        return await query.ToPagedResultAsync(pageNumber, pageSize, ct);
    }
}

/// <summary>
/// docs/api-design.md §6.4 <c>Activity entry</c>, member for member.
///
/// <para>
/// <b><see cref="Actor"/> is null exactly when <see cref="ActorKind"/> is <c>System</c></b> — the
/// two halves of docs/data-model.md §2.7's invariant, and the projection above cannot produce any
/// other combination because it derives the actor from <c>ActorUserId</c>, which the entity keeps
/// null for and only for system entries. <c>TicketActivityProjectionTests</c> asserts it rather than
/// leaving it to the comment.
/// </para>
///
/// <para>
/// The automatic <c>Pending → Open</c> carries <c>actorKind: "User"</c> and the <b>replying
/// customer</b> as actor (<b>R-14</b>) — it is not a system entry, and nothing in this projection
/// special-cases it.
/// </para>
/// </summary>
public sealed record TicketActivityDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActivityType,
    string ActorKind,
    UserSummaryDto? Actor,
    string? OldValue,
    string? NewValue,
    string Visibility,
    Guid? MessageId,
    Guid? InternalNoteId);
