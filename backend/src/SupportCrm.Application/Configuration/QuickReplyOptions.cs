using System.ComponentModel.DataAnnotations;

namespace SupportCrm.Application.Configuration;

/// <summary>
/// The agent canned-response library (T1-C), published by <c>GET /config/staff</c>.
/// <para>
/// <b>Staff-only, and that is the whole point of AP-17's split.</b> The first version of the
/// contract returned quick replies to every authenticated caller, Customers included — corrected as
/// <b>B-2</b>. A customer has no requirement that needs an agent's canned replies.
/// </para>
/// <para>
/// <b>Configuration, not a table</b> (docs/data-model.md §2.16). There is no authoring screen, no
/// per-agent library, no categories and no usage counter — changing the list is a redeploy (T2-I).
/// </para>
/// </summary>
public sealed class QuickReplyOptions
{
    public const string SectionName = "SupportCrm:QuickReplies";

    /// <inheritdoc cref="CategoryOptions.Items" path="/remarks"/>
    public List<QuickReplyOption> Items { get; init; } = [];
}

/// <summary>One canned reply — <c>{ id, title, body }</c> (docs/api-design.md §6.9).</summary>
public sealed class QuickReplyOption
{
    /// <summary>A stable identifier so the front end can key the list without using the index.</summary>
    [Required] public string Id { get; init; } = default!;

    [Required] public string Title { get; init; } = default!;

    [Required] public string Body { get; init; } = default!;
}
