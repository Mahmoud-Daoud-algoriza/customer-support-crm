using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>Department-level ticket access — the one implementation of docs/architecture.md §4.3.</b>
/// 00-implementation-plan §6 names this file as the single home of the rule, and §4.3 opens with
/// why: <em>"This is the rule most likely to be got wrong, so it gets one implementation and one
/// test suite."</em>
///
/// <para>
/// The rule (A-2, A-4):
/// <list type="table">
///   <item><term>Customer</term><description>Only tickets whose customer is themselves</description></item>
///   <item><term>Agent</term><description>Only tickets in their own department</description></item>
///   <item><term>Manager</term><description>All departments</description></item>
///   <item><term>Administrator</term><description>All departments</description></item>
/// </list>
/// </para>
///
/// <para>
/// <b>1. Every ticket query composes this</b> — list, detail, dashboard, report, portal, AI assist,
/// export. It is the single place the table above is expressed in code, so a later story adds a
/// screen by composing it rather than by restating it.
/// </para>
///
/// <para>
/// <b>2. Write paths re-check on load.</b> Loading a ticket for modification goes through
/// <see cref="LoadScopedAsync"/>, which composes <see cref="ForCaller"/> and throws
/// <see cref="NotFoundException"/> when the row is absent <b>or</b> out of scope —
/// <b>fetch-then-authorize, never authorize-then-fetch-by-id</b> (§4.3 point 3). An agent cannot
/// act on an out-of-department ticket by guessing its identifier.
/// </para>
///
/// <para>
/// <b>3. No caller-supplied department id, customer id or role is ever trusted.</b> Identity comes
/// from <see cref="ICurrentUser"/> alone (§4.3 point 1). A <c>departmentId</c> <em>filter</em>
/// narrows within what the caller may already see and can never widen it; another department's id
/// is <b>not an error — it simply matches nothing</b> (docs/api-design.md §4.3).
/// </para>
///
/// <para>
/// <b>4. <c>Branch</c> appears in no predicate in this file, and must never be added to it.</b>
/// Branch is a reporting and filtering attribute only (A-2, T2-K, docs/data-model.md §5
/// constraint 6); an agent sees in-department tickets regardless of the customer's branch. The
/// <c>Ticket</c> entity has no branch member at all, which makes the mistake unspellable here.
/// </para>
///
/// <para>
/// <b>5. No EF global query filter is used</b> (AD-5). §4.3's rejected alternative gives four
/// reasons: the filter is role-dependent, Managers and Administrators must bypass it, reporting
/// aggregates must not be silently narrowed, and <b>a filter that is silently absent fails open</b>.
/// An explicit helper a reader can find and a test can target is safer than an invisible one.
/// </para>
/// </summary>
public static class TicketScope
{
    /// <summary>
    /// Narrows a ticket query to what the caller may see. Composes onto any
    /// <see cref="IQueryable{T}"/> of <see cref="Ticket"/> and translates to SQL.
    /// <para>
    /// <b>A <c>Customer</c> with no linked profile matches nothing</b>, which is the correct answer
    /// rather than an error: <c>CustomerId</c> is null only in a state DM-1 does not produce for a
    /// signed-in customer, and returning every ticket would be the failure mode that matters.
    /// </para>
    /// </summary>
    public static IQueryable<Ticket> ForCaller(this IQueryable<Ticket> query, ICurrentUser caller)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(caller);

        return caller.Role switch
        {
            // Own tickets only. Comparing against a null CustomerId yields no rows — fail closed.
            UserRole.Customer => query.Where(t => t.CustomerId == caller.CustomerId),

            // Own department only. Same fail-closed property if DepartmentId were ever null.
            UserRole.Agent => query.Where(t => t.DepartmentId == caller.DepartmentId),

            // Manager, Administrator — unrestricted across departments (A-4).
            _ => query,
        };
    }

    /// <summary>
    /// Loads one ticket <b>for modification</b>, through the same scope a read uses.
    ///
    /// <para>
    /// <b>Absent and out-of-scope are the same answer: <c>404</c></b> (AP-4). Distinguishing them
    /// would let a caller confirm that a ticket exists in a department they cannot see, which is
    /// exactly what AP-4 exists to prevent — so this method cannot tell them apart either, and the
    /// message is one constant.
    /// </para>
    ///
    /// <para>
    /// The result is <b>tracked</b>, because every caller mutates it and commits in the same unit of
    /// work (docs/architecture.md §3). Read paths use <see cref="ForCaller"/> with
    /// <c>AsNoTracking</c> instead.
    /// </para>
    /// </summary>
    public static async Task<Ticket> LoadScopedAsync(
        this IQueryable<Ticket> query, Guid id, ICurrentUser caller, CancellationToken ct)
    {
        var ticket = await query.ForCaller(caller).FirstOrDefaultAsync(t => t.Id == id, ct);

        return ticket ?? throw new NotFoundException(NotFound);
    }

    /// <summary>
    /// One message for "no such ticket" and "not yours". A distinct message per case would undo
    /// AP-4 at the Problem Details layer, after this method had got it right.
    /// </summary>
    public const string NotFound = "Ticket not found.";
}
