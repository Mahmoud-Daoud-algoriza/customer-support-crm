using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Organization;

namespace SupportCrm.Infrastructure.Persistence;

/// <summary>
/// The single database context. One model, one schema, no per-module contexts (A-12).
/// There is deliberately no repository and no unit-of-work type: <see cref="DbContext"/> is
/// already both, and one unit of work is committed per request (AD-3, docs/architecture.md §3).
/// </summary>
public sealed class SupportCrmDbContext(DbContextOptions<SupportCrmDbContext> options) : DbContext(options)
{
    // Entity DbSets are added by the story that introduces each entity (docs/data-model.md §3).

    /// <summary>Story 03 — the routing and permission boundary (docs/data-model.md §2.2).</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>
    /// Story 03 — a reporting and filtering attribute only (docs/data-model.md §2.3).
    /// Never joined into an authorization predicate.
    /// </summary>
    public DbSet<Branch> Branches => Set<Branch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportCrmDbContext).Assembly);
}
