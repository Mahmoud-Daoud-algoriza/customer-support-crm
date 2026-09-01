using Microsoft.EntityFrameworkCore;
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
/// <b>Story 13 completed this class</b> with the remaining §5.7 reads and actions — the customer's
/// own list, their own detail, and the two transitions A-16 gives them. <b>Feedback is its own
/// service</b> (<c>CustomerFeedbackService</c>): it is a different entity with a different
/// invariant, and folding it in here would put a write-once reporting fact behind a ticket reader.
/// </para>
///
/// <para>
/// <b>Every read here composes <c>TicketScope</c>, and none of them re-states ownership.</b> A
/// customer sees only their own tickets because <c>ForCaller</c> narrows on
/// <c>CustomerId == caller.CustomerId</c> — the same helper every staff read composes (AD-5,
/// docs/architecture.md §4.3). There is no second ownership predicate in this file to forget.
/// </para>
/// </summary>
public sealed class PortalTicketService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketService tickets,
    TicketLifecycleService lifecycle)
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

    // ------------------------------------------------------------------------------ reads

    /// <summary>
    /// <c>GET /portal/tickets</c> — <b>the caller's own requests, and nothing else</b>
    /// (docs/api-design.md §5.7, docs/ui-design.md §7.1).
    ///
    /// <para>
    /// <b>Ownership is <c>TicketScope.ForCaller</c>'s, applied first</b>, so the <c>status</c> filter
    /// narrows within it and can never widen it. There is no <c>customerId</c> parameter to supply
    /// and no ownership predicate written here — a customer's own set is the <em>scope</em>, not a
    /// filter (AD-5).
    /// </para>
    ///
    /// <para>
    /// <b>The sort whitelist has exactly one field, <c>createdAt</c></b> (§5.7, AP-15) — anything
    /// else is a <c>400</c> rather than silently ignored. <b>The default direction is descending</b>:
    /// no approved document fixes one for this endpoint, and newest-first is the direction §5.5
    /// already fixes for every other customer-facing list (notes, attachments, the timeline), so it
    /// is the reading that introduces no second convention. A client wanting the other order asks
    /// for <c>sort=createdAt:asc</c>.
    /// </para>
    /// </summary>
    public async Task<PagedResult<PortalTicketDto>> ListAsync(
        string? status, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();

        // Validated against the whitelist even though it holds one field: an unknown sort field is a
        // 400, never silently ignored (AP-15). The field itself is discarded because there is only
        // one thing it can be — the direction is what varies.
        var (_, descendingRequested) = page.ParseSort(SortableFields, DefaultSortField);

        // ParseSort defaults to ascending, which is the right default for a generic whitelist helper
        // and the wrong one here, so it is overridden when the caller supplied no sort at all. An
        // EXPLICIT sort is obeyed exactly as written.
        var descending = string.IsNullOrWhiteSpace(page?.Sort) ? DefaultDescending : descendingRequested;

        // Scope first. The filter below narrows within it and can never widen it.
        var query = db.Tickets.AsNoTracking().ForCaller(currentUser);

        if (TicketStatusParser.ParseOptional(status) is { } parsed)
        {
            query = query.Where(t => t.Status == parsed);
        }

        var ordered = descending
            ? query.OrderByDescending(t => t.CreatedAt)
            : query.OrderBy(t => t.CreatedAt);

        return await Project(ordered).ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>GET /portal/tickets/{id}</c> — the customer's own request.
    ///
    /// <para>
    /// <b>Another customer's id is a <c>404</c></b>, worded identically to one that does not exist
    /// (<b>AP-4</b>) — and it is the same <c>TicketScope.ForCaller</c> narrowing plus the same
    /// <c>TicketScope.NotFound</c> message <c>LoadScopedAsync</c> uses, so the two cannot be told
    /// apart. It is composed on an <c>AsNoTracking</c> query and projected in the database, because
    /// this is a read: the tracked <c>LoadScopedAsync</c> exists for write paths that mutate and
    /// commit (docs/architecture.md §4.3).
    /// </para>
    /// </summary>
    public async Task<PortalTicketDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await Project(db.Tickets.AsNoTracking().ForCaller(currentUser).Where(t => t.Id == id))
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException(TicketScope.NotFound);
    }

    // ----------------------------------------------------------------------------- writes

    /// <summary>
    /// <c>POST /portal/tickets/{id}/transition</c> — <b>cancel own while <c>New</c></b>, or
    /// <b>reopen own <c>Resolved</c></b> (docs/api-design.md §5.7).
    ///
    /// <para>
    /// <b>It delegates, and delegating is the decision.</b> Story 06's
    /// <c>TicketLifecycleService.TransitionAsync</c> already enforces the whole order §5.6 fixes —
    /// scope (<c>404</c>), then <b>A-16</b> authority (<c>403 transition-not-permitted</c>), then
    /// <b>A-5</b> legality (<c>409 illegal-transition</c>) — and writes the history row and the audit
    /// entry. <b>The customer's permitted set is not restated here</b>: it is two cells of the A-16
    /// matrix, whose one home is <c>TransitionAuthority.MayInvoke</c> — cancel while <c>New</c> (the
    /// window <b>A-18</b> keeps genuinely open, because an auto-assigned ticket is still <c>New</c>)
    /// and reopen a <c>Resolved</c> one. A second copy in this file could drift from it, and the
    /// drift would be a security bug in the quiet direction.
    /// </para>
    ///
    /// <para>
    /// <b>The staff DTO it returns is discarded and the ticket re-read as the portal one.</b> That
    /// costs one extra query and buys exactly what AP-5 is about: a portal endpoint <em>cannot</em>
    /// answer with an assignee, a department, a priority or an SLA field, because the type it returns
    /// has no such member (AP-16, UI-11).
    /// </para>
    ///
    /// <para>
    /// <b>A customer's <c>Pending → Open</c> does not come through here.</b> R-13 makes it automatic
    /// on a reply, which is why A-16 gives them no such invocation and docs/ui-design.md §7.3 forbids
    /// the UI from offering a manual reopen on a <c>Pending</c> request.
    /// </para>
    /// </summary>
    public async Task<PortalTicketDto> TransitionAsync(
        Guid ticketId, TicketStatus targetStatus, CancellationToken ct)
    {
        await lifecycle.TransitionAsync(ticketId, targetStatus, ct);

        return await GetAsync(ticketId, ct);
    }

    /// <summary>
    /// The sort whitelist for <c>GET /portal/tickets</c> (AP-15). docs/api-design.md §5.7 enumerates
    /// it as a single field, so a second entry here would be a contract change.
    /// </summary>
    private static readonly Dictionary<string, string> SortableFields = new(StringComparer.Ordinal)
    {
        ["createdAt"] = nameof(Ticket.CreatedAt),
    };

    private const string DefaultSortField = nameof(Ticket.CreatedAt);

    /// <summary>
    /// <b>Newest first</b> when the caller asks for no particular order.
    /// <para>
    /// No approved document fixes a default direction for this endpoint. Newest-first is the one
    /// docs/api-design.md §5.5 already fixes for every other customer-facing list — notes,
    /// attachments, the interaction timeline — so taking it here introduces no second convention,
    /// and it is what §7.1's card list wants: a customer opening the portal is far more often
    /// tracking their latest request than their first.
    /// </para>
    /// </summary>
    private const bool DefaultDescending = true;

    /// <summary>
    /// <c>Ticket (portal)</c> — docs/api-design.md §6.4 — as <b>one projection used by both reads</b>,
    /// so the list row and the detail payload cannot diverge. §6.4 defines them as one shape.
    ///
    /// <para>
    /// <b><c>hasFeedback</c> is an existence check, never a column</b> (docs/api-design.md §7,
    /// finding <b>N-4</b>). It translates to an <c>EXISTS</c> against the unique index on
    /// <c>CustomerFeedback.TicketId</c>, and it stays a projection because a stored flag would be a
    /// second answer to a question the feedback row already answers.
    /// </para>
    ///
    /// <para>
    /// <b>The members that are absent are the payload's design.</b> No assignee (<b>AP-16</b>), no
    /// department, no priority, no SLA or breach field, no internal anything (<b>UI-11</b>) — and
    /// they are absent from <see cref="PortalTicketDto"/> itself, so this projection could not leak
    /// one even by mistake. A test asserts it on the serialized JSON rather than on the type.
    /// </para>
    /// </summary>
    private IQueryable<PortalTicketDto> Project(IQueryable<Ticket> query) =>
        query.Select(t => new PortalTicketDto(
            t.Id,
            t.Subject,
            t.Description,
            t.CategoryCode,
            t.Status.ToString(),
            t.IsUrgent,
            t.CreatedAt,
            t.ResolvedAt,
            db.CustomerFeedback.Any(f => f.TicketId == t.Id)));

    /// <summary>
    /// <c>Ticket (portal)</c> from the entity <see cref="SubmitAsync"/> has just written — every
    /// member is on the row, so re-reading it would be a query for data already in hand.
    /// <para>
    /// <c>hasFeedback</c> is <see langword="false"/> here as a <b>fact, not a placeholder</b>: a
    /// ticket created one statement ago is <c>New</c>, has never reached <c>Resolved</c>, and
    /// therefore could not have been rated even in principle.
    /// </para>
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
            HasFeedback: false);
}
