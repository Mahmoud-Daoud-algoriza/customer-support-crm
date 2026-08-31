using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Tickets;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// <b>Round-robin automatic assignment</b> — T2-D's second line, at the depth A-3 and T2-D ask for
/// and no deeper: <em>"round-robin across active agents in the ticket's department. No skills, no
/// load balancing, no capacity rules."</em>
///
/// <para>
/// It replaces Story 05's <c>NoAutoAssignmentPolicy</c> at the seam <c>TicketService.CreateAsync</c>
/// already calls — <b>the creation path needed no edit</b>, which is why Story 05 named the extension
/// point instead of leaving Story 09 to find a place to stand.
/// </para>
///
/// <para>
/// <b>It sets nothing. It chooses.</b> The caller assigns and records the activity row; this type is
/// a query with a decision in it, which is what makes it testable without a ticket to mutate and
/// keeps the notification in one place rather than two.
/// </para>
///
/// <para>
/// <b>It does not change status. The ticket stays <c>New</c></b> (A-18) — it cannot do otherwise,
/// because it returns a user id and touches no ticket. That matters beyond tidiness: the customer's
/// cancellation window depends on <c>New</c> surviving assignment (A-16).
/// </para>
///
/// <h3>The rotation rule, and why this one</h3>
/// <b>Least-recently-assigned wins</b>: of the eligible agents, the one whose most recent assigned
/// ticket is oldest — and an agent who has never been assigned anything sorts first. Successive
/// tickets therefore go to different agents, which is the property T2-D actually names.
///
/// <para>
/// The alternative the plan offers is a per-department cursor. This one was chosen because it holds
/// <b>no state of its own</b>: there is no counter to persist, migrate, reset or get out of step with
/// reality when a ticket is reassigned manually or an agent is deactivated. The rotation is derived
/// from the assignments themselves, so it is always consistent with what actually happened.
/// </para>
///
/// <para>
/// <b>No eligible candidate leaves the ticket unassigned, and that is a normal outcome</b> — not an
/// error and not an exception. A department with no active agent is a real configuration; an agent
/// assigns manually later.
/// </para>
/// </summary>
public sealed class RoundRobinAssignmentPolicy(IApplicationDbContext db) : IAutoAssignmentPolicy
{
    public async Task<Guid?> ChooseAssigneeAsync(Ticket ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        // Eligibility is exactly T2-D's three conditions, and the `User(departmentId, isActive)`
        // index serves them. `Role >= Agent` uses the A-4 ranking, so a Manager or Administrator
        // sitting in a department is eligible too — they are staff who work tickets.
        var candidates = await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive
                && u.DepartmentId == ticket.DepartmentId
                && u.Role != UserRole.Customer)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        // The most recent assignment per candidate, in one query rather than one per agent.
        // `Ticket(assignedUserId, createdAt)` is the index this rides on (docs/data-model.md §6).
        var lastAssigned = await db.Tickets
            .AsNoTracking()
            .Where(t => t.AssignedUserId != null && candidates.Contains(t.AssignedUserId.Value))
            .GroupBy(t => t.AssignedUserId!.Value)
            .Select(g => new { UserId = g.Key, LastAt = g.Max(t => t.CreatedAt) })
            .ToDictionaryAsync(x => x.UserId, x => x.LastAt, ct);

        // Never-assigned first (DateTimeOffset.MinValue), then oldest-first. The id is a
        // deterministic tiebreak: without it, two agents who have never been assigned anything would
        // be separated by whatever order the provider happened to return, and "successive tickets go
        // to different agents" would hold only by luck.
        return candidates
            .OrderBy(id => lastAssigned.TryGetValue(id, out var at) ? at : DateTimeOffset.MinValue)
            .ThenBy(id => id)
            .First();
    }
}
