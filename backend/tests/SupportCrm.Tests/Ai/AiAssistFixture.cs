using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Ai;

/// <summary>
/// The world Story 11's endpoint tests run against: <b>two departments</b>, an Agent in one, and a
/// customer with a portal login.
///
/// <para>
/// It takes an optional service override so the classes that replace the AI seam — a recorder, a rogue
/// classifier, a thrower — each get their own host. A shared host could not serve them: replacing a
/// singleton per test class is the only way to assert three different seam behaviours.
/// </para>
/// </summary>
public sealed class AiAssistFixture : IAsyncLifetime
{
    public static readonly Guid BillingDepartmentId = new("11111111-1111-1111-1111-111111111101");

    public static readonly Guid TechnicalDepartmentId = new("11111111-1111-1111-1111-111111111102");

    public AiAssistFixture()
        : this(serviceOverrides: null)
    {
    }

    /// <summary>
    /// <b><c>internal</c>, not public, and that is required rather than stylistic</b>: xUnit refuses a
    /// class fixture with more than one <em>public</em> constructor, and this type is used both as an
    /// <c>IClassFixture</c> (parameterless) and constructed directly by the classes that replace the AI
    /// seam. Internal keeps both uses available from this assembly.
    /// </summary>
    internal AiAssistFixture(Action<IServiceCollection>? serviceOverrides) =>
        Factory = new SupportCrmApiFactory { ServiceOverrides = serviceOverrides };

    public SupportCrmApiFactory Factory { get; }

    /// <summary>An Agent in Billing — the caller most assertions are made as.</summary>
    public Guid AgentId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid PortalUserId { get; private set; }

    public async Task InitializeAsync()
    {
        var branchId = await Factory.EnsureBranchAsync("Head Office");

        await EnsureDepartmentAsync(BillingDepartmentId, "Billing");
        await EnsureDepartmentAsync(TechnicalDepartmentId, "Technical");

        AgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, $"ai.agent.{Guid.NewGuid():N}@test.local",
            departmentId: BillingDepartmentId);

        CustomerId = await Factory.WithDbAsync(async db =>
        {
            var email = "ai.customer@test.local";

            var existing = await db.Customers.FirstOrDefaultAsync(c => c.Email == email);

            if (existing is not null)
            {
                return existing.Id;
            }

            var customer = Customer.Create(
                Guid.NewGuid(), email, "AI Customer", null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        PortalUserId = await Factory.AddPortalUserAsync(CustomerId, "ai.customer@test.local");
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// A ticket with a two-message thread, so a summary has something to summarize. Created directly
    /// to establish a precondition — never to assert behaviour an endpoint should be proving.
    /// </summary>
    public Task<Guid> AddTicketAsync(bool otherDepartment = false) =>
        Factory.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;

            var (firstResponseDueAt, resolutionDueAt) =
                SlaClock.ComputeAtCreation(now, TicketPriority.Medium, new SlaTargets(4, 24));

            var ticket = Ticket.Create(
                Guid.NewGuid(),
                CustomerId,
                otherDepartment ? TechnicalDepartmentId : BillingDepartmentId,
                "Card declined at checkout every time",
                "Every payment attempt is declined at the final step. The customer has tried two cards.",
                otherDepartment ? "technical" : "billing",
                TicketPriority.Medium,
                AgentId,
                now,
                firstResponseDueAt,
                resolutionDueAt);

            db.Tickets.Add(ticket);

            // The author is a USER id on both sides — the customer's portal login, not the customer
            // profile (DM-1). `Post` refuses an empty one, which is what caught this.
            db.TicketMessages.Add(TicketMessage.Post(
                Guid.NewGuid(), ticket.Id, PortalUserId, MessageDirection.Inbound,
                MessageChannel.Portal, "It is still failing on both cards.", now.AddSeconds(10)));

            db.TicketMessages.Add(TicketMessage.Post(
                Guid.NewGuid(), ticket.Id, AgentId, MessageDirection.Outbound,
                MessageChannel.Portal, "Thanks — I am looking into this now.", now.AddSeconds(20)));

            await db.SaveChangesAsync();

            return ticket.Id;
        });

    /// <summary>
    /// Every field an AI call could plausibly disturb, as one comparable value. A record's structural
    /// equality is what makes the before/after assertion a single line.
    /// </summary>
    public Task<TicketSnapshot> SnapshotAsync(Guid ticketId) =>
        Factory.WithDbAsync(async db =>
        {
            var t = await db.Tickets.AsNoTracking().FirstAsync(x => x.Id == ticketId);

            return new TicketSnapshot(
                t.Subject, t.Description, t.CategoryCode, t.Priority, t.Status, t.AssignedUserId,
                t.IsUrgent, t.FirstRespondedAt, t.ResolvedAt, t.ClosedAt,
                t.FirstResponseBreached, t.ResolutionBreached,
                t.FirstResponseDueAt, t.ResolutionDueAt);
        });

    /// <summary>The configured category codes, read from the running host's own configuration.</summary>
    public Task<List<string>> ConfiguredCategoryCodesAsync()
    {
        var options = Factory.Services.GetRequiredService<IOptions<CategoryOptions>>();

        return Task.FromResult(options.Value.Items.Select(c => c.Code).ToList());
    }

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

/// <summary>Every mutable ticket field, for the "no assist changes anything" assertion.</summary>
public sealed record TicketSnapshot(
    string Subject,
    string Description,
    string CategoryCode,
    TicketPriority Priority,
    TicketStatus Status,
    Guid? AssignedUserId,
    bool IsUrgent,
    DateTimeOffset? FirstRespondedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    bool FirstResponseBreached,
    bool ResolutionBreached,
    DateTimeOffset FirstResponseDueAt,
    DateTimeOffset ResolutionDueAt);
