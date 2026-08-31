using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>A-16 authority and A-5 legality, kept apart and proven apart</b> — <c>403</c> for a caller who
/// may not invoke a legal edge, <c>409</c> for an edge that is not in the graph
/// (docs/api-design.md §5.6).
///
/// <para>
/// <b>Why A-16's customer column is asserted at the authority layer rather than over HTTP —
/// finding I-23.</b> The plan's tests 5–8, 10 and 13 are written as a customer calling a transition
/// endpoint, but <b>no endpoint exists in this story that a <c>Customer</c> may call</b>: the staff
/// controller is <c>RequireAgent</c> by class policy (task 7), and the portal route
/// <c>POST /portal/tickets/{id}/transition</c> is published by <b>Story 13</b>, which calls this
/// same service. Adding it here would be starting Story 13. So the customer rules are asserted
/// against <see cref="TransitionAuthority"/> itself — <b>the exact predicate the portal endpoint
/// will call</b> — and the <c>403</c> a customer actually receives today from the role gate is
/// asserted over HTTP as well. Nothing about A-16 is left unproven; only its delivery is deferred.
/// </para>
/// </summary>
public sealed class TransitionAuthorityTests(TicketApiFixture fixture)
    : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";

    // ------------------------------------------------------- A-16, the customer column

    /// <summary>
    /// Test 5 — a customer may cancel their <b>own <c>New</c></b> ticket. A-16 plus A-18: the
    /// window lasts until an agent starts work, which is what makes <c>New</c> the boundary.
    /// </summary>
    [Fact]
    public void A_customer_may_cancel_their_own_New_ticket() =>
        Assert.True(TransitionAuthority.MayInvoke(
            Customer(), TicketAt(TicketStatus.New), TicketStatus.Cancelled));

    /// <summary>
    /// Test 6 — and may <b>not</b> cancel it once it is <c>Open</c>. This is an <b>authority</b>
    /// refusal, not a legality one: <c>Open → Cancelled</c> is a perfectly legal edge that an agent
    /// may take, so the caller gets <c>403 transition-not-permitted</c> and not <c>409</c>.
    /// </summary>
    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.Resolved)]
    public void A_customer_may_not_cancel_once_work_has_started(TicketStatus status)
    {
        Assert.False(TransitionAuthority.MayInvoke(
            Customer(), TicketAt(status), TicketStatus.Cancelled));

        // The edge itself is legal — which is exactly why this must be a 403 and not a 409.
        Assert.True(TicketLifecycle.IsLegal(status, TicketStatus.Cancelled));
    }

    /// <summary>
    /// Test 7 — <b>customers cannot close.</b> A-16 calls this out as a deliberate consequence: they
    /// may reopen a <c>Resolved</c> ticket, never close one.
    /// </summary>
    [Fact]
    public void A_customer_may_not_close()
    {
        Assert.False(TransitionAuthority.MayInvoke(
            Customer(), TicketAt(TicketStatus.Resolved), TicketStatus.Closed));

        Assert.True(TicketLifecycle.IsLegal(TicketStatus.Resolved, TicketStatus.Closed));
    }

    /// <summary>Test 8 — a customer may reopen their own <c>Resolved</c> ticket.</summary>
    [Fact]
    public void A_customer_may_reopen_their_own_Resolved_ticket() =>
        Assert.True(TransitionAuthority.MayInvoke(
            Customer(), TicketAt(TicketStatus.Resolved), TicketStatus.Open));

    /// <summary>
    /// The whole customer column at once: <b>exactly two permitted cells and no others</b>, over the
    /// full 6×6 matrix. A-16 gives a customer cancel-own-while-New and reopen-own-Resolved; anything
    /// else that ever became true here would be a widening of their authority.
    /// </summary>
    [Fact]
    public void The_customer_column_of_A16_has_exactly_two_permitted_cells()
    {
        var permitted = new List<(TicketStatus From, TicketStatus To)>();

        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                if (TransitionAuthority.MayInvoke(Customer(), TicketAt(from), to))
                {
                    permitted.Add((from, to));
                }
            }
        }

        Assert.Equal(
            [(TicketStatus.New, TicketStatus.Cancelled), (TicketStatus.Resolved, TicketStatus.Open)],
            permitted);
    }

    /// <summary>Test 13 — escalation is staff-only (A-16's last row).</summary>
    [Fact]
    public void A_customer_may_not_escalate()
    {
        Assert.False(TransitionAuthority.MayEscalate(Customer()));

        foreach (var role in new[] { UserRole.Agent, UserRole.Manager, UserRole.Administrator })
        {
            Assert.True(TransitionAuthority.MayEscalate(Staff(role)));
        }
    }

    /// <summary>
    /// <b>Agent, Manager and Administrator share one row</b> — A-16 states their transition
    /// authority is identical, and a Manager's cross-department reach is a <em>scope</em> rule
    /// applied before this. Asserted so a future "Managers only" cell cannot appear unnoticed.
    /// </summary>
    [Fact]
    public void All_three_staff_roles_have_identical_transition_authority()
    {
        foreach (var from in Enum.GetValues<TicketStatus>())
        {
            foreach (var to in Enum.GetValues<TicketStatus>())
            {
                Assert.True(TransitionAuthority.MayInvoke(Staff(UserRole.Agent), TicketAt(from), to));
                Assert.True(TransitionAuthority.MayInvoke(Staff(UserRole.Manager), TicketAt(from), to));
                Assert.True(TransitionAuthority.MayInvoke(
                    Staff(UserRole.Administrator), TicketAt(from), to));
            }
        }
    }

    // ------------------------------------------------------- over HTTP, as the roles that can call

    /// <summary>
    /// The <c>403</c> a customer actually receives today: the staff controller's <c>RequireAgent</c>
    /// gate, ahead of everything else in the §5.6 order. The portal route that reaches A-16 proper
    /// is Story 13's (finding I-23).
    /// </summary>
    [Fact]
    public async Task A_customer_role_token_is_refused_by_the_role_gate_on_every_lifecycle_route()
    {
        var customerUserId = await fixture.Factory.AddCustomerRoleUserAsync(
            $"authority.customer.{Guid.NewGuid():N}@tickets.local");

        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(customerUserId);

        var transition = await client.PostAsJsonAsync(
            $"{Tickets}/{ticketId}/transition", new { targetStatus = "Cancelled" });
        var escalate = await client.PostAsync($"{Tickets}/{ticketId}/escalate", null);
        var activity = await client.GetAsync($"{Tickets}/{ticketId}/activity");

        Assert.Equal(HttpStatusCode.Forbidden, transition.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, escalate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, activity.StatusCode);
    }

    /// <summary>
    /// Test 9 — an agent attempting <c>New → Resolved</c>. The edge is not in A-5's graph, so this
    /// is <b><c>409 illegal-transition</c></b> with <c>allowedTransitions</c> in the body — the one
    /// place the contract publishes that set (F-1).
    /// </summary>
    [Fact]
    public async Task An_illegal_edge_is_409_and_publishes_the_allowed_transitions()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Resolved" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("illegal-transition", problem.GetProperty("type").GetString());

        // §6.12 lists `detail` in the envelope; only `errors` is optional. It names the two statuses
        // and leaks no internals — and the front end renders from `type`, never from this (T2-J).
        var detail = problem.GetProperty("detail").GetString();

        Assert.NotNull(detail);
        Assert.Contains("New", detail);
        Assert.Contains("Resolved", detail);
        Assert.DoesNotContain("SupportCrm.", detail);
        Assert.Equal(
            ["Open", "Cancelled"],
            problem.GetProperty("allowedTransitions").EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty).ToArray());

        // Nothing half-applied: the ticket is still New.
        Assert.Equal(TicketStatus.New, await StatusOfAsync(ticketId));
    }

    /// <summary>A terminal ticket publishes an <b>empty</b> allowed set — truthful, not omitted.</summary>
    [Fact]
    public async Task A_terminal_ticket_publishes_an_empty_allowed_set()
    {
        var ticketId = await CancelledTicketAsync();

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Open" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(problem.GetProperty("allowedTransitions").EnumerateArray());
    }

    /// <summary>
    /// Test 10's principle — <b>scope beats authority</b>. An agent reaching another department's
    /// ticket is told <c>404</c>, never <c>403</c>: a <c>403</c> would confirm the ticket exists
    /// (AP-4). Asserted on every lifecycle route, because the rule is the controller's, not one
    /// endpoint's.
    /// </summary>
    [Fact]
    public async Task An_out_of_scope_ticket_is_404_on_every_lifecycle_route()
    {
        var technicalTicket = await fixture.AddTicketAsync(
            TicketApiFixture.TechnicalDepartmentId, fixture.NorthCustomerId, fixture.TechnicalAgentId,
            categoryCode: "technical");

        var billingAgent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var transition = await billingAgent.PostAsJsonAsync(
            $"{Tickets}/{technicalTicket}/transition", new { targetStatus = "Open" });
        var escalate = await billingAgent.PostAsync($"{Tickets}/{technicalTicket}/escalate", null);
        var activity = await billingAgent.GetAsync($"{Tickets}/{technicalTicket}/activity");

        Assert.Equal(HttpStatusCode.NotFound, transition.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, escalate.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, activity.StatusCode);
    }

    /// <summary>The legal path an agent walks, end to end, with the timestamps stamped on the way.</summary>
    [Fact]
    public async Task An_agent_walks_the_legal_path_and_the_lifecycle_timestamps_are_server_set()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        foreach (var target in new[] { "Open", "Pending", "Resolved", "Closed" })
        {
            var response = await client.PostAsJsonAsync(
                $"{Tickets}/{ticketId}/transition", new { targetStatus = target });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(target, ticket.GetProperty("status").GetString());
        }

        var final = await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.AsNoTracking().FirstAsync(t => t.Id == ticketId));

        Assert.NotNull(final.ResolvedAt);
        Assert.NotNull(final.ClosedAt);
    }

    /// <summary>
    /// A terminal ticket accepts <b>no</b> further transition — the other half of "terminal", proven
    /// over HTTP rather than only in the Domain.
    /// </summary>
    [Fact]
    public async Task A_closed_ticket_accepts_no_further_transition()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        foreach (var target in new[] { "Open", "Resolved", "Closed" })
        {
            await client.PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = target });
        }

        foreach (var target in Enum.GetNames<TicketStatus>())
        {
            var response = await client.PostAsJsonAsync(
                $"{Tickets}/{ticketId}/transition", new { targetStatus = target });

            // Closed → Closed is a no-op (200); everything else is refused.
            var expected = target == nameof(TicketStatus.Closed)
                ? HttpStatusCode.OK
                : HttpStatusCode.Conflict;

            Assert.Equal(expected, response.StatusCode);
        }

        Assert.Equal(TicketStatus.Closed, await StatusOfAsync(ticketId));
    }

    /// <summary>An unknown status name is a <c>400</c>, never confused with an illegal transition.</summary>
    [Fact]
    public async Task An_unknown_target_status_is_400_not_409()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Archived" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ------------------------------------------------------------------ escalation

    /// <summary>
    /// Test 11 — escalating a <c>High</c> ticket: priority becomes <c>Urgent</c>, <b>status is
    /// unchanged</b>, and an <c>Escalated</c> row is written with before and after.
    /// </summary>
    [Fact]
    public async Task Escalating_raises_priority_one_level_and_leaves_status_alone()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId,
            priority: TicketPriority.High);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsync($"{Tickets}/{ticketId}/escalate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Urgent", ticket.GetProperty("priority").GetString());
        Assert.Equal("New", ticket.GetProperty("status").GetString());

        var escalation = await fixture.Factory.WithDbAsync(async db =>
            await db.TicketActivities.AsNoTracking()
                .Where(a => a.TicketId == ticketId && a.ActivityType == TicketActivityType.Escalated)
                .SingleAsync());

        Assert.Equal("High", escalation.OldValue);
        Assert.Equal("Urgent", escalation.NewValue);
        Assert.Equal(TicketActorKind.User, escalation.ActorKind);
        Assert.Equal(fixture.BillingAgentId, escalation.ActorUserId);
    }

    /// <summary>Test 12 — <c>Urgent</c> stays <c>Urgent</c>, and it is still a <c>200</c>.</summary>
    [Fact]
    public async Task Escalating_an_urgent_ticket_leaves_it_urgent()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId,
            priority: TicketPriority.Urgent);

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsync($"{Tickets}/{ticketId}/escalate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Urgent", ticket.GetProperty("priority").GetString());
    }

    /// <summary>
    /// <b>A-20 holds on the escalation path too.</b> Escalation raises priority, and the due
    /// timestamps still do not move — the same rule Story 05 proved for <c>PATCH</c>, now on the
    /// second of its three call sites.
    /// </summary>
    [Fact]
    public async Task Escalating_does_not_move_the_sla_due_timestamps()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId,
            priority: TicketPriority.Low);

        var before = await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.AsNoTracking()
                .Where(t => t.Id == ticketId)
                .Select(t => new { t.FirstResponseDueAt, t.ResolutionDueAt })
                .FirstAsync());

        await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsync($"{Tickets}/{ticketId}/escalate", null);

        var after = await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.AsNoTracking()
                .Where(t => t.Id == ticketId)
                .Select(t => new { t.FirstResponseDueAt, t.ResolutionDueAt })
                .FirstAsync());

        Assert.Equal(before.FirstResponseDueAt, after.FirstResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }

    /// <summary>
    /// <b>A-21, end to end through the escalation path.</b> The fixture's departments have no
    /// manager, so this exercises the <em>fallback</em> rung — and the escalation still succeeds,
    /// which is the clause of A-21 that matters most: a missing manager suppresses a notification,
    /// never an escalation.
    /// </summary>
    [Fact]
    public async Task Escalation_succeeds_when_the_department_has_no_manager()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId,
            priority: TicketPriority.Medium);

        var departmentHasNoManager = await fixture.Factory.WithDbAsync(async db =>
            await db.Departments.AsNoTracking()
                .Where(d => d.Id == TicketApiFixture.BillingDepartmentId)
                .Select(d => d.ManagerUserId)
                .FirstAsync() is null);

        Assert.True(departmentHasNoManager, "The fixture's precondition changed.");

        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsync($"{Tickets}/{ticketId}/escalate", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("High", ticket.GetProperty("priority").GetString());
    }

    // ---------------------------------------------------------------------- helpers

    private async Task<TicketStatus> StatusOfAsync(Guid ticketId) =>
        await fixture.Factory.WithDbAsync(async db =>
            await db.Tickets.AsNoTracking().Where(t => t.Id == ticketId)
                .Select(t => t.Status).FirstAsync());

    private async Task<Guid> CancelledTicketAsync()
    {
        var ticketId = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId, fixture.HeadOfficeCustomerId, fixture.BillingAgentId);

        await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsJsonAsync($"{Tickets}/{ticketId}/transition", new { targetStatus = "Cancelled" });

        return ticketId;
    }

    private static Ticket TicketAt(TicketStatus status)
    {
        var now = DateTimeOffset.UtcNow;

        var ticket = Ticket.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Authority", "A-16 subject.",
            "billing", TicketPriority.Medium, Guid.NewGuid(), now, now.AddHours(4), now.AddHours(24));

        TicketStatus[] path = status switch
        {
            TicketStatus.New => [],
            TicketStatus.Open => [TicketStatus.Open],
            TicketStatus.Pending => [TicketStatus.Open, TicketStatus.Pending],
            TicketStatus.Resolved => [TicketStatus.Open, TicketStatus.Resolved],
            TicketStatus.Closed => [TicketStatus.Open, TicketStatus.Resolved, TicketStatus.Closed],
            TicketStatus.Cancelled => [TicketStatus.Cancelled],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        foreach (var step in path)
        {
            ticket.TransitionTo(step, now);
        }

        return ticket;
    }

    private static ICurrentUser Customer() => new StubCurrentUser(UserRole.Customer);

    private static ICurrentUser Staff(UserRole role) => new StubCurrentUser(role);

    /// <summary>
    /// A caller identity, for asserting <see cref="TransitionAuthority"/> in isolation.
    /// <b>It fakes only who is asking</b> — every rule under test is the real one.
    /// </summary>
    private sealed class StubCurrentUser(UserRole role) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid Id { get; } = Guid.NewGuid();

        public UserRole Role { get; } = role;

        public Guid? DepartmentId => null;

        public Guid? CustomerId => null;

        public string DisplayName => "Stub";

        public string Email => "stub@tickets.local";

        /// <summary>The real A-4 comparison, not a stubbed answer — <c>UserRoleExtensions</c>'s.</summary>
        public bool IsInRoleAtLeast(UserRole minimum) => Role.RankAtLeast(minimum);
    }
}
