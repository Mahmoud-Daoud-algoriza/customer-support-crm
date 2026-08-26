using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// Demo-data settings. Seeding runs at API startup (AD-8), which is a knowingly non-production
/// choice recorded as such.
/// <para>
/// <b>The seeded password is configuration, not a source constant</b> — the Story 02 plan requires
/// that no credential is hardcoded in source. <c>appsettings.Development.json</c> carries the
/// documented development default so a clean checkout comes up demo-ready with no external
/// credentials (product-scope §10 item 5); any other environment must supply
/// <c>SupportCrm__Seed__DefaultPassword</c> or seeding fails at startup.
/// </para>
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "SupportCrm:Seed";

    /// <summary>
    /// The password given to every seeded demo user. There is no password policy engine in this
    /// product (A-9, out of scope), so this is validated only for presence and a minimum length.
    /// </summary>
    [Required, MinLength(8)]
    public string DefaultPassword { get; init; } = default!;
}
