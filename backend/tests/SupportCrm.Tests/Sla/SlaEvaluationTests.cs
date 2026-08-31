using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Sla;

/// <summary>
/// The breach sweep — A-3's one breach action, T2-D's third line, and the assertions Story 09's plan
/// names for it.
///
/// <para>
/// Every test drives <c>SupportCrmApiFactory.RunSlaSweepAsync</c>, which reproduces
/// <c>SlaMonitorHostedService</c>'s scope setup exactly. <b>The timer does not run in the test
/// host</b> — <c>ConfigureWebHost</c> removes every hosted service — so "sweep twice and nothing
/// changes" is a real assertion rather than a race against a background thread.
/// </para>
/// </summary>
public sealed class SlaEvaluationTests(SlaFixture fixture) : IClassFixture<SlaFixture>
{
    /// <summary>
    /// <b>The whole breach action in one assertion</b> (T2-D): flagged, priority up exactly one
    /// level, <b>status unchanged</b>, and a history row attributed to the system.
    /// </summary>
    [Fact]
    public async Task A_ticket_past_its_resolution_target_is_flagged_escalated_and_recorded()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.BillingDepartmentId, TicketPriority.Medium);

        var before = await fixture.ReloadAsync(ticketId);

        await fixture.RunSweepAsync();

        var after = await fixture.ReloadAsync(ticketId);

        Assert.True(after.ResolutionBreached);

        // Exactly one level (Escalation.RaiseOneLevel), not "straight to Urgent".
        Assert.Equal(TicketPriority.High, after.Priority);

        // A-5: escalation is an action, not a transition. The status must be untouched.
        Assert.Equal(before.Status, after.Status);

        var breachRows = (await fixture.ActivityForAsync(ticketId))
            .Where(a => a.ActivityType == TicketActivityType.SlaBreached)
            .ToList();

        Assert.NotEmpty(breachRows);

        // The SLA monitor is the only system actor in this design (data-model §2.7): the row carries
        // ActorKind.System and a null actor id, not a stand-in user.
        Assert.All(breachRows, row =>
        {
            Assert.Equal(TicketActorKind.System, row.ActorKind);
            Assert.Null(row.ActorUserId);
        });
    }

    /// <summary>
    /// <b>Idempotence, and it comes from the latching flags rather than from external state.</b> The
    /// second pass finds nothing because the first pass's flags are part of the query's own predicate
    /// — no lock, no queue, no dedupe table.
    /// </summary>
    [Fact]
    public async Task Running_the_sweep_twice_changes_nothing_the_second_time()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.BillingDepartmentId, TicketPriority.Low);

        await fixture.RunSweepAsync();

        var afterFirst = await fixture.ReloadAsync(ticketId);
        var activityAfterFirst = (await fixture.ActivityForAsync(ticketId)).Count;
        var notificationsAfterFirst = (await fixture.NotificationsForAsync(ticketId)).Count;

        await fixture.RunSweepAsync();

        var afterSecond = await fixture.ReloadAsync(ticketId);

        Assert.Equal(afterFirst.Priority, afterSecond.Priority);
        Assert.Equal(activityAfterFirst, (await fixture.ActivityForAsync(ticketId)).Count);
        Assert.Equal(notificationsAfterFirst, (await fixture.NotificationsForAsync(ticketId)).Count);
    }

    /// <summary>
    /// <b>The flags latch</b> (data-model §2.6 invariant 5). Lowering the priority afterwards must not
    /// clear the breach — history and SLA reporting stay honest even when priority moves.
    /// </summary>
    [Fact]
    public async Task A_breached_ticket_keeps_its_flag_when_priority_is_later_lowered()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.BillingDepartmentId, TicketPriority.High);

        await fixture.RunSweepAsync();

        Assert.True((await fixture.ReloadAsync(ticketId)).ResolutionBreached);

        await fixture.Factory.WithDbAsync(async db =>
        {
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);

            ticket.ChangePriority(TicketPriority.Low);

            return await db.SaveChangesAsync();
        });

        var after = await fixture.ReloadAsync(ticketId);

        Assert.Equal(TicketPriority.Low, after.Priority);
        Assert.True(after.ResolutionBreached);
    }

    /// <summary><c>Urgent</c> is the ceiling — it stays there rather than wrapping or throwing.</summary>
    [Fact]
    public async Task An_urgent_breached_ticket_stays_urgent()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.BillingDepartmentId, TicketPriority.Urgent);

        await fixture.RunSweepAsync();

        var after = await fixture.ReloadAsync(ticketId);

        Assert.Equal(TicketPriority.Urgent, after.Priority);
        Assert.True(after.ResolutionBreached);
    }

    /// <summary>
    /// <b>A-3's no-pause rule, asserted explicitly.</b> <c>Pending</c> does not stop the clock
    /// (data-model §2.6 invariant 7). This is the simplification the plan says not to "fix", so it
    /// gets the test that would fail if someone did.
    /// </summary>
    [Fact]
    public async Task A_pending_ticket_still_breaches_on_schedule()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.BillingDepartmentId, TicketPriority.Medium,
            assignedTo: fixture.BillingAgentId);

        // New to Open to Pending, through the real state machine rather than by writing the column.
        await fixture.Factory.WithDbAsync(async db =>
        {
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            var now = DateTimeOffset.UtcNow;

            ticket.TransitionTo(TicketStatus.Open, now);
            ticket.TransitionTo(TicketStatus.Pending, now);

            return await db.SaveChangesAsync();
        });

        Assert.Equal(TicketStatus.Pending, (await fixture.ReloadAsync(ticketId)).Status);

        await fixture.RunSweepAsync();

        var after = await fixture.ReloadAsync(ticketId);

        Assert.True(after.ResolutionBreached);
        Assert.Equal(TicketStatus.Pending, after.Status);
    }

    /// <summary>
    /// <b>A-21's fallback rung, asserted on the recipients rather than on survival.</b> The Technical
    /// department has no manager, so the notification goes to every active <c>Manager</c> — and the
    /// flag and the priority raise happen regardless. <c>EscalationRecipientPolicyTests</c> covers the
    /// cascade itself; this proves the sweep publishes to what the policy returned.
    /// </summary>
    [Fact]
    public async Task A_department_with_no_manager_still_escalates_and_notifies_the_next_level()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.TechnicalDepartmentId, TicketPriority.Medium, categoryCode: "technical");

        await fixture.RunSweepAsync();

        var after = await fixture.ReloadAsync(ticketId);

        // The escalation is never blocked by a missing manager.
        Assert.True(after.ResolutionBreached);
        Assert.Equal(TicketPriority.High, after.Priority);

        var breachNotifications = (await fixture.NotificationsForAsync(ticketId))
            .Where(n => n.Type == NotificationType.SlaBreached)
            .ToList();

        // Rung 2: every active Manager. The fixture has exactly one, and it sits in the OTHER
        // department — which is the point, since a Manager's authority is cross-department (A-4).
        Assert.NotEmpty(breachNotifications);
        Assert.All(breachNotifications, n => Assert.Equal(fixture.BillingManagerId, n.RecipientUserId));
    }

    /// <summary>
    /// The sweep leaves alone what is not due. Asserted because a predicate bug that flagged
    /// everything would still pass every test above.
    /// </summary>
    [Fact]
    public async Task A_ticket_within_its_targets_is_untouched()
    {
        var ticketId = await fixture.AddHealthyTicketAsync(SlaFixture.BillingDepartmentId);

        await fixture.RunSweepAsync();

        var after = await fixture.ReloadAsync(ticketId);

        Assert.False(after.ResolutionBreached);
        Assert.False(after.FirstResponseBreached);
        Assert.Equal(TicketPriority.Low, after.Priority);
    }

    /// <summary>
    /// <b>Terminal tickets are excluded.</b> A <c>Closed</c> ticket past its deadline must not be
    /// escalated — there is nothing left to escalate, and A-5 forbids the transition anyway.
    /// </summary>
    [Fact]
    public async Task A_closed_ticket_past_its_deadline_is_not_swept()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(
            SlaFixture.BillingDepartmentId, TicketPriority.Medium,
            assignedTo: fixture.BillingAgentId);

        await fixture.Factory.WithDbAsync(async db =>
        {
            var ticket = await db.Tickets.FirstAsync(t => t.Id == ticketId);
            var now = DateTimeOffset.UtcNow;

            ticket.TransitionTo(TicketStatus.Open, now);
            ticket.TransitionTo(TicketStatus.Resolved, now);
            ticket.TransitionTo(TicketStatus.Closed, now);

            return await db.SaveChangesAsync();
        });

        await fixture.RunSweepAsync();

        var after = await fixture.ReloadAsync(ticketId);

        Assert.False(after.ResolutionBreached);
        Assert.Equal(TicketPriority.Medium, after.Priority);
    }
}
