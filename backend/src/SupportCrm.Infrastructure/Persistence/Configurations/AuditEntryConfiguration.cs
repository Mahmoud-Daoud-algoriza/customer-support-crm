using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="AuditEntry"/> (docs/data-model.md §2.14, §6, §6.1).
/// <para>
/// <b>Append-only by construction</b> (docs/architecture.md §2.4). Nothing here enforces that —
/// the entity exposing no mutator does. This file only maps it.
/// </para>
/// </summary>
public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    // String tiers from docs/data-model.md §6.1.
    private const int CodeMaxLength = 64;             // Action, TargetType, Outcome
    private const int ActorDescriptorMaxLength = 256; // Email tier — the recorder truncates to fit

    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.OccurredAt).IsRequired();

        builder.Property(a => a.Action).IsRequired().HasMaxLength(CodeMaxLength);
        builder.Property(a => a.TargetType).HasMaxLength(CodeMaxLength);

        builder.Property(a => a.Outcome)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(CodeMaxLength);

        // Unvalidated client input — the submitted identifier of a failed sign-in. AuditRecorder
        // truncates to this length rather than throwing (docs/data-model.md §6.1): an attempt with
        // an absurd identifier must still be recorded.
        builder.Property(a => a.ActorDescriptor).HasMaxLength(ActorDescriptorMaxLength);

        // Administrator filtering by date range and by actor (T2-H, docs/data-model.md §6).
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => a.ActorUserId);

        // "User 0..1 (actor)" — docs/data-model.md §2.14. Restrict, so an audited actor cannot be
        // deleted out from under the record; users are deactivated, never deleted, in any case.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // TargetType + TargetId deliberately carry NO foreign key: the target may be any entity,
        // and §2.14 accepts this contained exception to relational purity rather than adding one
        // nullable FK column per auditable type.
        builder.Property(a => a.TargetId);
    }
}
