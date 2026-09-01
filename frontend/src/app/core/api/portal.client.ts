import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';
import { AttachmentMetadata } from './attachments.client';
import { MessageDirection, TicketStatus } from './tickets.client';
import { Paged, PageRequest } from './paged';

/**
 * `Ticket (portal)` — docs/api-design.md §6.4.
 *
 * **No assignee (AP-16), no department, no priority, no SLA or breach fields.** The portal speaks
 * no staff vocabulary (UI-11), and the omissions are the payload's whole design. `status` *is*
 * here, using the same six-value vocabulary as the staff side — docs/ui-design.md §8 authorized no
 * separate customer wording.
 */
export interface PortalTicket {
    id: string;
    subject: string;
    description: string;
    categoryCode: string;
    status: TicketStatus;
    isUrgent: boolean;
    createdAt: string;
    resolvedAt?: string | null;
    hasFeedback: boolean;
}

/**
 * `POST /portal/tickets` — the web form (docs/api-design.md §5.7). **Four fields, and the three
 * that are missing are the contract:**
 *
 * - no `customerId` — it is the caller's own profile;
 * - no `departmentId` — the **category** chooses the department (A-14), and a customer never sees
 *   which one;
 * - no `priority` — customers do not set priority (A-6).
 *
 * `isUrgent` is A-17's **indication**. It is not a priority and must never be labelled as one.
 * Sending any of the three absent fields is a `400` (AP-10), so they are absent here too.
 */
export interface SubmitPortalTicketRequest {
    subject: string;
    description: string;
    categoryCode: string;
    isUrgent: boolean;
}

/**
 * The **portal** message shape — docs/api-design.md §6.4: it omits `channel` and `authorRole`, and
 * keeps `direction` so the thread can distinguish the two sides.
 *
 * **This is not `TicketMessage`, on purpose.** AP-5 separates the path spaces, and sharing one
 * interface across them would turn the portal's narrowing into an optional field that a component
 * could read anyway.
 */
export interface PortalMessage {
    id: string;
    ticketId: string;
    author: UserSummary;
    direction: MessageDirection;
    body: string;
    postedAt: string;
}

/**
 * The response to `POST /portal/tickets/{id}/messages` — docs/api-design.md §6.4.
 *
 * **`statusChanged` is true only when R-13's automatic `Pending → Open` fired.** The ticket's
 * current status travels with the message *"so the client never has to guess whether the transition
 * happened"* and never has to re-fetch — which is what lets the request detail screen show its
 * *"reopened"* cue in place (docs/ui-design.md §7.3).
 */
export interface PortalPostedMessage {
    message: PortalMessage;
    ticketStatus: TicketStatus;
    statusChanged: boolean;
}

/**
 * The body of `POST /portal/tickets/{id}/transition` — docs/api-design.md §5.7.
 *
 * **Two targets and no others** (A-16): `Cancelled` while the request is `New` — a window A-18
 * keeps genuinely open, because an auto-assigned request is still `New` — and `Open` to reopen a
 * `Resolved` one.
 *
 * **`Pending` is not among them.** Replying reopens a `Pending` request automatically (R-13), which
 * is exactly why docs/ui-design.md §7.3 forbids the UI from offering a manual reopen for one. The
 * type says so, so a screen cannot ask for it by mistake.
 */
export type PortalTransitionTarget = 'Cancelled' | 'Open';

/**
 * `Feedback` — docs/api-design.md §6.4. What `POST /portal/tickets/{id}/feedback` answers with.
 *
 * **`rating` carries no range in this type, and must not gain one (OQ-1).** The permitted values
 * come from `feedback.ratingScale` in `GET /config`; the contract fixes none, and neither does this
 * interface.
 */
export interface PortalFeedback {
    id: string;
    ticketId: string;
    rating: number;
    comment?: string | null;
    submittedAt: string;
}

/**
 * The typed client for the **customer** path space — docs/api-design.md §5.7.
 *
 * <h3>Why this is a separate client from `TicketsClient`</h3>
 * **AP-5.** `/portal` has different scoping (ownership, not department), different DTOs and
 * different authority. Two clients keep that split visible in the front end exactly as the contract
 * keeps it visible in the API — and **no DTO type is shared between the two path spaces**, so a
 * staff field cannot arrive on a portal screen by way of a common interface.
 *
 * <h3>There is no internal-notes method here</h3>
 * Not "not yet" — **never**. Internal notes are staff-only by path (T2-C, AP-5), and the absence of
 * a method is the front end's half of a rule the server enforces independently.
 *
 * <h3>Nothing here polls</h3>
 * Portal messaging is ordinary request/response (T3-B). There is no interval, no subscription and
 * no long-poll in this file, and none may be added and called real-time chat.
 *
 * Story 07 published three of §5.7's endpoints and **Story 13 added the rest** into this same
 * client — own list, own detail, transition, attachments and feedback. **The portal path space is
 * complete**, and a method that is not here corresponds to no route.
 */
