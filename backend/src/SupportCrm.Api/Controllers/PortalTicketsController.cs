using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Customers;
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
/// <b>Story 13 completed §5.7.</b> Story 07 published three endpoints — submit, read the thread,
/// reply — and Story 13 published the remaining six here: own list, own detail, transition, the two
/// attachment actions and feedback. With <c>PortalKnowledgeController</c>'s two, <b>the portal path
/// space is complete at eleven endpoints</b>, matching docs/api-design.md §5.7. <b>There is no
/// second portal prefix, and no further portal route is expected.</b>
/// </para>
///
/// <para>
/// <b>No action here returns an assignee, a department, a priority, an SLA field or an internal
/// note</b> (AP-16, UI-11) — and none <em>could</em>: the return types
/// (<see cref="PortalTicketDto"/>, <see cref="PortalMessageDto"/>, <see cref="FeedbackDto"/>,
/// <c>AttachmentMetadataDto</c>) have no such member. <c>PortalIsolationTests</c> asserts it on the
/// <b>serialized JSON</b> rather than on the type, because a type is what this file promises and the
/// payload is what a customer receives.
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
    TicketMessageService messages,
    CustomerFeedbackService feedback,
    AttachmentService attachments) : ApiControllerBase
{
    /// <summary>
    /// <c>GET /portal/tickets</c> — <b>the customer's own requests</b> (docs/api-design.md §5.7,
    /// docs/ui-design.md §7.1).
    ///
    /// <para>
    /// <b>There is no <c>customerId</c> parameter, and its absence is the rule.</b> Ownership is not
    /// a filter a caller supplies: <c>TicketScope.ForCaller</c> narrows the query to the caller's own
    /// profile before the <c>status</c> filter is applied (AD-5), so there is no id to guess and
    /// nothing a client could send to widen the result.
    /// </para>
    ///
    /// <para>
    /// Filter: <c>status</c>. Sort whitelist: <c>createdAt</c> alone (AP-15) — anything else is a
    /// <c>400</c>. Paged envelope, like every collection (AP-3).
    /// </para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<PortalTicketDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<PortalTicketDto>>> List(
        [FromQuery] string? status, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await portal.ListAsync(status, paging, ct));

    /// <summary>
    /// <c>GET /portal/tickets/{id}</c> — <b>the customer's own request</b> (docs/ui-design.md §7.3).
    ///
    /// <para>
    /// <b>Another customer's id is <c>404</c></b>, worded identically to one that does not exist
    /// (<b>AP-4</b>). The payload is <see cref="PortalTicketDto"/>: <b>no assignee</b> (AP-16),
    /// <b>no department, no priority, no SLA or breach field</b> (UI-11) — <em>"the UI cannot show
    /// what the contract does not return."</em>
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PortalTicketDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await portal.GetAsync(id, ct));

    /// <summary>
    /// <c>POST /portal/tickets/{id}/transition</c> — <b>the whole of a customer's lifecycle
    /// authority</b> (docs/api-design.md §5.7, <b>A-16</b>).
    ///
    /// <para>
    /// Two targets, and no others: <b><c>Cancelled</c> while the request is <c>New</c></b> — a window
    /// <b>A-18</b> keeps genuinely open, because an auto-assigned ticket is still <c>New</c> — and
    /// <b><c>Open</c> to reopen a <c>Resolved</c> one</b>. Anything else is <c>403
    /// transition-not-permitted</c>, decided by <c>TransitionAuthority</c> and <b>not by this
    /// action</b>, which compares no statuses inline.
    /// </para>
    ///
    /// <para>
    /// <b>A customer cannot close a request</b> (A-16, a deliberate consequence rather than an
    /// oversight), and <b>cannot ask for <c>Pending → Open</c></b> — R-13 makes that automatic on a
    /// reply, which is exactly why docs/ui-design.md §7.3 forbids the UI from offering a manual
    /// reopen on a <c>Pending</c> request.
    /// </para>
    ///
    /// <para>
    /// The refusals stay distinguishable: out of scope is <c>404</c> (AP-4), an unauthorized target
    /// is <c>403</c>, an edge outside A-5's graph is <c>409 illegal-transition</c> carrying
    /// <c>allowedTransitions</c> (§6.12).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType<PortalTicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PortalTicketDto>> Transition(
        Guid id, PortalTransitionRequest request, CancellationToken ct) =>
        Ok(await portal.TransitionAsync(id, TicketStatusParser.Parse(request.TargetStatus), ct));

    /// <summary>
    /// <c>GET /portal/tickets/{id}/attachments</c> — metadata only (docs/api-design.md §6.7).
    ///
    /// <para>
    /// <b>The file inherits the ticket's scope</b>: <c>AttachmentService</c> composes the same
    /// <c>TicketScope</c> every read does, so another customer's ticket is <c>404</c> before any
    /// attachment row is touched (AP-4). <b><c>storagePath</c> is in no response, ever</b>
    /// (docs/architecture.md §4.4).
    /// </para>
    ///
    /// <para>
    /// The download itself is <c>GET /attachments/{id}/content</c> — <b>one endpoint for every
    /// role</b>, and <b>AP-19</b>'s single deliberate exception to AP-5's path split: a byte stream
    /// has no DTO to vary by audience, and the authorization question is owner reachability, which is
    /// identical for both sides. There is no portal-specific download route and there must not be
    /// one.
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}/attachments")]
    [ProducesResponseType<PagedResult<AttachmentMetadataDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<AttachmentMetadataDto>>> Attachments(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await attachments.ListForTicketAsync(id, paging, ct));

    /// <summary>
    /// <c>POST /portal/tickets/{id}/attachments</c> — <c>multipart/form-data</c> (<b>AP-13</b>), the
    /// only action in this controller that is not <c>application/json</c>.
    ///
    /// <para>
    /// <b>Attachment upload is offered after submission, not on the form</b> (docs/ui-design.md §7.2,
    /// §7.3): the web form has exactly four inputs, and a file needs a ticket to belong to.
    /// </para>
    ///
    /// <para>
    /// The <c>IFormFile</c> boundary stops here, exactly as it does on the staff route: the
    /// Application layer carries no ASP.NET Core reference (AD-2, finding I-6). <b>Size is measured
    /// from the parsed body, never declared by the client</b>; over the configured cap is
    /// <c>413 attachment-too-large</c> (T2-A), surfaced inline on the uploader (docs/ui-design.md §9).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [ProducesResponseType<AttachmentMetadataDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<AttachmentMetadataDto>> UploadAttachment(
        Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
        {
            throw new ValidationException("A file is required. Send it as multipart/form-data.");
        }

        await using var content = file.OpenReadStream();

        var created = await attachments.UploadForTicketAsync(
            id,
            new AttachmentUpload(content, file.FileName, ContentTypeOf(file), file.Length),
            ct);

        // AP-19: the download is the shared, role-agnostic route, so that is what Location points at.
        return CreatedAtAction(
            actionName: nameof(AttachmentsController.Content),
            controllerName: "Attachments",
            routeValues: new { attachmentId = created.Id },
            value: created);
    }

    /// <summary>
    /// <c>POST /portal/tickets/{id}/feedback</c> — <b>the sole CSAT input in the system</b>
    /// (requirements §8.5, T2-F, docs/api-design.md §5.7).
    ///
    /// <para>
    /// <b>Once per ticket, write-once.</b> A second submission is <c>409
    /// feedback-already-submitted</c>; a request that has never reached <c>Resolved</c> is
    /// <c>409 feedback-not-available</c>; another customer's ticket is <c>404</c> (AP-4).
    /// </para>
    ///
    /// <para>
    /// <b>Declining is not an endpoint.</b> It is simply never calling this one — the absence of a
    /// row is the meaningful outcome (T2-F, docs/data-model.md §2.15), which is why there is no
    /// "decline" action here and no <c>DELETE</c> or <c>PATCH</c> counterpart either.
    /// </para>
    ///
    /// <para>
    /// <b>⚠ <c>rating</c>'s permitted values are not fixed by this contract (OQ-1).</b>
    /// <c>GET /config</c> publishes <c>feedback.ratingScale</c> and the service validates against it,
    /// answering <c>400</c> outside the range. <b>No scale is written into this action, its request
    /// model, the schema or the UI.</b>
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/feedback")]
    [ProducesResponseType<FeedbackDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeedbackDto>> SubmitFeedback(
        Guid id, SubmitFeedbackRequest request, CancellationToken ct)
    {
        var created = await feedback.SubmitAsync(id, request.Rating!.Value, request.Comment, ct);

        // There is no GET for one feedback row — §5.7 publishes none, and a write-once rating the
        // customer just sent needs no read endpoint. Location therefore points at the request whose
        // `hasFeedback` has now become true, which is the resource whose state changed.
        return CreatedAtAction(nameof(Get), new { id }, created);
    }

    /// <summary>
    /// A client may omit the part's content type. The domain requires one, so the boundary supplies
    /// the RFC 2046 default rather than letting an empty string reach a factory that would refuse it
    /// with a <c>500</c>. It is stored as metadata and trusted for nothing.
    /// </summary>
    private static string ContentTypeOf(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;

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

        // Retargeted at the detail action, which Story 13 published — Story 07 pointed this at the
        // thread only because GET /portal/tickets/{id} did not exist yet and a Location resolving to
        // a 404 would have been worse than a narrower one. The created resource is the request, so
        // that is what Location names.
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
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
