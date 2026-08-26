using Microsoft.EntityFrameworkCore;

namespace SupportCrm.Application.Abstractions;

/// <summary>
/// The paged envelope every collection endpoint returns (AP-3, docs/api-design.md §2.1).
/// Story 02's <c>GET /users</c> is the first endpoint to use it; every later list reuses it.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

/// <summary>
/// The paging and sorting inputs every collection endpoint accepts (docs/api-design.md §2.1).
/// </summary>
public sealed record PageQuery
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;

    /// <summary>1-based, default 1.</summary>
    public int? Page { get; init; }

    /// <summary>Default 25, capped at 100.</summary>
    public int? PageSize { get; init; }

    /// <summary><c>field:direction</c>, restricted to a per-endpoint whitelist (AP-15).</summary>
    public string? Sort { get; init; }
}

public static class PagedQueryExtensions
{
    /// <summary>
    /// Normalizes page and pageSize. A page below 1 or a pageSize below 1 falls back to the default
    /// rather than erroring: the contract fixes bounds, not a rejection rule
    /// (docs/api-design.md §2.1).
    /// </summary>
    public static (int Page, int PageSize) Normalize(this PageQuery? query)
    {
        var page = query?.Page is > 0 ? query.Page.Value : 1;
        var pageSize = query?.PageSize is > 0
            ? Math.Min(query.PageSize.Value, PageQuery.MaxPageSize)
            : PageQuery.DefaultPageSize;

        return (page, pageSize);
    }

    /// <summary>
    /// Parses <c>field:direction</c> against a whitelist. An unknown field — or an unknown
    /// direction — is a <c>400</c>, never silently ignored (AP-15, docs/api-design.md §2.1).
    /// </summary>
    public static (string Field, bool Descending) ParseSort(
        this PageQuery? query, IReadOnlyDictionary<string, string> whitelist, string defaultField)
    {
        if (string.IsNullOrWhiteSpace(query?.Sort))
        {
            return (defaultField, false);
        }

        var parts = query.Sort.Split(':', 2, StringSplitOptions.TrimEntries);
        var requested = parts[0];

        if (!whitelist.TryGetValue(requested, out var field))
        {
            throw new ValidationException(
                $"Unknown sort field '{requested}'. Sortable fields: {string.Join(", ", whitelist.Keys)}.");
        }

        var direction = parts.Length > 1 ? parts[1] : "asc";

        return direction switch
        {
            "asc" => (field, false),
            "desc" => (field, true),
            _ => throw new ValidationException(
                $"Unknown sort direction '{direction}'. Use 'asc' or 'desc'."),
        };
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct)
    {
        var totalItems = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PagedResult<T>(items, page, pageSize, totalItems, totalPages);
    }
}
