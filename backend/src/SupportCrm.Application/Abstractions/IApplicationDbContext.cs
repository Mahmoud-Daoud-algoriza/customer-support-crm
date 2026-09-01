using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Knowledge;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Abstractions;

/// <summary>
/// The <c>DbContext</c> abstraction that Application orchestrates persistence through
/// (docs/architecture.md §2.1). It exists for one reason: the dependency rule is compiler-enforced
/// (AD-2), so Application cannot name <c>SupportCrmDbContext</c>, which lives in Infrastructure.
/// <para>
/// <b>This is not a repository, and AD-3 still holds.</b> AD-3 forbids a repository or
/// unit-of-work <em>layer over</em> EF Core — a hand-written abstraction that wraps querying. This
/// interface wraps nothing: it exposes the same <see cref="DbSet{TEntity}"/>s and the same
/// <c>SaveChangesAsync</c>, adds no method of its own, and leaves <c>DbContext</c> as both the
/// repository and the unit of work. Application still writes LINQ directly against the sets.
/// </para>
/// One unit of work per request, committed once (docs/architecture.md §3).
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<AuditEntry> AuditEntries { get; }

    DbSet<Department> Departments { get; }

    DbSet<Branch> Branches { get; }

    DbSet<Customer> Customers { get; }

    DbSet<CustomerNote> CustomerNotes { get; }

    DbSet<Attachment> Attachments { get; }

    /// <summary>Story 05 — docs/data-model.md §2.6. Scoped through <c>TicketScope</c> (AD-5).</summary>
    DbSet<Ticket> Tickets { get; }

    /// <summary>Story 05 — the append-only spine of docs/data-model.md §2.7.</summary>
    DbSet<TicketActivity> TicketActivities { get; }

    /// <summary>
    /// Story 07 — the one normalized message model of docs/data-model.md §2.8, carrying the channel
    /// it arrived on (docs/architecture.md §5.2). Scoped through the ticket, never independently.
    /// </summary>
    DbSet<TicketMessage> TicketMessages { get; }

    /// <summary>
    /// Story 09 — the in-app notifications of docs/data-model.md §2.12. <b>Recipient-scoped in every
    /// query</b>: a notification belongs to one user and another user's row is a <c>404</c>.
    /// </summary>
    DbSet<Notification> Notifications { get; }

    /// <summary>
    /// Story 12 — the knowledge base of docs/data-model.md §2.13. Organization-wide, with
    /// <b>no</b> relationship to <c>Ticket</c>: suggested solutions are keyword retrieval at read
    /// time (AD-13), not stored links. The portal's visibility rule is composed once, in
    /// <c>PortalArticleService.PortalVisible</c>.
    /// </summary>
    DbSet<KnowledgeArticle> KnowledgeArticles { get; }

    /// <summary>
    /// Story 13 — the sole CSAT input of docs/data-model.md §2.15. It belongs to the <c>Tickets</c>
    /// module (<b>DM-7</b>): there is no <c>Portal</c> backend module. <b>Write-once and unique per
    /// ticket</b>, and the absence of a row is meaningful — declining is a normal outcome (T2-F), so
    /// reporting reads a missing row as "no response", never as a zero.
    /// </summary>
    DbSet<CustomerFeedback> CustomerFeedback { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
