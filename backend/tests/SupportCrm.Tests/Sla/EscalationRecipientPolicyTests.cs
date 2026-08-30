using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Sla;

/// <summary>
/// <b>A-21 — the escalation-recipient cascade, closing OQ-3 (2026-08-31).</b>
///
/// <para>
/// Every rung is asserted, because the whole point of the decision is what happens on the rungs the
/// demo never reaches: every seeded department has a manager, so rungs 2, 3 and 4 are unreachable
/// by hand and would otherwise ship unexercised.
/// </para>
///
/// <para>
/// <b>Each test builds its own host.</b> The rungs are defined by what does <em>not</em> exist —
/// "no active Manager anywhere" is a statement about the whole user table — so a shared fixture
/// would make one test's Manager another test's failure.
/// </para>
/// </summary>
public sealed class EscalationRecipientPolicyTests
{
    private static async Task<Guid> AddDepartmentAsync(
        SupportCrmApiFactory factory, string name, Guid? managerUserId = null) =>
        await factory.WithDbAsync(async db =>
        {
            var department = Department.Create(Guid.NewGuid(), name);

            if (managerUserId is { } manager)
            {
                department.AssignManager(manager);
            }

            db.Departments.Add(department);
            await db.SaveChangesAsync();

            return department.Id;
        });

    private static async Task<EscalationRecipients> ResolveAsync(
        SupportCrmApiFactory factory, Guid departmentId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<IEscalationRecipientPolicy>()
            .ResolveAsync(departmentId, CancellationToken.None);
    }

    /// <summary>Rung 1 — the ordinary path, and the only one the seeded demo exercises.</summary>
    [Fact]
    public async Task A_department_with_a_manager_notifies_exactly_that_manager()
    {
        await using var factory = new SupportCrmApiFactory();

        var manager = await factory.AddStaffUserAsync(UserRole.Manager, "dept.manager@a21.local");

        // A second Manager exists precisely so "exactly that manager" means something: a policy that
        // ignored ManagerUserId and returned every Manager would pass a count-of-one assertion only
        // by accident.
        await factory.AddStaffUserAsync(UserRole.Manager, "other.manager@a21.local");

        var departmentId = await AddDepartmentAsync(factory, "Has Manager", manager);

        var recipients = await ResolveAsync(factory, departmentId);

        Assert.Equal(EscalationRecipientTier.DepartmentManager, recipients.Tier);
        Assert.False(recipients.IsFallback);
        Assert.Equal([manager], recipients.UserIds);
    }

    /// <summary>Rung 2 — the branch OQ-3 was actually about.</summary>
    [Fact]
    public async Task A_department_with_no_manager_notifies_every_active_manager()
    {
        await using var factory = new SupportCrmApiFactory();

        var first = await factory.AddStaffUserAsync(UserRole.Manager, "a.manager@a21.local");
        var second = await factory.AddStaffUserAsync(UserRole.Manager, "b.manager@a21.local");

        // Neither of these may be notified: an Agent has no cross-department authority, and an
        // inactive Manager cannot act on what they are told.
        await factory.AddStaffUserAsync(UserRole.Agent, "agent@a21.local");
        await DeactivateAsync(factory, await factory.AddStaffUserAsync(
            UserRole.Manager, "inactive.manager@a21.local"));

        var departmentId = await AddDepartmentAsync(factory, "No Manager");

        var recipients = await ResolveAsync(factory, departmentId);

        Assert.Equal(EscalationRecipientTier.AllManagers, recipients.Tier);
        Assert.True(recipients.IsFallback);

        // Ordered by email, so the assertion is on a sequence rather than a set (determinism).
        Assert.Equal([first, second], recipients.UserIds);
    }

    /// <summary>Rung 3 — no Manager exists at all, so authority climbs to the Administrators.</summary>
    [Fact]
    public async Task With_no_active_manager_anywhere_it_notifies_every_active_administrator()
    {
        await using var factory = new SupportCrmApiFactory();

        var administrator = await factory.AddStaffUserAsync(
            UserRole.Administrator, "admin@a21.local");

        await factory.AddStaffUserAsync(UserRole.Agent, "agent@a21.local");

        var departmentId = await AddDepartmentAsync(factory, "No Manager Anywhere");

        var recipients = await ResolveAsync(factory, departmentId);

        Assert.Equal(EscalationRecipientTier.AllAdministrators, recipients.Tier);
        Assert.Equal([administrator], recipients.UserIds);
    }

