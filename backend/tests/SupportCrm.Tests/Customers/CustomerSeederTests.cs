using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Persistence.Seeders;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04 <b>slice 3</b> — plan task 9, <see cref="CustomerSeeder"/>.
///
/// <para>
/// <see cref="SupportCrmApiFactory"/> removes every <see cref="IDataSeeder"/> so that no test
/// depends on demo data it did not create. This class therefore runs the three seeders
/// <b>explicitly</b>, in their real <c>Order</c>, which is the only way to assert what task 9
/// actually promises — and it uses <see cref="ActivatorUtilities"/> so each seeder gets its real
/// dependencies from the container, overriding only the attachment storage so a test run does not
/// write into the build output.
/// </para>
/// </summary>
public sealed class CustomerSeederTests(SupportCrmApiFactory factory, CustomerSeederStorage storage)
    : IClassFixture<SupportCrmApiFactory>, IClassFixture<CustomerSeederStorage>
{
    private CustomerModuleHarness Harness => storage.Harness;

    /// <summary>
    /// Task 9's four requirements, asserted one by one: <em>"at least four customers spread across
    /// both seeded branches, at least two with a linked portal <c>User</c>, at least one with a
    /// note, and one with a small attachment."</em>
    /// </summary>
    [Fact]
    public async Task The_seeder_produces_what_task_9_promises()
    {
        await RunSeedersAsync(Harness);

        var customers = await factory.WithDbAsync(db => db.Customers.AsNoTracking().ToListAsync());

        Assert.True(customers.Count >= 4, $"expected at least 4 customers, found {customers.Count}");

        // The cross-branch spread is the point: it is what makes Story 05's "branch is not a
        // boundary" test meaningful (A-2, docs/data-model.md §5 constraint 6). Both SEEDED branches
        // must be occupied, not merely two distinct branches.
        Assert.Contains(customers, c => c.BranchId == OrganizationSeeder.Branches.HeadOffice);
        Assert.Contains(customers, c => c.BranchId == OrganizationSeeder.Branches.North);

        var portalLogins = await factory.WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Customer).ToListAsync());

        Assert.True(portalLogins.Count >= 2, $"expected at least 2 portal logins, found {portalLogins.Count}");

        // DM-1's Customer shape: a link to a profile, and no department.
        Assert.All(portalLogins, u =>
        {
            Assert.NotNull(u.CustomerId);
            Assert.Null(u.DepartmentId);
            Assert.True(u.IsActive);
        });

        Assert.True(await factory.WithDbAsync(db => db.CustomerNotes.AnyAsync()));
        Assert.True(await factory.WithDbAsync(db => db.Attachments.AnyAsync()));
    }

    /// <summary>
    /// <b>Both DM-1 shapes are present</b>, which is what makes A-19 demonstrable by hand: a
    /// customer with a login, whose email change propagates and audits, and a profile-only customer,
    /// whose change does neither. A seeder that gave every customer a login would hide half the
    /// rule.
    /// </summary>
    [Fact]
    public async Task Some_seeded_customers_have_a_login_and_some_deliberately_do_not()
    {
        await RunSeedersAsync(Harness);

        var linkedCustomerIds = await factory.WithDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.CustomerId != null).Select(u => u.CustomerId!.Value).ToListAsync());

        var allCustomerIds = await factory.WithDbAsync(db =>
            db.Customers.AsNoTracking().Select(c => c.Id).ToListAsync());

        Assert.NotEmpty(linkedCustomerIds);
        Assert.NotEmpty(allCustomerIds.Except(linkedCustomerIds));
    }

    /// <summary>
    /// <b>A-19's invariant holds in the seeded data itself.</b> A customer and their portal login
    /// carry the same address, so the very first <c>PATCH /customers/{id}</c> against demo data does
    /// not contradict the rule the demo is meant to show.
    /// </summary>
    [Fact]
    public async Task A_seeded_login_carries_the_same_address_as_its_customer()
    {
        await RunSeedersAsync(Harness);

        var pairs = await factory.WithDbAsync(db =>
            (from u in db.Users.AsNoTracking().Where(u => u.CustomerId != null)
             join c in db.Customers.AsNoTracking() on u.CustomerId!.Value equals c.Id
             select new { UserEmail = u.Email, CustomerEmail = c.Email }).ToListAsync());

        Assert.NotEmpty(pairs);
        Assert.All(pairs, p =>
            Assert.Equal(p.CustomerEmail, p.UserEmail, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <b>Idempotent</b>, like every other seeder (AD-8 applies them at every startup). Running
    /// twice against an existing volume must not duplicate a row or trip the unique email index.
    /// </summary>
    [Fact]
    public async Task Running_the_seeder_twice_changes_nothing()
    {
        await RunSeedersAsync(Harness);

        var before = await CountsAsync();

        await RunSeedersAsync(Harness);

        Assert.Equal(before, await CountsAsync());
    }

    /// <summary>
    /// The seeded attachment's bytes are reachable through the same storage the upload endpoint
    /// uses — so a demo download works, rather than the row pointing at a file that was never
    /// written.
    /// </summary>
    [Fact]
    public async Task The_seeded_attachment_can_actually_be_read_back()
    {
        await RunSeedersAsync(Harness);

        var attachment = await factory.WithDbAsync(db => db.Attachments.AsNoTracking().FirstAsync());

        await using var stream = await Harness.Storage.OpenAsync(attachment.StoragePath, default);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var text = await reader.ReadToEndAsync();

        Assert.NotEmpty(text);
        Assert.Equal(attachment.SizeBytes, Encoding.UTF8.GetByteCount(text));
    }

    /// <summary>
    /// <c>Order = 30</c> — after <see cref="IdentitySeeder"/> at 20, because the note's author and
    /// the attachment's uploader are seeded agents that must already exist.
    /// </summary>
    [Fact]
    public void The_seeder_runs_after_organization_and_identity()
    {
        var order = CustomerSeederOrder(Harness);

        Assert.Equal(30, order);
        Assert.True(order > 20, "CustomerSeeder must run after IdentitySeeder");
    }

    private int CustomerSeederOrder(CustomerModuleHarness harness)
    {
        using var scope = factory.Services.CreateScope();

        return ActivatorUtilities
            .CreateInstance<CustomerSeeder>(scope.ServiceProvider, harness.Storage)
            .Order;
    }

    private Task<(int Customers, int Logins, int Notes, int Attachments)> CountsAsync() =>
        factory.WithDbAsync(async db => (
            await db.Customers.CountAsync(),
            await db.Users.CountAsync(u => u.Role == UserRole.Customer),
            await db.CustomerNotes.CountAsync(),
            await db.Attachments.CountAsync()));

    /// <summary>
    /// The three seeders in their real <c>Order</c>: organization (10), identity (20), customers
    /// (30). Running them out of order is exactly what the ordering exists to prevent, so the test
    /// respects it rather than short-cutting to the one under test.
    /// </summary>
    private async Task RunSeedersAsync(CustomerModuleHarness harness)
    {
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;

        await ActivatorUtilities.CreateInstance<OrganizationSeeder>(sp).SeedAsync(default);
        await ActivatorUtilities.CreateInstance<IdentitySeeder>(sp).SeedAsync(default);

        // Only the storage is overridden, so the seeded file lands in the harness's temp root
        // instead of the build output. Everything else is the real registration.
        await ActivatorUtilities.CreateInstance<CustomerSeeder>(sp, harness.Storage).SeedAsync(default);
    }
}

/// <summary>
/// One attachment store for the whole class.
///
/// <para>
/// <b>It has to be class-scoped, because the database is.</b> <see cref="SupportCrmApiFactory"/> is
/// an <c>IClassFixture</c>, so every test here shares one database — and
/// <see cref="CustomerSeeder"/> is idempotent, so only the first test to run actually writes the
/// seeded file. A per-test storage root would leave every later test pointing at a row whose file
/// was written into a temp directory that has already been deleted.
/// </para>
/// </summary>
public sealed class CustomerSeederStorage : IDisposable
{
    public CustomerModuleHarness Harness { get; } = new();

    public void Dispose() => Harness.Dispose();
}
