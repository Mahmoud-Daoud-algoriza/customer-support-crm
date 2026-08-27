using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Organization;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Demo staff users — Story 02 task 10. <c>Order = 20</c>, after
/// <see cref="OrganizationSeeder"/> at 10, because every staff user requires a department that
/// already exists (DM-1).
/// <para>
/// <b>No credential is hardcoded in source.</b> The password comes from
/// <see cref="SeedOptions.DefaultPassword"/>, whose development default lives in
/// <c>appsettings.Development.json</c>; any other environment must supply
/// <c>SupportCrm__Seed__DefaultPassword</c> or startup fails.
/// </para>
/// </summary>
public sealed class IdentitySeeder(
    SupportCrmDbContext db,
    IPasswordHasher<User> passwordHasher,
    IOptions<SeedOptions> seedOptions,
    DepartmentValidator departments,
    TimeProvider clock,
    ILogger<IdentitySeeder> logger) : IDataSeeder
{
    public int Order => 20;

    /// <summary>Deterministic ids, so Stories 04 and 05 can reference a seeded actor.</summary>
    public static class Users
    {
        public static readonly Guid Administrator = new("22222222-2222-2222-2222-222222222201");
        public static readonly Guid Manager = new("22222222-2222-2222-2222-222222222202");
        public static readonly Guid BillingAgent = new("22222222-2222-2222-2222-222222222203");
        public static readonly Guid TechnicalAgent = new("22222222-2222-2222-2222-222222222204");
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var password = seedOptions.Value.DefaultPassword;
        var now = clock.GetUtcNow();

        // One Administrator, one Manager, and two Agents in DIFFERENT departments — so Story 05's
        // department-scoping tests have material, and so branch's non-scoping is demonstrable
        // against agents who share a branch but not a department.
        var toSeed = new[]
        {
            (Users.Administrator, "admin@supportcrm.local", "Amina Rashid",
                UserRole.Administrator, OrganizationSeeder.Departments.Billing, OrganizationSeeder.Branches.HeadOffice),
            (Users.Manager, "manager@supportcrm.local", "Marcus Bell",
                UserRole.Manager, OrganizationSeeder.Departments.Billing, OrganizationSeeder.Branches.HeadOffice),
            (Users.BillingAgent, "billing.agent@supportcrm.local", "Bilal Haddad",
                UserRole.Agent, OrganizationSeeder.Departments.Billing, OrganizationSeeder.Branches.HeadOffice),
            (Users.TechnicalAgent, "tech.agent@supportcrm.local", "Tara Nowak",
                UserRole.Agent, OrganizationSeeder.Departments.Technical, OrganizationSeeder.Branches.North),
        };

        var seeded = 0;

        foreach (var (id, email, displayName, role, departmentId, branchId) in toSeed)
        {
            if (await db.Users.AnyAsync(u => u.Id == id || u.Email == email, ct))
            {
                continue;
            }

            var user = User.CreateStaff(
                id: id,
                email: email,
                passwordHash: UnhashedSentinel,
                displayName: displayName,
                role: role,
                departmentId: departmentId,
                branchId: branchId,
                createdAt: now);

            user.SetPasswordHash(passwordHasher.HashPassword(user, password));

            db.Users.Add(user);
            seeded++;
        }

        if (seeded > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        await AssignDepartmentManagersAsync(ct);

        logger.LogInformation("IdentitySeeder: {Seeded} user(s) added.", seeded);
    }

    /// <summary>
    /// The second pass Story 03 task 3 defers to this seeder: <c>Department.ManagerUserId</c> must
    /// reference an <b>active</b> user of role <c>Manager</c> or <c>Administrator</c>
    /// (docs/data-model.md §2.2), which cannot be satisfied until those users exist.
    /// <para>
    /// <b>Every seeded department is given a manager, and that is a seeding convenience only — it is
    /// not an answer to OQ-3.</b> docs/data-model.md §2.2 says so explicitly: when a department has
    /// no manager, who is notified on SLA breach is undetermined, and this code invents no fallback.
    /// Do not let this seed data become the reason nobody notices the question is still open. It must
    /// be resolved before Story 09.
    /// </para>
    /// </summary>
    private async Task AssignDepartmentManagersAsync(CancellationToken ct)
    {
        var managers = new[]
        {
            (OrganizationSeeder.Departments.Billing, Users.Manager),
            (OrganizationSeeder.Departments.Technical, Users.Manager),
        };

        var assigned = 0;

        foreach (var (departmentId, managerUserId) in managers)
        {
            var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, ct);

            if (department is null || department.ManagerUserId is not null)
            {
                continue;
            }

            // The eligibility rule is an Application-layer rule, not a foreign key, and Story 03
            // task 4's DepartmentValidator is the single place it lives. This seeder does not
            // re-express it: a second copy of the rule here is exactly how the two would drift.
            //
            // The throw is caught rather than allowed to escape. A seeder runs at startup, and an
            // ineligible demo manager must not take the API down — nor may it silently appoint one.
            // The department is left WITHOUT a manager, which is a legal state (docs/data-model.md
            // §2.2), and the warning says so. No fallback recipient is substituted: that is OQ-3,
            // and it is open.
            try
            {
                await departments.EnsureManagerIsEligibleAsync(managerUserId, ct);
            }
            catch (ValidationException)
            {
                logger.LogWarning(
                    "Department {DepartmentId} left without a manager: {UserId} is not an active Manager or Administrator.",
                    departmentId, managerUserId);
                continue;
            }

            department.AssignManager(managerUserId);
            assigned++;
        }

        if (assigned > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("IdentitySeeder: {Assigned} department manager(s) assigned.", assigned);
    }

    /// <summary>
    /// Never persisted — replaced by the real hash on the next line. <c>CreateStaff</c> refuses an
    /// empty hash, so no user can exist without one.
    /// </summary>
    private const string UnhashedSentinel = "!unhashed";
}
