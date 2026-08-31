namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// <b>An A-5 legality violation — the <c>409 illegal-transition</c> of docs/api-design.md §5.6.</b>
/// Thrown by <see cref="Ticket.TransitionTo"/>, which is the only way a ticket's status changes, so
/// an illegal edge is refused <b>server-side</b> however it was reached — API, seeder, or a future
/// caller that does not exist yet.
///
/// <para>
/// <b>Why a Domain exception and not an Application one.</b> <c>SupportCrm.Domain</c> has zero
/// project references (AD-4), so it cannot name <c>AppException</c> and cannot carry a problem-type
/// slug. It does not need to: the rule is a Domain rule, and the translation to
/// <c>409 illegal-transition</c> — plus the <see cref="AllowedTransitions"/> extension the contract
/// requires — happens once in <c>Api/Errors/ProblemDetailsExceptionHandler</c>, the single
/// translation point architecture §2.1 already mandates.
/// </para>
///
/// <para>
/// <b><see cref="AllowedTransitions"/> is carried on the exception</b> rather than recomputed by the
/// handler, so the set the caller is told about is provably the set the refusal was made against.
/// </para>
/// </summary>
public sealed class IllegalTransitionException(
    TicketStatus from, TicketStatus to, IReadOnlyList<TicketStatus> allowedTransitions)
    : Exception($"A ticket in '{from}' cannot transition to '{to}'.")
{
    public TicketStatus From { get; } = from;

    public TicketStatus To { get; } = to;

    /// <summary>
    /// The legal targets from <see cref="From"/>. <b>Empty for a terminal status</b>, which is the
    /// truthful answer for a `Closed` or `Cancelled` ticket rather than an omission.
    /// </summary>
    public IReadOnlyList<TicketStatus> AllowedTransitions { get; } = allowedTransitions;
}
