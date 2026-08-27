using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;

namespace SupportCrm.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations, runs every registered <see cref="IDataSeeder"/> in ascending
/// <see cref="IDataSeeder.Order"/>, and then runs the configuration checks that read rows.
/// <para>
/// <b>The order is the point.</b> Story 16 Part A's checks 1 and 3 — every category maps to an
/// existing department (A-14), and <c>Registration:DefaultBranchId</c> is an existing branch
/// (A-15) — are referential, so they can only run once the tables exist <em>and</em> the seeders
/// have filled them. Running them during option binding would fail against an empty database on
/// every first start.
/// </para>
/// </summary>
public sealed class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportCrmDbContext>();

        // AD-8: migrations at startup is a deliberate assessment trade-off, not production practice.
        logger.LogInformation("Applying database migrations.");
        await db.Database.MigrateAsync(cancellationToken);

        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(s => s.Order).ToList();
        foreach (var seeder in seeders)
        {
            logger.LogInformation("Running seeder {Seeder} (order {Order}).",
                seeder.GetType().Name, seeder.Order);
            await seeder.SeedAsync(cancellationToken);
        }

        // Story 16 Part A, checks 1 and 3. AFTER seeding, and before the first request is served:
        // a failure here throws out of StartAsync, which stops the host — "invalid configuration
        // fails fast at startup with a clear message" (architecture §6.3, the intake's AC).
        logger.LogInformation("Validating configuration against the database.");
        await ConfigurationValidator.ValidateAgainstDatabaseAsync(
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>(),
            scope.ServiceProvider.GetRequiredService<IOptions<CategoryOptions>>().Value,
            scope.ServiceProvider.GetRequiredService<IOptions<RegistrationOptions>>().Value,
            cancellationToken);

        logger.LogInformation("Database initialization complete: {SeederCount} seeder(s) ran.", seeders.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
