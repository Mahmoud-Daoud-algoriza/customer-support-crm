using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="TicketInternalNote"/> (docs/data-model.md §2.9, §5 constraints 8, 16, 17,
/// 18, §6, §6.1). The Domain type stays free of EF attributes (AD-4).
///
/// <para>
/// <b>There is no visibility column, and its absence is the design.</b> A note is staff-only by
/// virtue of living in its own table that no customer-facing query names (§2.9's closing invariant)
/// — not by a flag that a projection could read wrongly. Nothing here enforces the rule because
/// there is nothing here to enforce: the rule is the schema's shape.
/// </para>
///
/// <para>
/// <b>Immutability is a property of the type, not of this mapping</b> (§5 constraint 16) — the
/// entity has no mutator and no service exposes an edit or a delete. This file maps what exists.
/// </para>
/// </summary>
public sealed class TicketInternalNoteConfiguration : IEntityTypeConfiguration<TicketInternalNote>
{
    public void Configure(EntityTypeBuilder<TicketInternalNote> builder)
    {
        builder.ToTable("TicketInternalNotes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        builder.Property(n => n.CreatedAt).IsRequired();

        // Body is the Text tier — authored multi-line content (§6.1), and §6.1 says Text is never
        // indexed. No MaxLength: a note is prose, and the tier fixes that as nvarchar(max).
        builder.Property(n => n.Body).IsRequired();

        // ---------------------------------------------------------------- relationships

        // Cascade from the ticket, exactly as TicketMessage does: a note has no meaning without its
        // ticket, and §2.9 makes the ticket its owner. Nothing deletes a ticket today — no endpoint
        // exists — so this describes ownership, it does not enable a delete path.
        builder.Property(n => n.TicketId).IsRequired();
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: the author must stay resolvable, because every note read projects their display
        // name (docs/api-design.md §6.4). A user is deactivated, never deleted (§2.1).
        builder.Property(n => n.AuthorUserId).IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---------------------------------------------------------------- indexes (§6)

        // Staff thread rendering — docs/data-model.md §6 declares exactly this index and no other
        // for this table. Body is Text and is therefore never an index key (§6.1).
        builder.HasIndex(n => new { n.TicketId, n.CreatedAt });
    }
}
