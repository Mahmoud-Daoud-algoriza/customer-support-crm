using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// The world Story 05's API tests run against: <b>two departments, two branches, an agent in each
/// department, and customers deliberately split across branches</b>.
///
/// <para>
/// The department ids are <b>the ones the configured category → department map already points at</b>
/// (appsettings <c>SupportCrm:Categories</c>), not fresh GUIDs. The test host runs no seeders, so
/// the rows have to exist for A-14's derivation to resolve — and using the configured ids means the
/// tests exercise the real map rather than a substituted one.
/// </para>
///
/// <para>
/// <b>Customers sit in a different branch from the agent who works their tickets, on purpose.</b>
/// That is the arrangement <c>BranchIsNotABoundaryTests</c> needs (A-2, docs/data-model.md §5
/// constraint 6): an agent must reach an in-department ticket regardless of the customer's branch.
/// </para>
/// </summary>
public sealed class TicketApiFixture : IAsyncLifetime
{
    /// <summary>The ids appsettings' category map resolves to (OrganizationSeeder's, Story 03).</summary>
    public static readonly Guid BillingDepartmentId = new("11111111-1111-1111-1111-111111111101");

    public static readonly Guid TechnicalDepartmentId = new("11111111-1111-1111-1111-111111111102");

    public SupportCrmApiFactory Factory { get; } = new();

    public Guid HeadOfficeBranchId { get; private set; }

    public Guid NorthBranchId { get; private set; }

    /// <summary>An Agent in Billing — the caller most of the scoping assertions are made as.</summary>
    public Guid BillingAgentId { get; private set; }

    public Guid TechnicalAgentId { get; private set; }

    public Guid ManagerId { get; private set; }

    /// <summary>Head Office.</summary>
    public Guid HeadOfficeCustomerId { get; private set; }

    /// <summary>North Branch — the cross-branch customer.</summary>
    public Guid NorthCustomerId { get; private set; }

    public async Task InitializeAsync()
    {
        HeadOfficeBranchId = await Factory.EnsureBranchAsync("Head Office");
        NorthBranchId = await Factory.EnsureBranchAsync("North Branch");

        await EnsureDepartmentWithIdAsync(BillingDepartmentId, "Billing");
        await EnsureDepartmentWithIdAsync(TechnicalDepartmentId, "Technical");

        BillingAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "billing.agent@tickets.local", departmentId: BillingDepartmentId);

        TechnicalAgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "technical.agent@tickets.local", departmentId: TechnicalDepartmentId);

        // A Manager's department is irrelevant to what they may see — A-4 gives them every
        // department — so giving them one proves the rule is about role, not about matching ids.
        ManagerId = await Factory.AddStaffUserAsync(
            UserRole.Manager, "manager@tickets.local", departmentId: BillingDepartmentId);

        HeadOfficeCustomerId = await AddCustomerAsync("head.office@tickets.local", HeadOfficeBranchId);
        NorthCustomerId = await AddCustomerAsync("north@tickets.local", NorthBranchId);
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a ticket directly, to establish a precondition — never to assert behaviour an
    /// endpoint should be proving. The due timestamps come from the real <see cref="SlaClock"/>, so
    /// a seeded row and a created row carry the same arithmetic.
    /// </summary>
    public Task<Guid> AddTicketAsync(
        Guid departmentId,
        Guid customerId,
        Guid createdBy,
        TicketPriority priority = TicketPriority.Medium,
        string categoryCode = "billing",
        Guid? assignedTo = null,
        string subject = "Seeded ticket") =>
        Factory.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            var (firstResponseDueAt, resolutionDueAt) =
                SlaClock.ComputeAtCreation(now, priority, new SlaTargets(4, 24));

            var ticket = Ticket.Create(
                Guid.NewGuid(), customerId, departmentId, subject, "Seeded description.",
                categoryCode, priority, createdBy, now, firstResponseDueAt, resolutionDueAt);

            if (assignedTo is { } assignee)
            {
                ticket.Assign(assignee);
            }

            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();

            return ticket.Id;
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

    /// <summary>
    /// <see cref="SupportCrmApiFactory.EnsureDepartmentAsync"/> allocates a fresh id; the category
    /// map needs these two specific ones, so the row is created with the configured id.
    /// </summary>
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
