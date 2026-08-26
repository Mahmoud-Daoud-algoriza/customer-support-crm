using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The language set the SPA offers, published anonymously by <c>GET /api/v1/config/bootstrap</c>
/// (T2-J). English and Arabic, switchable at runtime (docs/architecture.md §2.3).
/// </summary>
public sealed class LocalizationOptions
{
    public const string SectionName = "SupportCrm:Localization";

    [Required, MinLength(1)] public string[] Languages { get; init; } = [];

    [Required] public string DefaultLanguage { get; init; } = default!;
}

/// <summary>
/// Startup validation of the one rule data annotations cannot express: the default language must
/// be one of the offered languages. Invalid configuration fails fast (docs/architecture.md §6.3).
/// </summary>
public sealed class LocalizationOptionsValidator : IValidateOptions<LocalizationOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalizationOptions options)
    {
        if (options.Languages.Length == 0)
        {
            return ValidateOptionsResult.Fail(
                $"{LocalizationOptions.SectionName}:Languages must list at least one language.");
        }

        if (!options.Languages.Contains(options.DefaultLanguage, StringComparer.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"{LocalizationOptions.SectionName}:DefaultLanguage '{options.DefaultLanguage}' is not one of " +
                $"Languages [{string.Join(", ", options.Languages)}].");
        }

        return ValidateOptionsResult.Success;
    }
}
