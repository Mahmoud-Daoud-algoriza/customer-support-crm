using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Tickets;
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

    /// <summary>
    /// Story 05 — the unit of support work (docs/data-model.md §2.6). <c>DepartmentId</c> is the
    /// authorization boundary (A-2), applied through <c>TicketScope</c> and <b>never</b> through a
    /// global query filter (AD-5).
    /// </summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>
    /// Story 05 — the append-only history spine (docs/data-model.md §2.7). Written only by
    /// <c>TicketActivityRecorder</c>; there is no update or delete path anywhere.
    /// </summary>
    public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();

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

        ApplySqliteDateTimeOffsetWorkaround(modelBuilder);
    }

    /// <summary>
    /// The mirror of the collation guard above, for the other provider.
    ///
    /// <para>
    /// <b>SQLite cannot <c>ORDER BY</c> a <c>DateTimeOffset</c> at all</b> — its provider throws
    /// <c>NotSupportedException</c>, because a stored offset makes the text form non-sortable in
    /// general. SQL Server has no such limitation. That is not a cosmetic gap for this schema: the
    /// notes list, the attachment list and (from Story 06) the interaction timeline are all
    /// <b>newest first by contract</b> (docs/api-design.md §5.5), so on the hermetic test host those
    /// reads could not execute at all and would have no automated coverage whatsoever.
    /// </para>
    ///
    /// <para>
    /// So under SQLite — <b>and only under SQLite</b> — every <c>DateTimeOffset</c> is stored as UTC
    /// ticks, which sort correctly. <b>This is lossless here</b>: every timestamp in this system
    /// comes from <c>TimeProvider.GetUtcNow()</c> and is serialized as UTC with a trailing <c>Z</c>
    /// (docs/api-design.md §2, <c>UtcDateTimeOffsetConverter</c>), so no non-zero offset exists to
    /// lose. Ordering by UTC ticks and ordering by <c>datetimeoffset</c> therefore agree.
    /// </para>
    ///
    /// <para>
    /// <b>Production is untouched.</b> On SQL Server the columns stay <c>datetimeoffset</c>, exactly
    /// as the migrations declare. The guard is about the provider, not about the rule — the same
    /// reasoning the collation guard above already states.
    /// </para>
    ///
    /// <para>
    /// The provider is matched by name rather than with <c>Database.IsSqlite()</c>, which lives in
    /// the SQLite package: Infrastructure references only the SQL Server provider (AD-2), and
    /// referencing a second one so that production code can name the test's database would be the
    /// wrong trade.
    /// </para>
    /// </summary>
    private void ApplySqliteDateTimeOffsetWorkaround(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName != SqliteProviderName)
        {
            return;
        }

        var toTicks = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero));

        var nullableToTicks = new ValueConverter<DateTimeOffset?, long?>(
            v => v == null ? null : v.Value.UtcTicks,
            v => v == null ? null : new DateTimeOffset(v.Value, TimeSpan.Zero));

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(toTicks);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableToTicks);
                }
            }
        }
    }

    /// <summary>
    /// <c>Microsoft.EntityFrameworkCore.Sqlite</c>'s provider name. Only the test host uses it; see
    /// <see cref="ApplySqliteDateTimeOffsetWorkaround"/>.
    /// </summary>
    private const string SqliteProviderName = "Microsoft.EntityFrameworkCore.Sqlite";
}