@Injectable({ providedIn: 'root' })
export class PortalClient extends ApiClientBase {
    /**
     * `POST /portal/tickets` — the web form of requirements §3.5.
     *
     * **Authenticated only** (A-9). An anonymous caller is `401`; there is no anonymous variant of
     * this route, and anonymous submission remains an open question in product-scope §9.
     */
    submit(request: SubmitPortalTicketRequest): Observable<PortalTicket> {
        return this.post<PortalTicket>('portal/tickets', request);
    }

    /** `GET /portal/tickets/{id}/messages` — the customer's own thread, oldest first. */
    messages(id: string, paging?: PageRequest): Observable<Paged<PortalMessage>> {
        return this.get<Paged<PortalMessage>>(
            `portal/tickets/${id}/messages`,
            paging as Record<string, QueryValue>
        );
    }

    /**
     * `POST /portal/tickets/{id}/messages` — **the one status side effect in this API**.
     *
     * The body carries `body` and nothing else: `direction` is derived from the caller's role and
     * `channel` from the endpoint (§7, **PF-7**), and sending either is a `400`.
     *
     * Read `statusChanged` from the response rather than re-fetching the ticket — and **never offer
     * a manual reopen for a `Pending` request** (docs/ui-design.md §7.3): no such transition is
     * available to a customer (A-16), which is precisely why this one is automatic.
     */
    postMessage(id: string, body: string): Observable<PortalPostedMessage> {
        return this.post<PortalPostedMessage>(`portal/tickets/${id}/messages`, { body });
    }

    /**
     * `GET /portal/tickets` — **the caller's own requests** (docs/ui-design.md §7.1).
     *
     * **There is no `customerId` parameter, and there must never be one.** Ownership is the
     * server's scope, applied before any filter — a client neither supplies it nor could widen it.
     *
     * Filter: `status`. Sort whitelist: `createdAt` alone (AP-15); anything else is a `400`.
     */
    list(query?: PageRequest & { status?: string | null }): Observable<Paged<PortalTicket>> {
        return this.get<Paged<PortalTicket>>('portal/tickets', query as Record<string, QueryValue>);
    }

    /**
     * `GET /portal/tickets/{id}` — the customer's own request.
     *
     * Another customer's id is `404`, worded identically to one that does not exist (AP-4), so **no
     * screen may try to tell the two apart**.
     *
     * Named `getRequest` rather than `get`: `get` is the base class's protected HTTP verb, and the
     * portal's noun for a ticket is a *request* (docs/ui-design.md §7).
     */
    getRequest(id: string): Observable<PortalTicket> {
        return this.get<PortalTicket>(`portal/tickets/${id}`);
    }

    /**
     * `POST /portal/tickets/{id}/transition` — cancel or reopen.
     *
     * **The server is the authority.** A target the caller may not invoke comes back as
     * `403 transition-not-permitted`, and one outside A-5's graph as `409 illegal-transition` — so
     * a screen that offers too much can only be refused, never obeyed.
     */
    transition(id: string, targetStatus: PortalTransitionTarget): Observable<PortalTicket> {
        return this.post<PortalTicket>(`portal/tickets/${id}/transition`, { targetStatus });
    }

    /** `GET /portal/tickets/{id}/attachments` — metadata only; `storagePath` is in no response. */
    attachments(id: string, paging?: PageRequest): Observable<Paged<AttachmentMetadata>> {
        return this.get<Paged<AttachmentMetadata>>(
            `portal/tickets/${id}/attachments`,
            paging as Record<string, QueryValue>
        );
    }

    /**
     * `POST /portal/tickets/{id}/attachments` — `multipart/form-data` (AP-13). The part is named
     * `file`, which is what the controller binds.
     *
     * **Offered after submission, never on the form** (docs/ui-design.md §7.2): the web form has
     * exactly four inputs, and a file needs a request to belong to. Over the configured cap is
     * `413`, surfaced inline on the uploader.
     */
    uploadAttachment(id: string, file: File): Observable<AttachmentMetadata> {
        const form = new FormData();
        form.append('file', file, file.name);

        return this.post<AttachmentMetadata>(`portal/tickets/${id}/attachments`, form);
    }

    /**
     * `POST /portal/tickets/{id}/feedback` — **the sole CSAT input** (requirements §8.5, T2-F).
     *
     * **Once per request, write-once.** A second call is `409 feedback-already-submitted`; a request
     * that has never reached `Resolved` is `409 feedback-not-available`.
     *
     * **⚠ `rating` is validated against the configured scale, and this client asserts none (OQ-1).**
     * The caller reads the range from `feedback.ratingScale` in `GET /config`; a value outside it is
     * `400`. **Do not add a range check, a default or a star count here.**
     *
     * **There is no `decline` method**, because declining is simply never calling this one — the
     * absence of a row is the meaningful outcome (docs/data-model.md §2.15).
     */
    submitFeedback(id: string, rating: number, comment?: string | null): Observable<PortalFeedback> {
        // `comment` is omitted rather than sent as null when there is nothing to say: it is optional
        // in the contract, and an explicit null would be a value the customer did not enter.
        const body = comment ? { rating, comment } : { rating };

        return this.post<PortalFeedback>(`portal/tickets/${id}/feedback`, body);
    }
}
