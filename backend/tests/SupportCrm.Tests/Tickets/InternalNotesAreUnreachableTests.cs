namespace SupportCrm.Tests.Tickets;

/// <summary>
/// <b>The home the portal-thread isolation check needs the moment internal notes exist.</b>
///
/// <para>
/// Story 07's Done Criterion says <em>"messages are visible to the ticket's customer; internal notes
/// are not"</em>, and today the second half is <b>structural rather than testable</b>:
/// <c>TicketInternalNote</c> is Story 14's entity, <c>/tickets/{id}/internal-notes</c> is Story 14's
/// endpoint, and <c>PortalTicketsController</c> has no route that could reach either. There is
/// nothing to leak, so there is nothing to assert — the exclusion holds because the table is absent
/// from every portal read, not because a filter excludes it (docs/data-model.md §2.9, T2-C, AP-5).
/// </para>
///
/// <para>
/// <b>The skipped test below is deliberate and must not be deleted.</b> Story 07's plan task 6 asks
/// for exactly this stub, so that the day internal notes land there is already a named place for
/// the check rather than a gap someone has to notice. <b>Story 14 unskips it and fills the body</b>
/// — it must not be made vacuous instead.
/// </para>
/// </summary>
public sealed class InternalNotesAreUnreachableTests
{
    /// <summary>
    /// <b>Story 14 (agent-workspace/14-story-tasks-internal-notes) owns this test.</b>
    ///
    /// <para>
    /// What it must prove once <c>TicketInternalNote</c> exists:
    /// </para>
    /// <list type="number">
    ///   <item><description>An agent posts an internal note on a ticket the customer owns.</description></item>
    ///   <item><description><c>GET /portal/tickets/{id}/messages</c> does <b>not</b> contain it — not its body, not its id, not a placeholder.</description></item>
    ///   <item><description>The customer's interaction timeline does not contain it either (docs/data-model.md §5 constraint 18).</description></item>
    ///   <item><description>No <c>/portal</c> route reaches <c>/internal-notes</c> at all — a customer token on the staff route is refused by the role gate.</description></item>
    /// </list>
    /// </summary>
    [Fact(Skip = "Story 14 introduces TicketInternalNote and unskips this. See the class remarks.")]
    public void Internal_notes_never_appear_in_a_portal_thread()
    {
        // Intentionally empty: Story 14 writes the body together with the entity it asserts about.
        // An assertion here today would either pass vacuously or test nothing that exists.
        Assert.Fail("Story 14 must implement this test, not delete it.");
    }
}
