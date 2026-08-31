using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>Notification</c> — docs/data-model.md §2.12, §6.
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id);

        // Enum as string, per 00-implementation-plan §6: a stored ordinal breaks the moment a member
        // is reordered, and these four are read straight out of the database during a demo.
        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(n => n.CreatedAt).IsRequired();

        builder.Property(n => n.ReadAt);

        // **The index the list and the badge both ride on** (docs/data-model.md §6).
        // `GET /notifications` filters on the recipient and optionally on unread, and `unreadCount`
        // is a count over exactly this pair — so one composite index serves the screen, the badge and
        // the `unreadOnly=true` variant without a scan.
        builder.HasIndex(n => new { n.RecipientUserId, n.ReadAt })
            .HasDatabaseName("IX_Notifications_RecipientUserId_ReadAt");

        // `Restrict` on both, matching every other reference in this schema: a user is deactivated
        // and a ticket is cancelled — neither is ever deleted — so a cascade would express a
        // lifecycle the product does not have (docs/data-model.md §5).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
