using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The audit log read surface (docs/architecture.md §2.4, docs/api-design.md §5.12).
/// <para>
/// The policy is declared once, on the class: every action here is Administrator-only, and every
/// other role gets <c>403</c> (T2-H). <b>No write, update or delete endpoint exists here, and none
/// may be added</b> — the log is append-only by construction, and the only writer is
/// <c>AuditRecorder</c>, called from Application services, never from a controller
/// (docs/architecture.md §2.4).
/// </para>
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AuditController(AuditQueryService audit) : ApiControllerBase
{
    /// <summary>
    /// Paged, newest first. Filters <c>actorUserId</c>, <c>action</c>, <c>from</c>, <c>to</c>,
    /// combined with AND (docs/api-design.md §5.12).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> List(
        [FromQuery] AuditListFilter filter, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await audit.ListAsync(filter, paging, ct));
}
