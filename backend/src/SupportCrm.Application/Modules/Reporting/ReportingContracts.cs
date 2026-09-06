using System.Text.Json.Serialization;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Application.Modules.Reporting;

/// <summary>
/// <c>DashboardReport</c> — docs/api-design.md §6.8, group for group and member for member.
/// The response of the one reporting endpoint (§5.11).
///
/// <para>
/// <b>There is no ticket array anywhere in this shape, and that is a rule rather than an
/// omission</b>: the intake requires aggregation on the server — <em>"do not ship raw ticket lists
/// to the browser to be counted there"</em> — so the payload's size is independent of how many
/// tickets exist. A test asserts it.
/// </para>
/// </summary>
public sealed record DashboardReportDto(
    TicketCountsDto TicketCounts,
    IReadOnlyList<SlaAttainmentRowDto> SlaAttainment,
    IReadOnlyList<AgentPerformanceRowDto> AgentPerformance,
    SatisfactionDto Satisfaction);

/// <summary>§9.1 — four breakdowns of the same filtered ticket set (docs/api-design.md §6.8).</summary>
public sealed record TicketCountsDto(
    IReadOnlyList<CountByKeyDto> ByStatus,
    IReadOnlyList<CountByKeyDto> ByPriority,
    IReadOnlyList<CountByKeyDto> ByCategory,
    IReadOnlyList<CountByNamedKeyDto> ByDepartment);

/// <summary>
/// <c>{ key, count }</c> — the shape §6.8 gives status, priority and category.
/// <para>
/// <see cref="Key"/> is a <b>stable string code</b>, never a display label (docs/api-design.md §2):
/// the front end translates it, because the API returns codes and the client owns display text
/// (architecture §2.3).
/// </para>
/// </summary>
public sealed record CountByKeyDto(string Key, int Count);

/// <summary>
/// <c>{ key, name, count }</c> — the department shape of §6.8.
/// <para>
/// Department is the one breakdown that carries a <see cref="Name"/>, because its key is a
/// <b>GUID</b> and a GUID is not renderable. Status, priority and category keys are already
/// human-meaningful codes with translations, so they carry no name — a difference §6.8 fixes
/// deliberately.
/// </para>
/// </summary>
public sealed record CountByNamedKeyDto(string Key, string Name, int Count);

/// <summary>
/// §9.2 — one row per priority (docs/api-design.md §6.8).
///
/// <para>
/// <b><see cref="AttainmentPercent"/> is null when there are no tickets at that priority</b>, and
/// never <c>100.0</c>: claiming perfect attainment on no evidence is the same error as reporting a
/// <c>0.0</c> satisfaction average with no ratings. "Empty is not zero" applies to both ends.
/// </para>
/// </summary>
public sealed record SlaAttainmentRowDto(
    string Priority,
    int Met,
    int Breached,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AttainmentPercent);

/// <summary>
/// §9.3 — one row per agent (docs/api-design.md §6.8).
///
/// <para>
/// <b><see cref="AverageResolutionHours"/> is null when the agent has resolved nothing</b>, never
/// <c>0.0</c> — §6.8 states this explicitly, and a zero would read as instantaneous resolution.
/// </para>
///
/// <para>
/// <b><see cref="AssignedCount"/> is <em>currently</em> assigned</b> — open tickets on the agent's
/// plate now, excluding <c>Closed</c> and <c>Cancelled</c>. That reading was undefined by every
/// approved document (<b>PF-4</b> / <b>S9-9</b>) and was **decided by the product owner on
/// 2026-09-06**; it is encoded in exactly one place, <c>AgentPerformanceQuery</c>. The field name
/// and the response shape are unchanged either way, which is what §6.8 promised.
/// </para>
/// </summary>
public sealed record AgentPerformanceRowDto(
    UserSummaryDto Agent,
    int AssignedCount,
    int ResolvedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AverageResolutionHours);

/// <summary>
/// §9.4 — the CSAT summary (docs/api-design.md §6.8).
///
/// <para>
/// <b><see cref="AverageRating"/> is null when <see cref="ResponseCount"/> is 0.</b> Declining to
/// rate is a normal outcome (T2-F), so the absence of a row means <em>"no response"</em> and must
/// never be read as a low score (data-model §2.15). <c>0.0</c> here would read as universal
/// dissatisfaction.
/// </para>
///
/// <para>
/// <see cref="RatingScale"/> travels with the average so the front end can render <em>"4.2 of 5"</em>
/// <b>without hardcoding a denominator</b> — <b>OQ-1</b> is not answered by this story and nothing
/// here assumes a scale. The values come from the same configuration key <c>GET /config</c>
/// publishes.
/// </para>
/// </summary>
public sealed record SatisfactionDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] double? AverageRating,
    int ResponseCount,
    RatingScaleDto RatingScale);

/// <summary>The configured scale, mirroring <c>GET /config</c>'s own shape. This contract fixes no range (OQ-1).</summary>
public sealed record RatingScaleDto(int Min, int Max);

/// <summary>
/// The two filters of docs/api-design.md §5.11, and <b>deliberately only those two</b>.
///
/// <para>
/// <b>No date range, no agent filter, no status filter, no <c>format</c>.</b> A member added here
/// becomes an accepted query parameter, and an export parameter is exactly what T2-G excludes — so
/// the absence is the contract. An unknown query parameter is refused by the controller (AP-15).
/// </para>
/// </summary>
public sealed record DashboardFilter
{
    public Guid? DepartmentId { get; init; }

    /// <summary>
    /// Resolved <b>through the customer</b>, never through a ticket column — <c>Ticket</c> has no
    /// branch field (docs/api-design.md §4.4, data-model §2.3).
    /// </summary>
    public Guid? BranchId { get; init; }
}
