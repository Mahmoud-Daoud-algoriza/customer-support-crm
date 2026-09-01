using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="CustomerFeedback"/> (docs/data-model.md §2.15, §5 constraint 21, §6,
/// §6.1). The Domain type stays free of EF attributes (AD-4).
///
/// <para>
/// <b>Write-once is a property of the type, not of this mapping.</b> There is nothing to configure
/// that enforces it — the entity has no mutator and no service exposes an update or a delete
/// (§2.15). This file maps what exists.
/// </para>
///
/// <para>
/// <b>⚠ There is deliberately NO check constraint on <c>Rating</c> (OQ-1).</b>
/// docs/data-model.md §2.15: the model <em>"encodes no range"</em>, and no minimum, maximum or step
/// <em>"may be inferred from this document into a validation rule, a check constraint, or a UI
/// control."</em> A <c>CHECK (Rating BETWEEN …)</c> here would bake an unanswered product question
/// into the schema and make answering OQ-1 a migration instead of a configuration edit. The range is
/// validated in exactly one place — <c>CustomerFeedbackService</c>, against the
/// <c>Feedback rating scale</c> key (docs/architecture.md §6.3).
/// </para>
/// </summary>
public sealed class CustomerFeedbackConfiguration : IEntityTypeConfiguration<CustomerFeedback>
{
    public void Configure(EntityTypeBuilder<CustomerFeedback> builder)
    {
        builder.ToTable("CustomerFeedback");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Rating).IsRequired();

        // Comment is the Text tier (§6.1) — authored prose, never indexed, so no MaxLength.
        builder.Property(f => f.Comment);

        builder.Property(f => f.SubmittedAt).IsRequired();

        // ---------------------------------------------------------------- relationships

        // Restrict, NOT cascade — and the difference from TicketMessage is deliberate. A message is
        // correspondence the ticket owns; a rating is a REPORTING fact that feeds the §9.4 average,
        // and §2.15 makes the absence of a row meaningful ("no response"). Silently deleting one
        // with its ticket would change a reported average. Nothing deletes a ticket today — no
        // endpoint exists — so this states ownership rather than enabling a delete path.
        builder.Property(f => f.TicketId).IsRequired();
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(f => f.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------- indexes (§6)

        // The one index docs/data-model.md §6 declares for this table, and it is UNIQUE:
        // "Enforces one rating per ticket; feeds the §9.4 average" (§5 constraint 21). It is what
        // makes the second submission a database-level impossibility as well as a 409 the service
        // returns first.
        builder.HasIndex(f => f.TicketId).IsUnique();
    }
}
