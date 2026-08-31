using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// <b>The SLA breach sweep</b> — T2-D's third line and A-3's one breach action, run periodically
/// in-process by <c>SlaMonitorHostedService</c> (AD-6).
///
/// <h3>The clock is 24/7 and <c>Pending</c> does not pause it</h3>
/// A-3 in full: wall-clock hours from <c>createdAt</c>, no business hours, no holidays, no timezones,
/// <b>and no pause on <c>Pending</c></b> (docs/data-model.md §2.6 invariant 7). The query below
/// therefore filters on status only to exclude <b>terminal</b> tickets — a <c>Pending</c> ticket is
/// swept exactly like an <c>Open</c> one. *"The 24/7 no-pause clock is a known simplification. Do not
/// 'fix' it here."*
///
/// <h3>Idempotence comes from the latching flags</h3>
/// A re-run finds nothing, because the flags it set are part of the query's own predicate. There is
/// <b>no lock, no queue, no dedupe table and no external state</b> — which is what makes a coarse
/// timer safe: a tick that overlaps its predecessor cannot double-escalate a ticket, because the
/// first tick's flag has already removed it from the second tick's result set.
///
/// <h3>It escalates through Story 06, not beside it</h3>
/// <c>TicketLifecycleService.EscalateAsync(ticketId, ActorKind.System)</c> raises the priority one
/// level, leaves the status alone, writes the <c>Escalated</c> row and publishes to A-21's recipients.
/// <b>None of that is re-implemented here</b> — the intake requires the automatic trigger to reuse the
/// manual path, and the one rule this service adds is *when* it fires.
///
/// <h3>PF-5 is visible and deliberately unresolved</h3>
/// <c>firstRespondedAt</c> is set only by the first outbound message, so <b>a ticket resolved without
/// a reply is permanently first-response breached</b>. That is A-3 as written and this story does not
/// change it — doing so would be a change to A-3, not an implementation choice. Reported, not fixed.
/// </summary>
public sealed class SlaEvaluationService(
    IApplicationDbContext db,
    TicketLifecycleService lifecycle,
    TicketActivityRecorder activity,
    IEscalationRecipientPolicy escalationRecipients,
    INotificationPublisher notifications,
    TimeProvider clock,
    ILogger<SlaEvaluationService> logger)
{
    /// <summary>
    /// One pass over everything currently in breach. Returns how many tickets were flagged, which is
    /// what the hosted service logs — a sweep that silently does nothing is indistinguishable from a
    /// sweep that is not running.
    /// </summary>
    public async Task<int> EvaluateDueTicketsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // **The query the two filtered indexes exist for** (docs/data-model.md §6: "without these it
        // scans every ticket on every tick"). Both halves are sargable on the due-date columns, and
        // the already-breached rows are excluded by the same flags this pass sets — which is where
        // idempotence comes from.
        var due = await db.Tickets
            .Where(t => t.Status != TicketStatus.Closed
                && t.Status != TicketStatus.Cancelled
                && ((t.FirstResponseDueAt <= now && !t.FirstResponseBreached && t.FirstRespondedAt == null)
                    || (t.ResolutionDueAt <= now && !t.ResolutionBreached && t.ResolvedAt == null)))
            .ToListAsync(ct);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var ticket in due)
        {
            await FlagAndEscalateAsync(ticket, now, ct);
        }

        // **Committed once** (docs/architecture.md §3). Every flag, activity row, priority raise and
        // notification from this pass lands together or not at all; a partial sweep would leave
        // tickets flagged breached with no escalation and no notification to explain it.
        await db.SaveChangesAsync(ct);

        logger.LogInformation("SLA sweep flagged {Count} ticket(s) as breached.", due.Count);

        return due.Count;
    }

    private async Task FlagAndEscalateAsync(Ticket ticket, DateTimeOffset now, CancellationToken ct)
    {
        // Both clocks are evaluated, and a ticket can miss both in one pass — the flags are
        // independent facts, so one activity row per breached clock rather than one per ticket.
        var breachedFirstResponse =
            ticket.FirstResponseDueAt <= now && !ticket.FirstResponseBreached && ticket.FirstRespondedAt is null;

        var breachedResolution =
            ticket.ResolutionDueAt <= now && !ticket.ResolutionBreached && ticket.ResolvedAt is null;

        if (breachedFirstResponse)
        {
            ticket.MarkFirstResponseBreached();

            await RecordBreachAsync(ticket.Id, nameof(Ticket.FirstResponseDueAt), ct);
        }

        if (breachedResolution)
        {
            ticket.MarkResolutionBreached();

            await RecordBreachAsync(ticket.Id, nameof(Ticket.ResolutionDueAt), ct);
        }

        // **The SLA monitor is the only system actor in this design** (docs/data-model.md §2.7), and
        // this is the call that makes it one: `ActorKind.System` means the Escalated row carries a
        // null actor and no audit entry is written for a caller that does not exist.
        //
        // A-3 has ONE breach action, so one escalation per pass even when both clocks broke at once —
        // two would raise the priority two levels for a single sweep.
        await lifecycle.EscalateAsync(ticket.Id, ct, TicketActorKind.System);

        // A-21, resolved through the same policy instance Story 06's manual escalate uses. The
        // cascade itself appears nowhere in this file, which is the point of the interface — and an
        // empty recipient list is valid and never stops the sweep.
        var recipients = await escalationRecipients.ResolveAsync(ticket.DepartmentId, ct);

        foreach (var recipientId in recipients.UserIds)
        {
            await notifications.PublishAsync(
                recipientId, NotificationType.SlaBreached, ticket.Id, ct);
        }
    }

    /// <summary>
    /// The <c>SlaBreached</c> history row — <b><c>actorKind = System</c>, <c>actorUserId = null</c></b>.
    /// <c>newValue</c> names which clock was missed, so the trail distinguishes a first-response
    /// breach from a resolution breach without a second activity type.
    /// </summary>
    private Task RecordBreachAsync(Guid ticketId, string clockName, CancellationToken ct) =>
        activity.RecordBySystemAsync(
            ticketId, TicketActivityType.SlaBreached, newValue: clockName, ct: ct);
}
