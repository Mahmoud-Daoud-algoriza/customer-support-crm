using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Sla;

namespace SupportCrm.Application.Modules.Sla;

/// <summary>
/// One notification row — docs/api-design.md §6.6:
/// <c>{ id, type, ticketId, ticketSubject, createdAt, readAt }</c>.
///
/// <para>
/// <b><c>ticketSubject</c> is a projection, not a stored field</b> (§6): it is here so a list row is
/// readable without a call per notification, which is the difference between a usable panel and
/// twenty round trips behind a badge.
/// </para>
///
/// <para>
/// <b>No <c>recipientUserId</c>.</b> Every row in the response belongs to the caller by construction
/// — the query is recipient-scoped — so echoing the id back would be the only field on the payload
/// that told the reader nothing.
/// </para>
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Type,
    Guid TicketId,
    string TicketSubject,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

/// <summary>
/// <c>GET /notifications</c> — <b>the standard paged envelope plus <c>unreadCount</c> at the top
/// level</b> (docs/api-design.md §6.6). The badge renders that number.
///
/// <para>
/// <b><c>UnreadCount</c> is the caller's total unread, not the unread within this page.</b> A badge
/// that showed "3" because page 1 happened to hold three unread rows would be wrong in the only way
/// a badge can be wrong. It is therefore counted independently of the paging and independently of
/// <c>unreadOnly</c>.
/// </para>
/// </summary>
public sealed record NotificationPage(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    int UnreadCount)
{
    public static NotificationPage From(PagedResult<NotificationDto> page, int unreadCount) =>
        new(page.Items, page.Page, page.PageSize, page.TotalItems, page.TotalPages, unreadCount);
}
