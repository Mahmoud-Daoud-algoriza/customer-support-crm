using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="TicketTask"/> (docs/data-model.md §2.10, §5 constraints 10, 23, §6,
/// §6.1). The Domain type stays free of EF attributes (AD-4).
///
/// <para>
/// <b>Constraint 23 is not a check constraint here, and that is consistent with §5's own closing
/// note</b>: cross-row and configuration-dependent rules are enforced in the Application or Domain
/// layer rather than split between code and schema (architecture §2.1). <c>completedAt</c> is
/// written in exactly one place — <see cref="TicketTask.SetDone"/> — so the invariant holds by
/// construction and a database constraint would be a second home for one rule.
/// </para>
/// </summary>
public sealed class TicketTaskConfiguration : IEntityTypeConfiguration<TicketTask>
{
    // String tiers from docs/data-model.md §6.1. Pick a tier, never a number.
    private const int NameMaxLength = 200; // Name tier — a single-line human-readable label

    public void Configure(EntityTypeBuilder<TicketTask> builder)
    {
        builder.ToTable("TicketTasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title).IsRequired().HasMaxLength(NameMaxLength);

        // Required — the intake requires a due date (§2.10). A task with no deadline is not the
        // thing requirements §4.3 asks for.
        builder.Property(t => t.DueAt).IsRequired();

        builder.Property(t => t.IsDone).IsRequired();

        // Nullable by design: null is the "not done" half of constraint 23, not a missing value.
        builder.Property(t => t.CompletedAt);

        builder.Property(t => t.CreatedAt).IsRequired();

        // ---------------------------------------------------------------- relationships

        // Cascade from the ticket: a task has no meaning without it, and §2.10 makes the ticket its
        // owner — the same ownership TicketMessage and TicketInternalNote express.
        builder.Property(t => t.TicketId).IsRequired();
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(t => t.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on BOTH user relationships: assignee and creator must stay resolvable, because
        // the Task payload of docs/api-design.md §6.4 projects both display names. A user is
        // deactivated, never deleted (§2.1).
        builder.Property(t => t.AssignedUserId).IsRequired();
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

        // docs/data-model.md §6 declares exactly this index: TicketTask(assignedUserId, isDone,
        // dueAt), for "Open and overdue tasks on the dashboard (T2-C)".
        //
        // It is created REGARDLESS of how S9-1 is decided, as this story's plan directs: no endpoint
        // publishes a cross-ticket task list today, and the index is the data model's requirement,
        // not the endpoint's. It also serves the per-ticket read's assignee projection.
        builder.HasIndex(t => new { t.AssignedUserId, t.IsDone, t.DueAt });
    }
}
