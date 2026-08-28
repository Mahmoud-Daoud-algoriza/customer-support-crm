using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Modules.Customers;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The attachment download — <c>GET /api/v1/attachments/{attachmentId}/content</c>, docs/api-design.md
/// §5.5 and <b>AP-19</b>.
///
/// <para>
/// <b><c>[Authorize]</c> with no role policy, and that is the decision, not an omission.</b> AP-19
/// makes this the single deliberate exception to AP-5's portal path split: one download endpoint
/// serves every role, because a byte stream has no DTO to vary by audience and the authorization
/// question is identical for both owners — <em>may this caller reach the owning ticket or
/// customer?</em> That question is answered by owner reachability in
/// <see cref="AttachmentService.OpenForDownloadAsync"/>, which is why there is no policy here to
/// answer it by role instead.
/// </para>
///
/// <para>
/// <b>Missing and unreachable both come back as <c>404</c>, never <c>403</c></b> (AP-4): a
/// <c>403</c> would confirm that an id exists.
/// </para>
///
/// <para>
/// <b>No JSON body, and no <c>storagePath</c> anywhere in the response</b> (docs/api-design.md §6.7).
/// The bytes are the response; the stored path never leaves the server, so a file is not reachable
/// by guessing one (docs/architecture.md §4.4).
/// </para>
/// </summary>
[Authorize]
public sealed class AttachmentsController(AttachmentService attachments) : ApiControllerBase
{
    /// <summary>
    /// Streams the file with its <c>Content-Type</c> and a <c>Content-Disposition</c> filename.
    /// <para>
    /// The name offered to the browser is the <b>original</b> one the uploader sent, not the
    /// server-generated name on disk — that is the whole reason <c>Attachment.FileName</c> and
    /// <c>Attachment.StoragePath</c> are separate columns.
    /// </para>
    /// </summary>
    [HttpGet("{attachmentId:guid}/content")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Content(Guid attachmentId, CancellationToken ct)
    {
        var file = await attachments.OpenForDownloadAsync(attachmentId, ct);

        // FileStreamResult owns the stream from here and disposes it once the response is written.
        // It is deliberately not wrapped in a using: disposing it before the framework writes the
        // body would truncate every download.
        return File(file.Content, file.ContentType, file.FileName);
    }
}
