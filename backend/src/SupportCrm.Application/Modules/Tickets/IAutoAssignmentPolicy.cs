using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// The seam automatic assignment lands on — <b>Story 09's round-robin</b> (T2-D).
///
/// <para>
/// <b>Story 05 delivers manual assignment only</b> (the <c>ticket-core</c> intake says so in as many
/// words), so this story registers the no-op below and nothing else. The named extension point
/// exists now because <c>CreateAsync</c> is where automatic assignment has to run — <em>after
/// creation and before the response</em> — and leaving the seam unnamed would mean Story 09 editing
/// the creation path to find a place to stand.
/// </para>
///
/// <para>
/// <b>Whatever implements this must not change status</b> (A-18): automatic assignment at creation
/// leaves the ticket <c>New</c>, exactly as manual assignment does.
/// </para>
/// </summary>
public interface IAutoAssignmentPolicy
{
    /// <summary>
    /// Chooses an assignee for a newly created ticket, or returns null to leave it unassigned.
    /// <para>
    /// Story 09 replaces the no-op with round-robin across active agents <b>in the ticket's
    /// department</b> — no skills, no load balancing, no capacity rules (T2-D).
    /// </para>
    /// </summary>
    Task<Guid?> ChooseAssigneeAsync(Ticket ticket, CancellationToken ct);
}

/// <summary>
/// The registered implementation until Story 09 — it assigns nobody.
/// <para>
/// <b>This is the honest behaviour, not a stub.</b> Story 05 is manual assignment; a ticket created
/// today is genuinely unassigned until an agent assigns it, and the seeded data reflects that with
/// a deliberate mix of assigned and unassigned rows.
/// </para>
/// </summary>
public sealed class NoAutoAssignmentPolicy : IAutoAssignmentPolicy
{
    public Task<Guid?> ChooseAssigneeAsync(Ticket ticket, CancellationToken ct) =>
        Task.FromResult<Guid?>(null);
}
