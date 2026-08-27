using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// First-response and resolution targets, <b>per priority</b> (A-3, docs/architecture.md §6.3).
/// <para>
/// <b>A-3's clock is 24/7.</b> There are no business hours, no holiday calendar, no per-branch
/// timezone and no pause-on-customer-reply — product-scope §9 item 5 keeps all of that open, and
/// A-3 is what this project implements instead. So a target is a plain number of hours, and nothing
/// here carries a calendar.
/// </para>
/// <para>
/// <b>Not a rules engine and not a table</b> (docs/data-model.md §2.16, T2-D): no conditions, no
/// priorities-within-priorities, no per-department override. One row per priority level.
/// </para>
/// </summary>
public sealed class SlaTargetOptions
{
    public const string SectionName = "SupportCrm:Sla:Targets";

    /// <inheritdoc cref="CategoryOptions.Items" path="/remarks"/>
    [Required, MinLength(1)]
    public List<SlaTargetOption> Items { get; init; } = [];
}

/// <summary>The pair of targets for one priority level.</summary>
public sealed class SlaTargetOption
{
    /// <summary>
    /// One of <see cref="PriorityOptions.ApprovedLevels"/>. Every level must appear exactly once —
    /// a level with no target would leave a ticket with no due date, so it is a startup failure
    /// (ConfigurationValidator check 2).
    /// </summary>
    [Required] public string Priority { get; init; } = default!;

    /// <summary>Hours from ticket creation to the first-response deadline (A-3).</summary>
    [Range(1, int.MaxValue)] public int FirstResponseHours { get; init; }

    /// <summary>Hours from ticket creation to the resolution deadline (A-3).</summary>
    [Range(1, int.MaxValue)] public int ResolutionHours { get; init; }
}
