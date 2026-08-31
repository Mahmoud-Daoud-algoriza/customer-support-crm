import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientBase } from './api-client.base';
import { PageRequest } from './paged';

/** The four notification types of A-13 (docs/data-model.md §2.12) — and there are no others. */
export type NotificationType = 'TicketAssigned' | 'SlaBreached' | 'TicketEscalated' | 'CustomerReplied';

/**
 * One notification row — docs/api-design.md §6.6.
 *
 * **`ticketSubject` is projected by the server** so a row is readable without a call per
 * notification. **There is no `recipientUserId`**: every row in the response belongs to the caller by
 * construction, so echoing the id back would be the one field that told the reader nothing.
 */
export interface NotificationRow {
    id: string;
    type: NotificationType;
    ticketId: string;
    ticketSubject: string;
    createdAt: string;
    /** Absent when unread — the API omits null properties. */
    readAt?: string | null;
}

/**
 * `GET /notifications` — **the standard paged envelope plus `unreadCount` at the top level**
 * (docs/api-design.md §6.6), which is what the bell's badge renders.
 *
 * `unreadCount` is the caller's **total** unread, independent of paging and of `unreadOnly`. A badge
 * showing the unread rows on the current page would be wrong in the only way a badge can be.
 */
export interface NotificationPage {
    items: NotificationRow[];
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
    unreadCount: number;
}

/**
 * The two notification endpoints of docs/api-design.md §5.10 — and **only** those two.
 *
 * **There is no create method**: notifications are raised by the server as a consequence of
 * assignment, breach, escalation or a customer reply, never by a client. **And there is no
 * `markAllRead`** — `POST /notifications/read-all` was removed from the contract as unrequested
 * surface (AP-18), so there is nothing here to call and no control anywhere that would want it.
 *
 * **Nothing here is cached.** Unlike departments or configuration, this list changes while the user
 * is looking at it — the sweep runs on a timer — so a `shareReplay` would pin a stale badge.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsClient extends ApiClientBase {
    list(unreadOnly = false, paging?: PageRequest): Observable<NotificationPage> {
        return this.get<NotificationPage>('notifications', {
            ...(paging ?? {}),
            // `false` is the server's default, so it is left off rather than sent — the URL says
            // what was asked for and nothing more.
            ...(unreadOnly ? { unreadOnly: true } : {})
        });
    }

    /** `204`, and **idempotent**: a second read leaves the original `readAt` untouched. */
    markRead(id: string): Observable<void> {
        return this.post<void>(`notifications/${id}/read`, {});
    }
}
