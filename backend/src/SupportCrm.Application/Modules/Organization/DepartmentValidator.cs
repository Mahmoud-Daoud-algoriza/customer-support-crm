using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Organization;

/// <summary>
/// The one place the <c>Department.ManagerUserId</c> invariant is checked.
/// <para>
/// docs/data-model.md §2.2: <c>managerUserId</c>, when set, must reference an <b>active</b> user of
/// role <c>Manager</c> or <c>Administrator</c> — and that is an <b>Application-layer</b> rule, not a
/// foreign key. A foreign key can express "a user exists"; it cannot express "active" or "of these
/// two roles", so <c>DepartmentConfiguration</c> deliberately declares none and this class owns the
/// rule instead. The Domain layer cannot own it either: it is a cross-row check, and
/// <c>Department.AssignManager</c> can see only its own row.
/// </para>
/// </summary>
public sealed class DepartmentValidator(IApplicationDbContext db)
{
    /// <summary>
    /// Throws <see cref="ValidationException"/> unless the candidate exists, is active, and holds
    /// role <c>Manager</c> or <c>Administrator</c>.
    /// <para>
    /// Its only caller today is the seeder second pass Story 03 task 3 defers to
    /// <c>IdentitySeeder</c>. It is <b>not</b> reachable from an endpoint, because there is no write
    /// endpoint for a department (T2-I, docs/api-design.md §5.4) — a caller that appoints a manager
    /// would be new surface, not a new use of this method.
    /// </para>
    /// <para>
    /// <b>This method says nothing about a department with no manager.</b> The rule constrains a
    /// value that is <em>set</em>; leaving <c>ManagerUserId</c> null is legal, and what happens on an
    /// SLA breach in that case is <b>OQ-3</b> — open, and not answered here.
    /// </para>
    /// </summary>
    public async Task EnsureManagerIsEligibleAsync(Guid managerUserId, CancellationToken ct)
    {
        // One query, not three: "exists", "is active" and "has an eligible role" are a single
        // condition, and splitting them would let a caller report a reason that leaks whether the
        // id belongs to a real user.
        var eligible = await db.Users.AnyAsync(
            u => u.Id == managerUserId
                 && u.IsActive
                 && (u.Role == UserRole.Manager || u.Role == UserRole.Administrator),
            ct);

        if (!eligible)
        {
            throw new ValidationException(
                "managerUserId must reference an active user of role Manager or Administrator.");
        }
    }
}
