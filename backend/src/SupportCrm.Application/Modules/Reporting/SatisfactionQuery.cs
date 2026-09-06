using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Reporting;

/// <summary>
/// §9.4 — average rating and response count over <c>CustomerFeedback</c>, joined to the filtered
/// ticket set (T2-G, docs/api-design.md §6.8).
///
/// <para>
/// <b>"Empty is not zero" is the whole rule of this query.</b> When no ratings exist,
/// <c>averageRating</c> is <b>null</b> and never <c>0.0</c> — data-model §2.15 makes declining a
/// <em>normal outcome</em>, so the absence of a row means <em>"no response"</em>, and a zero would
/// read as universal dissatisfaction. §6.8 states the same rule in the contract.
/// </para>
///
/// <para>
/// <b>OQ-1 is not answered here and nothing in this file assumes a scale.</b> The configured
/// <c>min</c>–<c>max</c> travels with the average so the front end can render <em>"4.2 of 5"</em>
/// without hardcoding a denominator; the values come from the same <c>FeedbackOptions</c> key
/// <c>GET /config</c> publishes, read on every call. <b>No range literal appears anywhere below.</b>
/// </para>
///
/// <para>
/// <b>The feedback set is narrowed through the ticket</b>, so the department and branch filters
/// reach it without this query re-expressing either — the ticket set arrives already filtered.
/// </para>
/// </summary>
public static class SatisfactionQuery
{
    public static async Task<SatisfactionDto> RunAsync(
        IQueryable<Ticket> filtered,
        IApplicationDbContext db,
        FeedbackOptions feedback,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtered);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(feedback);

        var ticketIds = filtered.Select(t => t.Id);

        var ratings = db.CustomerFeedback.AsNoTracking()
            .Where(f => ticketIds.Contains(f.TicketId));

        // Both aggregates in one round trip. AVG over an empty set is null in SQL, which is exactly
        // the behaviour §6.8 requires — so the null is the database's answer rather than a
        // special case written here, and it cannot be got wrong by a later edit.
        var summary = await ratings
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Average = g.Average(f => (double?)f.Rating),
                Count = g.Count(),
            })
            .FirstOrDefaultAsync(ct);

        // No rows at all means the GroupBy produced nothing: responseCount 0, averageRating null.
        var responseCount = summary?.Count ?? 0;
        var averageRating = summary?.Average is { } value ? Math.Round(value, 1) : (double?)null;

        return new SatisfactionDto(
            averageRating,
            responseCount,
            new RatingScaleDto(feedback.Min, feedback.Max));
    }
}
