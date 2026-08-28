using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// <c>AttachmentMetadata</c> — docs/api-design.md §6.7. Returned by every attachment list and by a
/// successful upload.
/// <para>
/// <b><c>storagePath</c> is not a property of this type, and that is the enforcement.</b> §6.7 says
/// it is never returned; the way to guarantee that is for the response shape to have nowhere to put
/// it, rather than for every projection to remember to omit it. The bytes come only from
/// <c>GET /attachments/{attachmentId}/content</c> (AP-19).
/// </para>
/// </summary>
public sealed record AttachmentMetadataDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    UserSummaryDto UploadedBy,
    DateTimeOffset UploadedAt);

/// <summary>
/// One uploaded file, as the Application layer sees it.
///
/// <para>
/// <b>Why this exists instead of <c>IFormFile</c>.</b> The story plan sketches
/// <c>UploadForCustomerAsync(customerId, IFormFile)</c>, but <c>IFormFile</c> is
/// <c>Microsoft.AspNetCore.Http</c> — an HTTP type, in the layer docs/architecture.md §2.1 keeps
/// free of HTTP concerns, and <c>SupportCrm.Application</c> carries no ASP.NET Core reference at
/// all. The plan's own <c>IAttachmentStorage</c> already speaks <see cref="Stream"/> for exactly
/// this reason, so this record follows the seam that story 04 slice 1 already built.
/// </para>
///
/// <para>
/// The controller (task 8) maps an <c>IFormFile</c> onto this in one line:
/// <c>new AttachmentUpload(file.OpenReadStream(), file.FileName, file.ContentType, file.Length)</c>.
/// </para>
/// </summary>
/// <param name="Content">The bytes. The caller owns the stream's lifetime.</param>
/// <param name="FileName">
/// The <b>original client-supplied</b> name, kept for the download's <c>Content-Disposition</c>. It
/// is never used to build a path — <c>LocalDiskAttachmentStorage</c> generates the name on disk.
/// </param>
/// <param name="ContentType">The declared MIME type. Stored, never trusted for anything.</param>
/// <param name="SizeBytes">
/// Measured by the server from the parsed multipart body — <c>IFormFile.Length</c> — not declared by
/// the client. This is the value checked against <c>Attachments:MaxSizeBytes</c> (T2-A).
/// </param>
public sealed record AttachmentUpload(
    Stream Content,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>
/// The download's payload — <c>GET /attachments/{attachmentId}/content</c> (AP-19).
/// <para>
/// <b>There is no JSON body and no <c>storagePath</c> anywhere in the response</b>
/// (docs/api-design.md §6.7): the controller streams <see cref="Content"/> with
/// <see cref="ContentType"/> and a <c>Content-Disposition</c> built from <see cref="FileName"/>.
/// </para>
/// </summary>
/// <param name="Content">An open read stream. <b>The caller disposes it.</b></param>
public sealed record AttachmentContent(
    Stream Content,
    string FileName,
    string ContentType);
