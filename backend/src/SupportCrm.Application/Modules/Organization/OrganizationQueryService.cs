using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;

namespace SupportCrm.Application.Modules.Organization;

/// <summary>
/// The two read endpoints of docs/api-design.md §5.4 — the lists that later screens use to populate
/// filters and assignment pickers.
/// <para>
/// <b>There is no write path here, and there must never be one.</b> Departments and branches are
/// seeded and configured, not managed through an admin UI (T2-I, docs/api-design.md §5.4): no
/// <c>POST</c>, no <c>PATCH</c>, no <c>DELETE</c>. Adding one is a scope change, not a feature.
/// </para>
/// </summary>
public sealed class OrganizationQueryService(IApplicationDbContext db)
{
    /// <summary>
    /// The sort whitelist for both endpoints (AP-15). Anything not listed is a <c>400</c>.
    /// <para>
    /// <b>No approved document enumerates this set for these two endpoints.</b>
    /// docs/api-design.md §2.1 requires <em>a</em> per-endpoint whitelist and names none, so it is
    /// drawn from the published payload (§6.2) exactly as <c>UserAdminService</c> did for
    /// <c>/users</c>. <c>name</c> is the only sortable field: <c>id</c> and <c>managerUserId</c> are
    /// opaque identifiers and sort meaninglessly.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> SortableFields = new(StringComparer.Ordinal)
    {
        ["name"] = "Name",
    };

    /// <summary>
    /// Default sort. Both lists are alphabetical by name, which is also the stable order paging
    /// needs — and the order a filter dropdown wants anyway.
    /// </summary>
    private const string DefaultSortField = "Name";

    /// <summary>
    /// <c>GET /departments</c>.
    /// <para>
    /// The rows are <c>{ id, name, managerUserId? }</c> (docs/api-design.md §6.2).
    /// <b><c>managerUserId</c> is absent, not <c>null</c>, when the department has no manager</b> —
    /// nulls are omitted (docs/api-design.md §2), and a department need not have a manager
    /// (docs/data-model.md §2.2, <b>OQ-3</b>).
    /// </para>
    /// </summary>
    public async Task<PagedResult<DepartmentDto>> GetDepartmentsAsync(PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();

        // The field is discarded, not ignored: the whitelist has exactly one entry, so ParseSort
        // either returns Name or throws a 400 (AP-15). Adding a second entry means adding a branch
        // here — the discard is what makes that omission a compile-time-visible edit rather than a
        // silent one.
        var (_, descending) = page.ParseSort(SortableFields, DefaultSortField);

        var query = db.Departments.AsNoTracking();

        query = descending ? query.OrderByDescending(d => d.Name) : query.OrderBy(d => d.Name);

        return await query
            .Select(d => new DepartmentDto(d.Id, d.Name, d.ManagerUserId))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary><c>GET /branches</c> — rows are <c>{ id, name }</c> (docs/api-design.md §6.2).</summary>
    public async Task<PagedResult<BranchDto>> GetBranchesAsync(PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();
        var (_, descending) = page.ParseSort(SortableFields, DefaultSortField);

        var query = db.Branches.AsNoTracking();

        query = descending ? query.OrderByDescending(b => b.Name) : query.OrderBy(b => b.Name);

        return await query
            .Select(b => new BranchDto(b.Id, b.Name))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }
}

/// <summary>
/// <c>Department</c> — docs/api-design.md §6.2.
/// <para>
/// <c>ManagerUserId</c> is nullable and is <b>omitted</b> from the response when null, because the
/// contract says it "may be absent" rather than "may be null" (§6.2, §2). Nothing downstream may
/// treat its absence as an error — that is OQ-3, and it is open.
/// </para>
/// </summary>
public sealed record DepartmentDto(Guid Id, string Name, Guid? ManagerUserId);

/// <summary>
/// <c>Branch</c> — docs/api-design.md §6.2. Two fields, and that is the whole of it: branch is a
/// reporting and filtering attribute, so there is nothing else a caller legitimately needs (A-2).
/// </summary>
public sealed record BranchDto(Guid Id, string Name);
