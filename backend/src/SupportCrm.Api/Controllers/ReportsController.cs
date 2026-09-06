using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Modules.Reporting;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The reporting endpoint of docs/api-design.md §5.11 — requirements §9, T2-G.
///
/// <para>
/// <b>One endpoint, and there must never be a second.</b> T2-G is one dashboard with a small fixed
/// metric set: <b>no export endpoint, no <c>format=csv</c> parameter, no per-metric route, no saved
/// or scheduled report</b> (product-scope §8). All four groups come back in one response because
/// they are one screen.
/// </para>
///
/// <para>
/// <b><c>RequireManager</c>, and the <c>403</c> is correct here rather than AP-4's <c>404</c>.</b>
/// An Agent or Customer is refused a <b>capability</b> they can infer from their own role — nothing
/// is being hidden about which records exist, which is what AP-4's indistinguishability rule
/// protects. The plan and ui-design §5.7 both say `403`, and A-4's hierarchy admits Manager and
/// Administrator.
/// </para>
///
/// <para>
/// <b>The role gate is not a scope.</b> Managers and Administrators see <em>all</em> departments;
/// <c>departmentId</c> narrows the view because the user asked, never because of who they are
/// (architecture §4.3). <c>DashboardReportService</c> composes no <c>TicketScope</c>, deliberately.
/// </para>
///
/// Controllers are thin: bind, delegate to one Application service, return the result. Errors are
/// translated centrally by <c>ProblemDetailsExceptionHandler</c> (architecture §2.1).
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireManager)]
public sealed class ReportsController(DashboardReportService reports) : ApiControllerBase
{
    /// <summary>
    /// <c>GET /reports/dashboard?departmentId=&amp;branchId=</c> — all four metric groups
    /// (docs/api-design.md §5.11, §6.8).
    ///
    /// <para>
    /// <b>The two filters combine</b>, and <c>branchId</c> resolves <b>through the customer</b>
    /// because <c>Ticket</c> has no branch column (§4.4).
    /// </para>
    ///
    /// <para>
    /// <b>An unknown query parameter is a <c>400</c>, not silently ignored</b> (AP-15) — which is
    /// what makes <c>?format=csv</c> a refusal rather than a request that quietly returns JSON and
    /// leaves a caller believing an export exists. <c>DashboardFilter</c> declares exactly two
    /// members, so the absence of every other parameter is the enforcement.
    /// </para>
    ///
    /// <para>
    /// <b>Not paged.</b> §6.8 defines a fixed-size response — four groups whose size depends on the
    /// number of statuses, priorities, categories, departments and agents, never on the number of
    /// tickets. There is no ticket array to page.
    /// </para>
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardReportDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardReportDto>> Dashboard(
        [FromQuery] DashboardFilter filter, CancellationToken ct)
    {
        RejectUnknownQueryParameters();

        return Ok(await reports.GetAsync(filter, ct));
    }

    /// <summary>
    /// <b>AP-15's per-endpoint whitelist, applied to this endpoint's filters.</b>
    /// docs/api-design.md §2.1: <em>"An unknown filter or sort field is a <c>400</c>, never silently
    /// ignored."</em>
    ///
    /// <para>
    /// <b>Model binding does not do this for us.</b> <c>UnmappedMemberHandling.Disallow</c> — the
    /// AP-10 enforcement added for finding I-9 — governs <b>JSON bodies</b> only; story 07's
    /// <c>UnmappedRequestMemberTests</c> deliberately pinned query-string binding as one of the
    /// behaviours that fix left alone. An unknown query key is therefore ignored by default, and
    /// <c>?format=csv</c> would return <c>200</c> with a perfectly good JSON dashboard — leaving a
    /// caller believing an export exists and had been produced. <b>That is the specific
    /// misunderstanding T2-G's "no export" rule exists to prevent</b>, which is why this endpoint
    /// refuses rather than ignores.
    /// </para>
    ///
    /// <para>
    /// <b>Scoped to this action on purpose.</b> The same gap exists on other endpoints and fixing it
    /// globally would be a cross-cutting change to every filtered read — not this story's work
    /// (finding <b>I-39</b>). AP-15 words the rule as a <em>per-endpoint</em> whitelist, so applying
    /// it here is the rule as written, not a local exception to it.
    /// </para>
    /// </summary>
    private void RejectUnknownQueryParameters()
    {
        // The two filters of §5.11, and nothing else. Matched case-insensitively because the model
        // binder is case-insensitive, so `DepartmentId` must not be rejected as unknown.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            nameof(DashboardFilter.DepartmentId),
            nameof(DashboardFilter.BranchId),
        };

        var unknown = Request.Query.Keys.Where(key => !allowed.Contains(key)).ToList();

        if (unknown.Count > 0)
        {
            throw new SupportCrm.Application.Abstractions.ValidationException(
                $"Unknown query parameter(s): {string.Join(", ", unknown)}. "
                    + $"This endpoint accepts {string.Join(" and ", allowed)}.");
        }
    }
}
