using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The customer's own path space — docs/api-design.md §5.7.
///
/// <para>
/// <b><c>/portal</c> is a separate path space, and that is <b>AP-5</b>, not a routing preference.</b>
/// It has different scoping (ownership, not department), different DTOs (no internal notes, no
/// assignee identity, no priority, no SLA) and different authority. Two path spaces make the
/// authorization reasoning explicit in the contract — and they are the reason the internal-note
/// visibility rule of T2-C is <em>structural</em>: <b>there is no route here that reaches internal
/// notes</b>, so no filter can be forgotten.
/// </para>
///
/// <para>
/// <b>The policy is declared once, on the class: <c>RequireCustomer</c>.</b> Every staff role
/// satisfies it too, because A-4's roles are hierarchical — but nothing here would be useful to
/// them: every action is scoped by <c>TicketScope</c> to the caller's own customer profile, and a
/// staff caller has none, so their reads match nothing and <see cref="Submit"/> fails closed on the
/// missing profile.
/// </para>
///
/// <para>
/// <b>Story 07 publishes three of §5.7's nine endpoints</b>: submit, read the thread, reply.
/// <b><a href="../../../../.squad/plans/customer-portal/13-story-portal-self-service.md">Story
/// 13</a> publishes the rest into this same controller</b> — own list, own detail, transition,
/// attachments and feedback. Do not create a second portal prefix.
/// </para>
///
/// <para>
/// <b>There is no inbound-channel route here or anywhere else</b> (<b>AP-11</b>). Story 18's fake
/// adapter calls <c>TicketMessageService.PostAsync</c> in-process; publishing an HTTP ingestion
/// endpoint would force the undecided system-actor question (<b>PF-2</b>) into the contract.
/// </para>
///
/// <para>
/// <b>Nothing here is real-time, and nothing polls</b> (T3-B): portal messaging is ordinary
/// request/response, and no part of this contract is described as chat.
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireCustomer)]
[Route("api/v1/portal/tickets")]
public sealed class PortalTicketsController(
    PortalTicketService portal,
    TicketMessageService messages) : ApiControllerBase
{
    /// <summary>
    /// <c>POST /portal/tickets</c> — <b>the web form</b> (requirements §3.5, docs/api-design.md
    /// §5.7).
    ///
    /// <para>
    /// <b>Authenticated submission only</b> (<b>A-9</b>): the class policy refuses an anonymous
    /// caller with <c>401</c>, and there is no anonymous variant of this route. Anonymous
    /// submission stays an open question in product-scope §9, untouched.
    /// </para>
    ///
    /// <para>
    /// <b><c>customerId</c>, <c>departmentId</c> and <c>priority</c> are not accepted</b>
    /// (docs/api-design.md §5.7, §7). They are absent from
    /// <see cref="SubmitPortalTicketRequest"/>, so a body carrying one is a <c>400</c> rather than
    /// accepted-and-ignored (<b>AP-10</b>). An unknown or unmapped <c>categoryCode</c> is also a
    /// <c>400</c>, naming the allowed values.
    /// </para>
    /// </summary>
    [HttpPost]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PortalTicketDto>> Submit(
        SubmitPortalTicketRequest request, CancellationToken ct)
    {
        var created = await portal.SubmitAsync(request, ct);

        // The Location header points at the thread rather than at the ticket: GET
        // /portal/tickets/{id} is Story 13's and does not exist yet, and a Location pointing at a
        // 404 would be worse than a slightly narrower one. Story 13 should retarget it to the
        // detail action once that action exists.
        return CreatedAtAction(nameof(Messages), new { id = created.Id }, created);
    }

    /// <summary>
    /// <c>GET /portal/tickets/{id}/messages</c> — <b>the customer's own thread</b>.
    ///
    /// <para>
    /// <b>Another customer's ticket is <c>404</c></b>, worded identically to one that does not exist
    /// (<b>AP-4</b>) — the ownership narrowing is <c>TicketScope</c>'s, the same helper every staff
    /// read composes.
    /// </para>
    ///
    /// <para>
    /// <b>Internal notes are unreachable by path</b> (T2-C, AP-5): they are a different entity with
    /// a different endpoint that this controller does not have. The payload is
    /// <see cref="PortalMessageDto"/>, which omits <c>channel</c> and <c>authorRole</c>
    /// (docs/api-design.md §6.4).
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType<PagedResult<PortalMessageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<PortalMessageDto>>> Messages(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await messages.ListForPortalAsync(id, paging, ct));

    /// <summary>
    /// <c>POST /portal/tickets/{id}/messages</c> — <b>the one status side effect in this API</b>
    /// (docs/api-design.md §5.7).
    ///
    /// <para>
    /// Per <b>R-13</b>: if the ticket is <c>Pending</c>, posting the reply transitions it to
    /// <c>Open</c> <b>in the same transaction</b>, writing both a <c>MessagePosted</c> and a
    /// <c>StatusChanged</c> activity entry — the latter <b>attributed to the replying customer</b>
    /// with <c>actorKind = User</c> (<b>R-14</b>). It fires from <c>Pending</c> <b>only</b>: a reply
    /// on <c>New</c> leaves it <c>New</c>, and a reply on <c>Resolved</c> does <b>not</b> reopen it
    /// — reopening is the explicit transition A-16 gives the customer, and Story 13 publishes it.
    /// </para>
    ///
    /// <para>
    /// <b>The response is the envelope of §6.4</b>, not a bare message:
    /// <c>{ message, ticketStatus, statusChanged }</c>. <c>statusChanged</c> is true only when the
    /// automatic reopen fired, <b>so the client never has to guess and never has to re-fetch</b> —
    /// which is what lets the portal show its <em>"reopened"</em> cue in place
    /// (docs/ui-design.md §7.3).
    /// </para>
    ///
    /// <para>
    /// A reply on a <c>Closed</c> or <c>Cancelled</c> ticket is <c>409 ticket-terminal</c> (A-5).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType<PortalPostedMessageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PortalPostedMessageDto>> PostMessage(
        Guid id, PostMessageRequest request, CancellationToken ct)
    {
        // The channel is the ENDPOINT's fact, never the client's (docs/api-design.md §7).
        var posted = await messages.PostAsync(id, request.Body, MessageChannel.Portal, ct);

        return CreatedAtAction(nameof(Messages), new { id }, PortalPostedMessageDto.From(posted));
    }
}
