using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>One home for "may this user be given work on this ticket?"</b> — docs/data-model.md §5
/// constraint 10.
///
/// <para>
/// <b>Extracted by Story 14 rather than restated.</b> Story 05 wrote this check inline in
/// <c>TicketService.AssignAsync</c>; Story 14's plan directs its task service to <em>"reuse the
/// check Story 05 wrote for assignment"</em>, and a second copy of a security-relevant predicate is
/// exactly the kind of drift 00-implementation-plan §6's one-implementation-per-shared-rule table
/// exists to prevent. <b>The rule is unchanged</b> — both call sites now read it from here.
/// </para>
///
/// <para>
/// <b>The rule.</b> A ticket assignee, and equally a task assignee, must be an <b>active staff user
/// whose department equals the ticket's</b>. That is what makes "a ticket can never be assigned to
/// someone who could not then see it" true, and it holds for tasks for the same reason: a task on a
/// ticket its assignee cannot open is a to-do nobody can action.
/// </para>
///
/// <para>
/// <b>One slug and one message for every way the candidate is unsuitable</b> — not in the ticket's
/// department, not active, or a <c>Customer</c>-role login. Distinguishing them would tell a caller
/// which staff accounts exist and in which departments, which is the same disclosure AP-4 prevents
/// elsewhere.
/// </para>
/// </summary>
public static class TicketAssigneePolicy
{
    /// <summary>
    /// True when <paramref name="candidate"/> may be given work on <paramref name="ticket"/>.
    /// A null candidate — no such user — is false, so a caller that resolved nobody takes the same
    /// branch as one that resolved someone unsuitable.
    /// </summary>
    public static bool IsEligible(User? candidate, Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return candidate is not null
            && candidate.Role != UserRole.Customer
            && candidate.IsActive
            && candidate.DepartmentId == ticket.DepartmentId;
    }

    /// <summary>The stable error slug of docs/api-design.md §6.12. <b>Not minted here</b> — Story 05 introduced it.</summary>
    public const string OutOfDepartment = "assignee-out-of-department";

    public const string OutOfDepartmentMessage =
        "That agent is not in this ticket's department.";
}
