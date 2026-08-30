import { Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { ApiClientBase } from './api-client.base';

/** `GET /health` — docs/api-design.md §5.1. */
export interface HealthStatus {
    status: 'ok' | 'degraded';
    database: 'reachable' | 'unreachable';
    utcNow: string;
}

/** `BootstrapConfig` — docs/api-design.md §6.9. Anonymous, needed before sign-in. */
export interface BootstrapConfig {
    productName: string;
    logoUrl: string;
    primaryColor: string;
    languages: string[];
    defaultLanguage: string;
}

/**
 * `CustomerConfig` — `GET /config`, docs/api-design.md §6.9. **Customer-safe configuration only**:
 * every authenticated role may read it, which is why the ticket list draws its category options
 * from here rather than from `/config/staff` (AP-17, docs/ui-design.md §5.2).
 *
 * **Exactly two members.** A third would reach every Customer, which is the leak AP-17's tiering
 * exists to prevent — `departmentId` in particular appears nowhere here (A-14).
 */
export interface CustomerConfig {
    categories: { code: string; name: string }[];
    feedback: { ratingScale: { min: number; max: number } };
}

/**
 * The two platform endpoints Story 01 delivers, plus `GET /config` — the customer-safe tier, which
 * Story 05's ticket list reads for its category filter options (docs/ui-design.md §5.2).
 *
 * `GET /config/staff` (priorities, quick replies, SLA targets, the category → department map) is
 * not read here: Story 05 needs none of it. Priorities are fixed by A-6 and typed in
 * `tickets.client.ts`; the routing map is server-side (A-14).
 */
@Injectable({ providedIn: 'root' })
export class PlatformApiService extends ApiClientBase {
    private customerConfig$?: Observable<CustomerConfig>;

    getHealth(): Observable<HealthStatus> {
        return this.get<HealthStatus>('health');
    }

    getBootstrapConfig(): Observable<BootstrapConfig> {
        return this.get<BootstrapConfig>('config/bootstrap');
    }

    /**
     * **Cached for the session.** Categories change only by redeploy (T2-I: they are configuration
     * with no admin UI and no write endpoint), so re-fetching per screen would be pure waste — the
     * same reasoning `OrganizationClient` records for departments and branches.
     */
    getCustomerConfig(): Observable<CustomerConfig> {
        this.customerConfig$ ??= this.get<CustomerConfig>('config').pipe(
            shareReplay({ bufferSize: 1, refCount: false })
        );

        return this.customerConfig$;
    }
}
