using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Knowledge;

namespace SupportCrm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Demo knowledge-base articles — Story 12 task 6. <c>Order = 50</c>, after
/// <see cref="IdentitySeeder"/> at 20, because every article's author is the seeded Administrator
/// and that row must already exist (A-4).
///
/// <para>
/// <b>All three types, both visibilities, and one unpublished public article.</b> The last one is
/// there to prove a rule rather than to fill a list: <c>isPublished</c> is enforced
/// <em>separately</em> from <c>visibility</c> (docs/data-model.md §5 constraint 19), so a demo needs
/// an article that is <c>Public</c> and still unreachable from the portal. Without it, "public"
/// and "visible" would look like the same fact.
/// </para>
///
/// <para>
/// <b>The bodies deliberately overlap the seeded tickets' subjects and descriptions</b> —
/// invoices, refunds, declined cards, sign-in loops, CSV exports, seats. That overlap is what makes
/// <c>GET /tickets/{id}/suggested-articles</c> return something worth looking at in the demo, which
/// the intake asks for; with unrelated text the region would render an honest but useless empty
/// state. The matching itself is unchanged — plain keyword retrieval (AD-13).
/// </para>
///
/// <para>
/// <b>Idempotent.</b> Matched on id, so re-running against an existing volume creates nothing and
/// never appends a second copy.
/// </para>
///
/// <para>
/// <b>Content is stored as authored and is never translated</b> (A-11): these bodies are English,
/// and no Arabic variant of an article exists anywhere in the model.
/// </para>
/// </summary>
public sealed class KnowledgeSeeder(
    SupportCrmDbContext db,
    TimeProvider clock,
    ILogger<KnowledgeSeeder> logger) : IDataSeeder
{
    public int Order => 50;

    /// <summary>Deterministic ids, so a manual verification step can name an article.</summary>
    public static class Articles
    {
        /// <summary>Public, published — the portal's landing content.</summary>
        public static readonly Guid PayingAnInvoice = new("55555555-5555-5555-5555-555555555501");

        public static readonly Guid RefundsAndDuplicateCharges = new("55555555-5555-5555-5555-555555555502");

        public static readonly Guid CardDeclinedAtCheckout = new("55555555-5555-5555-5555-555555555503");

        public static readonly Guid SignInRedirectLoop = new("55555555-5555-5555-5555-555555555504");

        public static readonly Guid ExportingYourData = new("55555555-5555-5555-5555-555555555505");

        public static readonly Guid AddingUserSeats = new("55555555-5555-5555-5555-555555555506");

        /// <summary>Internal — staff only, never reachable from the portal.</summary>
        public static readonly Guid RefundApprovalRunbook = new("55555555-5555-5555-5555-555555555507");

        /// <inheritdoc cref="RefundApprovalRunbook"/>
        public static readonly Guid EscalatingAnOutage = new("55555555-5555-5555-5555-555555555508");

        /// <inheritdoc cref="RefundApprovalRunbook"/>
        public static readonly Guid SeatProvisioningChecklist = new("55555555-5555-5555-5555-555555555509");

        /// <summary>
        /// <b>Public but NOT published</b> — the row that proves the two flags are independent. It
        /// must not appear in a portal search or a portal read (constraint 19).
        /// </summary>
        public static readonly Guid StatementChangesDraft = new("55555555-5555-5555-5555-555555555510");
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        var toSeed = new (Guid Id, string Title, ArticleType Type, ArticleVisibility Visibility,
            bool IsPublished, string Body)[]
        {
            // ------------------------------------------------------------ Public and published
            (Articles.PayingAnInvoice, "Paying an invoice", ArticleType.Faq,
                ArticleVisibility.Public, true,
                "## Paying an invoice\n\n" +
                "An invoice can be paid by bank transfer or by card from the billing page. A transfer " +
                "usually clears within two working days, and the invoice stays marked overdue until it " +
                "does. If an invoice you have already paid still shows as outstanding, send us the " +
                "transfer reference and the invoice number and we will reconcile it."),

            (Articles.RefundsAndDuplicateCharges, "Refunds and duplicate charges", ArticleType.HelpArticle,
                ArticleVisibility.Public, true,
                "## Refunds and duplicate charges\n\n" +
                "A duplicate charge on a statement is refunded to the original payment method. Tell us " +
                "the statement date and the amount, and we will raise the refund request. Refunds " +
                "normally appear within five working days."),

            (Articles.CardDeclinedAtCheckout, "What to do when a card is declined at checkout",
                ArticleType.SolutionGuide, ArticleVisibility.Public, true,
                "## Card declined at checkout\n\n" +
                "1. Check that the billing address matches the one held by your bank.\n" +
                "2. Try a second card, in case the first has a per-transaction limit.\n" +
                "3. If every payment attempt is declined at the final step, the block is usually with " +
                "the issuing bank rather than with us — ask them to authorise the payment, then try " +
                "again."),

            (Articles.SignInRedirectLoop, "Sign-in returns to the sign-in form", ArticleType.SolutionGuide,
                ArticleVisibility.Public, true,
                "## Sign-in returns to the sign-in form\n\n" +
                "A sign-in that redirects back to the form with no error shown is nearly always a " +
                "stored session that has expired. Clear the site cookies for the portal and sign in " +
                "again. If the loop continues, tell us the browser and the time of the attempt."),

            (Articles.ExportingYourData, "Exporting your data to CSV", ArticleType.HelpArticle,
                ArticleVisibility.Public, true,
                "## Exporting your data to CSV\n\n" +
                "Use the export control on the reporting page. An export covering more than three " +
                "months of data can fail on large accounts; splitting the date range into shorter " +
                "periods and exporting each one is the reliable way round it."),

            (Articles.AddingUserSeats, "Adding user seats to your account", ArticleType.Faq,
                ArticleVisibility.Public, true,
                "## Adding user seats\n\n" +
                "Additional seats can be added at any point in the billing cycle and are charged pro " +
                "rata from the day they are added. Tell us how many seats you need and when you would " +
                "like them to start."),

            // -------------------------------------------------------------- Internal, staff only
            (Articles.RefundApprovalRunbook, "Runbook: approving a refund", ArticleType.SolutionGuide,
                ArticleVisibility.Internal, true,
                "## Runbook: approving a refund\n\n" +
                "Confirm the duplicate charge against the statement before promising anything. A " +
                "refund above the desk limit needs a manager, and the ticket should be escalated " +
                "rather than held. Record the approval in an internal note, never in a reply."),

            (Articles.EscalatingAnOutage, "Runbook: escalating a reporting outage", ArticleType.SolutionGuide,
                ArticleVisibility.Internal, true,
                "## Runbook: escalating a reporting outage\n\n" +
                "A reporting dashboard that is unavailable for an entire organisation is an outage, " +
                "not a ticket to work through the queue. Escalate immediately, and keep the customer " +
                "updated on the same ticket rather than opening a second one."),

            (Articles.SeatProvisioningChecklist, "Checklist: provisioning additional seats",
                ArticleType.HelpArticle, ArticleVisibility.Internal, true,
                "## Checklist: provisioning additional seats\n\n" +
                "Check the account's contracted seat ceiling before confirming. Seats added mid-cycle " +
                "are billed pro rata; the billing team reconciles them at the next cycle."),

            // ------------------------------------- Public, UNPUBLISHED: the constraint-19 witness
            (Articles.StatementChangesDraft, "Changes to your statement layout (draft)", ArticleType.Faq,
                ArticleVisibility.Public, false,
                "## Changes to your statement layout\n\n" +
                "This article is a draft. It is deliberately left unpublished so that a public " +
                "article which is still invisible to the portal exists in the demo data: visibility " +
                "and publication are two separate facts, and a portal read requires both."),
        };

        var seeded = 0;

        foreach (var row in toSeed)
        {
            // Matched on id, so re-running against an existing volume changes nothing.
            if (await db.KnowledgeArticles.AnyAsync(a => a.Id == row.Id, ct))
            {
                continue;
            }

            db.KnowledgeArticles.Add(KnowledgeArticle.Create(
                row.Id,
                row.Title,
                row.Body,
                row.Type,
                row.Visibility,
                row.IsPublished,

                // A-4: authoring is an Administrator capability, so the demo author is the seeded
                // Administrator and not an agent.
                IdentitySeeder.Users.Administrator,
                now));

            seeded++;
        }

        if (seeded == 0)
        {
            return;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeded {Count} knowledge articles.", seeded);
    }
}
