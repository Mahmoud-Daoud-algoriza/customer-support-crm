using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Infrastructure.Persistence.Configurations;

namespace SupportCrm.Infrastructure.Persistence;

/// <summary>
/// The single database context. One model, one schema, no per-module contexts (A-12).
/// There is deliberately no repository and no unit-of-work type: <see cref="DbContext"/> is
/// already both, and one unit of work is committed per request (AD-3, docs/architecture.md §3).
/// </summary>
public sealed class SupportCrmDbContext(DbContextOptions<SupportCrmDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    // Entity DbSets are added by the story that introduces each entity (docs/data-model.md §3).

    /// <summary>Story 03 — the routing and permission boundary (docs/data-model.md §2.2).</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>
    /// Story 03 — a reporting and filtering attribute only (docs/data-model.md §2.3).
    /// Never joined into an authorization predicate.
    /// </summary>
    public DbSet<Branch> Branches => Set<Branch>();

    /// <summary>
    /// Story 02 — the authoritative identity record. Role, department and active status are re-read
    /// from here on every authenticated request (AD-15, docs/architecture.md §4.1.1).
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Story 02 — the security and administration record (docs/data-model.md §2.14).
    /// Append-only: <see cref="AuditEntry"/> exposes no mutator, and only <c>AuditRecorder</c>
    /// writes it (docs/architecture.md §2.4).
    /// </summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportCrmDbContext).Assembly);

        // Case-insensitive email uniqueness is a product rule (A-9, A-10, docs/data-model.md §5
        // constraint 1), declared on the column so it cannot depend on a server-level default.
        //
        // The guard is about the provider name, not about the rule: "SQL_Latin1_General_CP1_CI_AS"
        // is SQL Server-specific, and the test host runs this same model on SQLite, which would
        // fail on an unknown collation. The rule is verified against real SQL Server in Story 02's
        // verification steps. See UserConfiguration.CaseInsensitiveCollation.
        if (Database.IsSqlServer())
        {
            modelBuilder.Entity<User>()
                .Property(u => u.Email)
                .UseCollation(UserConfiguration.CaseInsensitiveCollation);
        }
    }
}
