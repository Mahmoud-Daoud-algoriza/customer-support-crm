using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SupportCrm.Application.Modules.Ai;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Ai;

/// <summary>
/// <b>T1-F's degradation requirement, tested at the seam rather than at the UI</b>: <em>"a provider
/// failure or timeout surfaces as an unavailable AI feature; ticket creation, replies and status
/// changes all continue."</em>
///
/// <para>
/// The whole AI implementation here is a service that throws on every call. If support work still
/// completes, then nothing on the critical path depends on the AI seam — which is the claim, and it is
/// the kind of claim that quietly stops being true the first time someone awaits a summary inside a
/// creation path.
/// </para>
///
/// <para>
/// <b>Story 10 publishes no endpoint</b>, so there is deliberately no assertion here about a
/// <c>503</c>: that arrives with Story 11's three endpoints (api-design §5.8, AP-12).
/// </para>
/// </summary>
public sealed class AiOutageDoesNotBlockWorkTests : IAsyncLifetime
{
    private static readonly Guid BillingDepartmentId = new("11111111-1111-1111-1111-111111111101");

    private readonly SupportCrmApiFactory _factory = new()
    {
        // The seam is replaced wholesale with one that fails. Registered last, so it wins over the
        // composition root's fake.
        ServiceOverrides = services =>
        {
            services.RemoveAll<IAiAssistService>();
            services.AddScoped<IAiAssistService, AlwaysFailingAiService>();
        },
    };

    private Guid _agentId;
    private Guid _customerId;
    private Guid _portalUserId;

    public async Task InitializeAsync()
    {
        var branchId = await _factory.EnsureBranchAsync("Head Office");

        await _factory.WithDbAsync(async db =>
        {
            if (!await Task.FromResult(db.Departments.Any(d => d.Id == BillingDepartmentId)))
            {
                db.Departments.Add(
                    SupportCrm.Domain.Modules.Organization.Department.Create(BillingDepartmentId, "Billing"));
            }

            return await db.SaveChangesAsync();
        });

        _agentId = await _factory.AddStaffUserAsync(
            UserRole.Agent, "ai.outage.agent@test.local", departmentId: BillingDepartmentId);

        _customerId = await _factory.WithDbAsync(async db =>
        {
            var customer = SupportCrm.Domain.Modules.Customers.Customer.Create(
                Guid.NewGuid(), "ai.outage.customer@test.local", "AI Outage Customer", null, branchId,
                DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        _portalUserId = await _factory.AddPortalUserAsync(_customerId, "ai.outage.customer@test.local");
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();

        return Task.CompletedTask;
    }

    /// <summary>
    /// <b>The whole critical path, with the AI seam failing on every call</b>: create, reply,
    /// transition. All three succeed.
    /// </summary>
    [Fact]
    public async Task Support_work_continues_while_every_ai_call_fails()
    {
        var agent = _factory.CreateClientFor(_agentId);

        // 1. Creation — the path §7.3's categorization would sit on.
        var created = await agent.PostAsJsonAsync("/api/v1/tickets", new
        {
            customerId = _customerId,
            subject = "Created during an AI outage",
            description = "The AI seam throws on every call for the duration of this test.",
            categoryCode = "billing",
            priority = nameof(TicketPriority.Medium),
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var ticketId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        // 2. A staff reply — the path §7.2's suggested reply would sit on.
        var reply = await agent.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/messages", new { body = "Replying with no AI available." });

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        // 3. A status change — nothing about the lifecycle should touch the seam at all.
        var transition = await agent.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/transition", new { targetStatus = nameof(TicketStatus.Open) });

        Assert.Equal(HttpStatusCode.OK, transition.StatusCode);

        // And the ticket is in the state those three calls describe — not merely "no exception".
        var after = await agent.GetFromJsonAsync<JsonElement>($"/api/v1/tickets/{ticketId}");

        Assert.Equal(nameof(TicketStatus.Open), after.GetProperty("status").GetString());
    }

    /// <summary>
    /// A customer's own path is equally unaffected — portal submission and reply, with the seam
    /// failing throughout.
    /// </summary>
    [Fact]
    public async Task The_portal_still_submits_and_replies_while_every_ai_call_fails()
    {
        var customer = _factory.CreateClientFor(_portalUserId);

        var submitted = await customer.PostAsJsonAsync("/api/v1/portal/tickets", new
        {
            subject = "Submitted during an AI outage",
            description = "A customer should never learn that the AI seam is down.",
            categoryCode = "billing",
            isUrgent = false,
        });

        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);

        var ticketId = (await submitted.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetGuid();

        var reply = await customer.PostAsJsonAsync(
            $"/api/v1/portal/tickets/{ticketId}/messages", new { body = "Adding a detail." });

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);
    }

    /// <summary>
    /// The double really does fail — without this, both tests above would pass against a seam that
    /// was quietly working.
    /// </summary>
    [Fact]
    public async Task The_failing_seam_is_genuinely_failing()
    {
        using var scope = _factory.Services.CreateScope();

        var seam = scope.ServiceProvider.GetRequiredService<IAiAssistService>();

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => seam.SummarizeThreadAsync(new AiThreadContext("s", "d", [], false), default));
    }

    /// <summary>An <see cref="IAiAssistService"/> whose every capability is unavailable.</summary>
    private sealed class AlwaysFailingAiService : IAiAssistService
    {
        public Task<AiSummary> SummarizeThreadAsync(AiThreadContext context, CancellationToken ct) =>
            throw new AiUnavailableException();

        public Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct) =>
            throw new AiUnavailableException();

        public Task<AiClassification> SuggestClassificationAsync(
            AiClassificationRequest request, CancellationToken ct) =>
            throw new AiUnavailableException();
    }
}
