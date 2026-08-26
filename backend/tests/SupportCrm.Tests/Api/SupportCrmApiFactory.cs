using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SupportCrm.Infrastructure.Persistence;

namespace SupportCrm.Tests.Api;

/// <summary>
/// Hosts the real composition root and swaps only the database, so the tests exercise the actual
/// controllers, options validation, JSON contract and exception handling.
/// <para>
/// SQLite in a shared in-memory database is used rather than SQL Server so the suite is hermetic —
/// no container and no connection string are needed to run <c>dotnet test</c>. The provider is
/// relational, so <c>Database.CanConnectAsync()</c> in the health endpoint still opens a real
/// connection rather than being stubbed out.
/// </para>
/// <para>
/// <see cref="DatabaseInitializer"/> is removed: Story 01 introduces no entity and therefore
/// generates no migration (the first one is created by Story 03), so there is nothing for the
/// migrator to apply here.
/// </para>
/// </summary>
public sealed class SupportCrmApiFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    static SupportCrmApiFactory() =>
        // AddInfrastructure fails fast without a connection string, and WebApplicationBuilder reads
        // the environment while the host is being constructed — before ConfigureWebHost runs. The
        // value itself is never used, because the DbContext registration is replaced below.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SupportCrm", "Server=unused;Database=unused");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            // Every trace of the SQL Server registration has to go, or EF Core sees two providers
            // configured for one context.
            services.RemoveAll<IDbContextOptionsConfiguration<SupportCrmDbContext>>();
            services.RemoveAll<DbContextOptions<SupportCrmDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<SupportCrmDbContext>();

            _connection = new SqliteConnection("Data Source=SupportCrmTests;Mode=Memory;Cache=Shared");
            _connection.Open();

            // Story 01 introduces no entity, so there is no schema to create — an open connection
            // is all Database.CanConnectAsync() needs.
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
