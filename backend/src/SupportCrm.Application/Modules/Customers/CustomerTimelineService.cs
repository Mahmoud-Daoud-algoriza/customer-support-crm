using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// The customer interaction timeline — <c>GET /customers/{id}/timeline</c>, requirements §1.3
/// (Story 04 task 5). <c>RequireAgent</c>; the policy is applied by the controller, task 8.
///
/// <para>
/// <b>A read projection, assembled on read and never stored</b> (docs/architecture.md §2.5,
/// docs/data-model.md §2.4). There is no timeline table, no timeline entity and no writer — a
/// second store kept in step with ticket activity is exactly what §2.5 rejects.
/// </para>
///
/// <para>
/// <b>Two exclusions belong here, in the Application layer, applied once</b> — not in each UI
/// (T2-C, docs/architecture.md §2.5):
/// </para>
/// <list type="number">
///   <item>
///     <b>No entry whose <c>visibility</c> is <c>Internal</c> is ever returned</b>
///     (docs/data-model.md §5 constraint 18).
///   </item>
///   <item>
///     <b><c>TicketInternalNote</c> is never touched by this query at all.</b> It is a different
///     table and this projection does not join it (docs/data-model.md §2.9), so a rendering bug
///     cannot leak one. The visibility rule is structural, not a filter someone must remember.
///   </item>
/// </list>
///
/// <para>
/// <b>Customer notes do not appear here.</b> They are a separate collection with its own endpoint
/// (docs/api-design.md §5.5, <see cref="CustomerNoteService"/>).
/// </para>
///
/// <para>
/// <b>Story 06 completed this service</b> (task 6). Story 04 wrote it against the empty set — as
/// the <c>customer-records</c> intake authorized, <em>"build it against an empty ticket set and
/// enrich when tickets land"</em> — and it now projects real <c>TicketActivity</c> rows. <b>Story
/// 04's acceptance criterion still holds unchanged</b>: a customer with no tickets gets a
/// well-formed empty page rather than an error, and that is now a fact about the data rather than
/// about the schema.
/// </para>
/// </summary>
public sealed class CustomerTimelineService(IApplicationDbContext db)
{
    /// <summary>
    /// <c>GET /customers/{id}/timeline</c> — newest first.
    /// <para>
    /// A missing customer is a <c>404</c>; a customer with no activity is an <b>empty page</b>, not
    /// an error. The two are deliberately different outcomes.
    /// </para>
    /// </summary>
    public async Task<PagedResult<TimelineEntryDto>> GetAsync(
        Guid customerId, PageQuery? page, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException("Customer not found.");
        }

        var (pageNumber, pageSize) = page.Normalize();

        // Exclusion 1 — no Internal entry, ever (docs/data-model.md §5 constraint 18). It is a
        // WHERE on the projection, not a filter a screen applies.
        //
        // Exclusion 2 — TicketInternalNote is NOT JOINED. Its absence from this query is the whole
        // guarantee: there is no filter to forget and no "enrichment" that could leak a note body,
        // because the table never enters the read (§2.9). Do not add it.
        //
        // No Branch predicate either: a customer's tickets are theirs regardless of branch (A-2).
        //
        // Ticket(customerId, createdAt) and TicketActivity(ticketId, occurredAt) are both already
        // declared for this read in docs/data-model.md §6, so no new index was needed.
        var query =
            from ticket in db.Tickets.AsNoTracking().Where(t => t.CustomerId == customerId)
            join entry in db.TicketActivities.AsNoTracking() on ticket.Id equals entry.TicketId
            where entry.Visibility != TicketActivityVisibility.Internal
            orderby entry.OccurredAt descending, entry.Id descending
            select new TimelineEntryDto(
                entry.OccurredAt,
                ticket.Id,
                ticket.Subject,
                entry.ActivityType.ToString(),
                entry.ActorKind.ToString(),

                // Null exactly when the entry is a System one — §2.7's invariant, derived from the
                // stored actor rather than restated as a rule this projection could get wrong.
                entry.ActorUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == entry.ActorUserId)
                        .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).FirstOrDefault(),
                entry.OldValue,
                entry.NewValue);

        return await query.ToPagedResultAsync(pageNumber, pageSize, ct);
    }
}