    /// <summary>
    /// Rung 4 — nobody eligible exists. <b>This is not an error</b>: A-21 and docs/data-model.md
    /// §2.2 both require the escalation itself to proceed, so the policy resolves an empty list
    /// rather than throwing. A policy that threw here would take the priority raise down with it.
    /// </summary>
    [Fact]
    public async Task With_no_eligible_recipient_at_all_it_resolves_nobody_without_throwing()
    {
        await using var factory = new SupportCrmApiFactory();

        await factory.AddStaffUserAsync(UserRole.Agent, "only.agent@a21.local");

        var departmentId = await AddDepartmentAsync(factory, "Nobody Eligible");

        var recipients = await ResolveAsync(factory, departmentId);

        Assert.Equal(EscalationRecipientTier.None, recipients.Tier);
        Assert.Empty(recipients.UserIds);
    }

    /// <summary>
    /// <b>A manager who is no longer usable does not swallow the notification.</b>
    /// docs/data-model.md §2.2 requires the referenced user to be active and to hold Manager or
    /// Administrator, and AD-15 makes the user row authoritative over a stale reference — so a
    /// department whose manager was deactivated after appointment falls through to rung 2 rather
    /// than notifying an account that cannot act. Recorded as finding <b>I-21</b>.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_ineligible_department_manager_falls_through_to_the_next_rung(
        bool deactivated)
    {
        await using var factory = new SupportCrmApiFactory();

        // Either deactivated, or demoted out of the two roles §2.2 admits.
        var appointee = deactivated
            ? await factory.AddStaffUserAsync(UserRole.Manager, "stale.manager@a21.local")
            : await factory.AddStaffUserAsync(UserRole.Agent, "demoted.manager@a21.local");

        if (deactivated)
        {
            await DeactivateAsync(factory, appointee);
        }

        var standby = await factory.AddStaffUserAsync(UserRole.Manager, "standby@a21.local");

        var departmentId = await AddDepartmentAsync(factory, "Stale Manager", appointee);

        var recipients = await ResolveAsync(factory, departmentId);

        Assert.Equal(EscalationRecipientTier.AllManagers, recipients.Tier);
        Assert.Equal([standby], recipients.UserIds);
        Assert.DoesNotContain(appointee, recipients.UserIds);
    }

    /// <summary>
    /// <b>The authorization guarantee of A-21, asserted over every rung at once.</b> A
    /// <c>Notification</c> carries a <c>ticketId</c>, so a recipient who cannot read that ticket
    /// would be handed a dangling reference across the department boundary AP-4 protects. Only
    /// <c>Manager</c> and <c>Administrator</c> hold cross-department authority (A-4, A-16), so this
    /// test is what would fail if the cascade were ever widened sideways to agents.
    /// </summary>
    [Fact]
    public async Task It_only_ever_escalates_to_roles_with_cross_department_authority()
    {
        await using var factory = new SupportCrmApiFactory();

        var agent = await factory.AddStaffUserAsync(UserRole.Agent, "agent@a21.local");
        var customer = await factory.AddCustomerRoleUserAsync("customer@a21.local");

        var manager = await factory.AddStaffUserAsync(UserRole.Manager, "manager@a21.local");
        var administrator = await factory.AddStaffUserAsync(
            UserRole.Administrator, "admin@a21.local");

        var withManager = await AddDepartmentAsync(factory, "With Manager", manager);
        var withoutManager = await AddDepartmentAsync(factory, "Without Manager");

        var eligible = new[] { manager, administrator };

        foreach (var departmentId in new[] { withManager, withoutManager })
        {
            var recipients = await ResolveAsync(factory, departmentId);

            Assert.NotEmpty(recipients.UserIds);
            Assert.DoesNotContain(agent, recipients.UserIds);
            Assert.DoesNotContain(customer, recipients.UserIds);
            Assert.All(recipients.UserIds, id => Assert.Contains(id, eligible));
        }
    }

    /// <summary>
    /// <b>Deterministic, asserted by repetition rather than by reading the implementation.</b> The
    /// notification rows Story 09 writes are only assertable if the recipient list does not vary
    /// between calls.
    /// </summary>
    [Fact]
    public async Task The_recipient_order_is_stable_across_calls()
    {
        await using var factory = new SupportCrmApiFactory();

        foreach (var name in new[] { "c", "a", "d", "b" })
        {
            await factory.AddStaffUserAsync(UserRole.Manager, $"{name}.manager@a21.local");
        }

        var departmentId = await AddDepartmentAsync(factory, "Ordering");

        var first = await ResolveAsync(factory, departmentId);
        var second = await ResolveAsync(factory, departmentId);

        Assert.Equal(first.UserIds, second.UserIds);
        Assert.Equal(4, first.UserIds.Count);
    }

    private static Task DeactivateAsync(SupportCrmApiFactory factory, Guid userId) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FindAsync(userId)
                ?? throw new InvalidOperationException($"User {userId} was not created.");

            user.Deactivate();
            await db.SaveChangesAsync();

            return user.Id;
        });
}
