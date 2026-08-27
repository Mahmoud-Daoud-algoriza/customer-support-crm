namespace SupportCrm.Tests.Organization;

/// <summary>
/// <b>The acceptance criterion that matters most</b>, from the `departments-branches` intake:
/// <em>"Branch is demonstrably NOT a permission boundary: an agent can see in-department tickets
/// regardless of the customer's branch."</em>
/// <para>
/// It cannot be fully exercised until tickets exist, so this file is delivered in two halves:
/// </para>
/// <list type="number">
///   <item>
///     <b>Story 03 (here):</b> a source-level assertion that <c>Ticket</c> has no branch member at
///     all — the structural half of docs/data-model.md §2.3. It is <b>skipped</b>, because
///     <c>Ticket</c> does not exist yet, and it is enabled by Story 05.
///   </item>
///   <item>
///     <b>Story 05:</b> the behavioural half — an agent sees an in-department ticket whose customer
///     is in a different branch — is added to <b>this same file</b> by
///     <c>.squad/plans/ticket-management/05-story-ticket-core.md</c> <b>task 10</b>.
///   </item>
/// </list>
/// <para>
/// <b>Story 05 task 10 must remove the <c>Skip</c> below.</b> The pair is cross-referenced in both
/// directions so it cannot be lost: Story 03's plan names task 10, and task 10 names this file.
/// </para>
/// </summary>
public sealed class BranchIsNotABoundaryTests
{
    /// <summary>
    /// The assembly-qualified name is used rather than <c>typeof(Ticket)</c> because the type does
    /// not exist yet and the test project must still compile. Story 05 may replace this with
    /// <c>typeof(Ticket)</c> once it does.
    /// </summary>
    private const string TicketTypeName = "SupportCrm.Domain.Modules.Tickets.Ticket, SupportCrm.Domain";

    [Fact(Skip = "Enabled by Story 05 task 10, which creates Domain/Modules/Tickets/Ticket.cs. " +
                 "Ticket does not exist yet, so there is nothing to assert about its members.")]
    public void Ticket_has_no_branch_member()
    {
        var ticket = Type.GetType(TicketTypeName, throwOnError: false);

        Assert.NotNull(ticket);

        // Ticket deliberately has NO branchId (docs/data-model.md §2.3). A ticket's branch is
        // derived Ticket -> Customer -> Branch, and the absence of the column is what makes misuse
        // impossible rather than merely discouraged: a branch value within reach of the ticket
        // scoping helper is a value A-2 forbids that helper to read.
        //
        // Should a branch-level access rule ever be required, that contradicts A-2 and is a SCOPE
        // CHANGE to be raised against docs/product-scope.md first — not a fix to this test.
        var branchMembers = ticket!
            .GetMembers()
            .Where(m => m.Name.Contains("Branch", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(branchMembers);
    }
}
