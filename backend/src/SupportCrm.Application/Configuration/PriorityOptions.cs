using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The four priority levels of A-6, published to staff by <c>GET /config/staff</c>.
/// <para>
/// <b>Priorities are configuration, not a table</b> (docs/data-model.md §2.16) — but they are
/// <b>fixed configuration</b>: A-6 states four levels, and configuration may not add a fifth,
/// rename one, or reorder them. That is what <c>PriorityOptionsValidator</c> enforces at startup.
/// The section exists so the values have one home, not so they can be changed.
/// </para>
/// </summary>
public sealed class PriorityOptions
{
    public const string SectionName = "SupportCrm:Priorities";

    /// <summary>
    /// The approved level names, in the approved order — <b>A-6</b>, and also
    /// docs/data-model.md §2.6 and docs/api-design.md §6.9.
    /// </summary>
    /// <remarks>
    /// <b>Story 05 hand-off.</b> The Story 16 plan words this check as *"`Priorities` equals the
    /// `TicketPriority` enum, in order"* — but <c>TicketPriority</c> does not exist yet:
    /// <c>.squad/plans/ticket-management/05-story-ticket-core.md</c> creates
    /// <c>Domain/Modules/Tickets/TicketPriority.cs</c>, and Part A runs <b>before</b> Story 05 by
    /// design. Both spellings encode the same authority — the enum's own plan annotates it
    /// <c>// A-6</c> — so this array stands in until the enum lands.
    /// <para>
    /// <b>Story 05 must replace this array with <c>Enum.GetNames&lt;TicketPriority&gt;()</c></b> and
    /// delete it, so the four names live in exactly one place. Marked here rather than left to be
    /// noticed, following the same convention as Story 04's <c>openTicketCount</c> placeholder.
    /// </para>
    /// </remarks>
    public static readonly string[] ApprovedLevels = ["Low", "Medium", "High", "Urgent"];

    [Required, MinLength(1)]
    public string[] Levels { get; init; } = [];
}
