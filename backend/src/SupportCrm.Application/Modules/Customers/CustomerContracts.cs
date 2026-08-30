using System.ComponentModel.DataAnnotations;
using SupportCrm.Application.Modules.Organization;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// <c>Customer</c> — docs/api-design.md §6.3. The shape of <c>GET /customers/{id}</c>.
/// <para>
/// <b><c>storagePath</c> and <c>passwordHash</c> have no home here or anywhere near here</b>, and
/// <see cref="ExternalReference"/> is returned but never accepted: it is the ERP seam's one
/// persisted field (DM-6), read-only and settable through no endpoint (docs/api-design.md §8.3).
/// The domain type backs that up by exposing no mutator for it.
/// </para>
/// <para>
/// <c>phone</c> and <c>externalReference</c> are omitted from the JSON when null — the serializer
/// is configured <c>WhenWritingNull</c>, which is what docs/api-design.md §2 asks for.
/// </para>
/// </summary>
public sealed record CustomerDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    BranchDto Branch,
    string? ExternalReference,
    DateTimeOffset CreatedAt);

/// <summary>
/// <c>CustomerListItem</c> — docs/api-design.md §6.3. The row shape of <c>GET /customers</c>.
/// <para>
/// It carries <see cref="OpenTicketCount"/> and <b>not</b> <c>externalReference</c> or
/// <c>createdAt</c>: §6.3 gives the two payloads different fields on purpose, because
/// docs/ui-design.md §5.4's directory shows a ticket count and the detail screen shows the seam
/// field.
/// </para>
/// </summary>
public sealed record CustomerListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    BranchDto Branch,
    int OpenTicketCount);

/// <summary>
/// <c>POST /customers</c> — docs/api-design.md §5.5. <b>Exactly four fields.</b>
/// <para>
/// The fields a client may not send are absent rather than accepted and ignored (AP-10): there is
/// no <c>externalReference</c> (DM-6, §8.3), no <c>id</c> and no <c>createdAt</c>, so a request
/// carrying one is a <c>400</c> and the client is never misled into thinking it worked.
/// </para>
/// <para>
/// <b>The <c>400</c> is real, not aspirational.</b> Omission alone only makes a field unreachable;
/// the refusal comes from <c>UnmappedMemberHandling.Disallow</c>, set once on the MVC JSON options
/// in <c>Program.cs</c> (finding I-9). <c>UnmappedRequestMemberTests</c> covers it.
/// </para>
/// </summary>
public sealed record CreateCustomerRequest
{
    [Required, MaxLength(200)] public string FullName { get; init; } = default!;

    /// <summary>
    /// Required, a valid address, and unique across customers case-insensitively — the three
    /// validations docs/api-design.md §5.5 states, and the only three the model supports (A-10,
    /// docs/data-model.md §5 constraint 1).
    /// </summary>
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = default!;

    [MaxLength(64)] public string? Phone { get; init; }

    /// <summary>
    /// Required — A-2: a customer belongs to one branch (docs/data-model.md §2.4).
    /// <para>
    /// An agent creating a profile <b>does</b> choose the branch. Only a <em>self-registering</em>
    /// customer is given the configured default and never asked (A-15), and that is a different
    /// endpoint with a different request model (§5.2, task 7).
    /// </para>
    /// </summary>
    [Required] public Guid? BranchId { get; init; }
}

/// <summary>
/// <c>PATCH /customers/{id}</c> — docs/api-design.md §5.5. <b>Exactly four patchable fields.</b>
/// <para>
/// Every property is nullable because absent means "leave unchanged" — a PATCH carries only what is
/// changing (docs/api-design.md §2).
/// </para>
/// <para>
/// <b><c>email</c> is patchable</b>, and §5.5 says why: no approved source makes it immutable, and
/// A-10 makes it the customer's <em>identifier</em>, which is not the same thing. Changing it
/// propagates to a linked portal login in the same unit of work (<b>A-19</b>) — see
/// <see cref="CustomerService.UpdateAsync"/>.
/// </para>
/// <para>
/// <c>externalReference</c> is <b>not</b> here: DM-6 and §8.3 make it settable through no endpoint.
/// A request carrying it is a <c>400</c> rather than a silent no-op, because
/// <c>UnmappedMemberHandling.Disallow</c> is set on the MVC JSON options in <c>Program.cs</c>
/// (AP-10, finding I-9).
/// </para>
/// </summary>
public sealed record PatchCustomerRequest
{
    [MaxLength(200)] public string? FullName { get; init; }

    [EmailAddress, MaxLength(256)] public string? Email { get; init; }

    [MaxLength(64)] public string? Phone { get; init; }

    public Guid? BranchId { get; init; }
}

/// <summary>
/// Filters for <c>GET /customers</c> — docs/api-design.md §5.5: <c>q</c> and <c>branchId</c>, and
/// nothing else.
/// <para>
/// <b>Branch is a legitimate filter here</b> and a filter only. It is a reporting and filtering
/// attribute (T2-K, A-2) and appears in no authorization predicate anywhere
/// (docs/data-model.md §5 constraint 6): narrowing a list the caller may already see in full is not
/// scoping. There is deliberately <b>no department filter</b> — docs/ui-design.md §5.4 — because a
/// customer has no department.
/// </para>
/// </summary>
public sealed record CustomerListFilter
{
    /// <summary>Free-text match over full name and email (docs/api-design.md §5.5).</summary>
    public string? Q { get; init; }

    public Guid? BranchId { get; init; }
}
