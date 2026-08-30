using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="Ticket"/> (docs/data-model.md §2.6, §5 constraints 10–12, §6, §6.1). The
/// Domain type stays free of EF attributes (AD-4).
///
/// <para>
/// <b>There is no branch column and no branch navigation on this type, and there must never be
/// one.</b> A ticket's branch is derived <c>Ticket → Customer → Branch</c> (§2.3). A column here
/// would sit within arm's reach of <c>TicketScope</c>, where A-2 and §5 constraint 6 forbid its use.
/// </para>
///
/// <para>
/// <b>There is no global query filter on this type either</b> (AD-5). Department scoping is an
/// explicit helper a reader can find and a test can target; a filter that is accidentally absent
/// fails open, Managers and Administrators would have to bypass it, and reporting aggregates must
/// not be silently narrowed (docs/architecture.md §4.3, rejected alternative).
/// </para>
/// </summary>
public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    // String tiers from docs/data-model.md §6.1. Pick a tier, never a number.
    private const int CodeMaxLength = 64;  // Code tier — CategoryCode, which is an index-eligible code
    private const int LineMaxLength = 512; // Line tier — Subject, a single line of prose

    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Subject).IsRequired().HasMaxLength(LineMaxLength);

        // Description is the Text tier — unbounded prose, and §6.1 says Text is never indexed.
        builder.Property(t => t.Description).IsRequired();

        // A-6: validated against the configured list, not a table. Code tier, because it is a
        // compact identifier-like value and it appears in filters.
        builder.Property(t => t.CategoryCode).IsRequired().HasMaxLength(CodeMaxLength);

        // Stable string codes, never ordinals (docs/api-design.md §2) — so reordering either enum
        // cannot silently reinterpret stored rows.
        builder.Property(t => t.Priority).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);
        builder.Property(t => t.Status).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);

        builder.Property(t => t.IsUrgent).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        // Required and non-null (§2.6), which is why Story 05 computes them at creation rather than
        // Story 09. A-20 freezes them: no later priority change moves either one.
        builder.Property(t => t.FirstResponseDueAt).IsRequired();
        builder.Property(t => t.ResolutionDueAt).IsRequired();

        builder.Property(t => t.FirstResponseBreached).IsRequired();
        builder.Property(t => t.ResolutionBreached).IsRequired();

        // ---------------------------------------------------------------- relationships

        // Restrict throughout: a ticket's customer, department and assignee must stay resolvable,
        // because every read projects their names (docs/api-design.md §6.4). A user is deactivated,
        // never deleted (§2.1); a department and a branch are seeded configuration (T2-I).
        builder.Property(t => t.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // The authorization edge (A-2, §5 relationship table).
        builder.Property(t => t.DepartmentId).IsRequired();
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(t => t.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.AssignedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(t => t.CreatedByUserId).IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------- indexes (§6)

        // The department-scoped queue and every §9.1 count.
        builder.HasIndex(t => new { t.DepartmentId, t.Status });

        // The agent's own queue — the T1-C primary screen.
        builder.HasIndex(t => new { t.AssignedUserId, t.Status });

        // Portal ticket list (T2-F) and the customer interaction timeline (§1.3).
        builder.HasIndex(t => new { t.CustomerId, t.CreatedAt });

        // The two filtered due-date indexes, restricted to non-terminal, non-breached rows — the
        // SLA monitor's periodic sweep (AD-6, Story 09). Without them it scans every ticket on
        // every tick, which §6 calls out by name.
        //
        // The filter is written in SQL, so it names columns and stored enum codes rather than CLR
        // members. Bracket-quoted identifiers keep the same expression valid on SQL Server and on
        // the SQLite the test host runs the model against, exactly as the attachment XOR check does.
        builder.HasIndex(t => t.FirstResponseDueAt)
            .HasFilter(ActiveUnbreachedFirstResponse);

        builder.HasIndex(t => t.ResolutionDueAt)
            .HasFilter(ActiveUnbreachedResolution);

        // NOT indexed, on purpose (§6, "Not indexed on purpose"): Ticket.Priority and
        // Ticket.CategoryCode are low-cardinality and are always queried alongside a covered
        // column. Adding either would be a physical decision §6 reserves to itself.
    }

    /// <summary>
    /// Non-terminal means not <c>Closed</c> and not <c>Cancelled</c> — the two terminal statuses of
    /// A-5. Written as stored string codes because the column carries the code, not the ordinal.
    ///
    /// <para>
    /// <b>Two <c>&lt;&gt;</c> comparisons, not <c>NOT IN</c>, and that is a SQL Server requirement
    /// rather than a style choice.</b> A filtered-index predicate does not admit <c>NOT IN</c>:
    /// <c>CREATE INDEX … WHERE [Status] NOT IN ('Closed','Cancelled')</c> fails to parse with
    /// <c>Msg 102, Incorrect syntax near 'NOT'</c>, which took the whole <c>Tickets</c> migration
    /// down at startup. SQLite accepts both forms, so the hermetic test host could not have caught
    /// it — it was found by applying the migration to real SQL Server. Recorded as finding I-17.
    /// </para>
    /// </summary>
    private const string NonTerminal =
        "[Status] <> 'Closed' AND [Status] <> 'Cancelled'";

    private const string ActiveUnbreachedFirstResponse =
        $"{NonTerminal} AND [FirstResponseBreached] = 0";

    private const string ActiveUnbreachedResolution =
        $"{NonTerminal} AND [ResolutionBreached] = 0";
}
