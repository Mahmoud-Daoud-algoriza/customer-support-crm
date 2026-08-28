using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Application.Modules.Organization;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// Customer profiles — the four profile endpoints of docs/api-design.md §5.5 (Story 04 task 3).
/// <para>
/// <b>All four are <c>RequireAgent</c>.</b> "A Customer cannot browse the customer directory" is
/// the whole of that rule, and it is a <em>role gate on the endpoint</em>, not row filtering: a
/// customer profile is organization-wide and readable by any staff role
/// (docs/data-model.md §2.4). The policy is applied by the controller, which arrives with task 8;
/// there is deliberately no query filter here, and AD-5 keeps access scoping an explicit helper
/// rather than an EF filter in any case.
/// </para>
/// <para>
/// <b>Deleting a customer is not an application operation</b> (docs/data-model.md §2.4), so this
/// service has no delete method — not merely no endpoint. There is no merge or dedupe path either
/// (T2-A, docs/product-scope.md §8): a duplicate email is <em>rejected</em>, never reconciled.
/// </para>
/// <para>
/// <b>Branch is reached by an explicit join, not a navigation property.</b> No entity in this
/// codebase carries one (Story 02's <c>User</c> and Story 03's <c>Department</c> both go without),
/// and the join keeps that consistent while producing the same single SQL statement.
/// </para>
/// </summary>
public sealed class CustomerService(
    IApplicationDbContext db,
    IAuditRecorder audit,
    TimeProvider clock)
{
    /// <summary>
    /// The sort whitelist for <c>GET /customers</c> (AP-15). Anything not listed is a <c>400</c>.
    /// <para>
    /// Unlike <c>/users</c> and <c>/departments</c>, this set <b>is</b> enumerated by an approved
    /// document — docs/api-design.md §5.5 says <em>"Sort: <c>fullName</c>, <c>createdAt</c>"</em> —
    /// so it is copied, not drawn from the payload. Adding a third field is a contract change.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> SortableFields = new(StringComparer.Ordinal)
    {
        ["fullName"] = nameof(Customer.FullName),
        ["createdAt"] = nameof(Customer.CreatedAt),
    };

    /// <summary>Default sort — a stable order, without which paging is not meaningful.</summary>
    private const string DefaultSortField = nameof(Customer.FullName);

    /// <summary>
    /// <c>GET /customers</c> — paged, filtered by <c>q</c> and <c>branchId</c>
    /// (docs/api-design.md §5.5).
    /// </summary>
    public async Task<PagedResult<CustomerListItemDto>> ListAsync(
        CustomerListFilter filter, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();
        var (sortField, descending) = page.ParseSort(SortableFields, DefaultSortField);

        // An anonymous type, not a named record: EF translates member access on an anonymous
        // projection natively, whereas a `new SomeRecord(c, b).Customer.FullName` in a later Where
        // is not something the provider can see through.
        //
        // An INNER join is correct rather than lossy: Customer.BranchId is required and carries a
        // foreign key (A-2, docs/data-model.md §2.4), so a customer without a branch is not a state
        // the schema permits.
        var query =
            from c in db.Customers.AsNoTracking()
            join b in db.Branches.AsNoTracking() on c.BranchId equals b.Id
            select new { Customer = c, Branch = b };

        // Different parameters AND together (docs/api-design.md §2.1).
        if (filter.BranchId is { } branchId)
        {
            query = query.Where(x => x.Customer.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = filter.Q.Trim();
            query = query.Where(x =>
                x.Customer.FullName.Contains(term) || x.Customer.Email.Contains(term));
        }

        query = (sortField, descending) switch
        {
            (nameof(Customer.CreatedAt), false) => query.OrderBy(x => x.Customer.CreatedAt),
            (nameof(Customer.CreatedAt), true) => query.OrderByDescending(x => x.Customer.CreatedAt),
            (_, true) => query.OrderByDescending(x => x.Customer.FullName),
            _ => query.OrderBy(x => x.Customer.FullName),
        };

        return await query
            .Select(x => new CustomerListItemDto(
                x.Customer.Id,
                x.Customer.FullName,
                x.Customer.Email,
                x.Customer.Phone,
                new BranchDto(x.Branch.Id, x.Branch.Name),

                // Story 05: replace the constant 0 with the ticket subquery — an aggregate over this
                // customer's NON-TERMINAL tickets (docs/api-design.md §6.3). Ticket does not exist
                // yet, so the column is a literal and the list is honest about knowing nothing.
                //
                // Story 05 task 6 owns this line. COMPUTE IT IN ONE GROUPED SUBQUERY, not one query
                // per row: the directory is a paged list, and a per-row count would be N+1 by
                // construction.
                0))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>GET /customers/{id}</c> — docs/api-design.md §6.3. <c>branch</c> is the nested
    /// <c>{ id, name }</c> of §6.2, reusing <see cref="BranchDto"/> so the two payloads cannot
    /// drift apart.
    /// </summary>
    public async Task<CustomerDto> GetAsync(Guid id, CancellationToken ct) =>
        await (from c in db.Customers.AsNoTracking().Where(c => c.Id == id)
               join b in db.Branches.AsNoTracking() on c.BranchId equals b.Id
               select new CustomerDto(
                   c.Id,
                   c.FullName,
                   c.Email,
                   c.Phone,
                   new BranchDto(b.Id, b.Name),
                   c.ExternalReference,
                   c.CreatedAt))
            .SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Customer not found.");

    /// <summary>
    /// <c>POST /customers</c> — an agent creating a profile.
    /// <para>
    /// This path creates <b>no <c>User</c></b>: a customer who has never signed in has a profile and
    /// no login, which DM-1 makes the ordinary case, because an agent creates tickets on behalf of
    /// customers who may never touch the portal. A login appears only if that person later
    /// registers, and A-15 then links it to this profile rather than making a second customer.
    /// </para>
    /// </summary>
    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        // BranchId is [Required] on the request, so a missing value is already a 400 at binding.
        var branchId = request.BranchId!.Value;
        var email = request.Email.Trim();

        // Checked before insert so a duplicate is the 409 slug docs/api-design.md §5.5 specifies,
        // rather than a unique-index violation surfacing as a 500.
        //
        // The comparison relies on the COLUMN COLLATION, not on ToLower(): docs/data-model.md §6.1
        // declares SQL_Latin1_General_CP1_CI_AS on Customer.Email precisely so "two addresses
        // differing only in case are the same address" (A-10) is a property of the schema. Lowering
        // in C# would also defeat the unique index this query seeks on.
        if (await db.Customers.AnyAsync(c => c.Email == email, ct))
        {
            throw new ConflictException(
                "customer-email-in-use", "A customer with that email already exists.");
        }

        await EnsureBranchExistsAsync(branchId, ct);

        var customer = Customer.Create(
            id: Guid.NewGuid(),
            fullName: request.FullName,
            email: email,
            phone: request.Phone,
            branchId: branchId,
            createdAt: clock.GetUtcNow());

        db.Customers.Add(customer);

        // NOT audited. Creating a customer profile is business data, not a security event (AD-10) —
        // the one thing in this service that is audited is a change to a sign-in identifier.
        await db.SaveChangesAsync(ct);

        return await GetAsync(customer.Id, ct);
    }

    /// <summary>
    /// <c>PATCH /customers/{id}</c> — applies only the fields present, because a PATCH carries only
    /// what is changing (docs/api-design.md §2).
    ///
    /// <para>
    /// <b>A-19 — a customer's email and their portal sign-in are one address.</b> Changing
    /// <c>Customer.Email</c> sets a linked <c>User.Email</c> to the same value <b>in the same unit
    /// of work</b>, so there is no committed state in which the two differ
    /// (docs/product-scope.md A-19, docs/api-design.md §5.5, docs/data-model.md §5 constraints 1a
    /// and 1b). The six cases, exactly as the story plan tabulates them:
    /// </para>
    ///
    /// <list type="table">
    ///   <item><term>No <c>email</c> in the request</term><description>Nothing to do. No audit</description></item>
    ///   <item><term>Equal to the current one, case-insensitively</term><description>No-op for both rows. <b>Not</b> a <c>409</c> against the customer's own record. No audit — nothing changed</description></item>
    ///   <item><term>Another customer holds it</term><description><c>409 customer-email-in-use</c>. No audit — nothing was written</description></item>
    ///   <item><term>Another user holds it, staff included</term><description><c>409 user-already-exists</c> — PF-6's existing slug for PF-6's existing rule. No audit — nothing was written</description></item>
    ///   <item><term>Free, and a linked login exists</term><description>Both rows change, then <b>one</b> <c>SaveChangesAsync</c>. <b>Exactly one</b> <c>UserEmailChanged</c> entry</description></item>
    ///   <item><term>Free, and no linked login</term><description><c>Customer.Email</c> only. The ordinary case (DM-1). No audit — no login changed</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Atomicity is the existing rule, not a new mechanism.</b> docs/architecture.md §3 already
    /// gives one unit of work per request, owned by the Application service and committed once. Both
    /// tracked entities are mutated and committed together. <b>No explicit transaction is opened and
    /// <c>SaveChangesAsync</c> is not called twice</b> — two commits are exactly the divergence A-19
    /// exists to prevent.
    /// </para>
    ///
    /// <para>
    /// <b>The caller's session is unaffected.</b> The token asserts identity only and carries
    /// <c>sub</c>, never an email (AD-7), so a signed-in customer whose address changes mid-session
    /// is not signed out — they simply sign in with the new address next time.
    /// </para>
    /// </summary>
    public async Task<CustomerDto> UpdateAsync(
        Guid id, PatchCustomerRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Customer not found.");

        if (request.FullName is not null)
        {
            customer.Rename(request.FullName);
        }

        // Absent means "leave unchanged"; a present-but-blank phone clears it, which the domain
        // normalizes to null. The two are distinguishable because the property is nullable and a
        // PATCH carries only what is changing.
        if (request.Phone is not null)
        {
            customer.ChangePhone(request.Phone);
        }

        if (request.BranchId is { } branchId && branchId != customer.BranchId)
        {
            await EnsureBranchExistsAsync(branchId, ct);
            customer.ChangeBranch(branchId);
        }

        await ApplyEmailChangeAsync(customer, request.Email, ct);

        // THE one commit. Profile fields, a propagated User.Email and its audit entry all land
        // together or not at all (docs/architecture.md §3).
        await db.SaveChangesAsync(ct);

        return await GetAsync(customer.Id, ct);
    }

    /// <summary>
    /// The A-19 case table, in the order the story plan states it. Mutates tracked entities and
    /// records the audit entry; <b>it does not commit</b> — <see cref="UpdateAsync"/> owns the
    /// single <c>SaveChangesAsync</c>.
    /// </summary>
    private async Task ApplyEmailChangeAsync(
        Customer customer, string? requestedEmail, CancellationToken ct)
    {
        // Case 1 — absent. A PATCH carries only what changes.
        if (requestedEmail is null)
        {
            return;
        }

        var newEmail = requestedEmail.Trim();

        // Case 2 — the same address in a different case. A no-op for both rows, and explicitly NOT a
        // 409 against the customer's own record: the duplicate check below would otherwise match
        // this very customer on a case-only edit. Compared in memory with OrdinalIgnoreCase, so the
        // rule holds identically on SQL Server and on the SQLite the test host runs.
        if (string.Equals(newEmail, customer.Email, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Found by User.CustomerId, NEVER by matching on the old email: §5 constraint 3 guarantees
        // at most one, and an email match would be the very assumption A-19 removes. Looked up
        // BEFORE anything is written, in this same unit of work.
        var linkedUser = await db.Users.SingleOrDefaultAsync(u => u.CustomerId == customer.Id, ct);

        // Case 3 — another customer holds it. Self is excluded because case 2 already returned for a
        // case-only edit, but the exclusion is explicit rather than implied.
        if (await db.Customers.AnyAsync(c => c.Id != customer.Id && c.Email == newEmail, ct))
        {
            throw new ConflictException(
                "customer-email-in-use", "A customer with that email already exists.");
        }

        // Case 4 — another USER holds it, staff included. Constraint 1 applies the uniqueness of
        // User.email to the propagated value across ALL users, and a collision rejects the whole
        // operation and writes neither row.
        //
        // PF-6's existing slug for PF-6's existing rule — no new problem type is minted, because a
        // client already handles `user-already-exists` from POST /auth/register (§5.2).
        //
        // Guid.Empty, not null, is the sentinel: `u.Id != (Guid?)null` is NULL in SQL and would
        // silently exclude every row. No user has an empty id, so this excludes exactly the linked
        // login and nothing else — which matters only if the two rows had somehow drifted, a state
        // A-19 makes unreachable but which a hand-edited row could still produce.
        var linkedUserId = linkedUser?.Id ?? Guid.Empty;

        if (await db.Users.AnyAsync(u => u.Id != linkedUserId && u.Email == newEmail, ct))
        {
            throw new ConflictException(
                "user-already-exists", "A user with that email already exists.");
        }

        // Cases 5 and 6 — the address is free.
        customer.ChangeEmail(newEmail);

        if (linkedUser is null)
        {
            // Case 6 — a profile-only customer. No login to propagate to, which DM-1 makes the
            // ordinary case. No audit entry: no sign-in identifier changed.
            return;
        }

        // Case 5 — the propagation itself.
        linkedUser.ChangeEmail(newEmail);

        // Audited, and this is the ONLY audited thing in this service. Called BEFORE the caller's
        // SaveChangesAsync, exactly as UserAdminService does: RecordAsync adds to the change tracker
        // and does not commit, so the two row updates and the entry commit together.
        //
        // actorUserId is deliberately NOT passed — AuditRecorder resolves the actor from
        // ICurrentUser, the agent who issued this PATCH. The override exists for exactly one case, a
        // successful sign-in on an anonymous request, and using it here would be wrong.
        //
        // targetId is the LINKED USER's id, not the customer's: the audited fact is that a sign-in
        // identifier changed. AuditTargetType needs no new member — User already exists.
        //
        // There is no Failure counterpart. A rejected change throws above and writes nothing — the
        // same shape as every user-administration call site in Story 02.
        await audit.RecordAsync(
            AuditAction.UserEmailChanged, AuditOutcome.Success,
            AuditTargetType.User, linkedUser.Id, ct: ct);
    }

    /// <summary>
    /// <c>Customer.branchId</c> is required and carries a foreign key, so an unknown id would
    /// otherwise surface as a <c>DbUpdateException</c> and a <c>500</c>. Checked here instead, for a
    /// <c>400</c> that names the field — the same treatment <c>UserAdminService</c> gives
    /// <c>departmentId</c> and <c>branchId</c>.
    /// </summary>
    private async Task EnsureBranchExistsAsync(Guid branchId, CancellationToken ct)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == branchId, ct))
        {
            throw new ValidationException("branchId does not reference an existing branch.");
        }
    }
}
