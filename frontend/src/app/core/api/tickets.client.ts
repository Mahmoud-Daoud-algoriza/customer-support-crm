import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';
import { AttachmentMetadata } from './attachments.client';
import { Paged, PageRequest } from './paged';

/** The six statuses of A-5 — and no others. Stable string codes (docs/api-design.md §2). */
export const TICKET_STATUSES = ['New', 'Open', 'Pending', 'Resolved', 'Closed', 'Cancelled'] as const;

export type TicketStatus = (typeof TICKET_STATUSES)[number];

/** The four levels of A-6. The array order is the severity order. */
export const TICKET_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;

export type TicketPriority = (typeof TICKET_PRIORITIES)[number];

/** The customer as the staff ticket payload carries them — docs/api-design.md §6.4. No branch. */
export interface TicketCustomer {
    id: string;
    fullName: string;
    email: string;
}

/**
 * `Ticket (staff)` — docs/api-design.md §6.4, the shape of `GET /tickets/{id}`.
 *
 * **`assignee` may be non-null while `status` is `New`** (A-18): assignment is not the start of
 * work. Nothing rendering this may infer one from the other — the header shows them as two
 * independent facts.
 *
 * **`firstRespondedAt` may remain null on a resolved ticket** (finding PF-5). That is not a defect
 * and must not be rendered as one.
 */
export interface Ticket {
    id: string;
    subject: string;
    description: string;
    customer: TicketCustomer;
    departmentId: string;
    categoryCode: string;
    priority: TicketPriority;
    status: TicketStatus;
    isUrgent: boolean;
    assignee?: UserSummary | null;
    createdBy: UserSummary;
    createdAt: string;
    firstResponseDueAt: string;
    resolutionDueAt: string;
    firstRespondedAt?: string | null;
    resolvedAt?: string | null;
    closedAt?: string | null;
    firstResponseBreached: boolean;
    resolutionBreached: boolean;
}

/**
 * `Activity entry` — docs/api-design.md §6.4, the row shape of `GET /tickets/{id}/activity`.
 *
 * **`actor` is absent exactly when `actorKind` is `System`** — the SLA monitor, and nothing else.
 * The automatic `Pending → Open` carries `actorKind: 'User'` and the **replying customer** as actor
 * (**R-14**), so a row rendering "System" for it would be wrong.
 *
 * Nulls are **omitted** rather than sent, so every optional member here is `?`.
 */
export interface TicketActivityEntry {
    id: string;
    occurredAt: string;
    activityType: string;
    actorKind: 'User' | 'System';
    actor?: UserSummary | null;
    oldValue?: string | null;
    newValue?: string | null;
    visibility: 'CustomerVisible' | 'Internal';
    messageId?: string | null;
    internalNoteId?: string | null;
}

/**
 * `TicketListItem` — the row shape of `GET /tickets` (docs/api-design.md §6.4).
 *
 * **Everything the queue renders and nothing more** (docs/ui-design.md §5.1): no description, no
 * `isUrgent`, no lifecycle timestamps. A list must not ship every ticket's full text.
 */
export interface TicketListItem {
    id: string;
    subject: string;
    customer: { id: string; fullName: string };
    status: TicketStatus;
    priority: TicketPriority;
    categoryCode: string;
    departmentId: string;
    assignee?: UserSummary | null;
    createdAt: string;
    resolutionDueAt: string;
    firstResponseBreached: boolean;
    resolutionBreached: boolean;
}

/**
 * `POST /tickets` — docs/api-design.md §5.6.
 *
 * **`departmentId` is optional**: omitted, the server derives it from the category → department map
 * (A-14); supplied, it overrides, because the mapping is a default and not a cage for agents.
 *
 * **There is no `isUrgent` here, and there must never be.** It is customer input only (A-17), so
 * the server's request model has no such property and a body carrying one is a `400` (AP-10). The
 * portal's own creation endpoint (Story 07) accepts it.
 *
 * **There is no `status` either** — status is server-derived and changes only through Story 06's
 * transition endpoint (AP-1).
 */
export interface CreateTicketRequest {
    customerId: string;
    subject: string;
    description: string;
    categoryCode: string;
    priority: TicketPriority;
    departmentId?: string | null;
}

/**
 * `PATCH /tickets/{id}` — **exactly two patchable fields** (docs/api-design.md §5.6).
 *
 * **Changing `priority` does not move the SLA due dates** — they freeze at creation (**A-20**,
 * closing OQ-2). A screen must not recompute or re-render them as though they had shifted.
 */
