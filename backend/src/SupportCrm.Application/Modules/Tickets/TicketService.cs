using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// Ticket creation, listing, reading, patching and assignment — the five endpoints of
/// docs/api-design.md §5.6 that Story 05 publishes.
///
/// <para>
/// <b>Every read and every write composes <see cref="TicketScope"/></b> (docs/architecture.md §4.3
/// point 2). Writes go through <c>LoadScopedAsync</c>, so an out-of-department ticket is a
/// <c>404</c> before any modification logic runs — fetch-then-authorize, never
/// authorize-then-fetch-by-id.
/// </para>
///
/// <para>
/// <b>There is no status method here</b>, and that is deliberate: transitions are Story 06's
/// dedicated endpoint, behind A-5 legality and A-16 authority (AP-1). A patchable status would
/// route around both, so <see cref="Ticket"/> exposes no status mutator for this story to call.
/// </para>
///
/// <para>
/// <b>One unit of work per operation, committed once</b> (docs/architecture.md §3). The ticket and
/// its history entry commit together; no explicit transaction is opened, because one
/// <c>SaveChangesAsync</c> already is one.
/// </para>
/// </summary>
public sealed class TicketService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketActivityRecorder activity,
    IAutoAssignmentPolicy autoAssignment,
    IOptions<CategoryOptions> categoryOptions,
    IOptions<SlaTargetOptions> slaTargetOptions,
    TimeProvider clock)
{
    /// <summary>
    /// The sort whitelist for <c>GET /tickets</c> (AP-15). Anything not listed is a <c>400</c>.
    /// <para>
    /// Copied from docs/api-design.md §5.6, which enumerates it:
    /// <em>"Sort whitelist: <c>resolutionDueAt</c>, <c>firstResponseDueAt</c>, <c>createdAt</c>,
    /// <c>priority</c>"</em>. Adding a fifth field is a contract change.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> SortableFields = new(StringComparer.Ordinal)
    {
        ["resolutionDueAt"] = nameof(Ticket.ResolutionDueAt),
        ["firstResponseDueAt"] = nameof(Ticket.FirstResponseDueAt),
        ["createdAt"] = nameof(Ticket.CreatedAt),
        ["priority"] = nameof(Ticket.Priority),
    };

    /// <summary>
    /// <b>Default sort is SLA urgency</b> — <c>resolutionDueAt:asc</c> with breached tickets first
    /// (docs/api-design.md §5.6), which is what T1-C's queue requires.
    /// </summary>
    private const string DefaultSortField = nameof(Ticket.ResolutionDueAt);

    /// <summary>The literal §5.6 accepts on <c>assigneeId</c> to mean the caller's own queue.</summary>
    private const string MeToken = "me";

    // ------------------------------------------------------------------------------ reads

    /// <summary>
    /// <c>GET /tickets</c> — paged, filtered, and <b>scoped before anything else happens</b>.
    /// <para>
    /// The scope is applied first so no filter can widen it: a <c>departmentId</c> filter narrows
    /// within what the caller may already see, and another department's id simply matches nothing
    /// (docs/api-design.md §4.3).
    /// </para>
    /// </summary>
    public async Task<PagedResult<TicketListItemDto>> ListAsync(
        TicketListFilter filter, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();
        var (sortField, descending) = page.ParseSort(SortableFields, DefaultSortField);

        // Scope first. Everything below narrows within this and can never widen it.
        var scoped = db.Tickets.AsNoTracking().ForCaller(currentUser);

        var query =
            from t in scoped
            join c in db.Customers.AsNoTracking() on t.CustomerId equals c.Id
            select new { Ticket = t, Customer = c };

        // Different parameters AND together (docs/api-design.md §2.1).
        if (TicketStatusParser.ParseOptional(filter.Status) is { } status)
        {
            query = query.Where(x => x.Ticket.Status == status);
        }

        if (TicketPriorityParser.ParseOptional(filter.Priority) is { } priority)
        {
            query = query.Where(x => x.Ticket.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(filter.CategoryCode))
        {
            var code = filter.CategoryCode.Trim();
            query = query.Where(x => x.Ticket.CategoryCode == code);
        }

        if (ResolveAssigneeFilter(filter.AssigneeId) is { } assigneeId)
        {
            query = query.Where(x => x.Ticket.AssignedUserId == assigneeId);
        }

        // Narrows only — the scope above already fixed the ceiling.
        if (filter.DepartmentId is { } departmentId)
        {
            query = query.Where(x => x.Ticket.DepartmentId == departmentId);
        }

        if (filter.Breached is { } breached)
        {
            query = breached
                ? query.Where(x => x.Ticket.FirstResponseBreached || x.Ticket.ResolutionBreached)
                : query.Where(x => !x.Ticket.FirstResponseBreached && !x.Ticket.ResolutionBreached);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = filter.Q.Trim();
            query = query.Where(x =>
                x.Ticket.Subject.Contains(term) || x.Ticket.Description.Contains(term));
        }

        // Breached tickets first, then the chosen field — the default is SLA urgency
        // (docs/api-design.md §5.6), which is what T1-C's queue requires. The breach-first ordering
        // rides on the DEFAULT sort only: an explicit `createdAt:desc` means what it says, and
        // quietly reordering it would make the whitelist a suggestion.
        var breachedFirst = sortField == DefaultSortField;

        var ordered = breachedFirst
            ? query.OrderByDescending(x => x.Ticket.FirstResponseBreached || x.Ticket.ResolutionBreached)
            : query.OrderBy(x => 0);

        ordered = (sortField, descending) switch
        {
            (nameof(Ticket.FirstResponseDueAt), false) => ordered.ThenBy(x => x.Ticket.FirstResponseDueAt),
            (nameof(Ticket.FirstResponseDueAt), true) => ordered.ThenByDescending(x => x.Ticket.FirstResponseDueAt),
            (nameof(Ticket.CreatedAt), false) => ordered.ThenBy(x => x.Ticket.CreatedAt),
            (nameof(Ticket.CreatedAt), true) => ordered.ThenByDescending(x => x.Ticket.CreatedAt),
            // Priority sorts by SEVERITY, not alphabetically. The column stores stable string
            // codes (docs/api-design.md §2), so ordering it directly would yield
            // "High, Low, Medium, Urgent" — an ordering nobody asked for. The rank below is the
            // enum's own declaration order, which is the severity order Story 06's "escalation
            // raises priority exactly one level" already depends on. Recorded as finding I-14.
            (nameof(Ticket.Priority), false) => ordered.ThenBy(x => SeverityRank(x.Ticket.Priority)),
            (nameof(Ticket.Priority), true) => ordered.ThenByDescending(x => SeverityRank(x.Ticket.Priority)),
            (_, true) => ordered.ThenByDescending(x => x.Ticket.ResolutionDueAt),
            _ => ordered.ThenBy(x => x.Ticket.ResolutionDueAt),
        };

        // A stable tiebreak, without which paging is not meaningful when two rows share a due date.
        query = ordered.ThenBy(x => x.Ticket.Id);

        return await query
            .Select(x => new TicketListItemDto(
                x.Ticket.Id,
                x.Ticket.Subject,
                new TicketListCustomerDto(x.Customer.Id, x.Customer.FullName),
                x.Ticket.Status.ToString(),
                x.Ticket.Priority.ToString(),
                x.Ticket.CategoryCode,
                x.Ticket.DepartmentId,
                x.Ticket.AssignedUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == x.Ticket.AssignedUserId)
                        .Select(u => new UserSummaryDto(u.Id, u.DisplayName))
                        .FirstOrDefault(),
                x.Ticket.CreatedAt,
                x.Ticket.ResolutionDueAt,
                x.Ticket.FirstResponseBreached,
                x.Ticket.ResolutionBreached))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>GET /tickets/{id}</c>. Out of scope returns <c>404</c>, worded identically to a missing
    /// ticket (AP-4).
    /// </summary>
    public async Task<TicketDto> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.Tickets.AsNoTracking()
            .ForCaller(currentUser)
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Ticket = t,
                Customer = db.Customers.Where(c => c.Id == t.CustomerId)
                    .Select(c => new TicketCustomerDto(c.Id, c.FullName, c.Email)).First(),
                Assignee = t.AssignedUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == t.AssignedUserId)
                        .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).FirstOrDefault(),
                CreatedBy = db.Users.Where(u => u.Id == t.CreatedByUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
            })
            .FirstOrDefaultAsync(ct);

        if (dto is null)
        {
            throw new NotFoundException(TicketScope.NotFound);
        }

        return ToDto(dto.Ticket, dto.Customer, dto.Assignee, dto.CreatedBy);
    }

    // ----------------------------------------------------------------------------- writes

    /// <summary>
    /// <c>POST /tickets</c> — an agent creating a ticket on behalf of a customer.
    ///
    /// <para>
    /// <b>The order of operations is A-14's and must not be reordered:</b> validate the category,
    /// resolve the department, validate the customer, compute the SLA clock, persist as <c>New</c>,
    /// write the <c>Created</c> activity row, then offer the auto-assignment seam.
    /// </para>
    ///
    /// <para>
    /// <b><c>isUrgent</c> is never accepted here</b> (A-17): the request model has no such property,
    /// so a body carrying one is a <c>400</c> (AP-10). The ticket is created with <c>isUrgent =
    /// false</c>; Story 07's portal endpoint sets it.
    /// </para>
    /// </summary>
    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        // 1. Validate categoryCode against the configured list — unknown is 400 (§5 constraint 11).
        var category = FindCategory(request.CategoryCode);

        // 2. Resolve departmentId: a supplied value wins for staff, otherwise the A-14 map.
        //    A-14: "the mapping is the default, not a cage".
        var departmentId = request.DepartmentId ?? category.DepartmentId;

        // A department id in a request body is data to validate, never an identity to trust
        // (docs/architecture.md §4.3 point 1).
        if (!await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
        {
            throw new ValidationException($"Unknown department '{departmentId}'.");
        }

        // 3. Validate customerId exists.
        var customerId = request.CustomerId!.Value;
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new ValidationException($"Unknown customer '{customerId}'.");
        }

        var priority = TicketPriorityParser.Parse(request.Priority);

        // 4. Compute both due timestamps — required and non-null (§2.6), so they are computed here
        //    rather than by Story 09. A-20 freezes them from this moment on.
        var createdAt = clock.GetUtcNow();
        var (firstResponseDueAt, resolutionDueAt) =
            SlaClock.ComputeAtCreation(createdAt, priority, TargetsFor(priority));

        // 5. Persist with status = New and isUrgent = false (A-17: staff create never accepts it).
        var ticket = Ticket.Create(
            Guid.NewGuid(),
            customerId,
            departmentId,
            request.Subject,
            request.Description,
            category.Code,
            priority,
            currentUser.Id,
            createdAt,
            firstResponseDueAt,
            resolutionDueAt);

        db.Tickets.Add(ticket);

        // 6. The Created activity row, on the same path and in the same unit of work.
        await activity.RecordAsync(ticket.Id, TicketActivityType.Created, ct: ct);

        // 7. The auto-assignment seam. It is a no-op in this story — Story 09 replaces the policy
        //    with round-robin. It runs AFTER creation and BEFORE the response, which is where T2-D
        //    needs it, and it must not change status (A-18).
        if (await autoAssignment.ChooseAssigneeAsync(ticket, ct) is { } assigneeId)
        {
            ticket.Assign(assigneeId);
            await activity.RecordAsync(
                ticket.Id, TicketActivityType.Assigned, newValue: assigneeId.ToString(), ct: ct);
        }

        await db.SaveChangesAsync(ct);

        return await GetAsync(ticket.Id, ct);
    }

    /// <summary>
    /// <c>PATCH /tickets/{id}</c> — <b><c>categoryCode</c> and <c>priority</c> only</b>
    /// (docs/api-design.md §5.6). <c>status</c> is not a patchable field (AP-1).
    /// </summary>
    public async Task<TicketDto> PatchAsync(Guid id, PatchTicketRequest request, CancellationToken ct)
    {
        // The write path re-checks on load: out of scope is 404 before any modification logic.
        var ticket = await db.Tickets.LoadScopedAsync(id, currentUser, ct);

        if (!string.IsNullOrWhiteSpace(request.CategoryCode))
        {
            var category = FindCategory(request.CategoryCode);

            if (!string.Equals(category.Code, ticket.CategoryCode, StringComparison.Ordinal))
            {
                var previous = ticket.CategoryCode;
                ticket.ChangeCategory(category.Code);

                await activity.RecordAsync(
                    ticket.Id, TicketActivityType.CategoryChanged, previous, category.Code, ct: ct);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            var priority = TicketPriorityParser.Parse(request.Priority);

            if (priority != ticket.Priority)
            {
                var previous = ticket.Priority;
                ticket.ChangePriority(priority);

                // A-20 — the SLA due timestamps FREEZE. This call is deliberately kept even though
                // it does nothing: it is the one home of the rule, and Stories 06 and 09 call the
                // same method from their own priority-changing paths. Removing it would leave A-20
                // as something this code happens to satisfy rather than something it states.
                SlaClock.OnPriorityChanged(ticket, priority);

                await activity.RecordAsync(
                    ticket.Id,
                    TicketActivityType.PriorityChanged,
                    previous.ToString(),
                    priority.ToString(),
                    ct: ct);
            }
        }

        await db.SaveChangesAsync(ct);

        return await GetAsync(ticket.Id, ct);
    }

    /// <summary>
    /// <c>POST /tickets/{id}/assignment</c> — assign or reassign.
    ///
    /// <para>
    /// <b>Status is not touched</b> (A-18). A `New` ticket that gets an assignee stays `New`.
    /// </para>
    ///
    /// <para>
    /// The assignee must be an <b>active staff user whose department equals the ticket's</b>
    /// (docs/data-model.md §5 constraint 10) — otherwise <c>422 assignee-out-of-department</c>, so a
    /// ticket can never be assigned to someone who could not then see it.
    /// </para>
    ///
    /// <para>
    /// <b>An Agent may assign only within their own department; Manager and Administrator may
    /// reassign across departments</b> (the intake). For an Agent that rule is already total: the
    /// scoped load means they can only reach their own department's tickets, and the department
    /// check below means the assignee must be in that same department.
    /// </para>
    /// </summary>
    public async Task<TicketDto> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct)
    {
        var ticket = await db.Tickets.LoadScopedAsync(id, currentUser, ct);

        var assigneeId = request.AssignedUserId!.Value;

        var assignee = await db.Users.FirstOrDefaultAsync(u => u.Id == assigneeId, ct)
            ?? throw new UnprocessableException(OutOfDepartment, OutOfDepartmentMessage);

        // One message and one slug for every way the assignee is unsuitable — not in the ticket's
        // department, not active, or a Customer-role login. Distinguishing them would tell a caller
        // which staff accounts exist and in which departments.
        if (assignee.Role == UserRole.Customer ||
            !assignee.IsActive ||
            assignee.DepartmentId != ticket.DepartmentId)
        {
            throw new UnprocessableException(OutOfDepartment, OutOfDepartmentMessage);
        }

        var previousId = ticket.AssignedUserId;

        if (previousId == assigneeId)
        {
            // Re-assigning the same person changes nothing and writes no history row: an activity
            // trail of non-events is worse than no trail.
            return await GetAsync(ticket.Id, ct);
        }

        ticket.Assign(assigneeId);

        // The intake requires every assignment change to be recorded. Old and new carry display
        // names, so the history is readable without resolving ids (docs/data-model.md §2.7).
        var previousName = previousId is { } prior
            ? await db.Users.Where(u => u.Id == prior).Select(u => u.DisplayName).FirstOrDefaultAsync(ct)
            : null;

        await activity.RecordAsync(
            ticket.Id, TicketActivityType.Assigned, previousName, assignee.DisplayName, ct: ct);

        await db.SaveChangesAsync(ct);

        return await GetAsync(ticket.Id, ct);
    }

    // ----------------------------------------------------------------------------- helpers

    /// <summary>
    /// The configured category, or a <c>400</c> naming what is allowed (A-6, §5 constraint 11).
    /// Categories are configuration, never a table (docs/architecture.md §6.3).
    /// </summary>
    private CategoryOption FindCategory(string categoryCode)
    {
        var code = categoryCode?.Trim() ?? string.Empty;

        return categoryOptions.Value.Items
                   .FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
               ?? throw new ValidationException(
                   $"Unknown category '{categoryCode}'. Allowed values: " +
                   $"{string.Join(", ", categoryOptions.Value.Items.Select(c => c.Code))}.");
    }

    /// <summary>
    /// The configured targets for a priority, as the Domain wants them.
    /// <para>
    /// Startup validation already guarantees every priority has exactly one target with positive
    /// hours (ConfigurationValidator check 2), so a miss here would be a wiring fault rather than a
    /// user error — hence an exception that is not a Problem Details case.
    /// </para>
    /// </summary>
    private SlaTargets TargetsFor(TicketPriority priority)
    {
        var target = slaTargetOptions.Value.Items
                         .FirstOrDefault(t => string.Equals(
                             t.Priority, priority.ToString(), StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException(
                         $"No SLA target is configured for priority '{priority}'. Startup validation " +
                         "should have refused this configuration.");

        return new SlaTargets(target.FirstResponseHours, target.ResolutionHours);
    }

    /// <summary>
    /// <c>assigneeId=me</c> means the caller (docs/api-design.md §5.6) — the agent's own queue.
    /// An unparseable value is a <c>400</c> rather than a silently ignored filter.
    /// </summary>
    private Guid? ResolveAssigneeFilter(string? assigneeId)
    {
        if (string.IsNullOrWhiteSpace(assigneeId))
        {
            return null;
        }

        var value = assigneeId.Trim();

        if (string.Equals(value, MeToken, StringComparison.OrdinalIgnoreCase))
        {
            return currentUser.Id;
        }

        return Guid.TryParse(value, out var parsed)
            ? parsed
            : throw new ValidationException($"'{assigneeId}' is not a user id or the literal '{MeToken}'.");
    }

    /// <summary>
    /// Severity order for the <c>priority</c> sort, translated to a SQL <c>CASE</c>.
    /// <para>
    /// <b>Why this exists.</b> docs/api-design.md §5.6 puts <c>priority</c> in the sort whitelist,
    /// and §2 requires the column to store stable string codes — so a direct
    /// <c>ORDER BY [Priority]</c> is alphabetical: <c>High, Low, Medium, Urgent</c>. No approved
    /// document states the mechanism, and this is the only reading under which the whitelist entry
    /// is useful. The ranks are <see cref="TicketPriority"/>'s own declaration order. Finding I-14.
    /// </para>
    /// </summary>
    private static int SeverityRank(TicketPriority priority) =>
        priority == TicketPriority.Low ? 0
        : priority == TicketPriority.Medium ? 1
        : priority == TicketPriority.High ? 2
        : 3;

    private const string OutOfDepartment = "assignee-out-of-department";

    private const string OutOfDepartmentMessage =
        "That agent is not in this ticket's department.";

    private static TicketDto ToDto(
        Ticket t,
        TicketCustomerDto customer,
        UserSummaryDto? assignee,
        UserSummaryDto createdBy) =>
        new(
            t.Id,
            t.Subject,
            t.Description,
            customer,
            t.DepartmentId,
            t.CategoryCode,
            t.Priority.ToString(),
            t.Status.ToString(),
            t.IsUrgent,
            assignee,
            createdBy,
            t.CreatedAt,
            t.FirstResponseDueAt,
            t.ResolutionDueAt,
            t.FirstRespondedAt,
            t.ResolvedAt,
            t.ClosedAt,
            t.FirstResponseBreached,
            t.ResolutionBreached);
}
