using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Organization;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// <c>GET /api/v1/branches</c> — docs/api-design.md §5.4. The list the customer directory and the
/// reports filter use.
/// <para>
/// <b>No write action exists</b> (T2-I), and <b>no endpoint anywhere scopes by branch.</b> Branch is
/// a reporting and filtering attribute only (A-2, T2-K): it appears as a field on customer and user
/// payloads and as a filter on <c>GET /customers</c> and <c>GET /reports/dashboard</c>, and in no
/// authorization predicate (docs/api-design.md §4.4, docs/data-model.md §2.3).
/// </para>
/// <para>
/// The <c>Agent</c> gate here is the same one <c>DepartmentsController</c> carries, and for the same
/// reason: no <c>/portal</c> variant is published, so a Customer token gets <c>403</c>.
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAgent)]
public sealed class BranchesController(OrganizationQueryService organization) : ApiControllerBase
{
    /// <inheritdoc cref="DepartmentsController.List"/>
    [HttpGet]
    [ProducesResponseType<PagedResult<BranchDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<BranchDto>>> List(
        [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await organization.GetBranchesAsync(paging, ct));
}
