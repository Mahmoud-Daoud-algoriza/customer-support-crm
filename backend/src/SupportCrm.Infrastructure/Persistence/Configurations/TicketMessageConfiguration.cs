using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="TicketMessage"/> (docs/data-model.md §2.8, §5 constraints 8, 16, 17, §6,
/// §6.1). The Domain type stays free of EF attributes (AD-4).
///
/// <para>
/// <b>Immutability is a property of the type, not of this mapping.</b> There is nothing to configure
/// here that enforces it — the entity has no mutator and no service exposes an edit or a delete
/// (§5 constraint 16). This file maps what exists.
/// </para>
///
/// <para>
/// <b>Nothing here is channel-specific.</b> <see cref="TicketMessage.Channel"/> is one column, so a
/// second channel is a new enum member and <b>no schema change</b> — the claim
/// docs/architecture.md §5.2 makes and Story 18 demonstrates.
/// </para>
/// </summary>
public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    // String tiers from docs/data-model.md §6.1. Pick a tier, never a number.
    private const int CodeMaxLength = 64; // Code tier — the two enums, stored as string codes

    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.PostedAt).IsRequired();

        // Stable string codes, never ordinals (docs/api-design.md §2) — so adding a channel member
        // for Story 18's adapter cannot silently reinterpret stored rows.
        builder.Property(m => m.Direction).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);
        builder.Property(m => m.Channel).IsRequired().HasConversion<string>().HasMaxLength(CodeMaxLength);

        // Body is the Text tier — authored multi-line content (§6.1), and §6.1 says Text is never
        // indexed. No MaxLength: a reply is prose, and the tier fixes that as nvarchar(max).
        builder.Property(m => m.Body).IsRequired();

        // ---------------------------------------------------------------- relationships

        // Cascade from the ticket, for the same reason TicketActivity does: a message has no meaning
        // without its ticket, and §2.8 makes the ticket its owner. Nothing deletes a ticket today —
        // no endpoint exists — so this describes ownership, it does not enable a delete path.
        builder.Property(m => m.TicketId).IsRequired();
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(m => m.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: the author must stay resolvable, because every thread read projects their
        // display name (docs/api-design.md §6.4). A user is deactivated, never deleted (§2.1).
        builder.Property(m => m.AuthorUserId).IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------- indexes (§6)

        // Thread rendering — docs/data-model.md §6 declares exactly this index and no other for
        // this table. Body is Text and is therefore never an index key (§6.1).
        builder.HasIndex(m => new { m.TicketId, m.PostedAt });
    }
}
