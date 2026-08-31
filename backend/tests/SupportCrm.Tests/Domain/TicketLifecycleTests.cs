using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Domain;

/// <summary>
/// <b>A-5's graph, asserted as a graph.</b> Pure Domain unit tests — no database, no host, no HTTP:
/// legality is a Domain rule and this is where it is provable in isolation from every other rule
/// that could otherwise mask it.
///
/// <para>
/// <b>The whole 6×6 matrix is enumerated</b> rather than a handful of examples. A legality table is
/// exactly the kind of thing that is right for the cases someone thought of and wrong for the ones
/// they did not, so the test asserts the <em>complement</em> as hard as the set: every edge that is
/// not in A-5 throws.
/// </para>
/// </summary>
public sealed class TicketLifecycleTests
{
    private static readonly TicketStatus[] AllStatuses = Enum.GetValues<TicketStatus>();

    /// <summary>A-5, transcribed from docs/product-scope.md — the expected set, written independently
    /// of <see cref="TicketLifecycle"/> so the test is not the implementation restated.</summary>
    private static readonly (TicketStatus From, TicketStatus To)[] LegalEdges =
    [
        (TicketStatus.New, TicketStatus.Open),
        (TicketStatus.New, TicketStatus.Cancelled),
        (TicketStatus.Open, TicketStatus.Pending),
        (TicketStatus.Open, TicketStatus.Resolved),
        (TicketStatus.Open, TicketStatus.Cancelled),
        (TicketStatus.Pending, TicketStatus.Open),
        (TicketStatus.Pending, TicketStatus.Resolved),
        (TicketStatus.Pending, TicketStatus.Cancelled),
        (TicketStatus.Resolved, TicketStatus.Open),
        (TicketStatus.Resolved, TicketStatus.Closed),
        (TicketStatus.Resolved, TicketStatus.Cancelled),
    ];

    [Fact]
    public void The_six_statuses_are_exactly_A5s_set()
    {
        Assert.Equal(
            ["New", "Open", "Pending", "Resolved", "Closed", "Cancelled"],
            Enum.GetNames<TicketStatus>());
    }

    /// <summary>Every legal edge, and <b>every illegal one</b> — the full 6×6 matrix.</summary>
    [Fact]
    public void The_full_matrix_matches_A5_exactly()
    {
        foreach (var from in AllStatuses)
        {
            foreach (var to in AllStatuses)
            {
                var expected = LegalEdges.Contains((from, to));

                Assert.Equal(expected, TicketLifecycle.IsLegal(from, to));
            }
        }
    }

    /// <summary>The same matrix, but through the guarded mutator that actually enforces it.</summary>
    [Fact]
    public void Every_illegal_edge_is_refused_by_the_entity_itself()
    {
        foreach (var from in AllStatuses)
        {
            foreach (var to in AllStatuses)
            {
                var ticket = TicketAt(from);

                if (LegalEdges.Contains((from, to)))
                {
                    ticket.TransitionTo(to, Now);
                    Assert.Equal(to, ticket.Status);
                }
                else
                {
                    var refusal = Assert.Throws<IllegalTransitionException>(
                        () => ticket.TransitionTo(to, Now));

                    Assert.Equal(from, refusal.From);
                    Assert.Equal(to, refusal.To);

                    // Unchanged: a refused transition must not half-apply.
                    Assert.Equal(from, ticket.Status);
                }
            }
        }
    }

    /// <summary><c>Closed</c> and <c>Cancelled</c> accept no outgoing transition at all.</summary>
    [Theory]
    [InlineData(TicketStatus.Closed)]
    [InlineData(TicketStatus.Cancelled)]
    public void Terminal_statuses_have_no_outgoing_edge(TicketStatus terminal)
    {
        Assert.True(TicketLifecycle.IsTerminal(terminal));
        Assert.Empty(TicketLifecycle.LegalFrom(terminal));

        foreach (var to in AllStatuses)
        {
            Assert.Throws<IllegalTransitionException>(() => TicketAt(terminal).TransitionTo(to, Now));
        }
    }

