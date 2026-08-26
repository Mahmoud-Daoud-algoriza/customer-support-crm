using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
