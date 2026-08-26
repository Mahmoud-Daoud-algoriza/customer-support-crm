using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Organization;

namespace SupportCrm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Demo departments and branches.
/// <para>
/// <b>This is Story 03 task 3, executed early because Story 02 task 10 cannot run without it.</b>
/// Story 03's plan places tasks 1–3 before Story 02's user administration for exactly this reason:
/// a seeded staff user requires a department that already exists. Story 03 is otherwise not started
/// — no <c>OrganizationQueryService</c>, no controllers, no endpoints.
/// </para>
/// <c>Order = 10</c> — the first seeder to run (00-implementation-plan.md §5).
/// </summary>
public sealed class OrganizationSeeder(
    SupportCrmDbContext db,
    ILogger<OrganizationSeeder> logger) : IDataSeeder
{
    public int Order => 10;

    /// <summary>
    /// Deterministic ids, so later seeders (Stories 02, 04, 05) reference a department or branch
    /// without a lookup, and so re-running against an existing volume is idempotent.
    /// </summary>
    public static class Departments
    {
        public static readonly Guid Billing = new("11111111-1111-1111-1111-111111111101");
        public static readonly Guid Technical = new("11111111-1111-1111-1111-111111111102");
    }

    /// <inheritdoc cref="Departments"/>
    public static class Branches
    {
        public static readonly Guid HeadOffice = new("11111111-1111-1111-1111-111111111201");
        public static readonly Guid North = new("11111111-1111-1111-1111-111111111202");
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        // Two departments, so department scoping is demonstrable in Story 05 — and two branches, so
        // branch's *non*-scoping is demonstrable at the same time (A-2).
        var departments = new[]
        {
            (Departments.Billing, "Billing"),
            (Departments.Technical, "Technical"),
        };

        var branches = new[]
        {
            (Branches.HeadOffice, "Head Office"),
            (Branches.North, "North Branch"),
        };

        var seeded = 0;

        foreach (var (id, name) in departments)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == id, ct))
            {
                // No manager is assigned here. Manager assignment is a second pass run by
                // IdentitySeeder once Manager users exist, because Department.ManagerUserId must
                // reference an active Manager or Administrator (docs/data-model.md §2.2).
                db.Departments.Add(Department.Create(id, name));
                seeded++;
            }
        }

        foreach (var (id, name) in branches)
        {
            if (!await db.Branches.AnyAsync(b => b.Id == id, ct))
            {
                db.Branches.Add(Branch.Create(id, name));
                seeded++;
            }
        }

        if (seeded > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("OrganizationSeeder: {Seeded} row(s) added.", seeded);
    }
}
