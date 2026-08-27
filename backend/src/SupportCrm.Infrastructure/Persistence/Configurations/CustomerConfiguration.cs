using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Organization;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="Customer"/> (docs/data-model.md §2.4, §5 constraint 1, §6, §6.1). The
/// Domain type stays free of EF attributes (AD-4).
/// <para>
/// <b>There is no global query filter on this type and there must never be one.</b> A customer is
/// organization-wide and readable by any staff role (docs/data-model.md §2.4); the fact that a
/// <c>Customer</c>-role caller cannot browse the directory is a <em>role gate</em> on the endpoints,
/// not row filtering — and AD-5 keeps access scoping an explicit helper rather than an EF filter in
/// any case.
/// </para>
/// </summary>
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    // String tiers from docs/data-model.md §6.1. Pick a tier, never a number.
    private const int EmailMaxLength = 256; // Email tier — index-key eligible, which this needs
    private const int NameMaxLength = 200;  // Name tier — FullName, ExternalReference
    private const int CodeMaxLength = 64;   // Code tier — Phone

    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.FullName).IsRequired().HasMaxLength(NameMaxLength);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(EmailMaxLength);

        // Phone is the Code tier of §6.1 — "compact identifier-like values".
        builder.Property(c => c.Phone).HasMaxLength(CodeMaxLength);

        // The ERP seam's one persisted field (DM-6). Optional, unused by default, and settable
        // through no endpoint (docs/api-design.md §8.3) — which the Domain type enforces by
        // exposing no mutator. Mapped here so the column exists for an adapter that one day writes
        // it; not indexed, because no query looks it up.
        builder.Property(c => c.ExternalReference).HasMaxLength(NameMaxLength);

        builder.Property(c => c.CreatedAt).IsRequired();

        // A-10 identification and duplicate rejection (docs/data-model.md §6). The case-insensitive
        // collation is applied from SupportCrmDbContext.OnModelCreating under a provider guard, for
        // the same reason as User.Email — see UserConfiguration.CaseInsensitiveCollation.
        builder.HasIndex(c => c.Email).IsUnique();

        // Branch filtering in the customer directory (docs/ui-design.md §5.4) and in reports
        // (T2-K, T2-G) — docs/data-model.md §6.
        builder.HasIndex(c => c.BranchId);

        // A-2: required. Restrict — a branch that customers still reference cannot be deleted out
        // from under them.
        builder.Property(c => c.BranchId).IsRequired();
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(c => c.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
