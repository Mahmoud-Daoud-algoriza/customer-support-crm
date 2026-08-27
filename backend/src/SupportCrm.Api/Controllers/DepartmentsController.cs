using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Organization;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// <c>GET /api/v1/departments</c> — docs/api-design.md §5.4. The list later screens use for filters
/// and assignment pickers.
/// <para>
/// <b>One action, and no write action exists.</b> Departments are seeded and configured, not managed
/// through an admin UI (T2-I): there is no <c>POST</c>, <c>PATCH</c> or <c>DELETE</c> here, and
/// adding one is a scope change.
/// </para>
/// <para>
/// <b>Customers never call this.</b> Under A-14 a customer chooses a <em>category</em>, never a
/// department, so no <c>/portal</c> variant is published and the <c>Agent</c> gate is the whole
/// story: a Customer token gets <c>403</c> — a capability denial they can infer from their own role,
/// so AP-4's <c>404</c> rule does not apply (docs/api-design.md §4.2).
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAgent)]
public sealed class DepartmentsController(OrganizationQueryService organization) : ApiControllerBase
{
    /// <summary>
    /// The paged envelope, even though the list is short: AP-3 has one envelope for all collections,
    /// and a bare array for "small" collections guarantees a breaking change later.
    /// </summary>
    /// <remarks>
    /// The parameter is named <c>paging</c>, not <c>page</c>, for the reason
    /// <c>UsersController.List</c> records: a complex query parameter whose name matches an incoming
    /// query key switches the model binder to prefix mode and silently leaves <c>pageSize</c>
    /// unbound.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<DepartmentDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<DepartmentDto>>> List(
        [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await organization.GetDepartmentsAsync(paging, ct));
}
