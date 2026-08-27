using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Configuration;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// Configuration is split by audience into three tiers (<b>AP-17</b>, docs/api-design.md §5.1).
/// All three live here; Story 01 delivered the public tier and Story 16 Part A the other two.
///
/// <para>
/// <b>Every configured value sits in exactly one tier, and the tier is decided by who legitimately
/// needs it:</b>
/// </para>
/// <list type="table">
///   <item><term>Public — <c>/config/bootstrap</c></term><description>Anonymous. Branding, product
///     name, languages: needed to render the sign-in screen (T3-E, T2-J)</description></item>
///   <item><term>Customer-safe — <c>/config</c></term><description>Any authenticated user. The
///     category list and the feedback rating scale — a customer picks a category (A-14) and needs
///     the scale to render the feedback control</description></item>
///   <item><term>Staff-only — <c>/config/staff</c></term><description>Agent and above. Priorities,
///     quick replies, SLA targets, the category → department map</description></item>
/// </list>
///
/// <para>
/// <b>B-2 is why the split exists, and it is not to be undone.</b> The first version of the contract
/// returned quick replies and SLA targets to <b>every</b> authenticated caller, Customers included.
/// A customer has no requirement that needs any of them: they do not set priority (A-6), do not
/// choose a department (A-14), and the portal ticket payload carries no priority or SLA field.
/// <b>Do not merge these two endpoints back into one.</b>
/// </para>
///
/// <para>
/// <b>No endpoint in this API writes configuration</b> — changing it is a redeploy (T2-I). There is
/// no <c>POST</c>, <c>PATCH</c> or <c>PUT</c> here and there must never be one.
/// </para>
/// </summary>
public sealed class ConfigController(
    IOptions<BrandingOptions> branding,
    IOptions<LocalizationOptions> localization,
    IOptions<CategoryOptions> categories,
    IOptions<FeedbackOptions> feedback,
    IOptions<PriorityOptions> priorities,
    IOptions<QuickReplyOptions> quickReplies,
    IOptions<SlaTargetOptions> slaTargets) : ApiControllerBase
{
    /// <summary>
    /// Branding, product name and available languages — needed before sign-in (T3-E, T2-J).
    /// Anonymous.
    /// </summary>
    [HttpGet("bootstrap")]
    [AllowAnonymous]
    [ProducesResponseType<BootstrapConfigResponse>(StatusCodes.Status200OK)]
    public ActionResult<BootstrapConfigResponse> GetBootstrap() => Ok(new BootstrapConfigResponse(
        ProductName: branding.Value.ProductName,
        LogoUrl: branding.Value.LogoUrl,
        PrimaryColor: branding.Value.PrimaryColor,
        Languages: localization.Value.Languages,
        DefaultLanguage: localization.Value.DefaultLanguage));

    /// <summary>
    /// The <b>customer-safe</b> tier — every authenticated role, Customers included
    /// (docs/api-design.md §5.1, §6.9).
    /// <para>
    /// <b>The category rows carry <c>code</c> and <c>name</c> only — never <c>departmentId</c>.</b>
    /// Under A-14 a customer chooses a category and the server derives the department; the routing
    /// map is internal policy and is published on the staff tier alone. A test asserts that no
    /// object in this response has a <c>departmentId</c> key.
    /// </para>
    /// <para>
    /// The <c>ratingScale</c> values are whatever configuration holds. <b>The contract fixes no
    /// scale — OQ-1 is open</b> (docs/api-design.md §6.9), so nothing here validates or assumes a
    /// range.
    /// </para>
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType<CustomerConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<CustomerConfigResponse> GetCustomerConfig() => Ok(new CustomerConfigResponse(
        Categories: [.. categories.Value.Items.Select(c => new CustomerCategoryResponse(c.Code, c.Name))],
        Feedback: new FeedbackConfigResponse(
            new RatingScaleResponse(feedback.Value.Min, feedback.Value.Max))));

    /// <summary>
    /// The <b>staff-only</b> tier — Agent and above (docs/api-design.md §5.1, §6.9).
    /// <para>
    /// <b>A Customer calling this gets <c>403</c>, and <c>403</c> is correct here</b> — it is a
    /// capability denial the caller can infer from their own role, so AP-4's <c>404</c> rule does
    /// not apply (docs/api-design.md §4.2, §5.1). The policy is the whole of the rule; this action
    /// compares no roles inline.
    /// </para>
    /// </summary>
    [HttpGet("staff")]
    [Authorize(Policy = AuthorizationPolicies.RequireAgent)]
    [ProducesResponseType<StaffConfigResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<StaffConfigResponse> GetStaffConfig() => Ok(new StaffConfigResponse(
        Priorities: priorities.Value.Levels,
        QuickReplies: [.. quickReplies.Value.Items.Select(q => new QuickReplyResponse(q.Id, q.Title, q.Body))],
        SlaTargets: [.. slaTargets.Value.Items.Select(t =>
            new SlaTargetResponse(t.Priority, t.FirstResponseHours, t.ResolutionHours))],
        CategoryDepartmentMap: [.. categories.Value.Items.Select(c =>
            new CategoryDepartmentResponse(c.Code, c.DepartmentId))]));
}

/// <summary><c>BootstrapConfig</c> exactly as specified in docs/api-design.md §6.9.</summary>
public sealed record BootstrapConfigResponse(
    string ProductName,
    string LogoUrl,
    string PrimaryColor,
    string[] Languages,
    string DefaultLanguage);

/// <summary>
/// <c>CustomerConfig</c> — docs/api-design.md §6.9. <b>Exactly two members</b>, and a test asserts
/// the raw JSON has no third: a value that leaks in here reaches every Customer.
/// </summary>
public sealed record CustomerConfigResponse(
    CustomerCategoryResponse[] Categories,
    FeedbackConfigResponse Feedback);

/// <summary>
/// A category as a <b>customer</b> sees it. There is deliberately no <c>DepartmentId</c> member —
/// the omission is structural, not a serializer setting, so the routing map cannot leak through
/// this type by accident (A-14, AP-17).
/// </summary>
public sealed record CustomerCategoryResponse(string Code, string Name);

public sealed record FeedbackConfigResponse(RatingScaleResponse RatingScale);

/// <summary>The scale boundaries. <b>Values are configuration and OQ-1 is open</b> — see §6.9.</summary>
public sealed record RatingScaleResponse(int Min, int Max);

/// <summary><c>StaffConfig</c> — docs/api-design.md §6.9. The four staff-only groups.</summary>
public sealed record StaffConfigResponse(
    string[] Priorities,
    QuickReplyResponse[] QuickReplies,
    SlaTargetResponse[] SlaTargets,
    CategoryDepartmentResponse[] CategoryDepartmentMap);

public sealed record QuickReplyResponse(string Id, string Title, string Body);

public sealed record SlaTargetResponse(string Priority, int FirstResponseHours, int ResolutionHours);

/// <summary>One entry of the A-14 routing map. Staff-only, by AP-17.</summary>
public sealed record CategoryDepartmentResponse(string CategoryCode, Guid DepartmentId);
