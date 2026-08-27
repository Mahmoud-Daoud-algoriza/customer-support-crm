using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Infrastructure.Storage;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04, <b>slice 1</b>, task 2 — <see cref="LocalDiskAttachmentStorage"/>.
/// <para>
/// The upload and download <em>endpoints</em> are tasks 6 and 8 and do not exist yet, so this suite
/// exercises the storage seam directly. That is the right level for the one property the plan calls
/// out by name: the file name on disk is <b>server-generated, never the client's</b>, so a crafted
/// <c>fileName</c> cannot escape the configured root (docs/architecture.md §4.4).
/// </para>
/// <para>
/// It writes under the system temp directory rather than the repository, and cleans up after
/// itself.
/// </para>
/// </summary>
public sealed class AttachmentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"supportcrm-attachments-{Guid.NewGuid():N}");

    /// <summary>
    /// A relative <c>StorageRoot</c> resolves against the content root, so the test supplies the
    /// temp directory as the content root and an empty-ish relative root — the same code path the
    /// committed default takes.
    /// </summary>
    private LocalDiskAttachmentStorage CreateStorage() => new(
        Options.Create(new AttachmentOptions { MaxSizeBytes = 1024, StorageRoot = "attachments" }),
        new FakeHostEnvironment(_root),
        TimeProvider.System);

    [Fact]
    public async Task A_saved_file_round_trips_byte_for_byte()
    {
        var storage = CreateStorage();
        var bytes = new byte[] { 1, 2, 3, 4, 5, 200, 255, 0, 42 };

        var storagePath = await storage.SaveAsync(
            new MemoryStream(bytes), "quarterly-report.pdf", CancellationToken.None);

        await using var opened = await storage.OpenAsync(storagePath, CancellationToken.None);
        using var buffer = new MemoryStream();
        await opened.CopyToAsync(buffer);

        Assert.Equal(bytes, buffer.ToArray());
    }

    /// <summary>
    /// <b>The name on disk is not the client's.</b> The original is preserved in
    /// <c>Attachment.FileName</c> for the download's <c>Content-Disposition</c>
    /// (docs/api-design.md §6.7); the path carries a server-generated name plus the extension.
    /// </summary>
    [Fact]
    public async Task The_stored_path_does_not_contain_the_client_supplied_name()
    {
        var storage = CreateStorage();

        var storagePath = await storage.SaveAsync(
            new MemoryStream([1, 2, 3]), "quarterly-report.pdf", CancellationToken.None);

        Assert.DoesNotContain("quarterly-report", storagePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".pdf", storagePath, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>The property this class exists to hold.</b> Every one of these names is a real attempt to
    /// write outside the storage root, and each must land inside it instead — not be sanitized into
    /// a near miss, and not throw: an upload with an odd name is a legitimate upload.
    /// <para>
    /// Asserted on the resolved absolute path, so a <c>..</c> segment that survived into the stored
    /// path would fail here even if it looked harmless as a string.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\appsettings.json")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\drivers\\etc\\hosts")]
    [InlineData("....//....//escape.txt")]
    [InlineData("no-extension")]
    [InlineData("archive.tar.gz")]
    public async Task A_crafted_file_name_cannot_escape_the_storage_root(string craftedName)
    {
        var storage = CreateStorage();

        var storagePath = await storage.SaveAsync(
            new MemoryStream([1, 2, 3]), craftedName, CancellationToken.None);

        var resolved = Path.GetFullPath(Path.Combine(storage.Root, storagePath));

        Assert.StartsWith(
            storage.Root + Path.DirectorySeparatorChar, resolved, StringComparison.Ordinal);
        Assert.True(File.Exists(resolved));
    }

    /// <summary>
    /// The same refusal on the read side. <c>StoragePath</c> comes from our own column, so this
    /// never fires in normal operation — it is defence in depth against a corrupted or hand-edited
    /// row, which is cheaper than trusting a column.
    /// </summary>
    [Fact]
    public async Task Opening_a_path_outside_the_root_is_refused()
    {
        var storage = CreateStorage();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.OpenAsync("../../../appsettings.json", CancellationToken.None));
    }

    /// <summary>
    /// Two uploads of the same client file name are two distinct files. Without this, a second
    /// upload would overwrite the first, and <c>Attachment</c> rows would share a
    /// <c>StoragePath</c>.
    /// </summary>
    [Fact]
    public async Task Two_uploads_of_the_same_name_do_not_collide()
    {
        var storage = CreateStorage();

        var first = await storage.SaveAsync(
            new MemoryStream([1]), "photo.png", CancellationToken.None);
        var second = await storage.SaveAsync(
            new MemoryStream([2]), "photo.png", CancellationToken.None);

        Assert.NotEqual(first, second);

        await using var firstStream = await storage.OpenAsync(first, CancellationToken.None);
        Assert.Equal(1, firstStream.ReadByte());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    /// The storage resolves a relative root against <see cref="IHostEnvironment.ContentRootPath"/>,
    /// so the test supplies one pointing at a temp directory. Only that member is used.
    /// </summary>
    private sealed class FakeHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = nameof(AttachmentStorageTests);

        public string ContentRootPath { get; set; } = contentRootPath;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
