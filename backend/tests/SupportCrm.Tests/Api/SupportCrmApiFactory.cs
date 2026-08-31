using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Infrastructure.Persistence;

namespace SupportCrm.Tests.Api;

/// <summary>
/// Hosts the real composition root and swaps only the database, so the tests exercise the actual
/// controllers, middleware, authorization policies, options validation, JSON contract and exception
/// handling.
/// <para>
/// SQLite in a shared in-memory database is used rather than SQL Server so the suite is hermetic —
/// no container and no connection string are needed to run <c>dotnet test</c>. The provider is
/// relational, so the per-request identity resolution and <c>Database.CanConnectAsync()</c> both
/// open real connections rather than being stubbed out.
/// </para>
/// <para>
/// <b>What SQLite cannot verify:</b> the case-insensitive email collation, which is SQL Server
/// specific and applied under a provider guard in <c>SupportCrmDbContext.OnModelCreating</c>. That
/// rule is verified against real SQL Server in the story's verification steps, not here.
/// </para>
/// <para>
/// <see cref="DatabaseInitializer"/> and the demo seeders are removed. Each test builds exactly the
/// users it needs, so no test depends on demo data it did not create — and the seeders are exercised
/// against real SQL Server by the Compose verification instead.
/// </para>
/// </summary>
public sealed class SupportCrmApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// A distinct in-memory database per factory instance, so test classes running in parallel
    /// cannot see each other's users. Deactivating a user in one class must not fail another.
    /// </summary>
    private readonly string _databaseName = $"SupportCrmTests-{Guid.NewGuid():N}";

    private SqliteConnection? _connection;

    /// <summary>
    /// Extra configuration layered <b>over</b> <c>appsettings.json</c>, for the Story 16 Part A
    /// validation tests that need a deliberately broken value.
    /// <para>
    /// Set it with an object initializer before touching <see cref="WebApplicationFactory{T}.Services"/>;
    /// the host is built lazily, so a later change would not be seen. Empty for every other test,
    /// which is why they all read the real committed configuration.
    /// </para>
    /// </summary>
    public Dictionary<string, string?> ConfigurationOverrides { get; init; } = [];

    /// <summary>
    /// Extra service registrations applied <b>last</b>, so a replacement wins over the composition
    /// root's. Story 07 uses it to swap the logging <c>INotificationPublisher</c> for one that
    /// records, because A-13's notifications have no table until Story 09 and a log line is not
    /// assertable.
    /// <para>
    /// Set it with an object initializer before touching <see cref="WebApplicationFactory{T}.Services"/>,
    /// for the same reason <see cref="ConfigurationOverrides"/> says: the host is built lazily and a
    /// later change would not be seen. Null for every other test.
    /// </para>
    /// </summary>
    public Action<IServiceCollection>? ServiceOverrides { get; init; }

    static SupportCrmApiFactory()
    {
        // WebApplicationBuilder reads the environment while the host is being constructed — before
        // ConfigureWebHost runs — so anything startup validation requires has to be set here.
        //
        // The connection string value is never used: the DbContext registration is replaced below.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SupportCrm", "Server=unused;Database=unused");

        // A test-only signing key. In every real environment this comes from .env via Compose and
        // appsettings.json carries no value for it.
        Environment.SetEnvironmentVariable(
            "SupportCrm__Jwt__SigningKey", "test-only-signing-key-at-least-32-bytes-long");
    }

    /// <summary>Issues a real token for a user id, so tests call the API exactly as a client does.</summary>
    public string IssueTokenFor(Guid userId) =>
        Services.GetRequiredService<ITokenIssuer>().Issue(userId).AccessToken;

    public HttpClient CreateClientFor(Guid userId)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", IssueTokenFor(userId));

        return client;
    }

    /// <summary>Runs work against a fresh scope's <see cref="SupportCrmDbContext"/>.</summary>
    public async Task<T> WithDbAsync<T>(Func<SupportCrmDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();

        return await work(scope.ServiceProvider.GetRequiredService<SupportCrmDbContext>());
    }

    /// <summary>
    /// Creates a department and a staff user directly, bypassing the API. Used to establish the
    /// preconditions of a test — never to assert behaviour that an endpoint should be proving.
    /// </summary>
    public async Task<Guid> AddStaffUserAsync(
        UserRole role, string email, string password = "TestPassw0rd!", Guid? departmentId = null)
    {
        var department = departmentId ?? await EnsureDepartmentAsync("Test Department");

        return await WithDbAsync(async db =>
        {
            var hasher = Services.GetRequiredService<IPasswordHasher<User>>();

            var user = User.CreateStaff(
                id: Guid.NewGuid(),
                email: email,
                passwordHash: "!unhashed",
                displayName: $"{role} {email}",
                role: role,
                departmentId: department,
                branchId: null,
                createdAt: DateTimeOffset.UtcNow);

            user.SetPasswordHash(hasher.HashPassword(user, password));

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return user.Id;
        });
    }

    /// <summary>
    /// Inserts a <c>Customer</c> profile and a <c>Customer</c>-role user row linked to it.
    /// <para>
    /// <b>Why raw SQL for the user half:</b> the Domain still has no
    /// <c>User.CreateCustomerUser</c> — it arrives with Story 04 <b>task 7</b>, alongside
    /// <c>POST /auth/register</c> — and <c>CreateStaff</c> refuses the <c>Customer</c> role outright
    /// (DM-1). Pre-empting that factory here would be starting a later slice. The row is therefore
    /// written beneath the Domain, for the single purpose of proving that the <c>RequireAgent</c>
    /// gate refuses a Customer token: <c>CurrentUserMiddleware</c> reads the role from this row, so
    /// nothing less than a real row exercises the real gate.
    /// </para>
    /// <para>
    /// <b>Updated by Story 04's first slice.</b> Until then <c>CustomerId</c> could be any GUID,
    /// because the column carried no foreign key. It now does
    /// (<c>FK_Users_Customers_CustomerId</c>, completing the DM-1 link Story 02 left open), so the
    /// profile is created first — through the real <c>Customer.Create</c> factory — and the user
    /// points at it. That is what a portal login has always meant (DM-1); the helper was only ever
    /// able to fake it because the constraint was missing.
    /// </para>
    /// <para>
    /// This helper establishes a precondition. <b>It must never be used to assert behaviour an
    /// endpoint should be proving</b>, and its user half should give way to the Domain factory when
    /// Story 04 task 7 lands.
    /// </para>
    /// </summary>
    public async Task<Guid> AddCustomerRoleUserAsync(string email)
    {
        var branchId = await EnsureBranchAsync("Test Branch");

        return await WithDbAsync(async db =>
        {
            var id = Guid.NewGuid();

            // A-2: a customer belongs to one branch, and the column is required. The profile goes
            // through the Domain factory, because Customer exists now and nothing about it needs
            // faking.
            var customer = Customer.Create(
                id: Guid.NewGuid(),
                fullName: email,
                email: email,
                phone: null,
                branchId: branchId,
                createdAt: DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            // DepartmentId is null and CustomerId is set — the Customer shape of DM-1. The unique
            // filtered index on CustomerId still applies, so each call needs its own profile.
            // One interpolated string, not a concatenation: ExecuteSqlAsync takes a
            // FormattableString, so every interpolation hole becomes a parameter rather than
            // inlined SQL.
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO Users (Id, Email, PasswordHash, DisplayName, Role, DepartmentId, CustomerId, BranchId, IsActive, CreatedAt) VALUES ({id}, {email}, {Unhashed}, {email}, {CustomerRoleCode}, NULL, {customer.Id}, NULL, 1, {DateTimeOffset.UtcNow})");

            return id;
        });
    }

    /// <summary>
    /// A portal login for a <b>customer profile that already exists</b> — the DM-1 pairing Story 07's
    /// portal tests need, and the one thing <see cref="AddCustomerRoleUserAsync"/> cannot give them:
    /// that helper creates its own fresh profile, so it can never link a login to the fixture's
    /// customer, and every ownership assertion needs exactly that link.
    ///
    /// <para>
    /// It goes through the real <see cref="User.CreateCustomerUser"/> factory — Story 04 task 7
    /// landed it, so nothing about this shape needs faking any more.
    /// </para>
    ///
    /// <para>
    /// This helper establishes a precondition. <b>It must never be used to assert behaviour an
    /// endpoint should be proving.</b>
    /// </para>
    /// </summary>
    public Task<Guid> AddPortalUserAsync(Guid customerId, string email, string password = "TestPassw0rd!") =>
        WithDbAsync(async db =>
        {
            var existing = await db.Users.FirstOrDefaultAsync(u => u.CustomerId == customerId);

            if (existing is not null)
            {
                return existing.Id;
            }

            var hasher = Services.GetRequiredService<IPasswordHasher<User>>();

            var user = User.CreateCustomerUser(
                id: Guid.NewGuid(),
                email: email,
                passwordHash: Unhashed,
                displayName: email,
                customerId: customerId,
                createdAt: DateTimeOffset.UtcNow);

            user.SetPasswordHash(hasher.HashPassword(user, password));

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return user.Id;
        });

    /// <summary>The role code as it is persisted — a stable string, never an integer (api-design §2).</summary>
    private const string CustomerRoleCode = nameof(UserRole.Customer);

    private const string Unhashed = "!unhashed";

    /// <summary>
    /// Creates a branch directly, to establish a precondition. Branches have no write endpoint by
    /// design (T2-I), so a test cannot create one through the API and is not meant to.
    /// </summary>
    public Task<Guid> EnsureBranchAsync(string name) => WithDbAsync(async db =>
    {
        var existing = await db.Branches.FirstOrDefaultAsync(b => b.Name == name);

        if (existing is not null)
        {
            return existing.Id;
        }

        var branch = Branch.Create(Guid.NewGuid(), name);
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        return branch.Id;
    });

    public Task<Guid> EnsureDepartmentAsync(string name) => WithDbAsync(async db =>
    {
        var existing = await db.Departments.FirstOrDefaultAsync(d => d.Name == name);

        if (existing is not null)
        {
            return existing.Id;
        }

        var department = Department.Create(Guid.NewGuid(), name);
        db.Departments.Add(department);
        await db.SaveChangesAsync();

        return department.Id;
    });

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // The schema is created from the model rather than by running the migration: the migration
        // is SQL Server flavoured (a filtered index, a named collation) and this suite is about
        // behaviour, not DDL. The migration itself is verified by applying it to real SQL Server in
        // the story's verification steps.
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SupportCrmDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        // Layered last, so an override wins over appsettings.json — the same precedence the real
        // application gives environment variables (architecture §6.3).
        if (ConfigurationOverrides.Count > 0)
        {
            builder.ConfigureAppConfiguration(configuration =>
                configuration.AddInMemoryCollection(ConfigurationOverrides));
        }

        builder.ConfigureServices(services =>
        {
            // Removes DatabaseInitializer, so no migration runs and no demo data appears.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IDataSeeder>();

            // Every trace of the SQL Server registration has to go, or EF Core sees two providers
            // configured for one context.
            services.RemoveAll<IDbContextOptionsConfiguration<SupportCrmDbContext>>();
            services.RemoveAll<DbContextOptions<SupportCrmDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SupportCrmDbContext>();

            _connection = new SqliteConnection($"Data Source={_databaseName};Mode=Memory;Cache=Shared");
            _connection.Open();

            services.AddDbContext<SupportCrmDbContext>(options => options.UseSqlite(_connection));

            // Last, so a test's replacement wins over the composition root's registration.
            ServiceOverrides?.Invoke(services);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
