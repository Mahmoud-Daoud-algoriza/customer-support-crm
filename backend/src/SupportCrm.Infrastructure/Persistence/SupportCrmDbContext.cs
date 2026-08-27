using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;
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

    /// <summary>
    /// Story 04 — the CRM profile of requirements §1 (docs/data-model.md §2.4). Organization-wide
    /// and readable by any staff role; a <c>Customer</c>-role caller is refused by the endpoints'
    /// role gate, not by row filtering.
    /// </summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>
    /// Story 04 — an agent's note on a customer (docs/data-model.md §2.5).
    /// <b>Immutable once written</b>: <see cref="CustomerNote"/> exposes no mutator, so there is no
    /// update or delete path to expose later by accident (§5 constraint 16).
    /// </summary>
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();

    /// <summary>
    /// Story 04 — a single uploaded file, owned by a ticket <b>xor</b> a customer
    /// (docs/data-model.md §2.11, §5 constraint 20). Shared by the <c>Customers</c> and
    /// <c>Tickets</c> modules; there is deliberately no second attachment type (§3).
    /// </summary>
    public DbSet<Attachment> Attachments => Set<Attachment>();

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

            // Story 04. A-10 is the same product rule as A-9 for the same reason: "two addresses
            // differing only in case are the same address". The two columns hold the same address
            // compared across two tables (A-19, docs/data-model.md §5 constraint 1a), so they must
            // not differ in collation any more than they differ in width (§6.1).
            modelBuilder.Entity<Customer>()
                .Property(c => c.Email)
                .UseCollation(UserConfiguration.CaseInsensitiveCollation);
        }
    }
}
