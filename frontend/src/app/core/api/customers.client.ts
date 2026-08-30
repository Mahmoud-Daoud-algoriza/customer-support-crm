import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';
import { AttachmentMetadata } from './attachments.client';
import { Branch } from './organization.client';
import { Paged, PageRequest } from './paged';

/**
 * `Customer` — docs/api-design.md §6.3. The shape of `GET /customers/{id}`.
 *
 * `externalReference` is the ERP seam field (DM-6): **returned read-only, settable through no
 * endpoint**, which is why it appears here and in neither request type below.
 *
 * `phone` and `externalReference` are optional because the server omits a null rather than writing
 * one (`WhenWritingNull`, docs/api-design.md §2).
 */
export interface Customer {
    id: string;
    fullName: string;
    email: string;
    phone?: string;
    branch: Branch;
    externalReference?: string;
    createdAt: string;
}

/**
 * `CustomerListItem` — the row shape of `GET /customers` (docs/api-design.md §6.3).
 *
 * It carries `openTicketCount` and **not** `externalReference` or `createdAt`: §6.3 gives the two
 * payloads different fields on purpose, because the directory shows a ticket count
 * (docs/ui-design.md §5.4) and the detail screen shows the seam field.
 */
export interface CustomerListItem {
    id: string;
    fullName: string;
    email: string;
    phone?: string;
    branch: Branch;
    openTicketCount: number;
}

/**
 * `POST /customers` — docs/api-design.md §5.5. **Exactly four fields.**
 *
 * There is no `externalReference`, no `id` and no `createdAt`: a server-derived field is never
 * accepted from a client (AP-10), and the server answers `400` to a body carrying one rather than
 * accepting and ignoring it, so a request shaped here cannot mislead.
 *
 * `branchId` is required — an **agent** creating a profile does choose the branch. Only a
 * self-registering customer is given the configured default and never asked (A-15), and that is a
 * different endpoint with a different request type.
 */
export interface CreateCustomerRequest {
    fullName: string;
    email: string;
    phone?: string | null;
    branchId: string;
}

/**
 * `PATCH /customers/{id}` — docs/api-design.md §5.5. **Exactly four patchable fields.**
 *
 * Every property is optional because absent means "leave unchanged" — a PATCH carries only what is
 * changing (docs/api-design.md §2).
 *
 * **`email` is patchable**, and changing it also changes the sign-in address of the customer's
 * linked portal login, in the same unit of work (**A-19**). Two distinct `409`s can come back —
 * `customer-email-in-use` and `user-already-exists` — and in both the server wrote nothing.
 */
export interface PatchCustomerRequest {
    fullName?: string;
    email?: string;
    phone?: string | null;
    branchId?: string;
}

/**
 * `GET /customers` filters — docs/api-design.md §5.5. **The names mirror the API exactly**, so the
 * URL query parameters of the directory screen (UI-9) are the API's own names with no translation
 * step anywhere.
 *
 * `q` and `branchId`, and nothing else. **There is deliberately no department filter** — a customer
 * has no department (docs/ui-design.md §5.4).
 */
export interface CustomerListFilter extends PageRequest {
    q?: string | null;
    branchId?: string | null;
}

/**
 * `CustomerNote` — docs/api-design.md §6.3.
 *
 * **There is no `updatedAt`, because the note is immutable once written** (docs/data-model.md §2.5).
 * Its absence is the contract saying no edit path exists; no screen renders an edit or delete
 * control for one, because none could work.
 */
export interface CustomerNote {
    id: string;
    author: UserSummary;
    body: string;
    createdAt: string;
}

/**
 * `TimelineEntry` — `GET /customers/{id}/timeline`, docs/api-design.md §6.3.
 *
 * A **read projection** over the customer's tickets and ticket activity, never a stored row
 * (docs/architecture.md §2.5). **Internal entries are excluded server-side**, and customer notes
 * are a separate collection that never appears here.
 *
 * `activityType` and `actorKind` are strings rather than unions for the reason the server contract
 * gives: both belong to the `Tickets` vocabulary that Story 06 defines, and naming its members here
 * would invent them a story early. The projection is empty until Story 06 lands.
 */
export interface TimelineEntry {
    occurredAt: string;
    ticketId: string;
    ticketSubject: string;
    activityType: string;
    actorKind: string;
    /** Absent when `actorKind` is `System` — the SLA monitor. */
    actor?: UserSummary;
    oldValue?: string;
    newValue?: string;
}

/**
 * The typed client for the ten customer endpoints of docs/api-design.md §5.5. Feature components
 * never call `HttpClient` directly (docs/architecture.md §2.2) — this is the one place these
 * contracts are absorbed.
 *
 * **There is no `delete` method here, and there must never be one.** Deleting a customer is not an
 * application operation (docs/data-model.md §2.4) and the server publishes no such route. Nor is
 * there an edit or delete for a note: the entity is immutable (§2.5).
 */
@Injectable({ providedIn: 'root' })
export class CustomersClient extends ApiClientBase {
    list(filter: CustomerListFilter): Observable<Paged<CustomerListItem>> {
        return this.get<Paged<CustomerListItem>>('customers', filter as Record<string, QueryValue>);
    }

    getCustomer(id: string): Observable<Customer> {
        return this.get<Customer>(`customers/${id}`);
    }

    create(request: CreateCustomerRequest): Observable<Customer> {
        return this.post<Customer>('customers', request);
    }

    patchCustomer(id: string, request: PatchCustomerRequest): Observable<Customer> {
        return this.patch<Customer>(`customers/${id}`, request);
    }

    timeline(id: string, paging?: PageRequest): Observable<Paged<TimelineEntry>> {
        return this.get<Paged<TimelineEntry>>(`customers/${id}/timeline`, paging as Record<string, QueryValue>);
    }

    notes(id: string, paging?: PageRequest): Observable<Paged<CustomerNote>> {
        return this.get<Paged<CustomerNote>>(`customers/${id}/notes`, paging as Record<string, QueryValue>);
    }

    /**
     * `POST /customers/{id}/notes` — **one field**. Author and timestamp are server-set from the
     * caller's identity and are not accepted here (AP-10, docs/api-design.md §7).
     */
    addNote(id: string, body: string): Observable<CustomerNote> {
        return this.post<CustomerNote>(`customers/${id}/notes`, { body });
    }

    attachments(id: string, paging?: PageRequest): Observable<Paged<AttachmentMetadata>> {
        return this.get<Paged<AttachmentMetadata>>(`customers/${id}/attachments`, paging as Record<string, QueryValue>);
    }

    /**
     * `POST /customers/{id}/attachments` — `multipart/form-data` (AP-13).
     *
     * The part is named `file`, which is what the controller binds. `HttpClient` sets the multipart
     * boundary itself, so no `Content-Type` is set here — setting one would break the request.
     *
     * The size the server checks against the configured cap is the one it measures from the parsed
     * body, never a value declared here; over it is `413 attachment-too-large`.
     */
    upload(id: string, file: File): Observable<AttachmentMetadata> {
        const form = new FormData();
        form.append('file', file, file.name);

        return this.post<AttachmentMetadata>(`customers/${id}/attachments`, form);
    }
}
