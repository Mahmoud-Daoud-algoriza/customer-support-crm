using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="Attachment"/> (docs/data-model.md §2.11, §5 constraint 20, §6.1). The
/// Domain type stays free of EF attributes (AD-4).
/// </summary>
public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    // String tiers from docs/data-model.md §6.1. Pick a tier, never a number.
    private const int LineMaxLength = 512; // Line tier — FileName, StoragePath. Never indexed
    private const int NameMaxLength = 200; // Name tier — ContentType (§6.1: not Code; see below)

    /// <summary>
    /// <b>The owner XOR rule as a database constraint</b> (docs/data-model.md §5 constraint 20):
    /// exactly one of <c>TicketId</c> / <c>CustomerId</c> is non-null.
    /// <para>
    /// This is a <b>second</b> line, not the rule's home. The rule's home is
    /// <see cref="Attachment"/>'s two factories, which make a violation unconstructible. The
    /// constraint exists because §5 names this one as a constraint the database asserts — unlike
    /// constraints 2, 3, 10, 11, 17, 18, 19 and 21, which §5 explicitly leaves to the Application or
    /// Domain layer.
    /// </para>
    /// <para>
    /// Bracket-quoted identifiers so the same expression is valid on SQL Server and on the SQLite
    /// the test host runs the model against.
    /// </para>
    /// </summary>
    public const string OwnerXorConstraintName = "CK_Attachments_OwnerXor";

    private const string OwnerXorSql =
        "([TicketId] IS NULL AND [CustomerId] IS NOT NULL) OR " +
        "([TicketId] IS NOT NULL AND [CustomerId] IS NULL)";

    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments", table =>
            table.HasCheckConstraint(OwnerXorConstraintName, OwnerXorSql));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(LineMaxLength);

        // ContentType is the Name tier, NOT Code — docs/data-model.md §6.1 spells out why: a real
        // MIME type such as the 73-character .docx one would not fit the 64-character Code tier.
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(NameMaxLength);

        builder.Property(a => a.SizeBytes).IsRequired();

        // The Line tier. Never indexed and returned by no endpoint, ever
        // (docs/api-design.md §6.7) — the bytes come only from
        // GET /attachments/{attachmentId}/content (AP-19).
        builder.Property(a => a.StoragePath).IsRequired().HasMaxLength(LineMaxLength);

        builder.Property(a => a.UploadedAt).IsRequired();

        // Restrict, matching every foreign key Stories 02 and 03 declared and for the same reason:
        // a row that something still references is not deleted out from under it.
        //
        // Deleting a customer is not an application operation (docs/data-model.md §2.4) and an
        // attachment has no delete path in scope (§2.11), so no code path reaches either behaviour.
        // Restrict is nonetheless the safer of two inert choices: an attachment owns BYTES ON DISK
        // that no database cascade can remove, so a cascade would delete the row and orphan the
        // file permanently. A refusal is recoverable; an orphaned file is not.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(a => a.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Story 05 task 3 completes this relationship. The column and the XOR check constraint
        // existed from Story 04, because the owner rule is part of this entity's shape regardless
        // of when the other side lands — exactly as Story 02 did for User.CustomerId. Restrict, for
        // the same reason as the customer side: an attachment must not outlive its owner silently.
        builder.Property(a => a.TicketId);
        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: the uploader must stay resolvable, since docs/api-design.md §6.7 returns
        // uploadedBy as a UserSummary. A user is deactivated, never deleted (§2.1).
        builder.Property(a => a.UploadedByUserId).IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
