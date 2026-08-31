using System.Net.Http.Json;
using System.Text.Json;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>The two guarantees My queue rests on</b> (Story 08 plan task 2, docs/ui-design.md §5.1).
///
/// <para>
/// Story 05 already implements both — <c>assigneeId=me</c> resolves through
/// <c>TicketService.ResolveAssigneeFilter</c> and the default sort is
/// <c>resolutionBreached DESC, resolutionDueAt ASC</c>. <b>Neither was covered by a test</b>, so the
/// queue's whole promise — <em>"these are my tickets, most urgent first"</em> — was unproven. This
/// file adds the coverage and changes no production behaviour.
/// </para>
///
/// <para>
/// <b>Every ticket here is assigned to a freshly created agent</b>, not to the fixture's shared one.
/// The fixture is an <c>IClassFixture</c> and other classes add rows to the same host, so filtering
/// by a caller nobody else uses is what makes the assertion about <em>order</em> rather than about
/// whatever else happens to be in the table. That the filter provides the isolation is itself the
/// first thing being proved.
/// </para>
///
/// <para>
/// <b>SQLite caveat.</b> The breached-first key is a boolean and orders identically on any provider.
/// The <c>resolutionDueAt</c> tiebreak is a <c>DateTimeOffset</c>, which per CLAUDE.md §5 the SQLite
/// host does not conclusively prove; it is verified against real SQL Server in this slice's
/// end-to-end run. The values used here are hours apart and stored as UTC, so the comparison is not
/// a near-miss either way.
/// </para>
/// </summary>
public sealed class QueueOrderingTests(TicketApiFixture fixture) : IClassFixture<TicketApiFixture>
{
    private const string Tickets = "/api/v1/tickets";

    /// <summary>
    /// <b>Breached first, even when it is due later.</b> This is the assertion the design makes
    /// explicitly — <em>"ordered by SLA urgency with breached tickets first"</em> — and the one a
    /// naive <c>ORDER BY resolutionDueAt</c> would silently get wrong, because the breached ticket's
    /// deadline has already passed and sorts nowhere near the front on date alone.
    /// </summary>
    [Fact]
    public async Task With_no_sort_a_breached_ticket_outranks_an_earlier_unbreached_one()
    {
        var agentId = await AddQueueAgentAsync();

        // Due LATER than the unbreached one, so date alone would put it second.
        var breached = await AddQueueTicketAsync(agentId, dueInHours: 10, breached: true);
        var soon = await AddQueueTicketAsync(agentId, dueInHours: 1, breached: false);

        var ids = await QueueIdsAsync(agentId);

        Assert.Equal([breached, soon], ids);
    }

    /// <summary>
    /// <b>Unbreached tickets fall back to soonest-due.</b> Asserted separately from the rule above
    /// so a regression that dropped the date tiebreak entirely — leaving only the boolean — would
    /// still fail something.
    /// </summary>
    [Fact]
    public async Task With_no_sort_unbreached_tickets_run_soonest_due_first()
    {
        var agentId = await AddQueueAgentAsync();

        var later = await AddQueueTicketAsync(agentId, dueInHours: 8, breached: false);
        var sooner = await AddQueueTicketAsync(agentId, dueInHours: 2, breached: false);

        var ids = await QueueIdsAsync(agentId);

        Assert.Equal([sooner, later], ids);
    }

    /// <summary>
    /// <b><c>assigneeId=me</c> is the caller's own queue and nobody else's.</b> The literal is
    /// resolved server-side from the authenticated caller, so the screen never sends a user id —
    /// a caller-supplied identity is never trusted (docs/architecture.md §4.3 point 1).
    /// </summary>
    [Fact]
    public async Task Assignee_me_returns_only_the_callers_own_tickets()
    {
        var agentId = await AddQueueAgentAsync();

        var mine = await AddQueueTicketAsync(agentId, dueInHours: 3, breached: false);

        // Same department, so it is fully visible to the caller on the unfiltered list — which is
        // exactly why the queue needs the filter rather than relying on scoping.
        var anothersInSameDepartment = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId,
            assignedTo: fixture.BillingAgentId);

        var unassigned = await fixture.AddTicketAsync(
            TicketApiFixture.BillingDepartmentId,
            fixture.HeadOfficeCustomerId,
            fixture.BillingAgentId);

        var queue = await QueueIdsAsync(agentId);

        Assert.Equal([mine], queue);

        // The comparison that gives the assertion meaning: both other tickets ARE visible to this
        // caller unfiltered, so their absence above is the filter working, not the scope.
        var everything = await ListIdsAsync(agentId, $"{Tickets}?pageSize=100");

        Assert.Contains(anothersInSameDepartment, everything);
        Assert.Contains(unassigned, everything);
    }

    private Task<Guid> AddQueueAgentAsync() =>
        fixture.Factory.AddStaffUserAsync(
            UserRole.Agent,
            $"queue.agent.{Guid.NewGuid():N}@tickets.local",
            departmentId: TicketApiFixture.BillingDepartmentId);

    /// <summary>
    /// Creates an assigned ticket and then forces <c>ResolutionDueAt</c> and the latching breach
    /// flag to the values the ordering assertion needs.
    ///
    /// <para>
    /// Both are <c>private set</c> and <b>Story 09 owns the only production writer</b> of the flags,
    /// so this reaches them through EF's property access rather than through a domain method. That is
    /// deliberate: adding a public setter or a <c>MarkBreached</c> method to satisfy a test would put
    /// production API surface in the Domain that no approved document asks for.
    /// </para>
    /// </summary>
    private Task<Guid> AddQueueTicketAsync(Guid agentId, int dueInHours, bool breached)
    {
        return fixture.Factory.WithDbAsync(async db =>
        {
            var id = await fixture.AddTicketAsync(
                TicketApiFixture.BillingDepartmentId,
                fixture.HeadOfficeCustomerId,
                fixture.BillingAgentId,
                assignedTo: agentId,
                subject: $"Queue ticket due in {dueInHours}h");

            var ticket = await db.Tickets.FindAsync(id);
            var entry = db.Entry(ticket!);

            entry.Property(nameof(Ticket.ResolutionDueAt)).CurrentValue =
                DateTimeOffset.UtcNow.AddHours(dueInHours);

            entry.Property(nameof(Ticket.ResolutionBreached)).CurrentValue = breached;

            await db.SaveChangesAsync();

            return id;
        });
    }

    /// <summary>The request the queue screen actually makes: <c>assigneeId=me</c>, and no sort.</summary>
    private Task<List<Guid>> QueueIdsAsync(Guid callerId) =>
        ListIdsAsync(callerId, $"{Tickets}?assigneeId=me&pageSize=100");

    private async Task<List<Guid>> ListIdsAsync(Guid callerId, string url)
    {
        var page = await fixture.Factory.CreateClientFor(callerId)
            .GetFromJsonAsync<JsonElement>(url);

        return [.. page.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())];
    }
}
