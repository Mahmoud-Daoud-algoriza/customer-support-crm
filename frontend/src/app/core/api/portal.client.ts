import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';
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
 * Story 07 publishes three of §5.7's endpoints; **Story 13 adds the rest** — own ticket list,
 * detail, transition, attachments and feedback — into this same client.
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
}
