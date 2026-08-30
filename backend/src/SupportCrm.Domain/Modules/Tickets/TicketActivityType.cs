namespace SupportCrm.Domain.Modules.Tickets;

/// <summary>
/// The twelve activity types of docs/data-model.md §2.7, each traceable to a requirement.
/// <para>
/// Persisted as a <b>stable string code</b> (docs/api-design.md §2).
/// </para>
/// <para>
/// <b>Story 05 writes four of them</b> — <see cref="Created"/>, <see cref="Assigned"/>,
/// <see cref="PriorityChanged"/> and <see cref="CategoryChanged"/>. The rest are declared here
/// because the set is the model's, not this story's, and a later story adding a member would be a
/// data-model change rather than the ordinary use §2.7 describes:
/// <see cref="StatusChanged"/> and <see cref="Escalated"/> are Story 06's,
/// <see cref="SlaBreached"/> Story 09's, <see cref="MessagePosted"/> Story 07's,
/// <see cref="InternalNotePosted"/> Story 14's, the two AI members Story 11's, and
/// <see cref="FeedbackSubmitted"/> Story 13's.
/// </para>
/// </summary>
public enum TicketActivityType
{
    Created,
    StatusChanged,
    Assigned,
    PriorityChanged,
    CategoryChanged,
    Escalated,
    SlaBreached,
    MessagePosted,
    InternalNotePosted,
    AiSuggestionOffered,
    AiSuggestionResolved,
    FeedbackSubmitted,
}
