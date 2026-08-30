using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// The shared attachment service — Story 04 task 6. Local disk, size-capped, minimal (T2-A).
///
/// <para>
/// <b>One service for both owners.</b> <see cref="Attachment"/> lives in the <c>Customers</c> module
/// and is used by <c>Customers</c> and <c>Tickets</c> alike (docs/data-model.md §3), so the
/// ticket-side method bodies are written here too. <b>The two
/// <c>/tickets/{id}/attachments</c> endpoints are published by Story 05</b>, where <c>Ticket</c> and
/// its scoping helper exist — finding <b>S9-2</b>, resolved from the intakes rather than invented.
/// Story 05 has since published both, so the ticket half of Story 04's attachment criterion is met.
/// </para>
///
/// <para>
/// <b><c>storagePath</c> is returned by nothing here, ever</b> (docs/api-design.md §6.7).
/// <see cref="AttachmentMetadataDto"/> has no property for it, which is how that is enforced rather
/// than remembered. Bytes come only from <see cref="OpenForDownloadAsync"/>, which authorizes
/// through the owner (docs/architecture.md §4.4, AP-13, AP-19).
/// </para>
///
/// <para>
/// <b>Out of scope, and this is the class where each would otherwise creep in:</b> virus scanning,
/// cloud object storage, previews, versioning, and any delete path (the <c>customer-records</c>
/// intake; docs/data-model.md §2.11).
/// </para>
/// </summary>
public sealed class AttachmentService(
    IApplicationDbContext db,
    IAttachmentStorage storage,
    IOptions<AttachmentOptions> attachmentOptions,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    private long MaxSizeBytes => attachmentOptions.Value.MaxSizeBytes;

    /// <summary>
    /// <c>POST /customers/{id}/attachments</c> — <c>multipart/form-data</c> (AP-13).
    /// Over the configured cap is <c>413 attachment-too-large</c>.
    /// </summary>
    public async Task<AttachmentMetadataDto> UploadForCustomerAsync(
        Guid customerId, AttachmentUpload upload, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException("Customer not found.");
        }

        return await StoreAsync(upload, (id, path, uploader, at) =>
            Attachment.ForCustomer(
                id, customerId, upload.FileName, upload.ContentType, upload.SizeBytes,
                path, uploader, at), ct);
    }

    /// <summary>
    /// <c>POST /tickets/{id}/attachments</c> — the body landed with Story 04 and <b>Story 05
    /// publishes the endpoint and wires the scope</b> (S9-2, now closed). Reachability is decided
    /// by <see cref="EnsureTicketReachableAsync"/>, which composes <c>TicketScope</c>.
    /// </summary>
    public async Task<AttachmentMetadataDto> UploadForTicketAsync(
        Guid ticketId, AttachmentUpload upload, CancellationToken ct)
    {
        await EnsureTicketReachableAsync(ticketId, ct);

        return await StoreAsync(upload, (id, path, uploader, at) =>
            Attachment.ForTicket(
                id, ticketId, upload.FileName, upload.ContentType, upload.SizeBytes,
                path, uploader, at), ct);
    }

    /// <summary>
    /// <c>GET /customers/{id}/attachments</c> — metadata only (docs/api-design.md §6.7).
    /// </summary>
    public async Task<PagedResult<AttachmentMetadataDto>> ListForCustomerAsync(
        Guid customerId, PageQuery? page, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException("Customer not found.");
        }

        return await ListAsync(db.Attachments.Where(a => a.CustomerId == customerId), page, ct);
    }

    /// <summary>
    /// <c>GET /tickets/{id}/attachments</c> — as with the upload, the body landed with Story 04 and
    /// Story 05 published the endpoint (S9-2, now closed).
    /// </summary>
    public async Task<PagedResult<AttachmentMetadataDto>> ListForTicketAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        await EnsureTicketReachableAsync(ticketId, ct);

        return await ListAsync(db.Attachments.Where(a => a.TicketId == ticketId), page, ct);
    }

    /// <summary>
    /// <c>GET /attachments/{attachmentId}/content</c> — <b>AP-19: one endpoint for every role.</b>
    /// The single deliberate exception to AP-5's portal path split, because a byte stream has no DTO
    /// to vary by audience and the authorization question is identical for both:
    /// <em>may this caller reach the owning ticket or customer?</em>
    ///
    /// <para>
    /// <b>Authorization is by owner reachability, not by role</b>, so the controller carries
    /// <c>[Authorize]</c> with no role policy and the decision is made here:
    /// </para>
    /// <list type="bullet">
    ///   <item>a <b>customer-owned</b> file requires the caller to be an Agent or above — a customer
    ///     profile is staff-visible (docs/data-model.md §2.11);</item>
    ///   <item>a <b>ticket-owned</b> file requires the ticket to be reachable by the caller through
    ///     <c>TicketScope</c> — the same helper every ticket endpoint composes (AD-5).</item>
    /// </list>
    ///
    /// <para>
    /// <b>Missing and unreachable are the same answer: <c>404</c>, never <c>403</c></b> (AP-4). The
    /// endpoint must not reveal which of the two it was — a <c>403</c> would confirm that an id
    /// exists, which is the leak AP-4 exists to prevent.
    /// </para>
    /// </summary>
    public async Task<AttachmentContent> OpenForDownloadAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await db.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == attachmentId, ct)
            ?? throw new NotFoundException(NotReachable);

        if (attachment.CustomerId is not null)
        {
            // The role gate, expressed as a 404. IsInRoleAtLeast is the A-4 hierarchy check, so
            // Manager and Administrator pass it too (docs/api-design.md §4.2).
            if (!currentUser.IsInRoleAtLeast(UserRole.Agent))
            {
                throw new NotFoundException(NotReachable);
            }
        }
        else
        {
            // Ticket-owned. Refused today, for real reasons rather than as a stub — see the helper.
            await EnsureTicketReachableAsync(attachment.TicketId!.Value, ct);
        }

        return new AttachmentContent(
            await storage.OpenAsync(attachment.StoragePath, ct),
            attachment.FileName,
            attachment.ContentType);
    }

    /// <summary>
    /// A ticket-owned attachment inherits <b>the ticket's</b> scope — Story 05 task 7.
    ///
    /// <para>
    /// It answers the §4.3 question through the one helper that expresses it: an Agent reaches
    /// tickets in their own department, a Customer only their own, a Manager and an Administrator
    /// all of them. <c>TicketScope</c> is composed rather than restated, so this path cannot drift
    /// from the ticket endpoints (AD-5, 00-implementation-plan §6).
    /// </para>
    ///
    /// <para>
    /// <b>"No such ticket" and "not yours" are the same answer</b> — <c>404</c>, with the same
    /// message as a missing attachment (AP-4). <c>LoadScopedAsync</c> already throws exactly that,
    /// but its message names a ticket, and telling a caller that the <em>ticket</em> was not found
    /// while they asked about an <em>attachment</em> would leak which of the two exists. So the
    /// answer is re-thrown with this class's one constant.
    /// </para>
    ///
    /// <para>
    /// <b>Branch does not appear here</b>, and cannot: <c>TicketScope</c> has no branch predicate
    /// and <c>Ticket</c> has no branch member (A-2, docs/data-model.md §5 constraint 6).
    /// </para>
    /// </summary>
    private async Task EnsureTicketReachableAsync(Guid ticketId, CancellationToken ct)
    {
        var reachable = await db.Tickets.AsNoTracking()
            .ForCaller(currentUser)
            .AnyAsync(t => t.Id == ticketId, ct);

        if (!reachable)
        {
            throw new NotFoundException(NotReachable);
        }
    }

    /// <summary>
    /// One message for "no such attachment", "no such ticket" and "not yours" — AP-4 requires the
    /// three to be indistinguishable, and a distinct message per case would undo that at the
    /// Problem Details layer.
    /// </summary>
    private const string NotReachable = "Attachment not found.";

    /// <summary>
    /// The cap check, the write and the row — shared by both owners so the rule cannot be enforced
    /// on one path and forgotten on the other.
    /// </summary>
    private async Task<AttachmentMetadataDto> StoreAsync(
        AttachmentUpload upload,
        Func<Guid, string, Guid, DateTimeOffset, Attachment> build,
        CancellationToken ct)
    {
        // A zero-byte upload is not a file. Caught here as a 400 rather than reaching the domain
        // factory, which refuses it with an ArgumentOutOfRangeException and would surface as a 500.
        if (upload.SizeBytes <= 0)
        {
            throw new ValidationException("The uploaded file is empty.");
        }

        // T2-A, Attachments:MaxSizeBytes (Story 16 Part A). SizeBytes is measured by the server from
        // the parsed multipart body, not declared by the client (AP-13).
        if (upload.SizeBytes > MaxSizeBytes)
        {
            throw new PayloadTooLargeException(
                $"The attachment is {upload.SizeBytes} bytes; the configured maximum is {MaxSizeBytes}.");
        }

        // The bytes land first: the storage layer generates the name on disk, so a crafted FileName
        // cannot escape the root. Only then is the row written, so a failed write leaves no row
        // pointing at nothing.
        //
        // The converse — a committed file with no row, if SaveChangesAsync fails — is an orphaned
        // blob on disk. That is the cheaper of the two failures by a wide margin: it wastes space,
        // whereas a row pointing at a missing file breaks every later download. T2-A puts no
        // reconciliation sweep in scope, and inventing one here would be scope creep.
        var storagePath = await storage.SaveAsync(upload.Content, upload.FileName, ct);

        // uploadedByUserId is server-derived from the authenticated caller, never accepted from the
        // client (docs/api-design.md §7).
        var attachment = build(Guid.NewGuid(), storagePath, currentUser.Id, clock.GetUtcNow());

        db.Attachments.Add(attachment);

        // Not audited: uploading a file is business data, not a security event (AD-10).
        await db.SaveChangesAsync(ct);

        return new AttachmentMetadataDto(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.SizeBytes,
            new UserSummaryDto(currentUser.Id, currentUser.DisplayName),
            attachment.UploadedAt);
    }

    /// <summary>
    /// Newest first, paged. <b>Paged because AP-3 says every collection is</b> — "one paged
    /// envelope for all collections, even short ones", whose rejected alternative is bare arrays for
    /// small ones. docs/api-design.md §5.5 marks the notes list "Paged" and describes this one only
    /// as a "Metadata list"; AP-3 is the decision that settles the silence.
    /// <para>
    /// No sort parameter is published, so AP-15 has no whitelist to police here — the same choice
    /// <see cref="CustomerNoteService.ListAsync"/> makes, and the smaller surface.
    /// </para>
    /// </summary>
    private Task<PagedResult<AttachmentMetadataDto>> ListAsync(
        IQueryable<Attachment> attachments, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();

        // Joined to User because §6.7 embeds a UserSummary, not a bare id. An INNER join is correct:
        // UploadedByUserId is required and carries a Restrict foreign key, and a user is deactivated
        // rather than deleted (docs/data-model.md §2.1), so an uploader always resolves.
        //
        // StoragePath is NOT selected. It is not omitted later and not filtered out by the
        // serializer — it never enters the projection at all (docs/api-design.md §6.7).
        return (from a in attachments.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.UploadedByUserId equals u.Id
                orderby a.UploadedAt descending
                select new AttachmentMetadataDto(
                    a.Id,
                    a.FileName,
                    a.ContentType,
                    a.SizeBytes,
                    new UserSummaryDto(u.Id, u.DisplayName),
                    a.UploadedAt))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }
}
