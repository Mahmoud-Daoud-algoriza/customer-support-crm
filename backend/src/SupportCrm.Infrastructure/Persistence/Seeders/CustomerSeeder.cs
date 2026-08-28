using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Demo customers, portal logins, a note and an attachment — Story 04 task 9.
/// <c>Order = 30</c>, after <see cref="IdentitySeeder"/> at 20, because the note's author and the
/// attachment's uploader are seeded agents that must already exist.
///
/// <para>
/// <b>Four customers spread across both seeded branches, deliberately.</b> Task 9: <em>"the
/// cross-branch spread is what makes Story 05's 'branch is not a boundary' test meaningful"</em> —
/// that test needs an agent to reach an in-department ticket whose customer sits in a
/// <em>different</em> branch, which is only demonstrable if customers occupy both (A-2,
/// docs/data-model.md §5 constraint 6).
/// </para>
///
/// <para>
/// <b>Two customers have a portal login and two deliberately do not.</b> A profile with no login is
/// the ordinary case under DM-1 — an agent creates tickets on behalf of customers who may never
/// touch the portal — and having both shapes present is what makes A-19 demonstrable by hand:
/// patching the email of a linked customer changes two rows and writes an audit entry, patching an
/// unlinked one changes a single row and writes none.
/// </para>
///
/// <para>
/// <b>No credential is hardcoded.</b> The password comes from
/// <see cref="SeedOptions.DefaultPassword"/>, exactly as <see cref="IdentitySeeder"/> does.
/// </para>
/// </summary>
public sealed class CustomerSeeder(
    SupportCrmDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<SeedOptions> seedOptions,
    IAttachmentStorage storage,
    TimeProvider clock,
    ILogger<CustomerSeeder> logger) : IDataSeeder
{
    public int Order => 30;

    /// <summary>Deterministic ids, so Story 05's ticket seeder can reference a customer without a lookup.</summary>
    public static class Customers
    {
        public static readonly Guid AminaHaddad = new("33333333-3333-3333-3333-333333333301");
        public static readonly Guid BrunoOkafor = new("33333333-3333-3333-3333-333333333302");
        public static readonly Guid ChenWei = new("33333333-3333-3333-3333-333333333303");
        public static readonly Guid DianaRossi = new("33333333-3333-3333-3333-333333333304");
    }

    /// <inheritdoc cref="Customers"/>
    public static class PortalUsers
    {
        public static readonly Guid AminaHaddad = new("33333333-3333-3333-3333-333333333401");
        public static readonly Guid ChenWei = new("33333333-3333-3333-3333-333333333403");
    }

    private static readonly Guid SeededNoteId = new("33333333-3333-3333-3333-333333333501");
    private static readonly Guid SeededAttachmentId = new("33333333-3333-3333-3333-333333333601");

    public async Task SeedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Two in Head Office, two in North Branch. The spread is the point (see the class remarks).
        var toSeed = new[]
        {
            (Customers.AminaHaddad, "Amina Haddad", "amina.haddad@example.com", "+20 100 555 0101",
                OrganizationSeeder.Branches.HeadOffice),
            (Customers.BrunoOkafor, "Bruno Okafor", "bruno.okafor@example.com", "+234 802 555 0102",
                OrganizationSeeder.Branches.HeadOffice),
            (Customers.ChenWei, "Chen Wei", "chen.wei@example.com", "+86 138 5550 0103",
                OrganizationSeeder.Branches.North),
            (Customers.DianaRossi, "Diana Rossi", "diana.rossi@example.com", null,
                OrganizationSeeder.Branches.North),
        };

        var seededCustomers = 0;

        foreach (var (id, fullName, email, phone, branchId) in toSeed)
        {
            // Matched on id OR email: re-running against an existing volume must not trip the
            // unique email index (A-10), and must not create a second profile for one address.
            if (await db.Customers.AnyAsync(c => c.Id == id || c.Email == email, ct))
            {
                continue;
            }

            db.Customers.Add(Customer.Create(id, fullName, email, phone, branchId, now));
            seededCustomers++;
        }

        if (seededCustomers > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        var seededLogins = await SeedPortalLoginsAsync(now, ct);
        var seededNotes = await SeedNoteAsync(now, ct);
        var seededAttachments = await SeedAttachmentAsync(now, ct);

        logger.LogInformation(
            "CustomerSeeder: {Customers} customer(s), {Logins} portal login(s), {Notes} note(s), " +
            "{Attachments} attachment(s) added.",
            seededCustomers, seededLogins, seededNotes, seededAttachments);
    }

    /// <summary>
    /// Portal logins for two of the four profiles (DM-1).
    /// <para>
    /// <b>The login's email equals its customer's</b>, which is A-19's invariant rather than a
    /// convenience: a customer and their sign-in are one address, and seeding them apart would
    /// create demo data that the very first <c>PATCH /customers/{id}</c> would contradict.
    /// </para>
    /// </summary>
    private async Task<int> SeedPortalLoginsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var password = seedOptions.Value.DefaultPassword;

        var toSeed = new[]
        {
            (PortalUsers.AminaHaddad, Customers.AminaHaddad),
            (PortalUsers.ChenWei, Customers.ChenWei),
        };

        var seeded = 0;

        foreach (var (userId, customerId) in toSeed)
        {
            // CustomerId is checked too, not just the user id: §5 constraint 3 allows at most one
            // login per customer, and the filtered unique index enforces it.
            if (await db.Users.AnyAsync(u => u.Id == userId || u.CustomerId == customerId, ct))
            {
                continue;
            }

            var customer = await db.Customers.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == customerId, ct);

            if (customer is null)
            {
                // Only reachable if a profile was removed by hand. Skipping beats throwing: a broken
                // demo login is not worth refusing to start over.
                logger.LogWarning(
                    "CustomerSeeder: no customer {CustomerId}; portal login skipped.", customerId);
                continue;
            }

            var user = User.CreateCustomerUser(
                id: userId,
                email: customer.Email,
                passwordHash: UnhashedSentinel,
                displayName: customer.FullName,
                customerId: customer.Id,
                createdAt: now);

            user.SetPasswordHash(passwordHasher.HashPassword(user, password));

            db.Users.Add(user);
            seeded++;
        }

        if (seeded > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return seeded;
    }

    /// <summary>
    /// One note, authored by the seeded Billing agent — so the detail screen has attribution to
    /// render and the immutability rule has something to hold (docs/data-model.md §2.5).
    /// </summary>
    private async Task<int> SeedNoteAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (await db.CustomerNotes.AnyAsync(n => n.Id == SeededNoteId, ct))
        {
            return 0;
        }

        if (!await db.Users.AnyAsync(u => u.Id == IdentitySeeder.Users.BillingAgent, ct))
        {
            logger.LogWarning("CustomerSeeder: no billing agent; note skipped.");
            return 0;
        }

        db.CustomerNotes.Add(CustomerNote.Write(
            id: SeededNoteId,
            customerId: Customers.AminaHaddad,
            authorUserId: IdentitySeeder.Users.BillingAgent,
            body: "Prefers to be contacted in the morning. Billing address updated over the phone.",
            createdAt: now));

        await db.SaveChangesAsync(ct);

        return 1;
    }

    /// <summary>
    /// One small customer-owned attachment, written through <see cref="IAttachmentStorage"/> rather
    /// than straight to disk — so the seeded row's <c>StoragePath</c> is produced by the same code
    /// the upload endpoint will use, and the download therefore works against seeded data.
    /// </summary>
    private async Task<int> SeedAttachmentAsync(DateTimeOffset now, CancellationToken ct)
    {
        if (await db.Attachments.AnyAsync(a => a.Id == SeededAttachmentId, ct))
        {
            return 0;
        }

        if (!await db.Users.AnyAsync(u => u.Id == IdentitySeeder.Users.BillingAgent, ct))
        {
            logger.LogWarning("CustomerSeeder: no billing agent; attachment skipped.");
            return 0;
        }

        var bytes = Encoding.UTF8.GetBytes(
            "Demo attachment for the customer-management screens.\r\n" +
            "Seeded by CustomerSeeder (Story 04 task 9). Not a real document.\r\n");

        using var content = new MemoryStream(bytes);

        // The file lands first, then the row — the same order AttachmentService uses, and for the
        // same reason: a row pointing at a missing file breaks every later download.
        var storagePath = await storage.SaveAsync(content, "welcome-note.txt", ct);

        db.Attachments.Add(Attachment.ForCustomer(
            id: SeededAttachmentId,
            customerId: Customers.AminaHaddad,
            fileName: "welcome-note.txt",
            contentType: "text/plain",
            sizeBytes: bytes.Length,
            storagePath: storagePath,
            uploadedByUserId: IdentitySeeder.Users.BillingAgent,
            uploadedAt: now));

        await db.SaveChangesAsync(ct);

        return 1;
    }

    /// <summary>
    /// Never persisted: replaced by the real hash on the line after construction. It exists only
    /// because the factory refuses an empty hash, so no user can be saved without one.
    /// </summary>
    private const string UnhashedSentinel = "!unhashed";
}
