using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>The A-16 authority matrix — who may invoke a transition</b>, and the second of the two rules
/// a lifecycle change passes.
///
/// <para>
/// <b>Kept separate from legality on purpose.</b> <c>TicketLifecycle</c> (Domain) answers *"is this
/// edge in A-5's graph"*; this answers *"may this caller invoke it"*. The separation is what makes
/// the two failure modes distinguishable at the contract — <c>403 transition-not-permitted</c> here,
/// <c>409 illegal-transition</c> there (docs/api-design.md §5.6). Collapsing them into one check
/// would tell a customer who tried to close their ticket the same thing as one who tried an
/// impossible edge, and the two are not the same mistake.
/// </para>
///
/// <para>
/// <b>Scope is not authority either, and is enforced before this.</b> <c>TicketScope</c> answers
/// *"can this caller see the ticket at all"* with a <c>404</c> (AP-4) — so a customer acting on
/// <em>another</em> customer's ticket is never told they lack authority; they are told it does not
/// exist. Scope beats authority, in that order, always.
/// </para>
///
/// <para>
/// <b>Customers cannot close a ticket.</b> A-16 calls this out as a deliberate consequence rather
/// than an oversight: a customer may <em>reopen</em> a <c>Resolved</c> ticket and may
/// <em>cancel</em> their own while it is still <c>New</c>, and that is the whole of their authority.
/// Closure is manual and staff-only — there is no timer and no automatic closure anywhere in this
/// design.
/// </para>
/// </summary>
public static class TransitionAuthority
{
    /// <summary>
    /// May <paramref name="caller"/> move <paramref name="ticket"/> to <paramref name="target"/>?
    ///
    /// <para>
    /// <b>Agent, Manager and Administrator share one row</b> — A-16 states their authority over
    /// transitions is identical, and a Manager's *"all departments"* difference is a <em>scope</em>
    /// rule, already applied by <c>TicketScope</c> before this is called. Writing three identical
    /// branches would imply a distinction the matrix does not make.
    /// </para>
    ///
    /// <para>
    /// The customer branch is the whole of A-16's customer column: <b>cancel own, only while
    /// <c>New</c></b> (the window A-18 defines — once an agent starts work the ticket is
    /// <c>Open</c> and the window has closed), and <b>reopen own <c>Resolved</c></b>. Everything
    /// else is refused.
    /// </para>
    /// </summary>
    public static bool MayInvoke(ICurrentUser caller, Ticket ticket, TicketStatus target) =>
        caller.Role switch
        {
            UserRole.Customer =>
                (target is TicketStatus.Cancelled && ticket.Status is TicketStatus.New)      // A-16 + A-18
                || (target is TicketStatus.Open && ticket.Status is TicketStatus.Resolved),  // reopen own
            _ => true,
        };

    /// <summary>
    /// <b>Escalation is staff-only</b> (A-16's last row). It is an action rather than a transition,
    /// so it has its own check here rather than a target status.
    /// </summary>
    public static bool MayEscalate(ICurrentUser caller) => caller.Role != UserRole.Customer;

    /// <summary>The slug docs/api-design.md §5.6 fixes for an authority refusal.</summary>
    public const string NotPermitted = "transition-not-permitted";

    public const string NotPermittedMessage =
        "Your role may not perform this transition on this ticket.";
}
