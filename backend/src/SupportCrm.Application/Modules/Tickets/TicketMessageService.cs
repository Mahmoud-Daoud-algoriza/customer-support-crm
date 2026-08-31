using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>The one message ingestion service</b> — docs/architecture.md §5.2, requirements §3.3 and
/// §3.5, T2-B.
///
/// <para>
/// <b>One method serves every channel.</b> <see cref="PostAsync"/> is called by the staff reply
/// endpoint, by the portal reply endpoint, and — later, in-process — by Story 18's inbound fake
/// adapter. <b>Channel origin is <em>data</em>, not a code path</b>: the channel is a parameter that
/// is stored and never branched on, which is the whole claim §5.2 makes and the one a reviewer
/// should test. Adding email or WhatsApp means writing an adapter that calls this method, not a
/// second ingestion path.
/// </para>
///
/// <para>
/// <b>Story 18's adapter gets no HTTP route</b> (<b>AP-11</b>). Publishing an inbound endpoint would
/// need a system actor that <c>TicketMessage.authorUserId</c> cannot express — which is how
/// <b>PF-2</b> is avoided rather than papered over.
/// </para>
///
/// <para>
/// <b>This is not the notification seam.</b> In-app notifications (A-13) are a different thing and
/// must not be confused with the channel seam (§5.2). One of them appears below —
/// <c>CustomerReplied</c> — and it is raised by the <em>message</em>, never by the status change.
/// </para>
///
/// <para>
/// <b>One unit of work, committed once</b> (docs/architecture.md §3). The message, its activity row,
/// the first-response stamp and R-13's automatic transition are added together and saved together,
/// so no committed state has the message without the reopen it caused.
/// </para>
/// </summary>
public sealed class TicketMessageService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketActivityRecorder activity,
    TicketLifecycleService lifecycle,
    INotificationPublisher notifications,
    TimeProvider clock)
{
    /// <summary>
    /// <c>POST /tickets/{id}/messages</c> and <c>POST /portal/tickets/{id}/messages</c>, and
    /// eventually Story 18's inbound adapter.
    ///
    /// <para>
    /// <b>The order of operations is fixed and must not be rearranged:</b>
    /// </para>
    /// <list type="number">
    ///   <item><description><b>Scope</b> — <c>LoadScopedAsync</c>. Missing <em>or</em> out of scope is <c>404</c>, worded identically (AP-4). A customer replying to another customer's ticket lands here.</description></item>
    ///   <item><description><b>Terminal guard</b> — <c>Closed</c> or <c>Cancelled</c> is <c>409 ticket-terminal</c> (A-5, docs/data-model.md §5 constraint 8).</description></item>
    ///   <item><description><b>Derive direction</b> from the caller's role (<b>PF-7</b>) — never from the request.</description></item>
    ///   <item><description>Persist the message.</description></item>
    ///   <item><description>Write <b>exactly one</b> <c>MessagePosted</c> activity row, linking the message (DM-4, §5 constraint 17).</description></item>
    ///   <item><description>On <c>Outbound</c>, stamp <c>firstRespondedAt</c> if it is still null.</description></item>
    ///   <item><description>On <c>Inbound</c>, apply R-13's automatic transition.</description></item>
    ///   <item><description>On <c>Inbound</c> to an assigned ticket, raise <c>CustomerReplied</c> (A-13).</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Scope before the terminal guard</b> for the same reason the transition endpoint checks
    /// scope first: a <c>409</c> on someone else's ticket would confirm that it exists (AP-4).
    /// </para>
    /// </summary>
    public async Task<PostedMessageDto> PostAsync(
        Guid ticketId, string body, MessageChannel channel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ValidationException("A message body is required.");
        }

        // 1. Scope. Fetch-then-authorize, never authorize-then-fetch-by-id (architecture §4.3).
        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        // 2. Terminal. "Closed and Cancelled are terminal: no further messages, notes or
        //    transitions" (docs/data-model.md §2.6 invariant 2, §5 constraint 8; A-5).
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new ConflictException(TicketTerminal, TicketTerminalMessage);
        }

        // 3. Direction is DERIVED FROM THE AUTHOR'S ROLE and is never read from the request
        //    (PF-7, docs/api-design.md §7). The Customer role is the only inbound side; every staff
        //    role is outbound, which A-4's hierarchy makes exhaustive.
        var direction = currentUser.Role is UserRole.Customer
            ? MessageDirection.Inbound
            : MessageDirection.Outbound;

        var postedAt = clock.GetUtcNow();

        // 4. Persist. Immutable from this point on — there is no mutator (§5 constraint 16).
        var message = TicketMessage.Post(
            Guid.NewGuid(), ticket.Id, currentUser.Id, direction, channel, body, postedAt);

        db.TicketMessages.Add(message);

        // 5. Exactly one MessagePosted row, linking the message. The BODY IS NOT COPIED here —
        //    content lives once and the activity row is the ordering spine that points at it
        //    (DM-4, §5 constraint 17). Visibility is CustomerVisible by construction: the factory
        //    accepts no other, because a TicketMessage is customer-facing by definition.
        await activity.RecordMessagePostedAsync(ticket.Id, message.Id, ct);

        // 6. The first outbound message sets firstRespondedAt (docs/data-model.md §2.8). The
        //    "if it is still null" half is the entity's, not this method's — a second outbound
        //    message calls the same mutator and it is a no-op, so "once" cannot be got wrong here.
        if (direction is MessageDirection.Outbound)
        {
            ticket.MarkFirstResponded(postedAt);
        }

        var statusChanged = false;

        if (direction is MessageDirection.Inbound)
        {
            // 7. R-13 / R-14 — Story 06 owns the rule AND its "only from Pending" guard. The
            //    condition is deliberately NOT re-expressed here: restating it would be two homes
            //    for one rule, and the second would be the one that drifts.
            statusChanged = await lifecycle.ApplyAutomaticCustomerReplyTransitionAsync(
                ticket, currentUser.Id, ct);

            // 8. A-13's CustomerReplied — raised by the MESSAGE, and only when someone is on the
            //    hook for it. The automatic status change above raises NONE: A-13 defines exactly
            //    four notification types and none of them is a status change (docs/data-model.md
            //    §2.8, §2.12). These are two different rules that happen to fire on one request.
            if (ticket.AssignedUserId is { } assigneeId)
            {
                await notifications.PublishAsync(
                    assigneeId, NotificationType.CustomerReplied, ticket.Id, ct);
            }
        }

        await db.SaveChangesAsync(ct);

        var dto = new MessageDto(
            message.Id,
            message.TicketId,
            new UserSummaryDto(currentUser.Id, currentUser.DisplayName),
            currentUser.Role.ToString(),
            message.Direction.ToString(),
            message.Channel.ToString(),
            message.Body,
            message.PostedAt);

        return new PostedMessageDto(dto, ticket.Status.ToString(), statusChanged);
    }

    /// <summary>
    /// <c>GET /tickets/{id}/messages</c> — the staff thread, oldest first.
    /// <para>
    /// <b>A thread is read forwards.</b> It is a conversation, not a change log, so it is ordered
    /// ascending by <c>postedAt</c> — the <c>TicketMessage(TicketId, PostedAt)</c> index of
    /// docs/data-model.md §6 exists for exactly this read.
    /// </para>
    /// </summary>
    public async Task<PagedResult<MessageDto>> ListAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = await ScopeThreadAsync(ticketId, page, ct);

        return await ThreadQuery(ticketId)
            .Select(m => new MessageDto(
                m.Id,
                m.TicketId,
                db.Users.Where(u => u.Id == m.AuthorUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
                db.Users.Where(u => u.Id == m.AuthorUserId)
                    .Select(u => u.Role.ToString()).First(),
                m.Direction.ToString(),
                m.Channel.ToString(),
                m.Body,
                m.PostedAt))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>GET /portal/tickets/{id}/messages</c> — the customer's own thread.
    ///
    /// <para>
    /// <b>The same scope helper, and that is what makes ownership hold</b>: <c>TicketScope</c>
    /// narrows a <c>Customer</c> caller to tickets whose customer is themselves, so another
    /// customer's thread is a <c>404</c> here without this method knowing the rule (A-2, AP-4).
    /// </para>
    ///
    /// <para>
    /// <b>Internal notes are unreachable by construction</b>, not by a filter: this query reads
    /// <c>TicketMessages</c> and does not join <c>TicketInternalNote</c> at all — a different table,
    /// a different endpoint, and no portal path reaches it (T2-C, docs/data-model.md §2.9, AP-5).
    /// Story 14 asserts it once notes exist.
    /// </para>
    /// </summary>
    public async Task<PagedResult<PortalMessageDto>> ListForPortalAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = await ScopeThreadAsync(ticketId, page, ct);

        return await ThreadQuery(ticketId)
            .Select(m => new PortalMessageDto(
                m.Id,
                m.TicketId,
                db.Users.Where(u => u.Id == m.AuthorUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
                m.Direction.ToString(),
                m.Body,
                m.PostedAt))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// Scope first: an unreachable ticket's thread is <c>404</c>, worded identically to a ticket
    /// that does not exist. Reading a thread must not reveal what reading the ticket hides (AP-4).
    /// </summary>
    private async Task<(int PageNumber, int PageSize)> ScopeThreadAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        _ = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        return page.Normalize();
    }

    /// <summary>
    /// The thread, in order. One query shape, two projections — the staff and portal payloads differ
    /// (AP-5) but the rows and their ordering do not.
    /// </summary>
    private IQueryable<TicketMessage> ThreadQuery(Guid ticketId) =>
        db.TicketMessages.AsNoTracking()
            .Where(m => m.TicketId == ticketId)
            .OrderBy(m => m.PostedAt)
            .ThenBy(m => m.Id); // a stable tiebreak, so paging is meaningful within one timestamp

    /// <summary>
    /// The slug for a message on a terminal ticket. <b>It is not <c>illegal-transition</c></b>:
    /// nothing was transitioned, and reusing that slug would make the front end render a lifecycle
    /// message for a refused reply (AP-2, docs/api-design.md §6.12).
    /// </summary>
    private const string TicketTerminal = "ticket-terminal";

    private const string TicketTerminalMessage =
        "This ticket is closed. No further messages are accepted.";
}
