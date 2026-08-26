using Microsoft.EntityFrameworkCore;

namespace SupportCrm.Infrastructure.Persistence;

/// <summary>
/// The single database context. One model, one schema, no per-module contexts (A-12).
/// There is deliberately no repository and no unit-of-work type: <see cref="DbContext"/> is
/// already both, and one unit of work is committed per request (AD-3, docs/architecture.md §3).
/// </summary>
public sealed class SupportCrmDbContext(DbContextOptions<SupportCrmDbContext> options) : DbContext(options)
{
    // Entity DbSets are added by the story that introduces each entity (docs/data-model.md §3).

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportCrmDbContext).Assembly);
}