    [Theory]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.Resolved)]
    public void Non_terminal_statuses_are_not_terminal(TicketStatus status) =>
        Assert.False(TicketLifecycle.IsTerminal(status));

    /// <summary>
    /// The set the <c>409</c> body publishes. Asserted against A-5 directly, because this is what a
    /// refused caller is told to do next.
    /// </summary>
    [Fact]
    public void LegalFrom_publishes_exactly_the_allowed_targets()
    {
        Assert.Equal(
            [TicketStatus.Open, TicketStatus.Cancelled],
            TicketLifecycle.LegalFrom(TicketStatus.New));

        Assert.Equal(
            [TicketStatus.Open, TicketStatus.Closed, TicketStatus.Cancelled],
            TicketLifecycle.LegalFrom(TicketStatus.Resolved));
    }

    /// <summary>
    /// <b>The reopen edge exists in both directions of the argument:</b> a <c>Resolved</c> ticket can
    /// go back to <c>Open</c>, and that is a legality fact — whether a <em>customer</em> may invoke
    /// it is A-16 and is tested through the API.
    /// </summary>
    [Fact]
    public void Resolved_can_be_reopened()
    {
        var ticket = TicketAt(TicketStatus.Resolved);

        ticket.TransitionTo(TicketStatus.Open, Now);

        Assert.Equal(TicketStatus.Open, ticket.Status);
    }

    /// <summary>A-5's escalation arithmetic — including the one that does nothing.</summary>
    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Urgent)]
    [InlineData(TicketPriority.Urgent, TicketPriority.Urgent)]
    public void RaiseOneLevel_climbs_one_step_and_stops_at_Urgent(
        TicketPriority from, TicketPriority expected) =>
        Assert.Equal(expected, Escalation.RaiseOneLevel(from));

    /// <summary>
    /// <b><c>ResolvedAt</c> and <c>ClosedAt</c> are lifecycle side effects</b>, stamped by the
    /// transition and never accepted from a client (docs/api-design.md §7).
    /// </summary>
    [Fact]
    public void Resolving_and_closing_stamp_their_timestamps()
    {
        var ticket = TicketAt(TicketStatus.Open);

        Assert.Null(ticket.ResolvedAt);

        ticket.TransitionTo(TicketStatus.Resolved, Now);
        Assert.Equal(Now, ticket.ResolvedAt);
        Assert.Null(ticket.ClosedAt);

        ticket.TransitionTo(TicketStatus.Closed, Now.AddHours(1));
        Assert.Equal(Now.AddHours(1), ticket.ClosedAt);

        // Resolving did not move when the ticket closed.
        Assert.Equal(Now, ticket.ResolvedAt);
    }

    /// <summary>
    /// A cancelled ticket stamps <c>ClosedAt</c> too — it reached a terminal status, and leaving the
    /// column null would make it indistinguishable from an open ticket in any "when did this end"
    /// query.
    /// </summary>
    [Fact]
    public void Cancelling_stamps_ClosedAt_and_leaves_ResolvedAt_null()
    {
        var ticket = TicketAt(TicketStatus.New);

        ticket.TransitionTo(TicketStatus.Cancelled, Now);

        Assert.Equal(Now, ticket.ClosedAt);
        Assert.Null(ticket.ResolvedAt);
    }

    /// <summary>
    /// <b>Escalation does not touch status</b> (AP-7, A-5) — asserted at the Domain level, where the
    /// two concerns are separate types and mixing them would have to be deliberate.
    /// </summary>
    [Fact]
    public void Escalation_changes_priority_only()
    {
        var ticket = TicketAt(TicketStatus.New);
        var dueBefore = (ticket.FirstResponseDueAt, ticket.ResolutionDueAt);

        ticket.ChangePriority(Escalation.RaiseOneLevel(ticket.Priority));

        Assert.Equal(TicketStatus.New, ticket.Status);
        Assert.Equal(TicketPriority.High, ticket.Priority);

        // A-20: the due timestamps freeze.
        Assert.Equal(dueBefore, (ticket.FirstResponseDueAt, ticket.ResolutionDueAt));
    }

    private static readonly DateTimeOffset Now = new(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A ticket walked to <paramref name="status"/> <b>through the real transitions</b>, never by
    /// setting the property — there is no way to set it, which is the guarantee under test.
    /// </summary>
    private static Ticket TicketAt(TicketStatus status)
    {
        var ticket = Ticket.Create(
            Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            departmentId: Guid.NewGuid(),
            subject: "Lifecycle",
            description: "A ticket to walk through A-5.",
            categoryCode: "billing",
            priority: TicketPriority.Medium,
            createdByUserId: Guid.NewGuid(),
            createdAt: Now,
            firstResponseDueAt: Now.AddHours(4),
            resolutionDueAt: Now.AddHours(24));

        foreach (var step in PathTo(status))
        {
            ticket.TransitionTo(step, Now);
        }

        return ticket;
    }

    private static TicketStatus[] PathTo(TicketStatus status) => status switch
    {
        TicketStatus.New => [],
        TicketStatus.Open => [TicketStatus.Open],
        TicketStatus.Pending => [TicketStatus.Open, TicketStatus.Pending],
        TicketStatus.Resolved => [TicketStatus.Open, TicketStatus.Resolved],
        TicketStatus.Closed => [TicketStatus.Open, TicketStatus.Resolved, TicketStatus.Closed],
        TicketStatus.Cancelled => [TicketStatus.Cancelled],
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };
}
