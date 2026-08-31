using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Application.Modules.Sla;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>The ticket lifecycle — A-5 legality, A-16 authority, and escalation as an action</b>
/// (docs/api-design.md §5.6, AP-6, AP-7).
///
/// <para>
/// <b>Status is never a field.</b> AP-1 keeps <c>status</c> off <c>PATCH /tickets/{id}</c> so a
/// caller cannot bypass the matrix by writing a property, and AP-6 keeps every transition on
/// <b>one</b> endpoint so the matrix has one home and one <c>409</c> shape.
/// </para>
///
/// <para>
/// <b>One unit of work per request, committed once</b> (docs/architecture.md §3): the status change,
/// its activity row and its audit entry are added together and saved together, so no committed state
/// has one without the others.
/// </para>
/// </summary>
public sealed class TicketLifecycleService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketService tickets,
    TicketActivityRecorder activity,
    IAuditRecorder audit,
    IEscalationRecipientPolicy escalationRecipients,
    INotificationPublisher notifications,
    TimeProvider clock)
{
    /// <summary>
    /// <c>POST /tickets/{id}/transition</c>.
    ///
    /// <para>
    /// <b>The enforcement order is fixed by docs/api-design.md §5.6 and must not be rearranged:</b>
    /// </para>
    /// <list type="number">
    ///   <item><description><b>Role gate</b> — the controller's policy. <c>403</c>.</description></item>
    ///   <item><description><b>Scope</b> — <c>LoadScopedAsync</c>. Missing <em>or</em> out of scope is <c>404</c> (AP-4).</description></item>
    ///   <item><description><b>A-16 authority</b> — <c>403 transition-not-permitted</c>.</description></item>
    ///   <item><description><b>A-5 legality</b> — <c>409 illegal-transition</c>, carrying <c>allowedTransitions</c>.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Scope before authority is the security-relevant half of that order.</b> A customer acting
    /// on another customer's ticket must be told <c>404</c>, not <c>403</c> — a <c>403</c> would
    /// confirm the ticket exists (AP-4). Checking authority first would leak exactly that.
    /// </para>
    ///
    /// <para>
    /// <b>Authority before legality is the diagnostic half.</b> A customer trying to close their own
    /// ticket gets <c>403</c> — the edge <em>is</em> legal, they simply may not invoke it — while an
    /// agent trying <c>New → Resolved</c> gets <c>409</c>. Reversing these two would tell both
    /// callers the same thing about two different mistakes.
    /// </para>
    /// </summary>
    public async Task<TicketDto> TransitionAsync(
        Guid ticketId, TicketStatus targetStatus, CancellationToken ct)
    {
        // 2. Scope — fetch-then-authorize, never authorize-then-fetch-by-id (architecture §4.3).
        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        // 3. A-16 authority.
        if (!TransitionAuthority.MayInvoke(currentUser, ticket, targetStatus))
        {
            throw new ForbiddenException(
                TransitionAuthority.NotPermitted, TransitionAuthority.NotPermittedMessage);
        }

        var previous = ticket.Status;

        if (previous == targetStatus)
        {
            // A no-op transition writes no history row. An activity trail of non-events is worse
            // than no trail — the same rule AssignAsync already applies to a repeated assignee.
            return await tickets.GetAsync(ticket.Id, ct);
        }

        // 4. A-5 legality. The Domain refuses the edge; the handler turns it into 409 with
        //    allowedTransitions (docs/api-design.md §6.12).
        ticket.TransitionTo(targetStatus, clock.GetUtcNow());

        // 5. History, with before and after (the intake's acceptance criterion).
        await activity.RecordAsync(
            ticket.Id,
            TicketActivityType.StatusChanged,
            previous.ToString(),
            targetStatus.ToString(),
            ct: ct);

        // 6. Audit — a separate log, not derived from the history above (AD-10).
        await audit.RecordAsync(
            AuditAction.TicketStatusChanged,
            AuditOutcome.Success,
            AuditTargetType.Ticket,
            ticket.Id,
            ct: ct);

        await db.SaveChangesAsync(ct);

        return await tickets.GetAsync(ticket.Id, ct);
    }

    /// <summary>
    /// <b>R-13 / R-14 — a customer reply reopens a <c>Pending</c> ticket, automatically.</b>
    ///
    /// <para>
    /// <b>The rule lives here; its trigger does not.</b> This is a lifecycle rule, so it belongs
    /// with the rest of them — but nothing in Story 06 calls it. <b>Story 07's portal message
    /// endpoint is the caller</b>, and it passes the ticket it has already loaded and the customer
    /// who replied, so this runs <b>in the same transaction as the message</b>
    /// (docs/data-model.md §2.6 invariant 2b). It deliberately does not save.
    /// </para>
    ///
    /// <para>
    /// <b>It fires only from <c>Pending</c>.</b> A reply on a <c>New</c> ticket leaves it
    /// <c>New</c> — replying is not an agent starting work (A-18) — and a reply on a
    /// <c>Resolved</c> ticket does <b>not</b> reopen it: reopening <c>Resolved</c> stays the
    /// explicit transition A-16 gives the customer. Every other status is left alone.
    /// </para>
    ///
    /// <para>
    /// <b>Attributed to the replying customer, with <c>actorKind = User</c> (R-14).</b> It is
    /// <em>not</em> a <c>System</c> actor: the SLA monitor is the only system actor in this design,
    /// and attributing a customer-caused change to the system would make ticket history less
    /// truthful.
    /// </para>
    ///
    /// <para>
    /// <b>It generates no notification.</b> A-13 defines exactly four notification types and none of
    /// them is a status change (docs/data-model.md §2.12). The <c>CustomerReplied</c> notification
    /// belongs to the <em>message</em>, and is Story 07's to raise.
    /// </para>
    ///
    /// <para>
    /// <b>No authority check.</b> A-16 gives the customer no direct <c>Pending → Open</c> precisely
    /// because this is automatic rather than invoked — the caller is the server reacting to a
    /// reply, not a customer requesting a transition.
    /// </para>
    /// </summary>
    /// <returns><see langword="true"/> when the ticket was reopened.</returns>
    public async Task<bool> ApplyAutomaticCustomerReplyTransitionAsync(
        Ticket ticket, Guid replyingCustomerUserId, CancellationToken ct)
    {
        if (ticket.Status != TicketStatus.Pending)
        {
            return false;
        }

        var previous = ticket.Status;

        ticket.TransitionTo(TicketStatus.Open, clock.GetUtcNow());

        await activity.RecordForActorAsync(
            ticket.Id,
            replyingCustomerUserId,
            TicketActivityType.StatusChanged,
            previous.ToString(),
            TicketStatus.Open.ToString(),
            ct: ct);

        // No SaveChangesAsync: the caller's message write commits this with it (invariant 2b).
        return true;
    }

    /// <summary>
    /// <c>POST /tickets/{id}/escalate</c> — <b>an action, never a transition</b> (AP-7). Putting it
    /// in <c>/transition</c> would contradict A-5, which is explicit that escalation is not a status
    /// change.
    ///
    /// <para>
    /// <b>Priority up exactly one level; <c>Urgent</c> stays <c>Urgent</c>; status untouched.</b>
    /// There are no escalation tiers. Escalating an already-urgent ticket is a legal no-op and still
    /// returns <c>200</c> — A-5 gives escalation no failure mode of its own.
    /// </para>
    ///
    /// <para>
    /// <b>Recipients come from <see cref="IEscalationRecipientPolicy"/> — A-21 — and the cascade
    /// appears nowhere in this method.</b> One call, no branching here: the department-manager →
    /// every active Manager → every active Administrator → nobody ladder and its <c>Warning</c> log
    /// live in the policy, because <b>Story 09's breach sweep resolves recipients through the same
    /// one</b>. <b>An empty recipient list is a valid result and must not stop the escalation</b>
    /// (A-21): a missing manager suppresses a notification, never an escalation.
    /// </para>
    ///
    /// <para>
    /// <b><paramref name="actorKind"/> exists for Story 09.</b> The <c>sla-routing-escalation</c>
    /// intake requires the automatic breach trigger to <em>reuse this path rather than duplicate
    /// it</em>, so the signature is usable by a system caller from the start. Story 06 has no system
    /// caller; only the <c>User</c> branch is reachable today.
    /// </para>
    /// </summary>
    public async Task<TicketDto> EscalateAsync(
        Guid ticketId,
        CancellationToken ct,
        TicketActorKind actorKind = TicketActorKind.User)
    {
        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        // A-16's last row: escalation is staff-only.
        if (actorKind is TicketActorKind.User && !TransitionAuthority.MayEscalate(currentUser))
        {
            throw new ForbiddenException(
                TransitionAuthority.NotPermitted, "Your role may not escalate a ticket.");
        }

        var previous = ticket.Priority;
        var raised = Escalation.RaiseOneLevel(previous);

        ticket.ChangePriority(raised);

        // A-20 — the due timestamps FREEZE. Called even though it does nothing, because this is one
        // of the three paths that change a priority and the rule has exactly one home. Story 05's
        // PATCH and Story 09's breach escalation call the same method.
        SlaClock.OnPriorityChanged(ticket, raised);

        // Status is deliberately NOT touched. A-5: escalation is an action, not a transition.
        await RecordEscalationActivityAsync(ticket.Id, previous, raised, actorKind, ct);

        if (actorKind is TicketActorKind.User)
        {
            // Audited only on the user path. A system-initiated escalation has no actor, and
            // docs/data-model.md §2.14 admits a null actor for one reason only — a failed sign-in.
            // What a system escalation writes to the audit log is Story 09's to settle; inventing
            // an attribution here would bury a decision where nobody would look for it (finding
            // I-22).
            await audit.RecordAsync(
                AuditAction.TicketEscalated,
                AuditOutcome.Success,
                AuditTargetType.Ticket,
                ticket.Id,
                ct: ct);
        }

        // A-21. Resolve once, publish to each; an empty list is valid.
        var recipients = await escalationRecipients.ResolveAsync(ticket.DepartmentId, ct);

        foreach (var recipientId in recipients.UserIds)
        {
            await notifications.PublishAsync(
                recipientId, NotificationType.TicketEscalated, ticket.Id, ct);
        }

        await db.SaveChangesAsync(ct);

        return await tickets.GetAsync(ticket.Id, ct);
    }

    private Task RecordEscalationActivityAsync(
        Guid ticketId,
        TicketPriority previous,
        TicketPriority raised,
        TicketActorKind actorKind,
        CancellationToken ct) =>
        actorKind is TicketActorKind.System
            ? activity.RecordBySystemAsync(
                ticketId, TicketActivityType.Escalated, previous.ToString(), raised.ToString(), ct: ct)
            : activity.RecordAsync(
                ticketId, TicketActivityType.Escalated, previous.ToString(), raised.ToString(), ct: ct);
}
