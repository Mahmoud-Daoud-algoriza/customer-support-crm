using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="User"/> (docs/data-model.md §2.1, §6, §6.1). The Domain type stays free of
/// EF attributes (AD-4).
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    // String tiers from docs/data-model.md §6.1. Pick a tier, never a number.
    private const int EmailMaxLength = 256;        // Email tier — index-key eligible
    private const int DisplayNameMaxLength = 200;  // Name tier
    private const int PasswordHashMaxLength = 512; // Line tier — never indexed, never in a response
    private const int CodeMaxLength = 64;          // Code tier — Role, persisted as a string code

    /// <summary>
    /// Case-insensitive email uniqueness is a <b>product rule</b> (A-9, A-10, docs/data-model.md §5
    /// constraint 1), so the collation is declared on the column rather than left to the server
    /// default — a different deployment could set that default otherwise (docs/data-model.md §6.1).
    /// <para>
    /// It is applied from <c>SupportCrmDbContext.OnModelCreating</c> under a
    /// <c>Database.IsSqlServer()</c> guard, because the name is SQL Server-specific and the test
    /// host runs the same model on SQLite. The guard is about the provider, not about the rule: the
    /// rule is verified against real SQL Server in the story's verification steps.
    /// </para>
    /// </summary>
    public const string CaseInsensitiveCollation = "SQL_Latin1_General_CP1_CI_AS";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(EmailMaxLength);
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(DisplayNameMaxLength);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(PasswordHashMaxLength);

        // Stable string code, never an integer (docs/api-design.md §2) — so renumbering the enum
        // could never silently re-grant access.
        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(CodeMaxLength);

        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        // Sign-in lookup and duplicate rejection (docs/data-model.md §6).
        builder.HasIndex(u => u.Email).IsUnique();

        // Round-robin assignment candidates (T2-D) and user administration filtered by department.
        builder.HasIndex(u => new { u.DepartmentId, u.IsActive });

        // At most one User per Customer (docs/data-model.md §5 constraint 3). Filtered, because
        // every staff row has a null CustomerId and nulls must not collide with one another.
        builder.HasIndex(u => u.CustomerId)
            .IsUnique()
            .HasFilter("[CustomerId] IS NOT NULL");

        // Department and Branch exist (Story 03). Restrict: an organization row that staff still
        // reference cannot be deleted out from under them.
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(u => u.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // CustomerId carries no foreign key yet — Customer arrives with Story 04, which adds it in
        // its own migration. The column and its unique index exist now because DM-1 defines the
        // shape of a User regardless of when the other side lands.
        builder.Property(u => u.CustomerId);
    }
}
