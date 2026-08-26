using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Organization;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="Branch"/> (docs/data-model.md §2.3). The Domain type stays free of EF
/// attributes; everything persistence-specific lives here (AD-4).
/// <para>
/// There is no global query filter on this type and there must never be one: branch grants no
/// isolation (A-2) and appears in no authorization predicate (docs/data-model.md §5 constraint 6).
/// AD-5 records why access scoping is an explicit helper rather than an EF filter in any case.
/// </para>
/// </summary>
public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    /// <summary>
    /// The <b>Name</b> tier of docs/data-model.md §6.1 — <c>nvarchar(200)</c>. Index-key eligible,
    /// which this column needs: the name is unique (§2.3).
    /// <para>Do not pick a length here. Pick a tier from §6.1.</para>
    /// </summary>
    private const int NameMaxLength = 200;

    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength);

        builder.HasIndex(b => b.Name).IsUnique();
    }
}
