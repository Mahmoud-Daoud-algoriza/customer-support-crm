using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Sla;

/// <summary>
/// The world Story 09's tests run against — <b>two departments, one with a manager and one
/// deliberately without</b>, which is what A-21's cascade needs in order to be asserted rather than
/// assumed.
///
/// <para>
/// It is a separate fixture from <c>TicketApiFixture</c> on purpose: these tests create tickets whose
/// deadlines are in the past and then sweep the whole table, so they must not share a host with a
/// class that is counting its own rows.
/// </para>
/// </summary>
public sealed class SlaFixture : IAsyncLifetime
{
    public static readonly Guid BillingDepartmentId = new("11111111-1111-1111-1111-111111111101");

    public static readonly Guid TechnicalDepartmentId = new("11111111-1111-1111-1111-111111111102");

    public SupportCrmApiFactory Factory { get; } = new();

    public Guid BranchId { get; private set; }

    /// <summary>An Agent in Billing.</summary>
    public Guid BillingAgentId { get; private set; }

    /// <summary>A second Agent in Billing — round-robin needs somewhere to rotate to.</summary>
    public Guid BillingAgentTwoId { get; private set; }

    public Guid TechnicalAgentId { get; private set; }

    /// <summary>The Billing department's own manager — A-21 rung 1.</summary>
    public Guid BillingManagerId { get; private set; }

    public Guid CustomerId { get; private set; }

    public async Task InitializeAsync()
    {
        BranchId = await Factory.EnsureBranchAsync("Head Office");

        await EnsureDepartmentAsync(BillingDepartmentId, "Billing");
        await EnsureDepartmentAsync(TechnicalDepartmentId, "Technical");

        BillingAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "sla.billing.one@test.local", departmentId: BillingDepartmentId);

        BillingAgentTwoId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "sla.billing.two@test.local", departmentId: BillingDepartmentId);

        TechnicalAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "sla.technical@test.local", departmentId: TechnicalDepartmentId);

        BillingManagerId = await Factory.AddStaffUserAsync(
            UserRole.Manager, "sla.manager@test.local", departmentId: BillingDepartmentId);

        // Rung 1 of A-21 needs the department to actually name its manager.
        await Factory.WithDbAsync(async db =>
        {
            var billing = await db.Departments.FirstAsync(d => d.Id == BillingDepartmentId);

            billing.AssignManager(BillingManagerId);

            return await db.SaveChangesAsync();
        });

        CustomerId = await Factory.WithDbAsync(async db =>
        {
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.Email == "sla.customer@test.local");

            if (existing is not null)
            {
                return existing.Id;
            }

            var customer = Customer.Create(
                Guid.NewGuid(), "sla.customer@test.local", "SLA Customer", null, BranchId,
                DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a ticket whose SLA clock started <paramref name="createdHoursAgo"/> hours ago, so its
    /// deadlines are real arithmetic on a backdated origin rather than fabricated timestamps — the
    /// same thing the seeder does for its two overdue rows.
    /// </summary>
    public Task<Guid> AddOverdueTicketAsync(
        Guid departmentId,
        TicketPriority priority = TicketPriority.Medium,
        int createdHoursAgo = 240,
        string categoryCode = "billing",
        Guid? assignedTo = null) =>
        Factory.WithDbAsync(async db =>
        {
            var createdAt = DateTimeOffset.UtcNow.AddHours(-createdHoursAgo);

            var (firstResponseDueAt, resolutionDueAt) =
                SlaClock.ComputeAtCreation(createdAt, priority, new SlaTargets(4, 24));

            var ticket = Ticket.Create(
                Guid.NewGuid(), CustomerId, departmentId, "Overdue ticket", "Seeded for the sweep.",
                categoryCode, priority, BillingAgentId, createdAt, firstResponseDueAt, resolutionDueAt);

            if (assignedTo is { } assignee)
            {
                ticket.Assign(assignee);
            }

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return ticket.Id;
        });

    /// <summary>A ticket whose deadlines are comfortably in the future — the sweep must ignore it.</summary>
    public Task<Guid> AddHealthyTicketAsync(Guid departmentId) =>
        Factory.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;

            var ticket = Ticket.Create(
                Guid.NewGuid(), CustomerId, departmentId, "Healthy ticket", "Not due yet.",
                "billing", TicketPriority.Low, BillingAgentId, now,
                now.AddHours(240), now.AddHours(480));

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return ticket.Id;
        });

    /// <summary>Runs one sweep through the factory helper, which mirrors the hosted service.</summary>
    public Task<int> RunSweepAsync() => Factory.RunSlaSweepAsync();

    public Task<Ticket> ReloadAsync(Guid ticketId) =>
        Factory.WithDbAsync(async db =>
            await db.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticketId));

    public Task<List<Notification>> NotificationsForAsync(Guid ticketId) =>
        Factory.WithDbAsync(async db =>
            await db.Notifications.AsNoTracking().Where(n => n.TicketId == ticketId).ToListAsync());

    public Task<List<TicketActivity>> ActivityForAsync(Guid ticketId) =>
        Factory.WithDbAsync(async db =>
            await db.TicketActivities.AsNoTracking().Where(a => a.TicketId == ticketId).ToListAsync());

    /// <summary>Deactivates a user, for the eligibility assertions.</summary>
    public Task DeactivateAsync(Guid userId) =>
        Factory.WithDbAsync(async db =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == userId);

            user.Deactivate();

            return await db.SaveChangesAsync();
        });

    private Task EnsureDepartmentAsync(Guid id, string name) =>
        Factory.WithDbAsync(async db =>
        {
            if (await db.Departments.AnyAsync(d => d.Id == id))
            {
                return 0;
            }

            db.Departments.Add(Department.Create(id, name));

            return await db.SaveChangesAsync();
        });
}
