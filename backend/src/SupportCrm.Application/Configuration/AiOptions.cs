using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>Which implementation of the AI seam runs (docs/architecture.md §5.1, §6.3).</summary>
public enum AiProviderKind
{
    /// <summary>
    /// The deterministic offline fake. <b>The default, and that is a requirement rather than a
    /// convenience</b>: product-scope §10 item 5 requires the whole system to run with no external
    /// accounts or credentials, and A-7 requires the AI integration to degrade to an offline fake.
    /// </summary>
    Fake = 0,

    /// <summary>A real provider over HTTP. Requires an endpoint and a key, both from environment.</summary>
    Provider = 1,
}

/// <summary>
/// <c>SupportCrm:Ai</c> — the AI seam's configuration (docs/architecture.md §6.3).
///
/// <para>
/// <b>No provider is named here or anywhere in the contract</b> (api-design §8.1, AP-12).
/// <see cref="Endpoint"/> and <see cref="Model"/> are opaque strings the adapter interprets; which
/// provider they point at is product-scope §9 question 1 and <b>stays open</b>.
/// </para>
///
/// <para>
/// <b><see cref="ApiKey"/> comes from the environment and is never committed.</b> There is no
/// default and no placeholder — an absent key with <see cref="AiProviderKind.Provider"/> selected is
/// a startup failure, not a runtime surprise.
/// </para>
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "SupportCrm:Ai";

    /// <summary>Defaults to <see cref="AiProviderKind.Fake"/>, so the app starts with no configuration.</summary>
    public AiProviderKind Provider { get; init; } = AiProviderKind.Fake;

    public string? Endpoint { get; init; }

    public string? Model { get; init; }

    /// <summary>Environment only. Never appears in <c>appsettings.json</c>.</summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// The per-call timeout. A slow provider must not hold a request open indefinitely — after this,
    /// the capability reports unavailable and the rest of the screen keeps working.
    /// </summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; init; } = 15;
}
