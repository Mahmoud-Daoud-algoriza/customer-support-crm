using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;

namespace SupportCrm.Application.Modules.Administration;

/// <summary>
/// <c>GET /audit</c> — the read surface over the append-only security log
/// (docs/architecture.md §2.4, docs/api-design.md §5.12, §6.9).
///
/// <para>
/// <b>This service exposes exactly one method.</b> There is no create, update or delete — the log
/// is append-only by construction (T2-H), and <c>AuditRecorder</c> is the only writer, called from
/// Application services, never from here or from a controller.
/// </para>
///
/// <para>
/// <b>Newest first</b>, using the <c>AuditEntry(OccurredAt)</c> and <c>AuditEntry(ActorUserId)</c>
/// indexes (docs/data-model.md §6). This is a different question from
/// <c>TicketActivityQueryService</c>'s chronological read — that is a history read forwards; this is
/// "what happened most recently" (AD-10).
/// </para>
/// </summary>
public sealed class AuditQueryService(IApplicationDbContext db)
{
    public async Task<PagedResult<AuditEntryDto>> ListAsync(
        AuditListFilter filter, PageQuery? page, CancellationToken ct)
    {
        var (pageNumber, pageSize) = page.Normalize();

        var query = db.AuditEntries.AsNoTracking();

        // Filters are named for the field they filter; different parameters AND together
        // (docs/api-design.md §2.1, §5.12).
        if (filter.ActorUserId is { } actorUserId)
        {
            query = query.Where(a => a.ActorUserId == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (filter.From is { } from)
        {
            query = query.Where(a => a.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(a => a.OccurredAt <= to);
        }

        var projected = query
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)   // a stable tiebreak, so paging is meaningful within one timestamp
            .Select(a => new AuditEntryDto(
                a.Id,
                a.OccurredAt,
                a.ActorUserId == null
                    ? null
                    : db.Users.Where(u => u.Id == a.ActorUserId)
                        .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).FirstOrDefault(),
                a.ActorDescriptor,
                a.Action,
                a.TargetType,
                a.TargetId,
                a.Outcome.ToString()));

        return await projected.ToPagedResultAsync(pageNumber, pageSize, ct);
    }
}

/// <summary>
/// docs/api-design.md §6.9 <c>AuditEntry</c>, member for member.
/// <b><see cref="Actor"/> is null exactly when no user could be resolved</b> — a failed sign-in —
/// and <see cref="ActorDescriptor"/> then carries the submitted identifier
/// (docs/data-model.md §2.14).
/// </summary>
public sealed record AuditEntryDto(
    Guid Id,
    DateTimeOffset OccurredAt,
    UserSummaryDto? Actor,
    string? ActorDescriptor,
    string Action,
    string? TargetType,
    Guid? TargetId,
    string Outcome);

/// <summary>Filters for <c>GET /audit</c> — docs/api-design.md §5.12.</summary>
public sealed record AuditListFilter
{
    public Guid? ActorUserId { get; init; }

    public string? Action { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}
