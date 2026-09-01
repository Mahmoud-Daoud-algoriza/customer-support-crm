using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <c>POST /portal/tickets/{id}/feedback</c> — <b>the sole CSAT input in the system</b>
/// (requirements §8.5, T2-F, docs/api-design.md §5.7, docs/data-model.md §2.15).
///
/// <para>
/// <b>It lives in the <c>Tickets</c> module</b> (<b>DM-7</b>). <c>customer-portal</c> is an Angular
/// area and a planning slug, not a backend module (docs/architecture.md §1) — so there is no
/// eleventh module here, and this file sits beside the lifecycle service whose <c>Resolved</c>
/// transition is what makes feedback available at all.
/// </para>
///
/// <para>
/// <b>Four preconditions, in this order, and the order is the contract:</b>
/// </para>
/// <list type="number">
///   <item><description><b>Scope</b> — another customer's ticket is <c>404</c>, worded identically to one that does not exist (<b>AP-4</b>). It comes first so nothing below can confirm that a ticket exists.</description></item>
///   <item><description><b>Reached <c>Resolved</c></b> — otherwise <c>409</c>. T2-F offers the rating <em>when a ticket reaches <c>Resolved</c></em>, so a rating on a ticket that never did is a state conflict, not a validation error.</description></item>
///   <item><description><b>One per ticket</b> — an existing row is <c>409 feedback-already-submitted</c> (§5.7, docs/api-design.md §2.2).</description></item>
///   <item><description><b>Rating inside the configured range</b> — outside is <c>400</c> (<b>OQ-1</b>).</description></item>
/// </list>
///
/// <para>
/// <b>There is no update and no delete method, and there must never be one.</b> §2.15: write-once,
/// <em>"not editable, not resubmittable"</em>. <b>And there is nothing to call to decline</b> —
/// declining is simply never calling <see cref="SubmitAsync"/>, so the absence of a row is the
/// recorded outcome (T2-F). Reporting reads that absence as <em>"no response"</em>, never as a zero.
/// </para>
///
/// <para>
/// <b>⚠ OQ-1 — no rating constant appears in this file.</b> The permitted range is read from
/// <see cref="FeedbackOptions"/>, the <c>Feedback rating scale</c> key of docs/architecture.md §6.3
/// whose <b>values are deliberately undecided</b>. docs/data-model.md §2.15 forbids inferring a
/// range into a validation rule, and docs/api-design.md §5.7 says the contract fixes none — so this
/// service validates against configuration and asserts nothing about which numbers are right.
/// Answering OQ-1 is a configuration edit, not a code change.
/// </para>
/// </summary>
public sealed class CustomerFeedbackService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketActivityRecorder activity,
    IOptions<FeedbackOptions> feedbackOptions,
    TimeProvider clock)
{
    /// <summary>
    /// Records the one rating this ticket will ever have.
    ///
    /// <para>
    /// <b>One unit of work, committed once</b> (docs/architecture.md §3): the feedback row and its
    /// <c>FeedbackSubmitted</c> activity entry are added together and saved together, so no committed
    /// state has the rating without the history entry that explains where it came from.
    /// </para>
    /// </summary>
    public async Task<FeedbackDto> SubmitAsync(
        Guid ticketId, int rating, string? comment, CancellationToken ct)
    {
        // 1. Scope, first — fetch-then-authorize (docs/architecture.md §4.3). LoadScopedAsync is the
        //    write-path loader: it composes ForCaller and answers 404 for absent OR out-of-scope,
        //    which is the same answer AP-4 requires for another customer's ticket.
        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        // 2. "Has reached Resolved" — read from ResolvedAt, not from the current status.
        //
        //    Status alone cannot answer the question: TransitionTo stamps ResolvedAt when the ticket
        //    reaches Resolved and never clears it, so ResolvedAt is the ONLY record that the ticket
        //    ever got there. That matters twice: a Closed ticket is reached FROM Resolved and must
        //    qualify (the plan says so explicitly), and a ticket a customer reopened has been
        //    Resolved and is now Open — a status check would wrongly refuse the first and, if it
        //    listed Open, wrongly accept a ticket that was never resolved at all.
        if (ticket.ResolvedAt is null)
        {
            throw new ConflictException(
                NotResolved,
                "Feedback can be given once a request has been resolved.");
        }

        // 3. One per ticket (§2.15, §5 constraint 21). Checked here so the answer is a readable 409
        //    rather than a unique-index violation surfacing as a 500 — the index behind it is the
        //    guarantee, this is the message.
        if (await db.CustomerFeedback.AnyAsync(f => f.TicketId == ticket.Id, ct))
        {
            throw new ConflictException(
                AlreadySubmitted, "Feedback has already been submitted for this request.");
        }

        // 4. The configured range — OQ-1. Read from configuration on every call, never cached into a
        //    constant, so the answer to OQ-1 takes effect by redeploying configuration (T2-I).
        var scale = feedbackOptions.Value;

        if (rating < scale.Min || rating > scale.Max)
        {
            // A 400 naming the allowed range, exactly as an unknown filter value does — never a
            // silent clamp, which would store an answer the customer did not give.
            throw new ValidationException(
                $"A rating must be between {scale.Min} and {scale.Max}.");
        }

        var submittedAt = clock.GetUtcNow();

        var feedback = CustomerFeedback.Submit(
            Guid.NewGuid(), ticket.Id, rating, comment, submittedAt);

        db.CustomerFeedback.Add(feedback);

        // The history spine records it like every other thing that happened to the ticket
        // (docs/data-model.md §2.7). Attributed to the caller — the customer — through the recorder's
        // ICurrentUser, because an activity actor is server-derived (docs/api-design.md §7).
        //
        // No oldValue/newValue: FeedbackSubmitted is not a change to a field, and putting the rating
        // in newValue would publish the score into a customer-visible history row that §2.7 does not
        // define as carrying one.
        await activity.RecordAsync(ticket.Id, TicketActivityType.FeedbackSubmitted, ct: ct);

        await db.SaveChangesAsync(ct);

        return new FeedbackDto(
            feedback.Id, feedback.TicketId, feedback.Rating, feedback.Comment, feedback.SubmittedAt);
    }

    /// <summary>The slug docs/api-design.md §5.7 fixes for a second submission.</summary>
    public const string AlreadySubmitted = "feedback-already-submitted";

    /// <summary>
    /// A ticket that has never reached <c>Resolved</c>.
    ///
    /// <para>
    /// <b>A new slug, and deliberately not <c>feedback-already-submitted</c>.</b>
    /// docs/api-design.md §5.7 names two preconditions and fixes a slug for one of them, so this
    /// second refusal needs a <c>type</c> of its own — §6.12 requires a stable slug per error class
    /// precisely so the front end can translate by it. Reusing the other would tell a customer their
    /// rating was already recorded when in fact it was too early, which is the opposite mistake.
    /// This follows the precedent Story 07 set with <c>ticket-terminal</c>: a distinct 409 gets a
    /// distinct slug, recorded as a decision rather than folded into an existing one. <b>Recorded as
    /// finding I-36</b>, because §6.12 enumerates the published slugs and this adds one — whether
    /// that enumeration is meant to be exhaustive is the user's call, not this file's.
    /// </para>
    /// </summary>
    public const string NotResolved = "feedback-not-available";
}
