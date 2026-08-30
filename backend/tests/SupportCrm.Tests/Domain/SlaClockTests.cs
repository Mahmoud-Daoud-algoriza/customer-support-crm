using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Domain;

/// <summary>
/// A <b>pure Domain unit test — no database, no host</b> (Story 05 plan task 10).
///
/// <para>
/// The whole of A-3's arithmetic is that the clock is <b>24/7 wall-clock</b>: it crosses midnight,
/// weekends and holidays without noticing them, because there is nothing to notice. These tests
/// assert exactly that, which means they also pin the absence of business hours — the behaviour
/// product-scope §9 question 5 keeps deliberately open.
/// </para>
/// </summary>
public sealed class SlaClockTests
{
    private static readonly SlaTargets FourAndTwentyFour = new(FirstResponseHours: 4, ResolutionHours: 24);

    /// <summary>Both deadlines come from the configured hours, added to the creation instant.</summary>
    [Fact]
    public void ComputeAtCreation_adds_the_configured_hours()
    {
        var createdAt = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

        var (firstResponseDueAt, resolutionDueAt) =
            SlaClock.ComputeAtCreation(createdAt, TicketPriority.High, FourAndTwentyFour);

        Assert.Equal(createdAt.AddHours(4), firstResponseDueAt);
        Assert.Equal(createdAt.AddHours(24), resolutionDueAt);
    }

    /// <summary>
    /// <b>Across midnight.</b> A ticket raised late in the evening has a deadline the next day, and
    /// the clock does not pause overnight.
    /// </summary>
    [Fact]
    public void The_clock_runs_across_midnight()
    {
        var createdAt = new DateTimeOffset(2026, 3, 10, 22, 30, 0, TimeSpan.Zero);

        var (firstResponseDueAt, _) =
            SlaClock.ComputeAtCreation(createdAt, TicketPriority.High, FourAndTwentyFour);

        Assert.Equal(new DateTimeOffset(2026, 3, 11, 2, 30, 0, TimeSpan.Zero), firstResponseDueAt);
    }

    /// <summary>
    /// <b>Across a weekend.</b> 2026-03-13 is a Friday; a 24-hour resolution target lands on the
    /// Saturday, not on the following Monday. **No business hours, no holiday calendar** (A-3).
    /// </summary>
    [Fact]
    public void The_clock_runs_across_a_weekend()
    {
        var createdAt = new DateTimeOffset(2026, 3, 13, 16, 0, 0, TimeSpan.Zero);

        Assert.Equal(DayOfWeek.Friday, createdAt.DayOfWeek);

        var (_, resolutionDueAt) =
            SlaClock.ComputeAtCreation(createdAt, TicketPriority.High, FourAndTwentyFour);

        Assert.Equal(new DateTimeOffset(2026, 3, 14, 16, 0, 0, TimeSpan.Zero), resolutionDueAt);
        Assert.Equal(DayOfWeek.Saturday, resolutionDueAt.DayOfWeek);
    }

    /// <summary>
    /// <b>A-20 — <c>OnPriorityChanged</c> is a no-op, and that is the rule, not an omission.</b>
    /// It leaves both timestamps exactly where <c>ComputeAtCreation</c> put them, whichever
    /// direction the priority moves.
    /// <para>
    /// This test is not named by the plan. It exists because A-20's implementation is an empty
    /// method body: without an assertion, "recompute was rejected" survives only as a comment, and
    /// a later contributor could restore the rejected reading with nothing going red. Finding I-15.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(TicketPriority.Urgent)]
    [InlineData(TicketPriority.Low)]
    public void OnPriorityChanged_does_not_move_either_due_timestamp(TicketPriority newPriority)
    {
        var createdAt = new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero);

        var (firstResponseDueAt, resolutionDueAt) =
            SlaClock.ComputeAtCreation(createdAt, TicketPriority.Medium, FourAndTwentyFour);

        var ticket = Ticket.Create(
            Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            departmentId: Guid.NewGuid(),
            subject: "Freeze",
            description: "The deadline must not move.",
            categoryCode: "billing",
            priority: TicketPriority.Medium,
            createdByUserId: Guid.NewGuid(),
            createdAt: createdAt,
            firstResponseDueAt: firstResponseDueAt,
            resolutionDueAt: resolutionDueAt);

        ticket.ChangePriority(newPriority);
        SlaClock.OnPriorityChanged(ticket, newPriority);

        Assert.Equal(newPriority, ticket.Priority);
        Assert.Equal(firstResponseDueAt, ticket.FirstResponseDueAt);
        Assert.Equal(resolutionDueAt, ticket.ResolutionDueAt);
    }
}
