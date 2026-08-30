using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Domain.Modules.Sla;

/// <summary>
/// The per-priority SLA targets, as the Domain sees them (A-3).
///
/// <para>
/// <b>This is a Domain-side shape, not the configuration type.</b> The configured
/// <c>SlaTargetOptions</c> lives in the Application layer and speaks strings; this speaks
/// <see cref="TicketPriority"/>. The Application layer maps one to the other, which keeps
/// <c>SupportCrm.Domain</c> at zero references (AD-4) and keeps the arithmetic below testable
/// without a configuration host.
/// </para>
/// </summary>
/// <param name="FirstResponseHours">Hours from ticket creation to the first-response deadline.</param>
/// <param name="ResolutionHours">Hours from ticket creation to the resolution deadline.</param>
public sealed record SlaTargets(int FirstResponseHours, int ResolutionHours);

/// <summary>
/// The SLA arithmetic of A-3, and the <b>one</b> place it lives
/// (00-implementation-plan §6: *"SLA arithmetic (A-3) — <c>Domain/Modules/Sla/SlaClock.cs</c>"*).
/// It is in the Domain because docs/architecture.md §2.1 puts SLA target calculation there.
///
/// <para>
/// <b>The clock is 24/7 wall-clock</b> (A-3). There are <b>no business hours, no holiday calendar,
/// no timezone arithmetic and no pause on <c>Pending</c></b> — a `Pending` ticket's clock keeps
/// running (docs/data-model.md §2.6 invariant 7). Real SLA policy is product-scope §9 question 5
/// and is <b>deliberately still open</b>; nothing here anticipates it.
/// </para>
/// </summary>
public static class SlaClock
{
    /// <summary>
    /// Both deadlines, computed once at creation from the SLA clock origin (A-3).
    /// <para>
    /// Plain hour addition on a <see cref="DateTimeOffset"/>, which is exactly what a 24/7 clock
    /// means: it crosses midnight, weekends and holidays without noticing them, because A-3 says
    /// there is nothing to notice.
    /// </para>
    /// </summary>
    public static (DateTimeOffset FirstResponseDueAt, DateTimeOffset ResolutionDueAt)
        ComputeAtCreation(DateTimeOffset createdAt, TicketPriority priority, SlaTargets targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        // `priority` is not read: the caller has already selected this priority's targets, and
        // taking the parameter keeps the signature honest about what the result depends on — the
        // plan specifies it, and Story 09 reads better for it.
        _ = priority;

        return (
            createdAt.AddHours(targets.FirstResponseHours),
            createdAt.AddHours(targets.ResolutionHours));
    }

    /// <summary>
    /// <b>A-20 (2026-08-30, closing OQ-2): the due timestamps FREEZE at creation.</b> A priority
    /// change — Story 05's <c>PATCH</c>, Story 06's manual escalation, or Story 09's automatic
    /// breach escalation — does not move <c>FirstResponseDueAt</c> or <c>ResolutionDueAt</c>.
    ///
    /// <para>
    /// <b>This no-op is the rule.</b> It is called rather than inlined so A-20 has exactly one home,
    /// and so the decision is something the code states rather than something it merely happens to
    /// satisfy. Stories 06 and 09 call this same method.
    /// </para>
    ///
    /// <para>
    /// <b>Do not "fix" this by recomputing.</b> Recompute was the rejected reading
    /// (docs/data-model.md §2.6 invariant 6): it lets an escalation tighten a deadline retroactively
    /// and breach a ticket as a direct consequence of the escalation that its own breach triggered.
    /// Changing this body changes an approved product decision, which is the user's to make.
    /// </para>
    /// </summary>
    public static void OnPriorityChanged(Ticket ticket, TicketPriority newPriority)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        // Intentionally empty — see the remarks above. A-20 freezes the due timestamps.
        _ = newPriority;
    }
}
