using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Demo tickets — Story 05 task 9. <c>Order = 40</c>, after <see cref="CustomerSeeder"/> at 30,
/// because every ticket needs a customer, a department and a creating agent that already exist.
///
/// <para>
/// <b>The spread is the point.</b> The intake's acceptance criterion asks for <em>"enough tickets
/// across departments and priorities to demonstrate filtering"</em>, so the set below covers
/// <b>both departments</b>, <b>all four priorities</b>, <b>several categories</b> and a
/// <b>mix of assigned and unassigned</b> rows. A demo where every ticket looks alike cannot
/// demonstrate a filter.
/// </para>
///
/// <para>
/// <b>At least one ticket has a customer in a different branch from its assigned agent</b>, and it
/// is the row Story 03's branch test needs: an agent must reach an in-department ticket regardless
/// of the customer's branch (A-2, docs/data-model.md §5 constraint 6). Chen Wei and Diana Rossi are
/// in North Branch; the agents are not branch-scoped at all.
/// </para>
///
/// <para>
/// <b>Story 07 gave two of the six a history.</b> Story 05 left every ticket <c>New</c> because it
/// had no transition path; Story 06 added the guarded <c>TransitionTo</c>, and Story 07 added the
/// messages a thread is made of. Two tickets now move through legal A-5 edges and carry a thread:
/// </para>
/// <list type="bullet">
///   <item><description><b>A two-way thread</b> on the payments ticket — agent, customer, agent — so the staff thread, the portal thread and the AI assists of Story 11 all have real correspondence to render.</description></item>
///   <item><description><b>One ticket left in <c>Pending</c></b>, with a portal login that owns it. That pairing is the point: <b>the R-13 automatic reopen is demonstrable in one click</b> — sign in as that customer, post a reply, and the ticket returns to <c>Open</c> with a <c>StatusChanged</c> row attributed to them (R-14).</description></item>
/// </list>
///
/// <para>
/// <b>Story 13 gave the portal something to demonstrate.</b> The four screens of
/// docs/ui-design.md §7 each need a request in a particular state, and a demo where every request
/// looks alike cannot show any of them. Three <c>Resolved</c> requests were added, and the mix is
/// the point:
/// </para>
/// <list type="bullet">
///   <item><description><b>One <c>Resolved</c> request per portal customer with <em>no</em> feedback</b> — so the rating control is actually offered when either of them signs in (T2-F).</description></item>
///   <item><description><b>One <c>Resolved</c> request that <em>is</em> rated</b> — so Story 15's §9.4 satisfaction tile has a real average rather than an empty set, and so the "already rated, no control" branch of §7.3 is visible too.</description></item>
///   <item><description>The <b><c>New</c></b> and <b><c>Pending</c></b> rows already here carry the other two demonstrations: <b>cancel</b> is offered only while <c>New</c> (A-16, A-18) and the <b>automatic reopen</b> fires only from <c>Pending</c> (R-13).</description></item>
/// </list>
///
/// <para>
/// <b>The seeded rating is read from the configured scale, never written as a literal (OQ-1).</b>
/// docs/data-model.md §2.15 forbids inferring a range anywhere, and a hardcoded <c>5</c> here would
/// be exactly that — invisible, and wrong the moment OQ-1 answers differently. See
/// <see cref="SeedFeedback"/>.
/// </para>
///
/// <para>
/// <b>Every status here was reached through <see cref="Ticket.TransitionTo"/></b>, never by writing
/// the column: A-5's graph refuses an illegal edge however the ticket was reached, <em>including
/// from a seeder</em>. The rows left <c>New</c> are the honest state for a ticket nobody has picked
/// up.
/// </para>
///
/// <para>
/// <b>Due timestamps come from <see cref="SlaClock"/> and the configured targets</b>, not from
/// literals: the seeded data must be the same arithmetic the endpoints use, or the queue's
/// SLA-urgency ordering would be demonstrated against numbers nothing else produces.
/// </para>
/// </summary>
public sealed class TicketSeeder(
    SupportCrmDbContext db,
    IOptions<CategoryOptions> categoryOptions,
    IOptions<SlaTargetOptions> slaTargetOptions,
    IOptions<FeedbackOptions> feedbackOptions,
    TimeProvider clock,
    ILogger<TicketSeeder> logger) : IDataSeeder
{
    public int Order => 40;

    /// <summary>Deterministic ids, so a later story's seeder or a manual check can name a ticket.</summary>
    public static class Tickets
    {
        public static readonly Guid BillingOverdueInvoice = new("44444444-4444-4444-4444-444444444401");
        public static readonly Guid BillingRefundRequest = new("44444444-4444-4444-4444-444444444402");
        public static readonly Guid PaymentsCardDeclined = new("44444444-4444-4444-4444-444444444403");
        public static readonly Guid TechnicalLoginLoop = new("44444444-4444-4444-4444-444444444404");
        public static readonly Guid TechnicalExportFails = new("44444444-4444-4444-4444-444444444405");
        public static readonly Guid AccountSeatRequest = new("44444444-4444-4444-4444-444444444406");

        // Story 09 — deliberately created in the PAST so their SLA deadlines have already passed.
        public static readonly Guid BillingBreachedStatement = new("44444444-4444-4444-4444-444444444407");
        public static readonly Guid TechnicalBreachedOutage = new("44444444-4444-4444-4444-444444444408");

        // ---------------------------------------------------------------- Story 13 (task 6)
        // Three RESOLVED requests, one per portal-visible demonstration of §7.3's feedback block.
        // Both portal customers get an unrated one, so whichever login is used the control appears.
        public static readonly Guid BillingResolvedLatePayment = new("44444444-4444-4444-4444-444444444409");
        public static readonly Guid TechnicalResolvedSlowReports = new("44444444-4444-4444-4444-444444444410");

        /// <summary>The one that <b>is</b> rated — Story 15's satisfaction tile reads it.</summary>
        public static readonly Guid AccountResolvedRatedRename = new("44444444-4444-4444-4444-444444444411");
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        // Six tickets: 3 Billing (billing, billing, payments) and 3 Technical (technical,
        // technical, account); all four priorities present; three assigned and three not.
        //
        // The `assignee` column is deliberately null on half of them — Story 05 delivers MANUAL
        // assignment only, and the registered auto-assignment policy assigns nobody (T2-D is
        // Story 09's), so an unassigned demo ticket is the honest state rather than an omission.
        // `CreatedHoursAgo` backdates the SLA clock origin. It is 0 for every ticket except the two
        // Story 09 adds, whose deadlines must already have passed — see the note on those rows.
        var toSeed = new (Guid Id, Guid CustomerId, string CategoryCode, TicketPriority Priority,
            string Subject, string Description, Guid CreatedBy, Guid? AssignedTo, bool IsUrgent,
            int CreatedHoursAgo)[]
        {
            (Tickets.BillingOverdueInvoice, CustomerSeeder.Customers.AminaHaddad, "billing",
                TicketPriority.High, "Invoice 10432 is overdue but was paid",
                "The customer says invoice 10432 was paid by transfer on the 3rd, but the account still shows it outstanding.",
                IdentitySeeder.Users.BillingAgent, IdentitySeeder.Users.BillingAgent, false, 0),

            (Tickets.BillingRefundRequest, CustomerSeeder.Customers.BrunoOkafor, "billing",
                TicketPriority.Low, "Refund request for duplicate charge",
                "Duplicate charge on the March statement. The customer would like the second one refunded.",
                IdentitySeeder.Users.BillingAgent, null, false, 0),

            // Customer in North Branch, agent is not branch-scoped — the row Story 03's branch test
            // needs (A-2). Urgent, so the queue's SLA-urgency default sort has something to lead on.
            (Tickets.PaymentsCardDeclined, CustomerSeeder.Customers.ChenWei, "payments",
                TicketPriority.Urgent, "Card declined at checkout every time",
                "Every payment attempt is declined at the final step. The customer has tried two cards.",
                IdentitySeeder.Users.BillingAgent, IdentitySeeder.Users.BillingAgent, true, 0),

            (Tickets.TechnicalLoginLoop, CustomerSeeder.Customers.DianaRossi, "technical",
                TicketPriority.Urgent, "Sign-in redirects in a loop",
                "After entering credentials the page returns to the sign-in form with no error shown.",
                IdentitySeeder.Users.TechnicalAgent, IdentitySeeder.Users.TechnicalAgent, true, 0),

            (Tickets.TechnicalExportFails, CustomerSeeder.Customers.AminaHaddad, "technical",
                TicketPriority.Medium, "CSV export fails for large date ranges",
                "Exporting more than three months of data returns an error page. Smaller ranges work.",
                IdentitySeeder.Users.TechnicalAgent, null, false, 0),

            (Tickets.AccountSeatRequest, CustomerSeeder.Customers.ChenWei, "account",
                TicketPriority.Medium, "Request two additional user seats",
                "The customer would like two more seats added to their account before the next billing cycle.",
                IdentitySeeder.Users.TechnicalAgent, null, false, 0),

            // ---------------------------------------------------------------- Story 09 (task 9)
            // **These two are created in the past on purpose, and a seeded breach is not a bug.**
            // The SLA sweep flags what is overdue; with every seeded ticket created "now", the first
            // sweep after a fresh volume would find nothing and the queue's breached-first ordering,
            // the notification badge and Story 15's SLA tile would all demo against zero rows.
            //
            // The deadlines are not fabricated: `SlaClock.ComputeAtCreation` runs on a backdated
            // `createdAt`, so these rows carry exactly the deadlines A-3's arithmetic gives a ticket
            // that genuinely was opened four and two days ago. One per department, so both halves of
            // the reporting split have breach data.
            //
            // `High` resolves in 24h and `Urgent` in 8h, so both are comfortably past due.
            (Tickets.BillingBreachedStatement, CustomerSeeder.Customers.BrunoOkafor, "billing",
                TicketPriority.High, "Statement shows charges from a closed account",
                "Three charges on the latest statement belong to an account the customer closed last year.",
                IdentitySeeder.Users.BillingAgent, IdentitySeeder.Users.BillingAgent, false,
                CreatedHoursAgo: 96),

            (Tickets.TechnicalBreachedOutage, CustomerSeeder.Customers.DianaRossi, "technical",
                TicketPriority.Urgent, "Reporting dashboard has been unavailable since Monday",
                "The reporting dashboard returns a server error for every user in the customer's organisation.",
                IdentitySeeder.Users.TechnicalAgent, null, true,
                CreatedHoursAgo: 48),

            // ---------------------------------------------------------------- Story 13 (task 6)
            // **All three belong to a customer who HAS a portal login** — Amina and Chen Wei — which
            // is the whole point: a Resolved ticket whose customer cannot sign in demonstrates
            // nothing. They are backdated a little so the resolution reads as having happened, and
            // so the thread below them sits in the past.
            (Tickets.BillingResolvedLatePayment, CustomerSeeder.Customers.AminaHaddad, "billing",
                TicketPriority.Medium, "Late-payment fee applied after a bank delay",
                "A fee was charged because the transfer arrived a day late. The customer asked whether it can be waived.",
                IdentitySeeder.Users.BillingAgent, IdentitySeeder.Users.BillingAgent, false,
                CreatedHoursAgo: 30),

            (Tickets.TechnicalResolvedSlowReports, CustomerSeeder.Customers.ChenWei, "technical",
                TicketPriority.Low, "Reports took several minutes to open",
                "Opening the monthly report was very slow last week. It seems better now, but the customer wants it checked.",
                IdentitySeeder.Users.TechnicalAgent, IdentitySeeder.Users.TechnicalAgent, false,
                CreatedHoursAgo: 26),

            (Tickets.AccountResolvedRatedRename, CustomerSeeder.Customers.AminaHaddad, "account",
                TicketPriority.Medium, "Rename the account to the new company name",
                "The company changed name last month and the account should show the new one on invoices.",
                IdentitySeeder.Users.TechnicalAgent, IdentitySeeder.Users.TechnicalAgent, false,
                CreatedHoursAgo: 22),
        };

        var seeded = 0;

        // The tickets this run actually created. A re-run against an existing volume creates none,
        // and therefore seeds no thread either — the thread belongs to the ticket, and appending a
        // second copy of it on every startup would be the one way this seeder stops being
        // idempotent.
        var created = new Dictionary<Guid, Ticket>();

        foreach (var row in toSeed)
        {
            // Matched on id, so re-running against an existing volume changes nothing.
            if (await db.Tickets.AnyAsync(t => t.Id == row.Id, ct))
            {
                continue;
            }

            // A-14: the department comes from the category map, exactly as CreateAsync derives it.
            // A dangling category is a startup failure (ConfigurationValidator), so a miss here
            // means the seeder and the configuration disagree — worth failing loudly.
            var category = categoryOptions.Value.Items
                               .FirstOrDefault(c => string.Equals(
                                   c.Code, row.CategoryCode, StringComparison.OrdinalIgnoreCase))
                           ?? throw new InvalidOperationException(
                               $"TicketSeeder references category '{row.CategoryCode}', which is not configured.");

            // The SLA clock origin for this row. Backdated only for the Story 09 rows; `now` for
            // every other, so nothing about the original six changed.
            var createdAt = now.AddHours(-row.CreatedHoursAgo);

            var (firstResponseDueAt, resolutionDueAt) =
                SlaClock.ComputeAtCreation(createdAt, row.Priority, TargetsFor(row.Priority));

            var ticket = Ticket.Create(
                row.Id,
                row.CustomerId,
                category.DepartmentId,
                row.Subject,
                row.Description,
                category.Code,
                row.Priority,
                row.CreatedBy,
                createdAt,
                firstResponseDueAt,
                resolutionDueAt,
                row.IsUrgent);

            if (row.AssignedTo is { } assignee)
            {
                // A-18: assigning does not change status. The seeded rows stay `New` with an
                // assignee set, which is exactly the shape the UI must render as two facts.
                ticket.Assign(assignee);
            }

            db.Tickets.Add(ticket);

            // The history spine starts with the creation, and — where the row is assigned — the
            // assignment, because the intake requires every assignment change to be recorded.
            // Written directly rather than through TicketActivityRecorder: the recorder resolves its
            // actor from ICurrentUser, and a seeder runs at startup with no request and no caller.
            db.TicketActivities.Add(TicketActivity.ByUser(
                Guid.NewGuid(), ticket.Id, TicketActivityType.Created, row.CreatedBy, createdAt));

            if (row.AssignedTo is { } assignedTo)
            {
                var assigneeName = await db.Users
                    .Where(u => u.Id == assignedTo)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(ct);

                db.TicketActivities.Add(TicketActivity.ByUser(
                    Guid.NewGuid(), ticket.Id, TicketActivityType.Assigned, row.CreatedBy, createdAt,
                    newValue: assigneeName));
            }

            created[ticket.Id] = ticket;
            seeded++;
        }

        var messages = SeedThreads(created, now);
        var ratings = SeedFeedback(created, now);

        if (seeded > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "TicketSeeder: {Tickets} ticket(s), {Messages} message(s) and {Ratings} rating(s) seeded.",
            seeded, messages, ratings);
    }

    /// <summary>
    /// Gives two of the seeded tickets a lifecycle and a thread — Story 07 task 5.
    ///
    /// <para>
    /// <b>Nothing here writes a status column.</b> Every move goes through
    /// <see cref="Ticket.TransitionTo"/>, so the seeder is held to A-5's graph exactly as an endpoint
    /// is, and a future edit that seeds an illegal edge fails at startup rather than producing a
    /// ticket no transition could have reached.
    /// </para>
    ///
    /// <para>
    /// <b>Activity rows are written directly rather than through <c>TicketActivityRecorder</c></b>,
    /// for the reason the creation loop already records: the recorder resolves its actor from
    /// <c>ICurrentUser</c>, and a seeder runs at startup with no request and no caller. The
    /// <c>MessagePosted</c> rows use <see cref="TicketActivity.MessagePosted"/>, which is the same
    /// factory the recorder uses — so §5 constraint 17's <em>"exactly one activity row per
    /// message"</em> holds for seeded rows too.
    /// </para>
    ///
    /// <para>
    /// <b>Timestamps are a few SECONDS apart, and the unit matters.</b> They must be far enough
    /// apart to give the thread a deterministic order — and so <c>firstRespondedAt</c> is provably
    /// the <em>first</em> outbound message rather than whichever row happened to be inserted first —
    /// but they must also all be in the <b>past</b> by the time anyone can call the API.
    /// <para>
    /// Minutes were tried first and were wrong. Seeding runs at startup, the API becomes reachable
    /// seconds later, and a demo reply posted then carries the real clock — so a thread seeded at
    /// <c>now + 10 minutes</c> renders the live reply <em>above</em> the seeded question it answers,
    /// and the conversation reads backwards. Found by posting the R-13 reply against the running
    /// stack, which is the only place it is visible: the test host seeds nothing.
    /// </para>
    /// </para>
    /// </summary>
    private int SeedThreads(IReadOnlyDictionary<Guid, Ticket> created, DateTimeOffset now)
    {
        var messages = 0;

        // ---- The two-way thread. Agent, customer, agent — the shape Story 11's summariser and
        //      Story 13's portal detail both need something real to render.
        if (created.TryGetValue(Tickets.PaymentsCardDeclined, out var payments))
        {
            Transition(payments, TicketStatus.Open, IdentitySeeder.Users.BillingAgent, now.AddSeconds(5));

            messages += Post(
                payments, IdentitySeeder.Users.BillingAgent, MessageDirection.Outbound,
                "Thanks for reporting this. Which two cards did you try, and did either give a code?",
                now.AddSeconds(10));

            messages += Post(
                payments, CustomerSeeder.PortalUsers.ChenWei, MessageDirection.Inbound,
                "A Visa ending 4321 and a Mastercard ending 8890. Neither showed a code, just \"declined\".",
                now.AddSeconds(40));

            messages += Post(
                payments, IdentitySeeder.Users.BillingAgent, MessageDirection.Outbound,
                "Understood — I am raising this with the payments team and will come back to you today.",
                now.AddSeconds(55));
        }

        // ---- The Pending ticket. Its customer HAS a portal login (CustomerSeeder seeds Amina's),
        //      which is what makes the R-13 reopen a one-click demonstration rather than a story.
        if (created.TryGetValue(Tickets.BillingOverdueInvoice, out var invoice))
        {
            Transition(invoice, TicketStatus.Open, IdentitySeeder.Users.BillingAgent, now.AddSeconds(5));

            messages += Post(
                invoice, IdentitySeeder.Users.BillingAgent, MessageDirection.Outbound,
                "Could you send the transfer reference for the payment on the 3rd? I will match it against the invoice.",
                now.AddSeconds(15));

            // Waiting on the customer — and LEFT here. A reply from Amina's portal login reopens it
            // automatically (R-13), writing a StatusChanged row attributed to her (R-14).
            Transition(invoice, TicketStatus.Pending, IdentitySeeder.Users.BillingAgent, now.AddSeconds(20));
        }

        // ---- Story 13. Three requests taken all the way to `Resolved`, each with the outbound
        //      message that resolved it — a resolution with no explanation would render an empty
        //      thread under a feedback control, which is not what a customer would be rating.
        //
        //      The path is New → Open → Resolved, both edges legal under A-5 and both taken through
        //      TransitionTo, so ResolvedAt is stamped by the entity exactly as an endpoint stamps it.
        //      That matters: `ResolvedAt` is what CustomerFeedbackService reads to decide whether a
        //      request has REACHED Resolved, so a seeder writing the column directly would have
        //      produced rows the feedback endpoint refuses.
        messages += Resolve(
            created, Tickets.BillingResolvedLatePayment, IdentitySeeder.Users.BillingAgent,
            "I have waived the fee and it will drop off the next statement. Sorry for the trouble.",
            now);

        messages += Resolve(
            created, Tickets.TechnicalResolvedSlowReports, IdentitySeeder.Users.TechnicalAgent,
            "We found a slow query behind the monthly report and fixed it. Opening times are back to normal.",
            now);

        messages += Resolve(
            created, Tickets.AccountResolvedRatedRename, IdentitySeeder.Users.TechnicalAgent,
            "The account now shows the new company name, and the next invoice will carry it.",
            now);

        return messages;
    }

    /// <summary>
    /// One request taken from <c>New</c> to <c>Resolved</c>, with the outbound message that resolved
    /// it. Both transitions go through <see cref="Ticket.TransitionTo"/> and the message through
    /// <see cref="Post"/>, so a seeded resolution is indistinguishable from one an agent performed —
    /// including the <c>firstRespondedAt</c> stamp and the two <c>StatusChanged</c> history rows.
    /// </summary>
    private int Resolve(
        IReadOnlyDictionary<Guid, Ticket> created,
        Guid ticketId,
        Guid agentUserId,
        string closingMessage,
        DateTimeOffset now)
    {
        if (!created.TryGetValue(ticketId, out var ticket))
        {
            // A re-run against an existing volume created nothing, so there is nothing to resolve —
            // the same idempotence rule SeedThreads follows.
            return 0;
        }

        Transition(ticket, TicketStatus.Open, agentUserId, now.AddSeconds(5));

        var messages = Post(
            ticket, agentUserId, MessageDirection.Outbound, closingMessage, now.AddSeconds(15));

        Transition(ticket, TicketStatus.Resolved, agentUserId, now.AddSeconds(20));

        return messages;
    }

    /// <summary>
    /// The one seeded rating — Story 13 task 6, so Story 15's §9.4 satisfaction tile is non-trivial
    /// and §7.3's "already rated" branch is demonstrable.
    ///
    /// <para>
    /// <b>⚠ The value comes from the configured scale, never from a literal (OQ-1).</b>
    /// docs/data-model.md §2.15 states that no minimum, maximum or step <em>"may be inferred from
    /// this document into a validation rule, a check constraint, or a UI control"</em> — and a
    /// hardcoded number in a seeder is the quietest place of all to encode one. It seeds
    /// <see cref="FeedbackOptions.Max"/>: the top of whatever range is configured is *a satisfied
    /// answer* under every candidate OQ-1 might choose, including a binary one, so the row stays
    /// meaningful rather than becoming out of range the day the key changes.
    /// </para>
    ///
    /// <para>
    /// <b>The other two <c>Resolved</c> requests are deliberately left unrated.</b> §2.15: declining
    /// is a normal outcome and the absence of a row is meaningful — so the demo carries both cases,
    /// and reporting has "no response" rows to treat as such rather than as zeros.
    /// </para>
    /// </summary>
    private int SeedFeedback(IReadOnlyDictionary<Guid, Ticket> created, DateTimeOffset now)
    {
        if (!created.TryGetValue(Tickets.AccountResolvedRatedRename, out var ticket))
        {
            return 0;
        }

        db.CustomerFeedback.Add(CustomerFeedback.Submit(
            Guid.NewGuid(),
            ticket.Id,
            feedbackOptions.Value.Max,
            "Handled quickly and the invoice looks right now.",
            now.AddSeconds(30)));

        // The history spine records it, exactly as CustomerFeedbackService does. Written directly
        // rather than through TicketActivityRecorder for the reason the rest of this seeder records:
        // the recorder resolves its actor from ICurrentUser, and a seeder runs with no request.
        // Attributed to the CUSTOMER'S portal login, because that is who submits a rating.
        db.TicketActivities.Add(TicketActivity.ByUser(
            Guid.NewGuid(),
            ticket.Id,
            TicketActivityType.FeedbackSubmitted,
            CustomerSeeder.PortalUsers.AminaHaddad,
            now.AddSeconds(30)));

        return 1;
    }

    /// <summary>
    /// One legal A-5 edge, with the <c>StatusChanged</c> row that every transition writes
    /// (docs/data-model.md §2.7).
    /// </summary>
    private void Transition(Ticket ticket, TicketStatus target, Guid actorUserId, DateTimeOffset at)
    {
        var previous = ticket.Status;

        ticket.TransitionTo(target, at);

        db.TicketActivities.Add(TicketActivity.ByUser(
            Guid.NewGuid(), ticket.Id, TicketActivityType.StatusChanged, actorUserId, at,
            previous.ToString(), target.ToString()));
    }

    /// <summary>
    /// One message, its single <c>MessagePosted</c> row, and — on the first outbound one — the
    /// <c>firstRespondedAt</c> stamp. The same three effects <c>TicketMessageService</c> produces,
    /// through the same entity methods, so seeded data and endpoint data are indistinguishable.
    /// <para>
    /// <b><c>channel</c> is <c>Portal</c> for both directions</b>, which is what the two endpoints
    /// write (docs/api-design.md §7). Story 18's adapter is what introduces a third value — and it
    /// needs no schema change to do it, which is the seam's whole claim.
    /// </para>
    /// </summary>
    private int Post(
        Ticket ticket,
        Guid authorUserId,
        MessageDirection direction,
        string body,
        DateTimeOffset at)
    {
        var message = TicketMessage.Post(
            Guid.NewGuid(), ticket.Id, authorUserId, direction, MessageChannel.Portal, body, at);

        db.TicketMessages.Add(message);

        db.TicketActivities.Add(TicketActivity.MessagePosted(
            Guid.NewGuid(), ticket.Id, message.Id, authorUserId, at));

        if (direction is MessageDirection.Outbound)
        {
            // Idempotent on the entity, so the second outbound message leaves the stamp alone.
            ticket.MarkFirstResponded(at);
        }

        return 1;
    }

    /// <summary>
    /// The configured targets for a priority. Startup validation guarantees every priority has
    /// exactly one (ConfigurationValidator check 2), so a miss is a wiring fault.
    /// </summary>
    private SlaTargets TargetsFor(TicketPriority priority)
    {
        var target = slaTargetOptions.Value.Items
                         .FirstOrDefault(t => string.Equals(
                             t.Priority, priority.ToString(), StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException(
                         $"No SLA target is configured for priority '{priority}'.");

        return new SlaTargets(target.FirstResponseHours, target.ResolutionHours);
    }
}
