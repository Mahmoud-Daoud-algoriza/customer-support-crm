using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Customers;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04 <b>slice 3</b> — plan task 6, <see cref="AttachmentService"/>.
/// <para>
/// The upload and download endpoints are task 8, so the service is exercised directly against the
/// real <see cref="SupportCrm.Infrastructure.Storage.LocalDiskAttachmentStorage"/> rooted in a temp
/// directory — the round-trip assertions are about bytes genuinely reaching a disk and coming back.
/// </para>
/// </summary>
public sealed class AttachmentServiceTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    private const long Cap = 512;

    [Fact]
    public async Task An_uploaded_file_round_trips_and_keeps_its_original_name()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.roundtrip");

        var bytes = Encoding.UTF8.GetBytes("the quick brown fox");

        var (metadata, downloaded, fileName, contentType) = await CustomerModuleHarness.InScopeAsync(
            factory, async (sp, db) =>
            {
                var service = Build(harness, db, agentId, UserRole.Agent);

                var meta = await service.UploadForCustomerAsync(
                    customerId,
                    new AttachmentUpload(new MemoryStream(bytes), "quarterly report.pdf", "application/pdf", bytes.Length),
                    default);

                var content = await service.OpenForDownloadAsync(meta.Id, default);

                using var buffer = new MemoryStream();
                await content.Content.CopyToAsync(buffer);
                await content.Content.DisposeAsync();

                return (meta, buffer.ToArray(), content.FileName, content.ContentType);
            });

        Assert.Equal(bytes, downloaded);

        // The original name survives for Content-Disposition, even though the name on disk is
        // server-generated (docs/api-design.md §6.7).
        Assert.Equal("quarterly report.pdf", fileName);
        Assert.Equal("quarterly report.pdf", metadata.FileName);
        Assert.Equal("application/pdf", contentType);
        Assert.Equal(bytes.Length, metadata.SizeBytes);

        // uploadedBy is a UserSummary with a real display name, not a bare id (§6.7).
        Assert.Equal(agentId, metadata.UploadedBy.Id);
        Assert.NotEmpty(metadata.UploadedBy.DisplayName);
    }

    /// <summary>
    /// <b>T2-A — the cap is enforced</b>, and the plan fixes the outcome:
    /// <c>PayloadTooLargeException("attachment-too-large")</c>, which the Problem Details handler
    /// turns into a <c>413</c>.
    /// </summary>
    [Fact]
    public async Task An_oversized_upload_is_rejected_with_the_attachment_too_large_slug()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.toobig");

        var bytes = new byte[Cap + 1];

        var error = await Assert.ThrowsAsync<PayloadTooLargeException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
                await Build(harness, db, agentId, UserRole.Agent).UploadForCustomerAsync(
                    customerId,
                    new AttachmentUpload(new MemoryStream(bytes), "big.bin", "application/octet-stream", bytes.Length),
                    default)));

        Assert.Equal("attachment-too-large", error.ProblemType);

        // Rejected before anything was written: no row, and no orphaned file.
        Assert.Equal(0, await factory.WithDbAsync(db =>
            db.Attachments.CountAsync(a => a.CustomerId == customerId)));
    }

    /// <summary>
    /// A file exactly at the cap is <b>accepted</b>. The boundary is asserted because "greater than"
    /// and "greater than or equal" are a one-character difference that no other test would catch.
    /// </summary>
    [Fact]
    public async Task An_upload_exactly_at_the_cap_is_accepted()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.atcap");

        var metadata = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
            await Build(harness, db, agentId, UserRole.Agent).UploadForCustomerAsync(
                customerId,
                new AttachmentUpload(new MemoryStream(new byte[Cap]), "exact.bin", "application/octet-stream", Cap),
                default));

        Assert.Equal(Cap, metadata.SizeBytes);
    }

    /// <summary>
    /// A zero-byte upload is a <c>400</c>, caught before the domain factory — which refuses it with
    /// an <see cref="ArgumentOutOfRangeException"/> that would surface as a <c>500</c>.
    /// </summary>
    [Fact]
    public async Task An_empty_upload_is_a_validation_error_not_a_server_error()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.empty");

        await Assert.ThrowsAsync<ValidationException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
                await Build(harness, db, agentId, UserRole.Agent).UploadForCustomerAsync(
                    customerId,
                    new AttachmentUpload(new MemoryStream([]), "empty.txt", "text/plain", 0),
                    default)));
    }

    /// <summary>
    /// <b>AP-4 — out of scope is <c>404</c>, never <c>403</c>.</b> A customer-owned file requires an
    /// Agent; a <c>Customer</c>-role caller must not be able to tell "this file is not yours" from
    /// "there is no such file", because the first confirms an id exists.
    /// </summary>
    [Fact]
    public async Task A_customer_role_caller_gets_not_found_for_a_customer_owned_file()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.ap4");

        var attachmentId = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
            (await Build(harness, db, agentId, UserRole.Agent).UploadForCustomerAsync(
                customerId,
                new AttachmentUpload(new MemoryStream([1, 2, 3]), "private.txt", "text/plain", 3),
                default)).Id);

        // Same call, same id, as a Customer.
        var asCustomer = await Assert.ThrowsAsync<NotFoundException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
                await Build(harness, db, Guid.NewGuid(), UserRole.Customer)
                    .OpenForDownloadAsync(attachmentId, default)));

        // ...and an id that genuinely does not exist.
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
                await Build(harness, db, Guid.NewGuid(), UserRole.Customer)
                    .OpenForDownloadAsync(Guid.NewGuid(), default)));

        // Indistinguishable, which is the whole point of AP-4.
        Assert.Equal(missing.Message, asCustomer.Message);
        Assert.Equal(missing.ProblemType, asCustomer.ProblemType);
    }

    /// <summary>
    /// A Manager and an Administrator reach a customer-owned file too — <c>RequireAgent</c> is the
    /// A-4 hierarchy, not an exact-role match (docs/api-design.md §4.2).
    /// </summary>
    [Theory]
    [InlineData(UserRole.Agent)]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.Administrator)]
    public async Task Every_staff_role_reaches_a_customer_owned_file(UserRole role)
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync($"att.role.{role}");

        var attachmentId = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
            (await Build(harness, db, agentId, UserRole.Agent).UploadForCustomerAsync(
                customerId,
                new AttachmentUpload(new MemoryStream([9]), "shared.txt", "text/plain", 1),
                default)).Id);

        await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
        {
            var content = await Build(harness, db, Guid.NewGuid(), role)
                .OpenForDownloadAsync(attachmentId, default);

            await content.Content.DisposeAsync();

            return true;
        });
    }

    /// <summary>
    /// <b>The ticket half fails closed until Story 05.</b> The method bodies exist here (S9-2), but
    /// the scoping helper they depend on does not, so every ticket-scoped call is refused with the
    /// same <c>404</c> AP-4 requires. That is correct rather than a stub: no <c>Ticket</c> row can
    /// exist, so the honest answer to "may I reach ticket X" is that there is no ticket X.
    /// </summary>
    [Fact]
    public async Task Ticket_scoped_operations_are_refused_until_story_05_supplies_the_scoping_helper()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (_, agentId) = await SeedAsync("att.ticket");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
                await Build(harness, db, agentId, UserRole.Agent).UploadForTicketAsync(
                    Guid.NewGuid(),
                    new AttachmentUpload(new MemoryStream([1]), "t.txt", "text/plain", 1),
                    default)));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
                await Build(harness, db, agentId, UserRole.Agent)
                    .ListForTicketAsync(Guid.NewGuid(), null, default)));
    }

    /// <summary>Newest first, paged, and scoped to the customer asked for.</summary>
    [Fact]
    public async Task Attachments_are_listed_newest_first_for_their_own_customer()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.list");
        var (otherCustomerId, _) = await SeedAsync("att.list.other");

        var page = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
        {
            var service = Build(harness, db, agentId, UserRole.Agent,
                new CustomerModuleHarness.StepClock(new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero)));

            foreach (var name in new[] { "one.txt", "two.txt", "three.txt" })
            {
                await service.UploadForCustomerAsync(
                    customerId,
                    new AttachmentUpload(new MemoryStream([7]), name, "text/plain", 1),
                    default);
            }

            await service.UploadForCustomerAsync(
                otherCustomerId,
                new AttachmentUpload(new MemoryStream([7]), "elsewhere.txt", "text/plain", 1),
                default);

            return await service.ListForCustomerAsync(customerId, null, default);
        });

        Assert.Equal(3, page.TotalItems);
        Assert.Equal(["three.txt", "two.txt", "one.txt"], page.Items.Select(a => a.FileName));
    }

    /// <summary>
    /// <b><c>storagePath</c> is returned by nothing, ever</b> (docs/api-design.md §6.7). Asserted
    /// twice: the DTO has no property for it, and a serialized payload contains neither the key nor
    /// the stored value.
    /// </summary>
    [Fact]
    public async Task No_response_shape_carries_the_storage_path()
    {
        using var harness = new CustomerModuleHarness(Cap);
        var (customerId, agentId) = await SeedAsync("att.nopath");

        Assert.DoesNotContain(
            "StoragePath", typeof(AttachmentMetadataDto).GetProperties().Select(p => p.Name));

        var metadata = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
            await Build(harness, db, agentId, UserRole.Agent).UploadForCustomerAsync(
                customerId,
                new AttachmentUpload(new MemoryStream([4, 2]), "leaky.txt", "text/plain", 2),
                default));

        var storedPath = await factory.WithDbAsync(db => db.Attachments.AsNoTracking()
            .Where(a => a.Id == metadata.Id).Select(a => a.StoragePath).SingleAsync());

        var json = JsonSerializer.Serialize(metadata);

        Assert.DoesNotContain("storagePath", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(storedPath, json, StringComparison.Ordinal);
    }

    private static AttachmentService Build(
        CustomerModuleHarness harness,
        SupportCrm.Infrastructure.Persistence.SupportCrmDbContext db,
        Guid callerId,
        UserRole role,
        TimeProvider? clock = null) =>
        new(db,
            harness.Storage,
            Options.Create(new AttachmentOptions { MaxSizeBytes = Cap, StorageRoot = "files" }),
            new CustomerModuleHarness.Caller(callerId, role, $"{role} Caller"),
            clock ?? TimeProvider.System);

    private async Task<(Guid CustomerId, Guid AgentId)> SeedAsync(string slug)
    {
        var branchId = await factory.EnsureBranchAsync($"{slug} branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, $"{slug}.agent@test.local");

        var customerId = await factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), slug, $"{slug}@test.local", null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        return (customerId, agentId);
    }
}
