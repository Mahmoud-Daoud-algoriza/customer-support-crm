using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// Branding is read at runtime and never compiled into a component or a stylesheet
/// (docs/architecture.md §6.3, T3-E). Published anonymously by <c>GET /api/v1/config/bootstrap</c>.
/// </summary>
public sealed class BrandingOptions
{
    public const string SectionName = "SupportCrm:Branding";

    [Required] public string ProductName { get; init; } = default!;

    [Required] public string LogoUrl { get; init; } = default!;

    [Required] public string PrimaryColor { get; init; } = default!;
}
