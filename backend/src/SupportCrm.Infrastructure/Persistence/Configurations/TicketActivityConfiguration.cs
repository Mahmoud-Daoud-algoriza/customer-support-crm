using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="TicketActivity"/> (docs/data-model.md §2.7, §6, §6.1). The Domain type
/// stays free of EF attributes (AD-4).
///
/// <para>
/// <b>Append-only is a property of the type and its single writer, not of this mapping.</b> There
/// is nothing to configure here that enforces it — the entity has no mutator and
/// <c>TicketActivityRecorder</c> exposes no update or delete. This file only maps what exists.
/// </para>
/// </summary>
public sealed class TicketActivityConfiguration : IEntityTypeConfiguration<TicketActivity>
{
    // String tiers from docs/data-model.md §6.1.
    private const int CodeMaxLength = 64;  // Code tier — the two enums, stored as string codes
    private const int LineMaxLength = 512; // Line tier — OldValue / NewValue, "short text" per §2.7

    public void Configure(EntityTypeBuilder<TicketActivity> builder)
    {
        builder.ToTable("TicketActivities");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.OccurredAt).IsRequired();

        // Stable string codes, never ordinals (docs/api-design.md §2).
        builder.Property(a => a.ActivityType).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);
        builder.Property(a => a.ActorKind).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);
        builder.Property(a => a.Visibility).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);

        // §2.7 calls these "short text" — the Line tier, not Text, and not indexed either way.
        builder.Property(a => a.OldValue).HasMaxLength(LineMaxLength);
        builder.Property(a => a.NewValue).HasMaxLength(LineMaxLength);

        // ---------------------------------------------------------------- relationships

        builder.Property(a => a.TicketId).IsRequired();

        // Cascade, and it is the one place in this model where cascade is right: an activity entry
        // has no meaning without its ticket, and §2.7 makes the ticket its owner. Nothing deletes a
        // ticket today — no endpoint exists — so this describes the ownership, it does not enable a
        // delete path.
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Null exactly when ActorKind is System (§2.7 invariant). Restrict: the actor must stay
        // resolvable, because reads project their display name.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // MessageId is now a real foreign key — Story 07 landed the other side. It is set if and
        // only if ActivityType is MessagePosted (§2.7 invariant), which TicketActivity.MessagePosted
        // makes structural: no other factory accepts a message id, and that one accepts no type.
        //
        // Restrict, not Cascade: §5 constraint 17 says every message has exactly one activity row,
        // so a message must not be removable while its row points at it. Nothing deletes a message
        // in any case — §2.8 makes it immutable and no service exposes a delete.
        builder.Property(a => a.MessageId);
        builder.HasOne<TicketMessage>()
            .WithMany()
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Restrict);

        // InternalNoteId stays a plain column with no foreign key: TicketInternalNote is Story 14's
        // and does not exist yet. The column is part of this entity's shape regardless of when the
        // other side lands — the same placement Story 04 used for Attachment.TicketId.
        builder.Property(a => a.InternalNoteId);

        // ---------------------------------------------------------------- indexes (§6)

        // The history read and the timeline projection — §6 calls it "the single most frequent read
        // after the queue".
        builder.HasIndex(a => new { a.TicketId, a.OccurredAt });
    }
}
