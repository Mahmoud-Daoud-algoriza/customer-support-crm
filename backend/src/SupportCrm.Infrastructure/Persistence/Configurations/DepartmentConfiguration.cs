using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Organization;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="Department"/> (docs/data-model.md §2.2). The Domain type stays free of
/// EF attributes; everything persistence-specific lives here (AD-4).
/// </summary>
public sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    /// <summary>
    /// Not fixed by docs/data-model.md, which states only that the name is unique. A bound is
    /// nevertheless required: SQL Server cannot build a unique index over <c>nvarchar(max)</c>.
    /// 200 characters is ample for a department name and is an implementation choice, not a
    /// product rule.
    /// </summary>
    private const int NameMaxLength = 200;

    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength);

        builder.HasIndex(d => d.Name).IsUnique();

        // No foreign key to User, deliberately.
        //
        // Two reasons, both from docs/data-model.md §2.2:
        //   1. The real rule is cross-row and conditional — the referenced user must exist, be
        //      active, and hold role Manager or Administrator. A foreign key can express none of
        //      the last two, so it would give the illusion of enforcement while the Application
        //      layer still had to do the work. DepartmentValidator owns it.
        //   2. User.DepartmentId points the other way. A second FK from Department to User would
        //      create a create-order cycle between the two tables.
        builder.Property(d => d.ManagerUserId);
    }
}
