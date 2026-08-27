import { Injectable } from '@angular/core';
import { Observable, map, shareReplay } from 'rxjs';
import { ApiClientBase } from './api-client.base';
import { Paged } from './paged';

/**
 * `Department` — docs/api-design.md §6.2.
 *
 * `managerUserId` is **absent**, not `null`, when the department has no manager: a department need
 * not have one (docs/data-model.md §2.2, **OQ-3**). Nothing in the UI may treat its absence as an
 * error, and no screen invents a substitute recipient.
 */
export interface Department {
    id: string;
    name: string;
    managerUserId?: string;
}

/**
 * `Branch` — docs/api-design.md §6.2. Two fields, and that is all there is: branch is a reporting
 * and filtering attribute only (A-2, T2-K).
 */
export interface Branch {
    id: string;
    name: string;
}

/**
 * `GET /departments` and `GET /branches` — docs/api-design.md §5.4. Agent+.
 *
 * **Both lists are cached for the session.** They change only by redeploy (T2-I: departments and
 * branches are seeded and configured, with no admin UI and no write endpoint), so re-fetching them
 * per screen would be pure waste. `shareReplay` keeps the first response for every later
 * subscriber; a page reload is the refresh, which is exactly as often as the data can change.
 *
 * **There is no create, update or delete method here, and there must never be one** — the server
 * publishes no such endpoint (docs/api-design.md §5.4).
 */
@Injectable({ providedIn: 'root' })
export class OrganizationClient extends ApiClientBase {
    private departments$?: Observable<Department[]>;
    private branches$?: Observable<Branch[]>;

    getDepartments(): Observable<Department[]> {
        this.departments$ ??= this.get<Paged<Department>>('departments', { pageSize: MAX_PAGE_SIZE }).pipe(
            map((result) => result.items),
            shareReplay({ bufferSize: 1, refCount: false })
        );

        return this.departments$;
    }

    getBranches(): Observable<Branch[]> {
        this.branches$ ??= this.get<Paged<Branch>>('branches', { pageSize: MAX_PAGE_SIZE }).pipe(
            map((result) => result.items),
            shareReplay({ bufferSize: 1, refCount: false })
        );

        return this.branches$;
    }
}

/**
 * Both endpoints return the standard paged envelope even though the lists are short (AP-3), so a
 * single page has to be asked for explicitly. 100 is the contract's maximum (docs/api-design.md
 * §2.1), not a number chosen here.
 *
 * **The bound is real:** an organization with more than 100 departments or branches would be
 * silently truncated by this client. Paging a filter dropdown is not behaviour any approved document
 * defines, so it is not invented here — if that organization ever exists, it is a question for
 * `docs/api-design.md` and `docs/ui-design.md`, not a client-side loop.
 */
const MAX_PAGE_SIZE = 100;
