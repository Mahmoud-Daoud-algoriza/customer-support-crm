namespace SupportCrm.Application.Abstractions;

/// <summary>
/// Where an attachment's bytes live. <b>Application declares the interface, Infrastructure
/// implements it</b> — the same seam pattern as <see cref="ITokenIssuer"/>
/// (docs/architecture.md §5, AD-2, AD-11), because the dependency rule is compiler-enforced and
/// Application cannot name a file-system type in Infrastructure.
/// <para>
/// <b>Local disk by design</b> — T2-A fixes storage as local disk with a size cap, and the
/// <c>customer-records</c> intake puts cloud object storage, virus scanning, previews and
/// versioning out of scope. This interface is not an abstraction over storage providers; it is the
/// layer boundary. It has exactly the two operations the story needs and no delete, because
/// docs/data-model.md §2.11 puts no delete path in scope.
/// </para>
/// <para>
/// <b>The returned path is never a URL and never reaches a client</b>
/// (docs/api-design.md §6.7): it is stored in <c>Attachment.StoragePath</c>, and bytes are served
/// only through <c>GET /attachments/{attachmentId}/content</c>, which authorizes through the owning
/// ticket or customer (docs/architecture.md §4.4, AP-13, AP-19).
/// </para>
/// </summary>
public interface IAttachmentStorage
{
    /// <summary>
    /// Writes <paramref name="content"/> and returns the value to store in
    /// <c>Attachment.StoragePath</c>.
    /// <para>
    /// <paramref name="fileName"/> is the <b>original client-supplied name</b> and is used only for
    /// its extension. An implementation <b>must generate the name on disk itself</b>, so a crafted
    /// name cannot escape the storage root; the original is preserved in
    /// <c>Attachment.FileName</c> for the download's <c>Content-Disposition</c>.
    /// </para>
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct);

    /// <summary>
    /// Opens a stored file for reading.
    /// <para>
    /// <b>Authorization has already happened</b> by the time this is called — the Application layer
    /// resolves the attachment's owner and decides reachability (AP-19). An implementation must
    /// still refuse a path that resolves outside the storage root, because defence in depth is
    /// cheaper than trusting a column.
    /// </para>
    /// </summary>
    Task<Stream> OpenAsync(string storagePath, CancellationToken ct);
}
