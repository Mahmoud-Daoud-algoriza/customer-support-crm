using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The attachment size cap (T2-A), consumed by Story 04's upload endpoints.
/// <para>
/// An upload above the cap is <c>413</c> with <c>type: attachment-too-large</c>
/// (docs/api-design.md §2.2, §6.12), and the cap is <b>published to clients</b> so the UI can state
/// it before the upload rather than after (docs/ui-design.md §9).
/// </para>
/// </summary>
public sealed class AttachmentOptions
{
    public const string SectionName = "SupportCrm:Attachments";

    /// <summary>
    /// Maximum size of a single uploaded file, in bytes.
    /// <para>
    /// <b>No approved document fixes this number.</b> docs/architecture.md §6.3 does not list the
    /// cap among its eleven rows; the Story 16 plan adds it as configuration precisely so the value
    /// is visible and changeable without a code change, and so it is never a constant in an
    /// upload handler. The committed default is in <c>appsettings.json</c>.
    /// </para>
    /// </summary>
    [Range(1, long.MaxValue)] public long MaxSizeBytes { get; init; }

    /// <summary>
    /// The directory the bytes are written under — <b>local disk by design</b> (T2-A,
    /// docs/architecture.md §4.4). Consumed only by <c>LocalDiskAttachmentStorage</c>.
    /// <para>
    /// <b>Why this key exists.</b> Story 04's plan (task 2) requires the storage implementation to
    /// write "under a configured root", so a deployment can point it at a mounted volume rather
    /// than at a path compiled into a handler. It is the <b>one</b> key this story adds:
    /// docs/architecture.md §6.3's table does not list it, and Story 16 Part A closed its own key
    /// set at the attachment cap. It is deployment plumbing, not a product decision — nothing about
    /// it is an answer to an open question.
    /// </para>
    /// <para>
    /// A <b>relative</b> value is resolved against the host's content root, which is what makes the
    /// committed default work unchanged on a developer machine, in the test host and in Compose.
    /// An absolute value is used as given — Compose supplies one pointing at its volume.
    /// </para>
    /// <para>
    /// <b>It is never returned to a client.</b> Neither this key nor <c>Attachment.StoragePath</c>
    /// appears in any response (docs/api-design.md §6.7); the published cap is
    /// <see cref="MaxSizeBytes"/> alone.
    /// </para>
    /// </summary>
    [Required] public string StorageRoot { get; init; } = default!;
}
