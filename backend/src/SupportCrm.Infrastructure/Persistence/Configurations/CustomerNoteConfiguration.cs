using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for <see cref="CustomerNote"/> (docs/data-model.md §2.5, §5 constraint 16, §6.1). The
/// Domain type stays free of EF attributes (AD-4).
/// <para>
/// Immutability is a property of the Domain type — it has no mutator — so nothing is needed here to
/// enforce it, and nothing here should try: a database trigger would put the rule in a second place.
/// </para>
/// <para>
/// <b>No index beyond the two foreign keys.</b> docs/data-model.md §6 declares indexes only where a
/// named query needs one and lists none for this table — notably not the
/// <c>(customerId, createdAt)</c> composite it <em>does</em> declare for the analogous
/// <c>TicketInternalNote</c>. The foreign key on <see cref="CustomerNote.CustomerId"/> is indexed as
/// a matter of course (§6's opening line) and already serves the per-customer, newest-first list
/// (docs/api-design.md §5.5). Adding the composite would be speculative indexing, which §6 forbids.
/// </para>
/// </summary>
public sealed class CustomerNoteConfiguration : IEntityTypeConfiguration<CustomerNote>
{
    public void Configure(EntityTypeBuilder<CustomerNote> builder)
    {
        builder.ToTable("CustomerNotes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();

        // The Text tier of docs/data-model.md §6.1 — nvarchar(max), multi-line authored content.
        // Never an index key, which is why no length is set.
        builder.Property(n => n.Body).IsRequired();

        builder.Property(n => n.CreatedAt).IsRequired();

        // Restrict, matching every foreign key Stories 02 and 03 declared. Deleting a customer is
        // not an application operation (docs/data-model.md §2.4) and a note is immutable with no
        // delete path at all (§2.5, §5 constraint 16), so no code path reaches either behaviour —
        // Restrict states that, rather than authorising a cascade nothing asks for. A note exists
        // to be attributed; destroying one as a side effect is the wrong default.
        builder.Property(n => n.CustomerId).IsRequired();
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(n => n.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: the note's attribution must stay resolvable (docs/data-model.md §4 — User 1 →
        // many authored notes). A user is deactivated, never deleted (§2.1), so this never fires —
        // and if it ever did, losing the author of a note that exists to be attributed is exactly
        // the wrong outcome.
        builder.Property(n => n.AuthorUserId).IsRequired();
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
