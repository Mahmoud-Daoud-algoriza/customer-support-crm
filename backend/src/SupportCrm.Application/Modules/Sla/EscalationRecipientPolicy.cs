using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// The one implementation of <see cref="IEscalationRecipientPolicy"/> — <b>A-21's cascade, written
/// once</b> (docs/product-scope.md §7, closing OQ-3 on 2026-08-31).
///
/// <list type="number">
///   <item><description>The department's manager, when <c>ManagerUserId</c> is set and that user is still eligible.</description></item>
///   <item><description>Otherwise every active <see cref="UserRole.Manager"/>.</description></item>
///   <item><description>Otherwise every active <see cref="UserRole.Administrator"/>.</description></item>
///   <item><description>Otherwise nobody — and the escalation still happens.</description></item>
/// </list>
///
/// <para>
/// <b>Why the fallback cannot leak across the department boundary.</b> Every rung returns only
/// <c>Manager</c> or <c>Administrator</c> users, and A-4 and A-16 already give both roles authority
/// over <em>all</em> departments. A <c>Notification</c> carries a <c>ticketId</c>
/// (docs/data-model.md §2.12), so a recipient who could not read that ticket would be handed a
/// dangling reference across the boundary AP-4 exists to protect — which is precisely why the
/// cascade climbs and never spreads sideways to agents. <c>OnlyEscalatesToAuthorityRoles</c> in
/// <c>EscalationRecipientPolicyTests</c> asserts this over every rung.
/// </para>
///
/// <para>
/// <b>The Warning lives here, not at the call sites.</b> Story 06's manual escalate and Story 09's
/// automatic sweep both fall back through this method, so logging the fallback in the policy is
/// what makes the two paths unable to diverge — the point of giving A-21 one home at all. The two
/// plans previously specified different levels for this line (Story 06 said <c>Information</c>,
/// Story 09 said <c>Warning</c>); <b>Warning is the standardised level</b>, fixed by the same
/// decision.
/// </para>
/// </summary>
public sealed class EscalationRecipientPolicy(
    IApplicationDbContext db,
    ILogger<EscalationRecipientPolicy> logger) : IEscalationRecipientPolicy
{
    /// <summary>
    /// The two roles A-21 may ever notify. Both hold cross-department authority (A-4, A-16), which
    /// is what makes every rung of the cascade safe to deliver.
    /// </summary>
    private static readonly UserRole[] AuthorityRoles = [UserRole.Manager, UserRole.Administrator];

    public async Task<EscalationRecipients> ResolveAsync(Guid departmentId, CancellationToken ct)
    {
        // Rung 1. The department's own manager.
        //
        // "Set" is not sufficient on its own: docs/data-model.md §2.2 requires the referenced user
        // to be ACTIVE and to hold Manager or Administrator, and AD-15 makes the user row — not the
        // stale reference — authoritative. A manager deactivated after appointment would otherwise
        // swallow the notification, which is the failure A-21 exists to prevent. Reading it as
        // "set AND still eligible" is recorded as finding I-21.
        var departmentManagerId = await db.Departments
            .Where(d => d.Id == departmentId)
            .Select(d => d.ManagerUserId)
            .FirstOrDefaultAsync(ct);

        if (departmentManagerId is { } managerId)
        {
            var eligible = await db.Users.AnyAsync(
                u => u.Id == managerId && u.IsActive && AuthorityRoles.Contains(u.Role), ct);

            if (eligible)
            {
                return new EscalationRecipients(
                    EscalationRecipientTier.DepartmentManager, [managerId]);
            }
        }


        // Rung 2. Every active Manager.
        var managers = await ActiveIdsInRoleAsync(UserRole.Manager, ct);

        if (managers.Count > 0)
        {
            LogFallback(departmentId, EscalationRecipientTier.AllManagers, managers.Count);

            return new EscalationRecipients(EscalationRecipientTier.AllManagers, managers);
        }

        // Rung 3. Every active Administrator.
        var administrators = await ActiveIdsInRoleAsync(UserRole.Administrator, ct);

        if (administrators.Count > 0)
        {
            LogFallback(departmentId, EscalationRecipientTier.AllAdministrators, administrators.Count);

            return new EscalationRecipients(
                EscalationRecipientTier.AllAdministrators, administrators);
        }

        // Rung 4. Nobody — which is not an error, and must not stop the escalation (A-21).
        LogFallback(departmentId, EscalationRecipientTier.None, 0);

        return EscalationRecipients.None;
    }

    /// <summary>
    /// <b>Ordered by email</b> so the recipient list is deterministic. Id order would be stable too,
    /// but <c>Guid</c> collation differs between SQL Server and the SQLite test host, so a test that
    /// asserted an order would pass in one place and fail in the other.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> ActiveIdsInRoleAsync(UserRole role, CancellationToken ct) =>
        await db.Users
            .Where(u => u.IsActive && u.Role == role)
            .OrderBy(u => u.Email)
            .Select(u => u.Id)
            .ToListAsync(ct);

    /// <summary>
    /// One line, one level, one place. <b>Warning</b> is the level A-21 standardises for every rung
    /// below the department manager, so the manual and automatic escalation paths cannot report the
    /// same condition differently.
    /// </summary>
    private void LogFallback(Guid departmentId, EscalationRecipientTier tier, int recipientCount) =>
        logger.LogWarning(
            "Escalation notification fell back to {Tier} for department {DepartmentId}: the "
            + "department has no active, eligible manager. Recipients resolved: {RecipientCount} "
            + "(A-21). The escalation itself is unaffected.",
            tier, departmentId, recipientCount);
}
