using System.ComponentModel.DataAnnotations;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Modules.Identity;

/// <summary>
/// <c>Identity</c> — docs/api-design.md §6.1. The <b>per-request resolved</b> values (AP-9), not
/// token claims.
/// <para>
/// <c>isActive</c> is always <c>true</c> in a successful response: an inactive user gets <c>401</c>
/// (§4.1), so there is no path by which <c>false</c> could be serialized here.
/// </para>
/// </summary>
public sealed record IdentityDto(
    Guid Id,
    string DisplayName,
    string Email,
    UserRole Role,
    Guid? DepartmentId,
    Guid? BranchId,
    Guid? CustomerId,
    bool IsActive);

/// <summary><c>AuthToken</c> — docs/api-design.md §6.1.</summary>
public sealed record AuthTokenDto(string AccessToken, DateTimeOffset ExpiresAt, IdentityDto User);

/// <summary>
/// <c>User</c> — docs/api-design.md §6.1. The shape of <c>GET /users/{id}</c> and of a
/// <c>GET /users</c> row.
/// <para><b><c>passwordHash</c> is absent, and appears in no response, ever.</b></para>
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    Guid? DepartmentId,
    Guid? BranchId,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary><c>UserSummary</c> — docs/api-design.md §6.1. Embedded wherever a person is referenced.</summary>
public sealed record UserSummaryDto(Guid Id, string DisplayName);

/// <summary><c>POST /auth/login</c> — docs/api-design.md §6.11.</summary>
public sealed record LoginRequest
{
    [Required] public string Email { get; init; } = default!;

    [Required] public string Password { get; init; } = default!;
}

/// <summary>
/// <c>POST /users</c> — docs/api-design.md §5.3.
/// <para>
/// The fields a client may <b>not</b> send are absent from this type rather than accepted and
/// ignored (AP-10): there is no <c>isActive</c>, no <c>customerId</c> and no <c>createdAt</c>, so a
/// request carrying one is a <c>400</c> and the client is never misled into thinking it worked.
/// </para>
/// <para>
/// <b>The <c>400</c> is real, not aspirational.</b> Omission alone only makes a field unreachable;
/// the refusal comes from <c>UnmappedMemberHandling.Disallow</c>, set once on the MVC JSON options
/// in <c>Program.cs</c> (finding I-9). <c>UnmappedRequestMemberTests</c> covers it.
/// </para>
/// </summary>
public sealed record CreateUserRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; init; } = default!;

    [Required] public string Password { get; init; } = default!;

    [Required, MaxLength(200)] public string DisplayName { get; init; } = default!;

    /// <summary><c>Customer</c> is rejected by the service — customers never arrive here (DM-1).</summary>
    [Required] public UserRole? Role { get; init; }

    /// <summary>Required for every staff role (DM-1).</summary>
    public Guid? DepartmentId { get; init; }

    public Guid? BranchId { get; init; }
}

/// <summary>
/// <c>PATCH /users/{id}</c> — docs/api-design.md §5.3. Exactly four patchable fields.
/// <para>
/// Email and password are not patchable here. <c>role</c> and <c>departmentId</c> are
/// Administrator-set and never self-set (docs/api-design.md §7).
/// </para>
/// <para>
/// A body carrying <c>email</c> is a <c>400</c>, not an accepted no-op:
/// <c>UnmappedMemberHandling.Disallow</c> on the MVC JSON options in <c>Program.cs</c> turns the
/// unmapped member into a refusal (AP-10, finding I-9). A-19's propagation reaches
/// <c>User.Email</c> through <c>User.ChangeEmail</c> from the customer patch instead (finding I-3).
/// </para>
/// Every property is nullable because absent means "leave unchanged" — a PATCH carries only the
/// fields being changed (docs/api-design.md §2).
/// </summary>
public sealed record PatchUserRequest
{
    [MaxLength(200)] public string? DisplayName { get; init; }

    public UserRole? Role { get; init; }

    public Guid? DepartmentId { get; init; }

    public Guid? BranchId { get; init; }
}