export interface PatchTicketRequest {
    categoryCode?: string;
    priority?: TicketPriority;
}

/**
 * `GET /tickets` filters — docs/api-design.md §5.6, and exactly those seven. **The names mirror the
 * API exactly** (UI-9), so the ticket list's URL query parameters need no translation step.
 *
 * `assigneeId` accepts the literal `me`, which is what produces the agent's own queue.
 *
 * **`departmentId` narrows; it can never widen.** The server scopes first, so an agent supplying
 * another department's id gets an empty page — not an error, and not another department's rows.
 *
 * **There is no branch filter, and there must never be one** (A-2, T2-K, docs/ui-design.md §5.2).
 */
export interface TicketListFilter extends PageRequest {
    status?: TicketStatus | null;
    priority?: TicketPriority | null;
    categoryCode?: string | null;
    assigneeId?: string | null;
    departmentId?: string | null;
    breached?: boolean | null;
    q?: string | null;
}

/**
 * The typed client for the staff ticket endpoints Story 05 publishes (docs/api-design.md §5.6).
 * Feature components never call `HttpClient` directly (docs/architecture.md §2.2).
 *
 * **There is no delete method here, and there never will be** — a ticket is cancelled, never
 * deleted. Story 06 adds `transition`, `escalate` and `activity`.
 */
@Injectable({ providedIn: 'root' })
export class TicketsClient extends ApiClientBase {
    list(filter: TicketListFilter): Observable<Paged<TicketListItem>> {
        return this.get<Paged<TicketListItem>>('tickets', filter as Record<string, QueryValue>);
    }

    getTicket(id: string): Observable<Ticket> {
        return this.get<Ticket>(`tickets/${id}`);
    }

    create(request: CreateTicketRequest): Observable<Ticket> {
        return this.post<Ticket>('tickets', request);
    }

    patchTicket(id: string, request: PatchTicketRequest): Observable<Ticket> {
        return this.patch<Ticket>(`tickets/${id}`, request);
    }

    /**
     * `POST /tickets/{id}/assignment` — assign or reassign.
     *
     * **The response's `status` is unchanged** (A-18). An out-of-department assignee comes back as
     * `422 assignee-out-of-department`, which the detail screen renders inline.
     */
    assign(id: string, assignedUserId: string): Observable<Ticket> {
        return this.post<Ticket>(`tickets/${id}/assignment`, { assignedUserId });
    }

    /**
     * `POST /tickets/{id}/transition` — the one endpoint for the whole A-5 × A-16 matrix (AP-6).
     *
     * **Four distinguishable refusals**, each rendered differently by the caller: `404` for a ticket
     * outside scope, `403 transition-not-permitted` for A-16 authority, `409 illegal-transition` for
     * A-5 legality — carrying `allowedTransitions` — and `400` for a status name that is not one of
     * the six.
     */
    transition(id: string, targetStatus: TicketStatus): Observable<Ticket> {
        return this.post<Ticket>(`tickets/${id}/transition`, { targetStatus });
    }

    /**
     * `POST /tickets/{id}/escalate` — **its own endpoint, never part of `transition`** (AP-7).
     *
     * **No body**: the effect is fixed — priority up exactly one level, `Urgent` stays `Urgent`,
     * status unchanged. It returns `200` even when the department has no manager, because under
     * **A-21** the notification climbs to the next authority level and an empty recipient set never
     * blocks the escalation.
     */
    escalate(id: string): Observable<Ticket> {
        return this.post<Ticket>(`tickets/${id}/escalate`, {});
    }

    /**
     * `GET /tickets/{id}/activity` — the append-only history, chronological.
     *
     * **Internal entries are included** (docs/api-design.md §5.6): this is the staff read, and the
     * route is staff-only. The customer-facing filter is the server's timeline projection, not this
     * client's job.
     */
    activity(id: string, paging?: PageRequest): Observable<Paged<TicketActivityEntry>> {
        return this.get<Paged<TicketActivityEntry>>(`tickets/${id}/activity`, paging as Record<string, QueryValue>);
    }

    attachments(id: string, paging?: PageRequest): Observable<Paged<AttachmentMetadata>> {
        return this.get<Paged<AttachmentMetadata>>(`tickets/${id}/attachments`, paging as Record<string, QueryValue>);
    }

    /** `multipart/form-data` (AP-13). The part is named `file`, which is what the controller binds. */
    upload(id: string, file: File): Observable<AttachmentMetadata> {
        const form = new FormData();
        form.append('file', file, file.name);

        return this.post<AttachmentMetadata>(`tickets/${id}/attachments`, form);
    }
}
