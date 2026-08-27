using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;

namespace SupportCrm.Infrastructure.Storage;

/// <summary>
/// <see cref="IAttachmentStorage"/> on local disk — the whole of T2-A
/// (docs/architecture.md §4.4, docs/data-model.md §2.11).
/// <para>
/// <b>No cloud object storage, no virus scanning, no previews, no versioning</b> — every one is on
/// the <c>customer-records</c> intake's Out of scope list, and this class is the place each would
/// otherwise creep in.
/// </para>
/// <para>
/// <b>The name on disk is server-generated, never the client's.</b> That is the security property
/// this type exists to hold: a crafted <c>fileName</c> — <c>../../appsettings.json</c>, an absolute
/// path, an NTFS alternate data stream — cannot escape the storage root, because the client's name
/// is never used to build a path. Only the extension is carried over, and only after it is
/// validated. The original name is preserved in <c>Attachment.FileName</c> for the download's
/// <c>Content-Disposition</c> (docs/api-design.md §6.7).
/// </para>
/// </summary>
public sealed class LocalDiskAttachmentStorage : IAttachmentStorage
{
    /// <summary>
    /// Files are grouped by upload date, so one directory does not accumulate every file ever
    /// uploaded. It is a filesystem nicety and nothing reads meaning from it: the stored path is
    /// opaque to every layer above.
    /// </summary>
    private const string DateFolderFormat = "yyyy/MM";

    /// <summary>Bounds the carried-over extension, so a pathological one cannot bloat the path.</summary>
    private const int MaxExtensionLength = 16;

    private readonly string _root;
    private readonly TimeProvider _clock;

    /// <summary>
    /// A <b>relative</b> <c>StorageRoot</c> is resolved against the host's content root, which is
    /// what lets the committed default work unchanged on a developer machine, in the test host and
    /// in Compose. An absolute one is used as given.
    /// </summary>
    public LocalDiskAttachmentStorage(
        IOptions<AttachmentOptions> options, IHostEnvironment environment, TimeProvider clock)
    {
        _clock = clock;
        _root = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, options.Value.StorageRoot));
    }

    /// <summary>The resolved absolute root. Exposed for diagnostics and tests, never to a client.</summary>
    public string Root => _root;

    /// <inheritdoc />
    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        // The relative path is what gets stored, so the root can move between deployments without
        // rewriting rows — and so no absolute server path is ever persisted.
        var relativePath = Path.Combine(
            _clock.GetUtcNow().ToString(DateFolderFormat, System.Globalization.CultureInfo.InvariantCulture),
            $"{Guid.NewGuid():N}{SafeExtension(fileName)}");

        var absolutePath = ResolveWithinRoot(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        // FileMode.CreateNew, not Create: a collision on a fresh GUID would mean something is very
        // wrong, and silently overwriting an existing attachment is the worst available outcome.
        await using var target = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        await content.CopyToAsync(target, ct);

        // Stored with forward slashes, so a path written on one platform reads on the other.
        return relativePath.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <inheritdoc />
    public Task<Stream> OpenAsync(string storagePath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

        // Re-checked even though this value came from our own column: if a row is ever corrupted or
        // hand-edited, the failure should be a refusal rather than an arbitrary file read.
        var absolutePath = ResolveWithinRoot(storagePath);

        Stream stream = new FileStream(
            absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096,
            useAsync: true);

        return Task.FromResult(stream);
    }

    /// <summary>
    /// Resolves a relative path against the root and <b>refuses anything that lands outside it</b>.
    /// <para>
    /// The comparison is on the fully resolved path, so <c>..</c> segments, mixed separators and a
    /// rooted path are all caught by the same check rather than by a blocklist of shapes.
    /// </para>
    /// </summary>
    private string ResolveWithinRoot(string relativePath)
    {
        var resolved = Path.GetFullPath(Path.Combine(_root, relativePath));

        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!resolved.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "An attachment path resolved outside the configured storage root and was refused.");
        }

        return resolved;
    }

    /// <summary>
    /// The client's extension, or none.
    /// <para>
    /// <see cref="Path.GetExtension(string)"/> is given the file name only after its directory
    /// separators are stripped, so <c>../../x.json</c> cannot contribute a directory. Anything that
    /// is not a plain <c>.</c> plus letters and digits is dropped rather than sanitized — the
    /// extension is a convenience for the operator browsing the volume, and the download's file
    /// name comes from <c>Attachment.FileName</c>, so dropping it costs nothing.
    /// </para>
    /// </summary>
    private static string SafeExtension(string fileName)
    {
        var leaf = fileName.Replace('\\', '/').Split('/')[^1];
        var extension = Path.GetExtension(leaf);

        if (extension.Length < 2 || extension.Length > MaxExtensionLength)
        {
            return string.Empty;
        }

        return extension.Skip(1).All(char.IsLetterOrDigit)
            ? extension.ToLowerInvariant()
            : string.Empty;
    }
}
