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
 * `Message` — docs/api-design.md §6.4, the **staff** shape of `GET /tickets/{id}/messages`.
 *
 * **`direction` and `channel` are server-derived** (§7, **PF-7**): direction from the author's
 * role, channel from the endpoint used. Neither is ever sent — see `postMessage` below.
 *
 * **The portal has its own type** (`PortalMessage` in `portal.client.ts`), which omits `channel`
 * and `authorRole`. They are deliberately not the same type: AP-5 splits the path spaces, and one
 * shared interface would make the portal's narrowing an optional field rather than a fact.
 */
export interface TicketMessage {
    id: string;
    ticketId: string;
    author: UserSummary;
    authorRole: string;
    direction: MessageDirection;
    channel: MessageChannel;
    body: string;
    postedAt: string;
}

/** Customer → `Inbound`, staff → `Outbound`. Decided by the server, never by this client. */
export type MessageDirection = 'Inbound' | 'Outbound';

/**
 * The channel a message arrived on — the seam of docs/architecture.md §5.2. Two values are real
 * today; **a new one arrives with the adapter that implements it** (Story 18), and this union is
 * the only place the front end would learn about it.
 */
export type MessageChannel = 'WebForm' | 'Portal';

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
 * `InternalNote` — the shape of `GET /tickets/{id}/internal-notes` (docs/api-design.md §6.4).
 *
 * **There is deliberately no portal counterpart of this interface** — not a narrowed one, not one
 * with fields omitted. A narrowed type would imply a portal read exists to narrow, and the whole
 * T2-C design is that none does (docs/data-model.md §2.9, AP-5).
 *
 * **No `visibility` member**, because there is no column: a note is internal by being a note.
 */
export interface InternalNote {
    id: string;
    ticketId: string;
    author: UserSummary;
    body: string;
    createdAt: string;
}

/**
 * `Task` — the shape of `GET /tickets/{id}/tasks` (docs/api-design.md §6.4).
 *
 * **`completedAt` is server-set** and is absent while the task is not done: nulls are omitted from
 * every payload (docs/api-design.md §2), so `isDone` is the flag to branch on, never the presence
 * of this field.
 */
export interface TicketTask {
    id: string;
    ticketId: string;
    title: string;
    dueAt: string;
    assignee: UserSummary;
    isDone: boolean;
    completedAt?: string;
    createdBy: UserSummary;
    createdAt: string;
}

/**
 * The body of `POST /tickets/{id}/tasks`. All three are required (docs/data-model.md §2.10).
 *
 * **`isDone` and `completedAt` are absent on purpose** — a task is created not-done and the
 * timestamp is the server's (AP-10).
 */
export interface CreateTicketTaskRequest {
    title: string;
    dueAt: string;
    assignedUserId: string;
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

    /**
     * `GET /tickets/{id}/messages` — the customer-visible thread, **oldest first**, because a
     * conversation is read forwards.
     *
     * **This is not the internal-notes endpoint.** Internal notes are a different entity on a
     * different route (Story 14, T2-C), so no merged list is filtered here and a rendering bug
     * cannot leak one.
     */
    messages(id: string, paging?: PageRequest): Observable<Paged<TicketMessage>> {
        return this.get<Paged<TicketMessage>>(`tickets/${id}/messages`, paging as Record<string, QueryValue>);
    }

    /**
     * `POST /tickets/{id}/messages` — an outbound reply.
     *
     * **The body carries `body` and nothing else.** `direction`, `channel`, the author and the
     * timestamp are all server-derived, and sending any of them is a `400` (AP-10, §7) — so this
     * signature takes a string, not an object a caller could add a field to.
     *
     * **An agent reply never transitions the ticket**, which is why this returns a bare message
     * rather than the portal's `{ message, ticketStatus, statusChanged }` envelope. A reply on a
     * `Closed` or `Cancelled` ticket comes back as `409 ticket-terminal`.
     */
    postMessage(id: string, body: string): Observable<TicketMessage> {
        return this.post<TicketMessage>(`tickets/${id}/messages`, { body });
    }

    /**
     * `GET /tickets/{id}/internal-notes` — **staff only, by path** (AP-5, T2-C).
     *
     * **There is no portal counterpart of this method, and none may be added.** `portal.client.ts`
     * has no route that could reach internal notes, which is how the visibility rule holds: a
     * customer cannot see a note because nothing on their side can ask for one, not because a
     * filter removes it (docs/data-model.md §2.9).
     *
     * **Its own endpoint, deliberately.** The notes region never filters a merged list — there is no
     * merged list — so a rendering bug cannot leak one (docs/ui-design.md §5.3).
     */
    internalNotes(id: string, paging?: PageRequest): Observable<Paged<InternalNote>> {
        return this.get<Paged<InternalNote>>(`tickets/${id}/internal-notes`, paging as Record<string, QueryValue>);
    }

    /**
     * `POST /tickets/{id}/internal-notes` — `{ body }` and nothing else.
     *
     * **No `visibility` is sent, and there is no parameter for one.** A note is internal by virtue
     * of being a note; author and timestamp are server-derived, and sending any of the three is a
     * `400` (AP-10). A note on a `Closed` or `Cancelled` ticket comes back `409 ticket-terminal`.
     */
    postInternalNote(id: string, body: string): Observable<InternalNote> {
        return this.post<InternalNote>(`tickets/${id}/internal-notes`, { body });
    }

    /** `GET /tickets/{id}/tasks` — the ticket's to-dos, soonest due first. */
    tasks(id: string, paging?: PageRequest): Observable<Paged<TicketTask>> {
        return this.get<Paged<TicketTask>>(`tickets/${id}/tasks`, paging as Record<string, QueryValue>);
    }

    /**
     * `POST /tickets/{id}/tasks` — `{ title, dueAt, assignedUserId }`.
     *
     * An assignee outside the ticket's department, or deactivated, comes back
     * `422 assignee-out-of-department` — the same slug ticket assignment uses, so the error layer
     * already has its translation.
     */
    createTask(id: string, request: CreateTicketTaskRequest): Observable<TicketTask> {
        return this.post<TicketTask>(`tickets/${id}/tasks`, request);
    }

    /**
     * `PATCH /tickets/{id}/tasks/{taskId}` — `{ isDone }`.
     *
     * **`completedAt` is server-set and is never sent** (AP-10, §7): the signature takes a boolean,
     * not an object a caller could add a timestamp to.
     */
    setTaskDone(id: string, taskId: string, isDone: boolean): Observable<TicketTask> {
        return this.patch<TicketTask>(`tickets/${id}/tasks/${taskId}`, { isDone });
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
