using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;

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
/// <b>Story 06 completes this service.</b> <c>Ticket</c> and <c>TicketActivity</c> do not exist yet,
/// so it is written <em>against the empty set</em> exactly as the <c>customer-records</c> intake
/// authorizes — <em>"build it against an empty ticket set and enrich when tickets land"</em>. It
/// returns a well-formed empty page today, which is what the intake's acceptance criterion
/// requires: <em>"the timeline for a customer with no tickets renders an empty state rather than an
/// error"</em>. That criterion is <b>met now and stays met</b> once tickets exist.
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

        // Story 06: join TicketActivity here.
        //
        // The query task 9 of 06-story-ticket-lifecycle.md replaces this with:
        //
        //     from t in db.Tickets.AsNoTracking().Where(t => t.CustomerId == customerId)
        //     join a in db.TicketActivities.AsNoTracking() on t.Id equals a.TicketId
        //     where a.Visibility != ActivityVisibility.Internal      // exclusion 1
        //     orderby a.OccurredAt descending
        //     select new TimelineEntryDto(...)
        //
        // TicketInternalNote is absent from that sketch ON PURPOSE — exclusion 2. Do not add it,
        // and do not "enrich" the projection with note bodies: the table is not joined, which is
        // what makes the rule impossible to break by accident (docs/data-model.md §2.9).
        //
        // Ticket(customerId, createdAt) and TicketActivity(ticketId, occurredAt) are already
        // declared for this read in docs/data-model.md §6, so no new index is needed.
        //
        // Until then the honest answer is that there is no activity: no ticket exists, and none can,
        // because the Tickets module has not been built. Returning an empty page is a fact about the
        // data, not a stub.
        //
        // The envelope is built directly rather than through ToPagedResultAsync, which composes EF's
        // async operators over a real IQueryable. Story 06 replaces this whole block with the query
        // above and calls ToPagedResultAsync on it, like every other list in the codebase.
        return new PagedResult<TimelineEntryDto>(
            Items: [],
            Page: pageNumber,
            PageSize: pageSize,
            TotalItems: 0,

            // Zero, not one: an empty collection has no pages. PagedQueryExtensions makes the same
            // choice for a genuinely empty query, so the two agree when Story 06 swaps them over.
            TotalPages: 0);
    }
}
