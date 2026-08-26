using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// Administrator user management — the five endpoints of docs/api-design.md §5.3.
/// <para>
/// The policy is declared once, on the class: every action here is Administrator-only. The gate is
/// enforcement point 1 of docs/architecture.md §4.2, and the role it reads was resolved from the
/// authoritative row during this request, not taken from the token (AD-15).
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class UsersController(UserAdminService users) : ApiControllerBase
{
    /// <summary>
    /// Paged list. Filters <c>role</c>, <c>departmentId</c>, <c>isActive</c>, <c>q</c>; an unknown
    /// sort field is a <c>400</c>, never silently ignored (AP-15).
    /// </summary>
    /// <remarks>
    /// The paging parameter is named <c>paging</c>, not <c>page</c>: a complex query parameter whose
    /// name matches an incoming query key makes the model binder switch to prefix mode and look for
    /// <c>page.page</c>, silently leaving <c>pageSize</c> unbound and the page size stuck at its
    /// default. A test asserts the bound value for exactly this reason.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserDto>>> List(
        [FromQuery] UserListFilter filter, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await users.ListAsync(filter, paging, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await users.GetAsync(id, ct));

    /// <summary>
    /// Creates a staff user. <b>The <c>Customer</c> role is rejected</b> — customers arrive through
    /// registration or by an agent creating a profile (DM-1).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken ct)
    {
        var created = await users.CreateAsync(request, ct);

        // 201 with the Location header, per docs/api-design.md §2.2.
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Partial update of <c>displayName</c>, <c>role</c>, <c>departmentId</c>, <c>branchId</c> only.
    /// <c>role</c> and <c>departmentId</c> are Administrator-set and never self-set
    /// (docs/api-design.md §7).
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Patch(Guid id, PatchUserRequest request, CancellationToken ct) =>
        Ok(await users.PatchAsync(id, request, ct));

    /// <summary>
    /// Deactivates the user. <c>204</c>, and the user's next request gets <c>401</c>
    /// (docs/api-design.md §4.1).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await users.DeactivateAsync(id, ct);

        return NoContent();
    }
}
