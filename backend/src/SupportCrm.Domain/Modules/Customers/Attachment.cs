namespace SupportCrm.Domain.Modules.Customers;

/// <summary>
/// A single uploaded file — requirements §1.4, T2-A, docs/data-model.md §2.11. Local disk,
/// size-capped, minimal: <b>no virus scanning, no cloud object storage, no previews, no
/// versioning</b> (the <c>customer-records</c> intake's Out of scope list).
/// <para>
/// <b>Exactly one owner — a ticket XOR a customer, never both and never neither</b>
/// (docs/data-model.md §5 constraint 20). The rule is enforced by construction: the only two ways to
/// create an attachment are <see cref="ForCustomer"/> and <see cref="ForTicket"/>, each setting one
/// owner, so <em>no constructor allows a violation</em>. The database also carries a check
/// constraint, which is a second line rather than the rule's home.
/// </para>
/// <para>
/// <b>This type lives in the <c>Customers</c> module and is shared with <c>Tickets</c></b> —
/// docs/data-model.md §3 lists its module as "Customers/Tickets". There is deliberately no second
/// attachment type: the <c>Tickets</c> Application service calls the shared attachment service.
/// </para>
/// <para>
/// Immutable metadata, and no delete path is in scope (docs/data-model.md §2.11), so this type has
/// private setters and no mutator.
/// </para>
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class Attachment
{
    private Attachment()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>
    /// Set iff <see cref="CustomerId"/> is null. The foreign key arrives with Story 05, when
    /// <c>Ticket</c> exists; the column exists now because the XOR rule is part of this entity's
    /// shape regardless of when the other side lands.
    /// </summary>
    public Guid? TicketId { get; private set; }

    /// <summary>Set iff <see cref="TicketId"/> is null.</summary>
    public Guid? CustomerId { get; private set; }

    /// <summary>
    /// The <b>original</b> client-supplied name, kept for the download's
    /// <c>Content-Disposition</c>. It is <em>not</em> the name on disk: see
    /// <see cref="StoragePath"/>.
    /// </summary>
    public string FileName { get; private set; } = default!;

    /// <summary>
    /// The <c>Name</c> tier of docs/data-model.md §6.1, not <c>Code</c> — a real MIME type such as
    /// <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c> is 73
    /// characters, so the 64-character tier would reject a legitimate <c>.docx</c>.
    /// </summary>
    public string ContentType { get; private set; } = default!;

    /// <summary>Checked against the configured cap at upload (T2-A, <c>Attachments:MaxSizeBytes</c>).</summary>
    public long SizeBytes { get; private set; }

    /// <summary>
    /// The location on local disk. <b>Not a URL, and returned by no endpoint, ever</b>
    /// (docs/api-design.md §6.7): the bytes come only from
    /// <c>GET /attachments/{attachmentId}/content</c>, which passes the owner's authorization path
    /// so a file is not reachable by guessing a path (docs/architecture.md §4.4, AP-13, AP-19).
    /// <para>
    /// The name it holds is <b>server-generated</b>, never the client's, so a crafted
    /// <see cref="FileName"/> cannot escape the storage root.
    /// </para>
    /// </summary>
    public string StoragePath { get; private set; } = default!;

    /// <summary>Server-set from <c>ICurrentUser</c> (docs/api-design.md §7).</summary>
    public Guid UploadedByUserId { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>
    /// A customer-owned file. Staff-visible, following its owner's scope
    /// (docs/data-model.md §2.11).
    /// </summary>
    public static Attachment ForCustomer(
        Guid id,
        Guid customerId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A customer-owned attachment requires a customer.", nameof(customerId));
        }

        var attachment = CreateCore(
            id, fileName, contentType, sizeBytes, storagePath, uploadedByUserId, uploadedAt);

        attachment.CustomerId = customerId;

        return attachment;
    }

    /// <summary>
    /// A ticket-owned file. Department-scoped, following its owner's scope
    /// (docs/data-model.md §2.11); reachability is decided by the Story 05 ticket scoping helper.
    /// </summary>
    public static Attachment ForTicket(
        Guid id,
        Guid ticketId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("A ticket-owned attachment requires a ticket.", nameof(ticketId));
        }

        var attachment = CreateCore(
            id, fileName, contentType, sizeBytes, storagePath, uploadedByUserId, uploadedAt);

        attachment.TicketId = ticketId;

        return attachment;
    }

    /// <summary>
    /// Everything both factories share. Private, so the XOR rule has no third way around it: this
    /// method leaves <b>both</b> owner columns null and only a factory sets one.
    /// </summary>
    private static Attachment CreateCore(
        Guid id,
        string fileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        Guid uploadedByUserId,
        DateTimeOffset uploadedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        // The cap itself is configuration, checked by the Application layer, which is the only place
        // that can read it. A non-positive size is a different thing: it is not a file.
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeBytes), sizeBytes, "An attachment must have a positive size.");
        }

        if (uploadedByUserId == Guid.Empty)
        {
            throw new ArgumentException("An attachment requires an uploader.", nameof(uploadedByUserId));
        }

        return new Attachment
        {
            Id = id,
            TicketId = null,
            CustomerId = null,
            FileName = fileName.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            StoragePath = storagePath,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = uploadedAt,
        };
    }
}
