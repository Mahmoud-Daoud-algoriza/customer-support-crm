import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';
import { Paged, PageRequest } from './paged';

/**
 * `AuditEntry` — docs/api-design.md §6.9, the row shape of `GET /audit`.
 *
 * **`actor` is absent exactly when no user could be resolved** — a failed sign-in — and
 * `actorDescriptor` then carries the submitted identifier instead (docs/data-model.md §2.14).
 * Nulls are omitted rather than sent (Program.cs, `WhenWritingNull`), so every optional member here
 * is `?`.
 */
export interface AuditEntry {
    id: string;
    occurredAt: string;
    actor?: UserSummary | null;
    actorDescriptor?: string | null;
    action: string;
    targetType?: string | null;
    targetId?: string | null;
    outcome: 'Success' | 'Failure';
}

/** Filters for `GET /audit` — docs/api-design.md §5.12. */
export interface AuditListFilter extends PageRequest {
    actorUserId?: string | null;
    action?: string | null;
    from?: string | null;
    to?: string | null;
}

/**
 * The typed client for `GET /audit` — Administrator-only (docs/architecture.md §2.4,
 * docs/api-design.md §5.12).
 *
 * **There is no write method here, and there must never be one** — the log is append-only by
 * construction (T2-H), the same discipline `TicketsClient` states for `transition` instead of
 * `delete`.
 */
@Injectable({ providedIn: 'root' })
export class AuditClient extends ApiClientBase {
    list(filter: AuditListFilter): Observable<Paged<AuditEntry>> {
        return this.get<Paged<AuditEntry>>('audit', filter as Record<string, QueryValue>);
    }
}
