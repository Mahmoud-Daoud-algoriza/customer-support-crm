using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Customers;
using SupportCrm.Application.Modules.Tickets;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The staff ticket endpoints of docs/api-design.md §5.6 — <b>Story 05's seven, plus Story 06's
/// three</b>: <c>POST /transition</c>, <c>POST /escalate</c> and <c>GET /activity</c>.
///
/// <para>
/// <b>The policy is declared once, on the class: every action here is <c>RequireAgent</c>.</b> A
/// <c>Customer</c> calling any of them gets <c>403</c> — the staff path space is role-gated, and the
/// portal has its own <c>/portal/tickets</c> routes (Story 07, AP-16). A Manager and an
/// Administrator satisfy the policy, which is the A-4 hierarchy.
/// </para>
///
/// <para>
/// <b>The role gate is not the department rule.</b> Scoping to a department happens inside
/// <c>TicketService</c>, through <c>TicketScope</c>, on every read and every write — including
/// write paths, which re-check on load so a guessed id is refused (docs/architecture.md §4.3
/// points 2 and 3). An out-of-department ticket answers <c>404</c>, never <c>403</c> (AP-4).
/// </para>
///
/// <para>
/// <b>The remaining seven <c>/tickets/{id}</c> endpoints are added to this same controller or a
/// sibling in this folder</b> by Stories 07, 11, 12 and 14 — messages, internal notes, tasks and the
/// AI assists. <b>Do not create a second route prefix.</b>
/// </para>
///
/// <para>
/// <b><c>status</c> is not a field on <c>PATCH /tickets/{id}</c>, and no delete action exists
/// anywhere.</b> A status change goes through the dedicated <c>/transition</c> endpoint, behind A-5
/// legality and A-16 authority (AP-1, AP-6) — modelling it as a patchable field would let a caller
/// bypass the matrix by writing a property. A ticket is cancelled, never deleted.
/// </para>
///
/// Controllers are thin: bind, delegate to one Application service, return the result. Errors are
/// translated centrally by <c>ProblemDetailsExceptionHandler</c>, so there is no <c>try</c>/
/// <c>catch</c> here (docs/architecture.md §2.1).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAgent)]
public sealed class TicketsController(
    TicketService tickets,
    TicketLifecycleService lifecycle,
    TicketActivityQueryService activity,
    AttachmentService attachments) : ApiControllerBase
{
    // ------------------------------------------------------------------ Tickets

    /// <summary>
    /// The queue. Paged and <b>department-scoped</b>: an Agent sees only their own department's
    /// tickets with no filter applied at all.
    ///
    /// <para>
    /// Filters are <c>status</c>, <c>priority</c>, <c>categoryCode</c>, <c>assigneeId</c> (which
    /// accepts the literal <c>me</c>), <c>departmentId</c>, <c>breached</c> and <c>q</c>. Sortable
    /// by <c>resolutionDueAt</c>, <c>firstResponseDueAt</c>, <c>createdAt</c> and <c>priority</c>
    /// only; anything else is a <c>400</c> rather than being silently ignored (AP-15).
    /// </para>
    ///
    /// <para>
    /// <b>Default sort is SLA urgency</b> — <c>resolutionDueAt:asc</c> with breached tickets first.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The paging parameter is named <c>paging</c>, not <c>page</c>, for the reason
    /// <see cref="UsersController.List"/> records: a complex query parameter whose name matches an
    /// incoming query key switches the model binder to prefix mode and silently leaves
    /// <c>pageSize</c> unbound.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<TicketListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] TicketListFilter filter, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await tickets.ListAsync(filter, paging, ct));

    /// <summary>
    /// One ticket. Out of scope answers <c>404</c> with the same body as a genuinely missing id
    /// (AP-4) — a caller must not be able to confirm that a ticket exists in a department they
    /// cannot see.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await tickets.GetAsync(id, ct));

    /// <summary>
    /// Creates a ticket on behalf of a customer.
    ///
    /// <para>
    /// <c>departmentId</c> is optional: omitted, it comes from the category → department map
    /// (A-14); supplied, it overrides, because the mapping is a default and not a cage for agents.
    /// </para>
    ///
    /// <para>
    /// <b><c>isUrgent</c> is not accepted here</b> — it is customer input only (A-17), so
    /// <see cref="CreateTicketRequest"/> has no such property and a body carrying one is a
    /// <c>400</c> rather than being accepted and ignored (AP-10, finding I-9). The same holds for
    /// <c>status</c> and the SLA fields, all server-derived (docs/api-design.md §7).
    /// </para>
    /// </summary>
    [HttpPost]
    [ProducesResponseType<TicketDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TicketDto>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var created = await tickets.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// <b><c>categoryCode</c> and <c>priority</c> only</b> (docs/api-design.md §5.6).
    /// <c>status</c> is not a patchable field (AP-1) and <c>assignedUserId</c> has its own endpoint.
    /// <para>
    /// Changing the priority <b>does not move the SLA due dates</b> — they freeze at creation
    /// (<b>A-20</b>, closing OQ-2).
    /// </para>
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> Patch(
        Guid id, PatchTicketRequest request, CancellationToken ct) =>
        Ok(await tickets.PatchAsync(id, request, ct));

    /// <summary>
    /// Assigns or reassigns the ticket.
    ///
    /// <para>
    /// <b>Assignment does not change status</b> (A-18): a `New` ticket that gets an assignee stays
    /// `New`. The response carries both facts, and a client must render them independently.
    /// </para>
    ///
    /// <para>
    /// An assignee who is not an active staff user in the ticket's department is
    /// <c>422 assignee-out-of-department</c> — so a ticket can never be assigned to someone who
    /// could not then see it (docs/data-model.md §5 constraint 10).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/assignment")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketDto>> Assign(
        Guid id, AssignTicketRequest request, CancellationToken ct) =>
        Ok(await tickets.AssignAsync(id, request, ct));

    // ---------------------------------------------------------------- Lifecycle

    /// <summary>
    /// <c>POST /tickets/{id}/transition</c> — <b>one endpoint for the whole A-5 × A-16 matrix</b>
    /// (AP-6). A verb per transition would scatter the matrix across seven endpoints and let the
    /// <c>409</c> shape drift verb by verb.
    ///
    /// <para>
    /// <b>The four refusals are distinguishable on purpose</b> (docs/api-design.md §5.6):
    /// <c>403</c> from the role gate, <c>404</c> for a ticket outside the caller's scope (AP-4),
    /// <c>403 transition-not-permitted</c> for A-16 authority, and <c>409 illegal-transition</c> for
    /// A-5 legality — the last carrying <c>allowedTransitions</c> in the problem detail, which is
    /// the only place the contract publishes that set today (finding <b>F-1</b>).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TicketDto>> Transition(
        Guid id, TransitionTicketRequest request, CancellationToken ct) =>
        Ok(await lifecycle.TransitionAsync(id, TicketStatusParser.Parse(request.TargetStatus), ct));

    /// <summary>
    /// <c>POST /tickets/{id}/escalate</c> — <b>its own endpoint, never part of
    /// <c>/transition</c></b> (AP-7), because A-5 is explicit that escalation is an action and not a
    /// status change.
    ///
    /// <para>
    /// <b>No body.</b> There is nothing to supply: the effect is fixed — priority up exactly one
    /// level, <c>Urgent</c> stays <c>Urgent</c>, status unchanged. Accepting a target priority would
    /// invent escalation tiers that A-5 does not have.
    /// </para>
    ///
    /// <para>
    /// Returns <c>200</c> with the ticket <b>in every case</b>, including when the department has no
    /// manager: under <b>A-21</b> the notification climbs to the next authority level, and an empty
    /// recipient set never blocks the escalation.
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/escalate")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TicketDto>> Escalate(Guid id, CancellationToken ct) =>
        Ok(await lifecycle.EscalateAsync(id, ct));

    /// <summary>
    /// <c>GET /tickets/{id}/activity</c> — the append-only history, chronological and paged.
    /// <b>Internal entries are included</b>: §5.6 words it as *"Full history, internal entries
    /// included"*, and this route is staff-only by the class policy with no portal path reaching it
    /// (AP-5). The customer-facing filter lives in the timeline projection, not here.
    /// </summary>
    [HttpGet("{id:guid}/activity")]
    [ProducesResponseType<PagedResult<TicketActivityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TicketActivityDto>>> Activity(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await activity.ListAsync(id, paging, ct));

    // -------------------------------------------------------------- Attachments

    /// <summary>
    /// Metadata list (docs/api-design.md §6.7). <b>Completes Story 04's attachment acceptance
    /// criterion</b>, whose ticket half was deferred here by finding S9-2.
    /// <para>
    /// The file inherits <b>the ticket's</b> scope: an unreachable ticket is <c>404</c>, worded
    /// identically to a missing attachment (AP-4). <c>storagePath</c> is in no response, ever.
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}/attachments")]
    [ProducesResponseType<PagedResult<AttachmentMetadataDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<AttachmentMetadataDto>>> Attachments(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await attachments.ListForTicketAsync(id, paging, ct));

    /// <summary>
    /// Uploads one file to a ticket — <c>multipart/form-data</c> (AP-13), the only endpoint in this
    /// controller that is not <c>application/json</c>.
    /// <para>
    /// The <c>IFormFile</c> boundary is here, exactly as it is on the customer upload: the
    /// Application layer carries no ASP.NET Core reference (AD-2, finding I-6). Size is measured
    /// from the parsed body, never declared by the client; over the configured cap is
    /// <c>413 attachment-too-large</c> (T2-A).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/attachments")]
    [ProducesResponseType<AttachmentMetadataDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<AttachmentMetadataDto>> Upload(
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

        return CreatedAtAction(
            actionName: nameof(AttachmentsController.Content),
            controllerName: "Attachments",
            routeValues: new { attachmentId = created.Id },
            value: created);
    }

    /// <summary>
    /// A client may omit the part's content type. The domain requires one, so the boundary supplies
    /// the RFC 2046 default rather than letting an empty string reach a factory that would refuse it
    /// with a <c>500</c>. It is stored as metadata and trusted for nothing.
    /// </summary>
    private static string ContentTypeOf(IFormFile file) =>
        string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
}
