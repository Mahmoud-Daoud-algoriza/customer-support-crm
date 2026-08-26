using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// Token issuance settings. <b>The signing key is a secret and comes from the environment only</b>
/// (docs/architecture.md §4.1, §6.3) — <c>appsettings.json</c> carries no value for it, so a missing
/// key fails at startup rather than falling back to something guessable.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "SupportCrm:Jwt";

    /// <summary>
    /// HMAC-SHA256 needs at least 256 bits of key material; a shorter key is rejected at startup
    /// rather than weakening the signature silently.
    /// </summary>
    public const int MinimumSigningKeyLength = 32;

    [Required, MinLength(MinimumSigningKeyLength)]
    public string SigningKey { get; init; } = default!;

    [Required] public string Issuer { get; init; } = default!;

    [Required] public string Audience { get; init; } = default!;

    /// <summary>
    /// Token lifetime in minutes.
    /// <para>
    /// <b>No approved document fixes this number.</b> docs/architecture.md §4.1 requires
    /// "short-lived tokens; expiry means signing in again" and rules out refresh rotation, but names
    /// no value. It is configuration rather than a constant so the choice is visible and changeable
    /// without a code change; the committed default is in <c>appsettings.json</c>.
    /// </para>
    /// </summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; }
}
