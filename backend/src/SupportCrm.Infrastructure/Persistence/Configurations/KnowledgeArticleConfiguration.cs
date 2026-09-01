using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Knowledge;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// <c>KnowledgeArticle</c> — docs/data-model.md §2.13, §6, §6.1.
///
/// <para>
/// <b>No search index is declared here, and that is the decision, not an omission.</b>
/// docs/data-model.md §6: at assessment data volumes a straightforward contains-match over
/// <c>title</c> and <c>body</c> is sufficient and <em>"needs no special index"</em>. SQL Server
/// full-text indexing on those two columns is recorded there as <em>the available upgrade if
/// matching quality proves inadequate — a database feature, not a new component</em>. <b>This story
/// does not take it</b>, and nothing here creates a full-text catalogue.
/// </para>
///
/// <para>
/// <b><c>Body</c> is the Text tier and is therefore never an index key</b> (§6.1): it is searched by
/// a scan, which is exactly what AD-13 chose.
/// </para>
///
/// <para>
/// <b>There is no foreign key to <c>Ticket</c>, and none may be added</b> (§2.13): suggested
/// solutions are computed at read time, not stored.
/// </para>
/// </summary>
public sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    private const int NameMaxLength = 200; // Name tier — Title (§6.1)
    private const int CodeMaxLength = 64;  // Code tier — Type and Visibility, stored as string codes

    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("KnowledgeArticles");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title).IsRequired().HasMaxLength(NameMaxLength);

        // Text tier — nvarchar(max), the default for an unbounded string. Multi-line authored
        // content, never an index key (§6.1).
        builder.Property(a => a.Body).IsRequired();

        // Enums as strings, per 00-implementation-plan §6: a stored ordinal breaks the moment a
        // member is reordered, and these codes are read straight out of the database during a demo.
        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(CodeMaxLength)
            .IsRequired();

        builder.Property(a => a.Visibility)
            .HasConversion<string>()
            .HasMaxLength(CodeMaxLength)
            .IsRequired();

        builder.Property(a => a.IsPublished).IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();

        builder.Property(a => a.UpdatedAt).IsRequired();

        // `Restrict`, matching every other reference in this schema: a user is deactivated, never
        // deleted, so a cascade would express a lifecycle the product does not have (§5).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
