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
/// <b>Every ticket is <c>New</c> and unresolved.</b> Story 05 has no transition path — Story 06
/// adds it — so seeding a `Resolved` row would require reaching past the state machine that does
/// not exist yet. The statuses become varied when Story 06 can move them legally.
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
        var toSeed = new (Guid Id, Guid CustomerId, string CategoryCode, TicketPriority Priority,
            string Subject, string Description, Guid CreatedBy, Guid? AssignedTo, bool IsUrgent)[]
        {
            (Tickets.BillingOverdueInvoice, CustomerSeeder.Customers.AminaHaddad, "billing",
                TicketPriority.High, "Invoice 10432 is overdue but was paid",
                "The customer says invoice 10432 was paid by transfer on the 3rd, but the account still shows it outstanding.",
                IdentitySeeder.Users.BillingAgent, IdentitySeeder.Users.BillingAgent, false),

            (Tickets.BillingRefundRequest, CustomerSeeder.Customers.BrunoOkafor, "billing",
                TicketPriority.Low, "Refund request for duplicate charge",
                "Duplicate charge on the March statement. The customer would like the second one refunded.",
                IdentitySeeder.Users.BillingAgent, null, false),

            // Customer in North Branch, agent is not branch-scoped — the row Story 03's branch test
            // needs (A-2). Urgent, so the queue's SLA-urgency default sort has something to lead on.
            (Tickets.PaymentsCardDeclined, CustomerSeeder.Customers.ChenWei, "payments",
                TicketPriority.Urgent, "Card declined at checkout every time",
                "Every payment attempt is declined at the final step. The customer has tried two cards.",
                IdentitySeeder.Users.BillingAgent, IdentitySeeder.Users.BillingAgent, true),

            (Tickets.TechnicalLoginLoop, CustomerSeeder.Customers.DianaRossi, "technical",
                TicketPriority.Urgent, "Sign-in redirects in a loop",
                "After entering credentials the page returns to the sign-in form with no error shown.",
                IdentitySeeder.Users.TechnicalAgent, IdentitySeeder.Users.TechnicalAgent, true),

            (Tickets.TechnicalExportFails, CustomerSeeder.Customers.AminaHaddad, "technical",
                TicketPriority.Medium, "CSV export fails for large date ranges",
                "Exporting more than three months of data returns an error page. Smaller ranges work.",
                IdentitySeeder.Users.TechnicalAgent, null, false),

            (Tickets.AccountSeatRequest, CustomerSeeder.Customers.ChenWei, "account",
                TicketPriority.Medium, "Request two additional user seats",
                "The customer would like two more seats added to their account before the next billing cycle.",
                IdentitySeeder.Users.TechnicalAgent, null, false),
        };

        var seeded = 0;

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

            var (firstResponseDueAt, resolutionDueAt) =
                SlaClock.ComputeAtCreation(now, row.Priority, TargetsFor(row.Priority));

            var ticket = Ticket.Create(
                row.Id,
                row.CustomerId,
                category.DepartmentId,
                row.Subject,
                row.Description,
                category.Code,
                row.Priority,
                row.CreatedBy,
                now,
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
                Guid.NewGuid(), ticket.Id, TicketActivityType.Created, row.CreatedBy, now));

            if (row.AssignedTo is { } assignedTo)
            {
                var assigneeName = await db.Users
                    .Where(u => u.Id == assignedTo)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(ct);

                db.TicketActivities.Add(TicketActivity.ByUser(
                    Guid.NewGuid(), ticket.Id, TicketActivityType.Assigned, row.CreatedBy, now,
                    newValue: assigneeName));
            }

            seeded++;
        }

        if (seeded > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("TicketSeeder: {Tickets} ticket(s) seeded.", seeded);
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
