using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// How often the SLA breach sweep runs — <c>SupportCrm:Sla:SweepIntervalSeconds</c>, Story 09, AD-6.
///
/// <para>
/// <b>Configuration rather than a constant, and coarse rather than precise.</b> A-3 states that
/// <em>"timing precision is explicitly not a goal"</em>: the granularity is minutes, so the default of
/// 60 seconds is already finer than the product needs. It is configurable mainly so a demo or a test
/// host can sweep quickly without waiting a minute to see a breach appear.
/// </para>
///
/// <para>
/// <b>It binds to <c>SupportCrm:Sla</c>, the section that also holds <c>Targets</c></b> — the two are
/// one operator-facing concern, and giving the interval its own top-level section would suggest they
/// are configured independently of each other.
/// </para>
/// </summary>
public sealed class SlaMonitorOptions
{
    public const string SectionName = "SupportCrm:Sla";

    /// <summary>
    /// Seconds between sweeps. **A floor of one second**, because zero or a negative value would make
    /// <c>PeriodicTimer</c> throw at startup — a misconfiguration should fail validation with a
    /// sentence, not an unhandled exception in a background thread.
    /// </summary>
    [Range(1, 86_400)]
    public int SweepIntervalSeconds { get; init; } = 60;
}
