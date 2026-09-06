using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Modules.Integrations;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Organization;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Integrations;

/// <summary>
/// The world Story 18's seam tests run in: an ordinary host, one ticket to record against — and
/// <b>a poisoned network</b>.
///
/// <h3>Every outbound HTTP request in this host throws</h3>
/// <see cref="FailingHttpMessageHandler"/> is installed as the primary handler for <b>every</b>
/// named and typed <c>HttpClient</c> the composition root creates. So the whole seam suite runs
/// against a host that cannot reach anything, and *"attempts no external call"* stops being a
/// property one test remembered to check and becomes a property the suite cannot pass without.
///
/// <para>
/// This is the behavioural half of the claim. The structural half — that the adapter's constructor
/// takes no HTTP dependency at all, so there is nothing for it to call <em>with</em> — is asserted
/// by reflection in <see cref="ChannelSeamTests"/>. Neither alone is enough: a handler can be
/// bypassed by a raw <c>new HttpClient()</c>, and a constructor check says nothing about a static.
/// </para>
///
/// <h3>The ticket is a precondition, never the thing under test</h3>
/// It is created directly, with the real <see cref="SlaClock"/> arithmetic, exactly as
/// <c>TicketApiFixture</c> does — so a seeded row and a created row carry the same timestamps.
/// </summary>
public sealed class IntegrationSeamFixture : IAsyncLifetime
{
    /// <summary>The id appsettings' category map resolves "billing" to (OrganizationSeeder, Story 03).</summary>
    private static readonly Guid BillingDepartmentId = new("11111111-1111-1111-1111-111111111101");

    public SupportCrmApiFactory Factory { get; } = new()
    {
        ServiceOverrides = services => services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(
                builder => builder.PrimaryHandler = new FailingHttpMessageHandler())),
    };

    /// <summary>An Agent in Billing — the caller an outbound send is triggered by.</summary>
    public Guid AgentId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid TicketId { get; private set; }

    public async Task InitializeAsync()
    {
        var branchId = await Factory.EnsureBranchAsync("Seam Branch");

        await Factory.WithDbAsync(async db =>
        {
            if (!await db.Departments.AnyAsync(d => d.Id == BillingDepartmentId))
            {
                db.Departments.Add(Department.Create(BillingDepartmentId, "Billing"));
            }

            return await db.SaveChangesAsync();
        });

        AgentId = await Factory.AddStaffUserAsync(
            UserRole.Agent, "seam.agent@integrations.local", departmentId: BillingDepartmentId);

        CustomerId = await Factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), "seam.customer@integrations.local", "Seam Customer",
                null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        TicketId = await AddTicketAsync();
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Calls the adapter <b>in-process</b>, as production would: it has no HTTP route and must never
    /// get one (<b>AP-11</b>).
    ///
    /// <para>
    /// The staff identity is installed on the scope the same way Story 09's SLA sweep installs the
    /// system identity. An outbound send is triggered by a staff caller, so the direction
    /// <c>TicketMessageService.PostAsync</c> derives from the role is <c>Outbound</c> (PF-7) — the
    /// adapter never passes a direction, which is the point.
    /// </para>
    /// </summary>
    public async Task<OutboundDeliveryResult> SendAsync(OutboundMessage message)
    {
        using var scope = Factory.Services.CreateScope();

        await InstallAgentIdentityAsync(scope.ServiceProvider);

        return await scope.ServiceProvider
            .GetRequiredService<IOutboundChannelAdapter>()
            .SendAsync(message, CancellationToken.None);
    }

    /// <summary>Resolves a seam service on a scope carrying the agent identity.</summary>
    public async Task<T> WithAgentScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();

        await InstallAgentIdentityAsync(scope.ServiceProvider);

        return await work(scope.ServiceProvider);
    }

    /// <summary>A second ticket, when a test must not see another's messages.</summary>
    public Task<Guid> AddTicketAsync() => Factory.WithDbAsync(async db =>
    {
        var now = DateTimeOffset.UtcNow;
        var (firstResponseDueAt, resolutionDueAt) =
            SlaClock.ComputeAtCreation(now, TicketPriority.Medium, new SlaTargets(4, 24));

        var ticket = Ticket.Create(
            Guid.NewGuid(), CustomerId, BillingDepartmentId, "Seam ticket", "Seeded description.",
            "billing", TicketPriority.Medium, AgentId, now, firstResponseDueAt, resolutionDueAt);

        ticket.Assign(AgentId);

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        return ticket.Id;
    });

    private async Task InstallAgentIdentityAsync(IServiceProvider provider)
    {
        var agent = await Factory.WithDbAsync(db => db.Users.FirstAsync(u => u.Id == AgentId));

        provider.GetRequiredService<CurrentUserAccessor>()
            .Set(agent.Id, agent.Email, agent.DisplayName, agent.Role, agent.DepartmentId, agent.CustomerId);
    }
}

/// <summary>
/// Fails every request it is given, loudly. Installed as the primary handler for every
/// <c>HttpClient</c> in <see cref="IntegrationSeamFixture"/>'s host.
/// </summary>
public sealed class FailingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(
            $"An external call was attempted to '{request.RequestUri}'. The Story 18 seams must " +
            "attempt none: the console channel adapter and the no-op ERP gateway reach nothing " +
            "(docs/architecture.md §5.2, §5.3).");
}
