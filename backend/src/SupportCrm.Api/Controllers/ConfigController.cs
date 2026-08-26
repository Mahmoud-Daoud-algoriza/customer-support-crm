using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Configuration;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// Configuration is split by audience into three tiers (AP-17, docs/api-design.md §5.1).
/// Story 01 delivers the <b>public</b> tier only.
/// <para>
/// <c>GET /config</c> (customer-safe) and <c>GET /config/staff</c> (staff-only) are deliberately
/// absent: both require authentication (Story 02) and publish values Story 16 Part A defines.
/// This file is extended there, not duplicated.
/// </para>
/// No endpoint in this API writes configuration — changing it is a redeploy (T2-I).
/// </summary>
public sealed class ConfigController(
    IOptions<BrandingOptions> branding,
    IOptions<LocalizationOptions> localization) : ApiControllerBase
{
    /// <summary>
    /// Branding, product name and available languages — needed before sign-in (T3-E, T2-J).
    /// Anonymous.
    /// </summary>
    [HttpGet("bootstrap")]
    [ProducesResponseType<BootstrapConfigResponse>(StatusCodes.Status200OK)]
    public ActionResult<BootstrapConfigResponse> GetBootstrap() => Ok(new BootstrapConfigResponse(
        ProductName: branding.Value.ProductName,
        LogoUrl: branding.Value.LogoUrl,
        PrimaryColor: branding.Value.PrimaryColor,
        Languages: localization.Value.Languages,
        DefaultLanguage: localization.Value.DefaultLanguage));
}

/// <summary><c>BootstrapConfig</c> exactly as specified in docs/api-design.md §6.9.</summary>
public sealed record BootstrapConfigResponse(
    string ProductName,
    string LogoUrl,
    string PrimaryColor,
    string[] Languages,
    string DefaultLanguage);
