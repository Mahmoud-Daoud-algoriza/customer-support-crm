using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Customers;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The nine customer-scoped endpoints of docs/api-design.md §5.5 (Story 04 task 8).
///
/// <para>
/// <b>The policy is declared once, on the class: every action here is <c>RequireAgent</c>.</b> That
/// gate is the whole of the intake's rule <em>"a Customer cannot browse the customer directory"</em>
/// — enforcement point 1 of docs/architecture.md §4.2, reading the role resolved from the
/// authoritative row during this request rather than from the token (AD-15). A Manager and an
/// Administrator satisfy it, which is the A-4 hierarchy.
/// </para>
///
/// <para>
/// <b>There is no scoping beyond the role gate, and there must not be.</b> A customer profile is
/// organization-wide and readable by any staff role (docs/data-model.md §2.4). In particular
/// <c>branchId</c> on the list is a <em>filter</em>, never a boundary: branch appears in no
/// authorization predicate anywhere (A-2, §5 constraint 6).
/// </para>
///
/// <para>
/// <b>There is no delete action.</b> Deleting a customer is not an application operation
/// (docs/data-model.md §2.4), and the services expose no method that could back one.
/// </para>
///
/// Controllers are thin: bind, delegate to one Application service, return the result. Errors are
/// translated centrally by <c>ProblemDetailsExceptionHandler</c>, so there is no <c>try</c>/
/// <c>catch</c> here (docs/architecture.md §2.1).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAgent)]
public sealed class CustomersController(
    CustomerService customers,
    CustomerNoteService notes,
    CustomerTimelineService timeline,
    AttachmentService attachments) : ApiControllerBase
{
    // ---------------------------------------------------------------- Profiles

    /// <summary>
    /// Paged list. Filters <c>q</c> (name or email) and <c>branchId</c>; sortable by
    /// <c>fullName</c> and <c>createdAt</c> only, and an unknown sort field is a <c>400</c> rather
    /// than being silently ignored (AP-15).
    /// </summary>
    /// <remarks>
    /// The paging parameter is named <c>paging</c>, not <c>page</c>, for the reason
    /// <see cref="UsersController.List"/> records: a complex query parameter whose name matches an
    /// incoming query key switches the model binder to prefix mode and silently leaves
    /// <c>pageSize</c> unbound.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<CustomerListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<CustomerListItemDto>>> List(
        [FromQuery] CustomerListFilter filter, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await customers.ListAsync(filter, paging, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await customers.GetAsync(id, ct));

    /// <summary>
    /// Creates a profile. A duplicate email is <c>409 customer-email-in-use</c> — rejected, never
    /// reconciled, because there is no merge or dedupe tooling (A-10, T2-A).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var created = await customers.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Partial update of <c>fullName</c>, <c>phone</c>, <c>branchId</c> and <c>email</c>.
    ///
    /// <para>
    /// <b>Changing <c>email</c> also changes a linked portal login's sign-in address, in the same
    /// unit of work (A-19).</b> Two distinct conflicts can come back, and a client handles both:
    /// <c>409 customer-email-in-use</c> when another customer holds the address, and
    /// <c>409 user-already-exists</c> when another user does — staff included. Neither writes
    /// anything. The rule lives in <see cref="CustomerService.UpdateAsync"/>; this action only binds.
    /// </para>
    ///
    /// <para><c>externalReference</c> is settable through no endpoint (DM-6, §8.3), which
    /// <see cref="PatchCustomerRequest"/> enforces by having no property for it (AP-10).</para>
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<CustomerDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Patch(
        Guid id, PatchCustomerRequest request, CancellationToken ct) =>
        Ok(await customers.UpdateAsync(id, request, ct));

    // ---------------------------------------------------------------- Timeline

    /// <summary>
    /// The requirements §1.3 interaction history — a <b>read projection</b> over the customer's
    /// tickets and ticket activity, newest first, never a stored log (docs/architecture.md §2.5).
    /// <para>
    /// A customer with no activity gets an <b>empty page, not an error</b> (the intake's acceptance
    /// criterion). It reads empty until Story 06 completes the projection.
    /// </para>
    /// </summary>
    [HttpGet("{id:guid}/timeline")]
    [ProducesResponseType<PagedResult<TimelineEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<TimelineEntryDto>>> Timeline(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await timeline.GetAsync(id, paging, ct));

    // ---------------------------------------------------------------- Notes

    /// <summary>Paged, newest first (docs/api-design.md §5.5).</summary>
    [HttpGet("{id:guid}/notes")]
    [ProducesResponseType<PagedResult<CustomerNoteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<CustomerNoteDto>>> Notes(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await notes.ListAsync(id, paging, ct));

    /// <summary>
    /// Adds a note. The body carries <c>{ body }</c> and nothing else: author and timestamp are
    /// server-set from the authenticated caller (docs/api-design.md §7).
    ///
    /// <para>
    /// <b>A note cannot be edited or deleted afterwards</b> — there is no such endpoint and no such
    /// service method, because the entity is immutable once written (docs/data-model.md §2.5, §5
    /// constraint 16).
    /// </para>
    ///
    /// <para>
    /// <b>On the <c>Location</c> header.</b> §2.2 pairs <c>201</c> with a <c>Location</c>, but §5.5
    /// publishes no single-note endpoint for it to point at, so it addresses the collection the note
    /// now belongs to. Pointing at a URL that does not resolve would be worse, and inventing a
    /// <c>GET /customers/{id}/notes/{noteId}</c> to have a target would be adding contract surface
    /// no requirement asks for (AP-18).
    /// </para>
    /// </summary>
    [HttpPost("{id:guid}/notes")]
    [ProducesResponseType<CustomerNoteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerNoteDto>> AddNote(
        Guid id, CreateNoteRequest request, CancellationToken ct)
    {
        var created = await notes.AddAsync(id, request, ct);

        return CreatedAtAction(nameof(Notes), new { id }, created);
    }

    // ---------------------------------------------------------------- Attachments

    /// <summary>Metadata only — <c>storagePath</c> is in no response, ever (docs/api-design.md §6.7).</summary>
    [HttpGet("{id:guid}/attachments")]
    [ProducesResponseType<PagedResult<AttachmentMetadataDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<AttachmentMetadataDto>>> Attachments(
        Guid id, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await attachments.ListForCustomerAsync(id, paging, ct));

    /// <summary>
    /// Uploads one file — <c>multipart/form-data</c>, the only endpoint in this controller that is
    /// not <c>application/json</c> (AP-13).
    ///
    /// <para>
    /// <b>This action is the <c>IFormFile</c> boundary.</b> <c>IFormFile</c> is an HTTP type and the
    /// Application layer carries no ASP.NET Core reference (docs/architecture.md §2.1, AD-2), so it
    /// is mapped here onto <see cref="AttachmentUpload"/> — the same reason the story's
    /// <c>IAttachmentStorage</c> speaks <see cref="Stream"/>. Recorded as finding I-6.
    /// </para>
    ///
    /// <para>
    /// <c>SizeBytes</c> comes from <c>IFormFile.Length</c>, which the server measures from the parsed
    /// body rather than trusting a client-declared value; over the configured cap is
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

        var created = await attachments.UploadForCustomerAsync(
            id,
            new AttachmentUpload(content, file.FileName, ContentTypeOf(file), file.Length),
            ct);

        // The created attachment IS addressable — by its download URL — so unlike a note this
        // Location points at the resource itself.
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
