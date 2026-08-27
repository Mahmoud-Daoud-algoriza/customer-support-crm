using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Persistence.Configurations;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04, <b>slice 1</b> — tasks 1 and 2 only: the <c>Customers</c> domain module and its data
/// layer. Nothing here touches an endpoint, because no customer endpoint exists yet: the services
/// and controllers are tasks 3–8, and <c>CustomerAccessTests</c> (plan task 10) arrives with them.
/// <para>
/// What this file proves is what tasks 1 and 2 actually claim: the three entities have the shape
/// docs/data-model.md §2.4, §2.5 and §2.11 give them, the invariants those sections call
/// structural <em>are</em> structural, and the mapped schema carries the indexes, the foreign keys
/// and the XOR check constraint that §5 and §6 require.
/// </para>
/// </summary>
public sealed class CustomerDataLayerTests
{
    // ---------------------------------------------------------------- Domain shape (task 1)

    /// <summary>
    /// docs/data-model.md §2.4 lists <c>externalReference</c>, and DM-6 plus
    /// docs/api-design.md §8.3 make it <b>read-only and settable through no endpoint</b>. The
    /// guarantee is the absence of a mutator, so absence is what is asserted — a public setter or a
    /// <c>SetExternalReference</c> method would put an ERP-seam field within reach of a request
    /// model.
    /// </summary>
    [Fact]
    public void Customer_externalReference_has_no_public_mutator()
    {
        var property = typeof(Customer).GetProperty(nameof(Customer.ExternalReference))!;

        Assert.False(property.SetMethod?.IsPublic == true);

        // IsSpecialName excludes the property's own getter, which is a declared method too.
        var mutators = typeof(Customer)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => !m.IsSpecialName
                        && m.Name.Contains("ExternalReference", StringComparison.Ordinal))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(mutators);
    }

    /// <summary>
    /// A-2 makes <c>Customer.branchId</c> required, so the factory refuses an empty one rather than
    /// deferring to a foreign-key violation at commit time.
    /// </summary>
    [Fact]
    public void Customer_cannot_be_created_without_a_branch()
    {
        var error = Assert.Throws<ArgumentException>(() => Customer.Create(
            Guid.NewGuid(), "Ada Lovelace", "ada@example.com", null, Guid.Empty,
            DateTimeOffset.UtcNow));

        Assert.Equal("branchId", error.ParamName);
    }

    /// <summary>
    /// <b>docs/data-model.md §5 constraint 16 — a note is immutable once written</b>, and §2.5 makes
    /// that structural rather than merely unexposed. So <see cref="CustomerNote"/> must carry no
    /// public setter and no instance method at all: "no update path, not merely no endpoint".
    /// <para>
    /// This is the test that keeps the intake's "cannot be silently altered by another user" true as
    /// later stories touch this module. A story that adds an editor breaks it, which is the intended
    /// alarm.
    /// </para>
    /// </summary>
    [Fact]
    public void CustomerNote_exposes_no_mutator_at_all()
    {
        var publicSetters = typeof(CustomerNote)
            .GetProperties()
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicSetters);

        // Property getters are declared methods too, so IsSpecialName filters them out. An "Edit"
        // or "Delete" method would not be special-name and would fail this.
        var instanceMethods = typeof(CustomerNote)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(instanceMethods);
    }

    /// <summary>
    /// The same assertion for <see cref="Attachment"/>: metadata is immutable and no delete path is
    /// in scope (docs/data-model.md §2.11).
    /// </summary>
    [Fact]
    public void Attachment_exposes_no_mutator_at_all()
    {
        var publicSetters = typeof(Attachment)
            .GetProperties()
            .Where(p => p.SetMethod?.IsPublic == true)
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(publicSetters);

        var instanceMethods = typeof(Attachment)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(instanceMethods);
    }

    /// <summary>
    /// <b>docs/data-model.md §5 constraint 20 — exactly one owner, a ticket XOR a customer.</b>
    /// The rule's home is the two factories: each sets one owner and there is no third way in, so a
    /// both-owners or no-owner attachment is unconstructible. That is asserted positively here, and
    /// the negative — that no other public creation path exists — is asserted alongside it.
    /// </summary>
    [Fact]
    public void Attachment_factories_each_set_exactly_one_owner()
    {
        var customerOwned = Attachment.ForCustomer(
            Guid.NewGuid(), Guid.NewGuid(), "invoice.pdf", "application/pdf", 1024,
            "2026/08/abc.pdf", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.NotNull(customerOwned.CustomerId);
        Assert.Null(customerOwned.TicketId);

        var ticketOwned = Attachment.ForTicket(
            Guid.NewGuid(), Guid.NewGuid(), "screenshot.png", "image/png", 2048,
            "2026/08/def.png", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.NotNull(ticketOwned.TicketId);
        Assert.Null(ticketOwned.CustomerId);

        // No public constructor and no third factory: those two are the whole creation surface.
        Assert.Empty(typeof(Attachment).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        var factories = typeof(Attachment)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal([nameof(Attachment.ForCustomer), nameof(Attachment.ForTicket)], factories);
    }

    // ---------------------------------------------------------------- Mapped schema (task 2)

    /// <summary>
    /// The three sets exist on both the context and the Application abstraction — the latter is what
    /// tasks 3–6 will write LINQ against, and a set missing there would surface as a compile error
    /// in the next slice rather than as a failure here.
    /// </summary>
    [Fact]
    public async Task The_three_customer_sets_are_mapped_on_the_context_and_the_abstraction()
    {
        await using var factory = new SupportCrmApiFactory();

        await factory.WithDbAsync(db =>
        {
            Assert.NotNull(db.Customers);
            Assert.NotNull(db.CustomerNotes);
            Assert.NotNull(db.Attachments);

            var abstraction = typeof(IApplicationDbContext);
            Assert.NotNull(abstraction.GetProperty(nameof(IApplicationDbContext.Customers)));
            Assert.NotNull(abstraction.GetProperty(nameof(IApplicationDbContext.CustomerNotes)));
            Assert.NotNull(abstraction.GetProperty(nameof(IApplicationDbContext.Attachments)));

            return Task.FromResult(true);
        });
    }

    /// <summary>
    /// docs/data-model.md §6: <c>Customer(email)</c> <b>unique</b> — A-10 identification and
    /// duplicate rejection — and <c>Customer(branchId)</c> for branch filtering (T2-K, T2-G).
    /// <para>
    /// Asserted against the <b>model</b> rather than the migration, for the reason
    /// <c>NoConfigurationEntityTests</c> already gives: the model is the thing that would change,
    /// and a migration can be regenerated.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Customer_carries_the_two_indexes_the_model_requires()
    {
        await using var factory = new SupportCrmApiFactory();

        await factory.WithDbAsync(db =>
        {
            var entity = db.Model.FindEntityType(typeof(Customer))!;

            var emailIndex = entity.GetIndexes().Single(i =>
                i.Properties.Count == 1 && i.Properties[0].Name == nameof(Customer.Email));
            Assert.True(emailIndex.IsUnique);

            Assert.Contains(entity.GetIndexes(), i =>
                i.Properties.Count == 1 && i.Properties[0].Name == nameof(Customer.BranchId));

            // A-2: required, not merely present.
            Assert.False(entity.FindProperty(nameof(Customer.BranchId))!.IsNullable);

            return Task.FromResult(true);
        });
    }

    /// <summary>
    /// The string tiers of docs/data-model.md §6.1 — <b>pick a tier, never a number</b>. Asserted
    /// because §6.1 exists precisely so that <c>User.email</c> and <c>Customer.email</c>, the same
    /// address compared across two tables (A-19), cannot end up different widths.
    /// </summary>
    [Fact]
    public async Task Customer_and_user_email_share_the_email_tier_width()
    {
        await using var factory = new SupportCrmApiFactory();

        await factory.WithDbAsync(db =>
        {
            var customerEmail = db.Model.FindEntityType(typeof(Customer))!
                .FindProperty(nameof(Customer.Email))!.GetMaxLength();
            var userEmail = db.Model.FindEntityType(typeof(User))!
                .FindProperty(nameof(User.Email))!.GetMaxLength();

            Assert.Equal(256, customerEmail);
            Assert.Equal(userEmail, customerEmail);

            // Text tier: multi-line authored content, no length, never an index key (§6.1).
            Assert.Null(db.Model.FindEntityType(typeof(CustomerNote))!
                .FindProperty(nameof(CustomerNote.Body))!.GetMaxLength());

            // Name tier, not Code — §6.1 spells out why: a 73-character .docx MIME type.
            Assert.Equal(200, db.Model.FindEntityType(typeof(Attachment))!
                .FindProperty(nameof(Attachment.ContentType))!.GetMaxLength());

            return Task.FromResult(true);
        });
    }

    /// <summary>
    /// <b>docs/data-model.md §5 constraint 20 as a database constraint.</b> §5 names this one as a
    /// constraint the database asserts, unlike constraints 2, 3, 10, 11, 17, 18, 19 and 21, which it
    /// explicitly leaves to the Application or Domain layer.
    /// <para>
    /// Asserted <b>behaviourally</b> — the created schema refuses the row — rather than by reading
    /// the constraint out of the model, which is the stronger claim: a constraint present in the
    /// model but absent from the DDL would pass a metadata check and fail here. A row with
    /// <b>both</b> owners and a row with <b>neither</b> are both refused, which is the half the
    /// Domain factories cannot prove, because they make such a row unconstructible in C#.
    /// </para>
    /// <para>
    /// Written beneath the Domain on purpose: raw SQL is the only way to attempt the shape the
    /// factories forbid.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("both")]
    [InlineData("neither")]
    public async Task The_database_refuses_an_attachment_owned_by_other_than_exactly_one_thing(
        string shape)
    {
        await using var factory = new SupportCrmApiFactory();

        var branchId = await factory.EnsureBranchAsync("XOR Branch");
        var uploaderId = await factory.AddStaffUserAsync(UserRole.Agent, $"xor-{shape}@example.com");

        var customerId = await factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), "XOR Customer", $"xor-{shape}-customer@example.com", null, branchId,
                DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        // The "both" case supplies a real customer id, so the insert cannot be refused by the
        // customer foreign key instead of by the check constraint under test.
        Guid? customerColumn = shape == "both" ? customerId : null;
        Guid? ticketColumn = shape == "both" ? Guid.NewGuid() : null;

        // A provider exception, not a DbUpdateException: this is raw SQL, so nothing goes through
        // the change tracker. The assertion is on the constraint's name, which is what proves the
        // refusal came from the XOR rule rather than from some other column.
        var error = await Assert.ThrowsAnyAsync<DbException>(() => factory.WithDbAsync(async db =>
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO Attachments (Id, CustomerId, TicketId, FileName, ContentType, SizeBytes, StoragePath, UploadedByUserId, UploadedAt) VALUES ({Guid.NewGuid()}, {customerColumn}, {ticketColumn}, {"f.pdf"}, {"application/pdf"}, {1L}, {"2026/08/f.pdf"}, {uploaderId}, {DateTimeOffset.UtcNow})")));

        Assert.Contains(
            AttachmentConfiguration.OwnerXorConstraintName, error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Task 2's last item — the DM-1 link Story 02 left open.</b> <c>User.CustomerId</c> now
    /// carries a foreign key to <c>Customers</c>, and the unique filtered index that Story 02
    /// created keeps §5 constraint 3 true: at most one <c>User</c> per <c>Customer</c>.
    /// </summary>
    [Fact]
    public async Task User_customerId_now_has_its_foreign_key_and_still_allows_at_most_one_login()
    {
        await using var factory = new SupportCrmApiFactory();

        await factory.WithDbAsync(db =>
        {
            var user = db.Model.FindEntityType(typeof(User))!;

            var foreignKey = Assert.Single(
                user.GetForeignKeys(),
                fk => fk.Properties.Count == 1
                      && fk.Properties[0].Name == nameof(User.CustomerId));

            Assert.Equal(typeof(Customer), foreignKey.PrincipalEntityType.ClrType);

            var index = user.GetIndexes().Single(i =>
                i.Properties.Count == 1 && i.Properties[0].Name == nameof(User.CustomerId));
            Assert.True(index.IsUnique);

            return Task.FromResult(true);
        });
    }
}
