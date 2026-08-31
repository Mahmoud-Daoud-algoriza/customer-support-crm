using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// The customer's own side of a ticket — <c>POST /portal/tickets</c>, the <b>web form</b> of
/// requirements §3.5 (T2-B, docs/api-design.md §5.7).
///
/// <para>
/// <b>Authenticated submission only.</b> There is no anonymous path (<b>A-9</b>): the endpoint sits
/// behind <c>RequireCustomer</c>, and the profile the ticket is linked to is the caller's own —
/// read from <see cref="ICurrentUser"/>, never from the body.
/// </para>
///
/// <para>
/// <b>It creates nothing itself.</b> Every rule a new ticket obeys — the A-14 department
/// derivation, the A-3 SLA arithmetic frozen by A-20, the <c>Created</c> activity row and the T2-D
/// auto-assignment seam — lives in <c>TicketService.CreateCoreAsync</c>, which the staff endpoint
/// uses too. This service supplies the three portal-specific decisions and nothing more.
/// </para>
///
/// <para>
/// <b>Story 13 completes this class</b> with the remaining §5.7 reads and actions (own ticket list,
/// detail, transition, feedback). Story 07 publishes submission and the thread only.
/// </para>
/// </summary>
public sealed class PortalTicketService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketService tickets)
{
    /// <summary>
    /// <c>POST /portal/tickets</c>.
    ///
    /// <para>
    /// <b>Three fields the customer cannot supply, each settled here:</b>
    /// </para>
    /// <list type="number">
    ///   <item><description><b><c>customerId</c></b> — the caller's own profile (docs/api-design.md §7).</description></item>
    ///   <item><description><b><c>departmentId</c></b> — <see langword="null"/> is passed to the creation path, which derives it from the category map (<b>A-14</b>). There is no override on this path: <em>"customers do not choose a department"</em>.</description></item>
    ///   <item><description><b><c>priority</c></b> — <see cref="SubmittedPriority"/>. See its remarks: no approved document states one, and this is finding <b>I-25</b>.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b><c>isUrgent</c> is accepted, stored, and changes nothing else</b> (<b>A-17</b>). It is
    /// explicitly <em>not</em> mapped onto priority: agents and the AI suggestion may use it when
    /// deciding priority, which is a human decision, not a derivation.
    /// </para>
    ///
    /// <para>
    /// <b>The originating text becomes <c>Ticket.description</c>, not a first
    /// <c>TicketMessage</c></b> (docs/data-model.md §2.6: <em>"Replies are <c>TicketMessage</c>
    /// rows, not copies of this"</em>). A thread that opened with a duplicate of the description
    /// would make every summary and every reply-suggestion read it twice.
    /// </para>
    /// </summary>
    public async Task<PortalTicketDto> SubmitAsync(
        SubmitPortalTicketRequest request, CancellationToken ct)
    {
        // A-9 / DM-1: a Customer-role caller always has a profile. Null here would mean a login
        // whose CustomerId was never set, which DM-1 does not produce — a wiring fault, not a user
        // error, so it is not a Problem Details case.
        var customerId = currentUser.CustomerId
            ?? throw new InvalidOperationException(
                "A Customer-role caller reached the portal with no linked profile (DM-1).");

        var ticket = await tickets.CreateCoreAsync(
            customerId,

            // A-14: no override. The category chooses the department, and the customer never sees
            // which one it was — GET /config publishes code and name only (AP-17).
            departmentIdOverride: null,
            request.Subject,
            request.Description,
            request.CategoryCode,
            SubmittedPriority,
            request.IsUrgent,
            ct);

        await db.SaveChangesAsync(ct);

        return ToDto(ticket);
    }

    /// <summary>
    /// The priority a customer-submitted ticket is created with.
    ///
    /// <para>
    /// <b>Finding I-25 — no approved document states one.</b> <c>Ticket.priority</c> is required
    /// (docs/data-model.md §2.6) and both SLA due timestamps are computed from it at creation (A-3),
    /// so a value must exist the moment the ticket does. But <b>A-6</b> says priority is <em>"set by
    /// the agent or by the AI suggestion"</em> and <b>A-17</b> forbids <c>isUrgent</c> from setting
    /// it — which leaves the value at creation undefined by every source.
    /// </para>
    ///
    /// <para>
    /// <b><c>Medium</c> is the smallest defensible reading:</b> it is the neutral middle of A-6's
    /// four-level scale and therefore the value that claims least before an agent or the AI has
    /// decided. It is a constant rather than a configuration key because architecture §6.3's
    /// configuration table has no such entry, and adding one would be new contract surface.
    /// </para>
    ///
    /// <para>
    /// <b>If the user reads it differently</b> — <c>Low</c> for "not yet triaged", or a configured
    /// default — the change is this one line plus its test. Nothing else in the codebase depends on
    /// which value it is.
    /// </para>
    /// </summary>
    private const TicketPriority SubmittedPriority = TicketPriority.Medium;

    /// <summary>
    /// <c>Ticket (portal)</c> — docs/api-design.md §6.4. Built from the entity rather than re-read,
    /// because <see cref="SubmitAsync"/> has just written it and every member is on the row.
    /// </summary>
    private static PortalTicketDto ToDto(Ticket ticket) =>
        new(
            ticket.Id,
            ticket.Subject,
            ticket.Description,
            ticket.CategoryCode,
            ticket.Status.ToString(),
            ticket.IsUrgent,
            ticket.CreatedAt,
            ticket.ResolvedAt,

            // Story 13 replaces this with the CustomerFeedback existence check. A ticket that was
            // created one statement ago has none under any implementation.
            HasFeedback: false);
}
