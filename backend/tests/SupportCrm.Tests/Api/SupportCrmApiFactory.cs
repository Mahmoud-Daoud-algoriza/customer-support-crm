using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SupportCrm.Application.Abstractions;
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
    /// Inserts a <c>Customer</c>-role user row directly.
    /// <para>
    /// <b>Why raw SQL:</b> the Domain has no <c>User.CreateCustomerUser</c> yet — it arrives with
    /// Story 04, when <c>Customer</c> exists — and <c>CreateStaff</c> refuses the <c>Customer</c>
    /// role outright (DM-1). Pre-empting that factory here would be starting Story 04. The row is
    /// therefore written beneath the Domain, for the single purpose of proving that the
    /// <c>RequireAgent</c> gate refuses a Customer token: <c>CurrentUserMiddleware</c> reads the role
    /// from this row, so nothing less than a real row exercises the real gate.
    /// </para>
    /// <para>
    /// This helper establishes a precondition. <b>It must never be used to assert behaviour an
    /// endpoint should be proving</b>, and it should give way to the Domain factory once Story 04
    /// lands.
    /// </para>
    /// </summary>
    public Task<Guid> AddCustomerRoleUserAsync(string email) => WithDbAsync(async db =>
    {
        var id = Guid.NewGuid();

        // DepartmentId is null and CustomerId is set — the Customer shape of DM-1. CustomerId
        // carries no foreign key until Story 04 adds the Customers table, so an unreferenced id is
        // valid here, and the unique filtered index on it still applies.
        // One interpolated string, not a concatenation: ExecuteSqlAsync takes a FormattableString,
        // so every interpolation hole becomes a parameter rather than inlined SQL.
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO Users (Id, Email, PasswordHash, DisplayName, Role, DepartmentId, CustomerId, BranchId, IsActive, CreatedAt) VALUES ({id}, {email}, {Unhashed}, {email}, {CustomerRoleCode}, NULL, {Guid.NewGuid()}, NULL, 1, {DateTimeOffset.UtcNow})");

        return id;
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
