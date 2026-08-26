using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;

namespace SupportCrm.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and then runs every registered <see cref="IDataSeeder"/> in
/// ascending <see cref="IDataSeeder.Order"/>.
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

        logger.LogInformation("Database initialization complete: {SeederCount} seeder(s) ran.", seeders.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
