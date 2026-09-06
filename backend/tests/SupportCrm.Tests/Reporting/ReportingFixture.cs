using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Reporting;

/// <summary>
/// The world Story 15's dashboard tests run against — <b>two departments, two branches, and
/// customers deliberately placed in the branch that does NOT match their agent's</b>.
///
/// <para>
/// That last arrangement is the point of the fixture, not an incidental detail: api-design §4.4
/// says a ticket's branch is its <b>customer's</b> branch, and the only way to prove the filter
/// resolves through the customer rather than through the assigned agent is to make the two differ
/// and watch which one the filter follows (the plan's test 4).
/// </para>
///
/// <para>
/// <b>The test host runs no seeders</b>, so every row here is created explicitly and the counts the
/// metric tests assert are exactly what this fixture put in — never a seeded number that could drift.
/// </para>
/// </summary>
public sealed class ReportingFixture : IAsyncLifetime
{
    /// <summary>The ids appsettings' category map resolves to, as the ticket fixtures use.</summary>
    public static readonly Guid BillingDepartmentId = new("11111111-1111-1111-1111-111111111101");

    public static readonly Guid TechnicalDepartmentId = new("11111111-1111-1111-1111-111111111102");

    public SupportCrmApiFactory Factory { get; } = new();

    public Guid HeadOfficeBranchId { get; private set; }

    public Guid NorthBranchId { get; private set; }

    public Guid BillingAgentId { get; private set; }

    public Guid TechnicalAgentId { get; private set; }

    /// <summary>An active agent who is assigned nothing and has resolved nothing — the null-average row.</summary>
    public Guid IdleAgentId { get; private set; }

    public Guid ManagerId { get; private set; }

    public Guid AdministratorId { get; private set; }

    public Guid CustomerUserId { get; private set; }

    /// <summary>A Head Office customer whose tickets are worked by the Billing agent.</summary>
    public Guid HeadOfficeCustomerId { get; private set; }

    /// <summary>A North Branch customer — the cross-branch case test 4 needs.</summary>
    public Guid NorthCustomerId { get; private set; }

    public async Task InitializeAsync()
    {
        HeadOfficeBranchId = await Factory.EnsureBranchAsync("Reporting Head Office");
        NorthBranchId = await Factory.EnsureBranchAsync("Reporting North");

        await EnsureDepartmentWithIdAsync(BillingDepartmentId, "Billing");
        await EnsureDepartmentWithIdAsync(TechnicalDepartmentId, "Technical");

        BillingAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "billing.agent@reporting.local", departmentId: BillingDepartmentId);

        TechnicalAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "technical.agent@reporting.local", departmentId: TechnicalDepartmentId);

        IdleAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "idle.agent@reporting.local", departmentId: BillingDepartmentId);

        ManagerId = await Factory.AddStaffUserAsync(
            UserRole.Manager, "manager@reporting.local", departmentId: BillingDepartmentId);

        AdministratorId = await Factory.AddStaffUserAsync(
            UserRole.Administrator, "admin@reporting.local", departmentId: BillingDepartmentId);

        HeadOfficeCustomerId = await AddCustomerAsync("headoffice@reporting.local", HeadOfficeBranchId);
        NorthCustomerId = await AddCustomerAsync("north@reporting.local", NorthBranchId);

        CustomerUserId = await Factory.AddPortalUserAsync(
            HeadOfficeCustomerId, "headoffice@reporting.local");
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a ticket directly, to establish a precondition. Resolution and breach are set through
    /// the entity's own methods, so a fixture row is indistinguishable from one an endpoint produced.
    /// </summary>
    public Task<Guid> AddTicketAsync(
        Guid departmentId,
        Guid customerId,
        Guid createdBy,
        TicketPriority priority = TicketPriority.Medium,
        string categoryCode = "billing",
        Guid? assignedTo = null,
        TicketStatus? status = null,
        double? resolvedAfterHours = null,
        bool breached = false) =>
        Factory.WithDbAsync(async db =>
        {
            var createdAt = DateTimeOffset.UtcNow.AddDays(-7);
            var (firstResponseDueAt, resolutionDueAt) =
                SlaClock.ComputeAtCreation(createdAt, priority, new SlaTargets(4, 24));

            var ticket = Ticket.Create(
                Guid.NewGuid(), customerId, departmentId, "Reporting fixture ticket",
                "Seeded for a dashboard assertion.", categoryCode, priority, createdBy,
                createdAt, firstResponseDueAt, resolutionDueAt);

            if (assignedTo is { } assignee)
            {
                ticket.Assign(assignee);
            }

            // Walk the real A-5 graph rather than writing the column, so ResolvedAt is stamped by
            // the entity exactly as a transition endpoint stamps it.
            if (resolvedAfterHours is { } hours)
            {
                ticket.TransitionTo(TicketStatus.Open, createdAt.AddMinutes(5));
                ticket.TransitionTo(TicketStatus.Resolved, createdAt.AddHours(hours));
            }

            if (status is { } target && ticket.Status != target)
            {
                ticket.TransitionTo(target, DateTimeOffset.UtcNow);
            }

            if (breached)
            {
                ticket.MarkResolutionBreached();
            }

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return ticket.Id;
        });

    public Task AddFeedbackAsync(Guid ticketId, int rating) =>
        Factory.WithDbAsync(async db =>
        {
            db.CustomerFeedback.Add(CustomerFeedback.Submit(
                Guid.NewGuid(), ticketId, rating, null, DateTimeOffset.UtcNow));

            return await db.SaveChangesAsync();
        });

    private Task<Guid> AddCustomerAsync(string email, Guid branchId) =>
        Factory.WithDbAsync(async db =>
        {
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);

            if (existing is not null)
            {
                return existing.Id;
            }

            var customer = Customer.Create(
                Guid.NewGuid(), email, email, null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

    private Task EnsureDepartmentWithIdAsync(Guid id, string name) =>
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
