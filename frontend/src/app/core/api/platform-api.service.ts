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

/** One configured canned response — `GET /config/staff`, docs/api-design.md §6.9. */
export interface QuickReply {
    id: string;
    title: string;
    body: string;
}

/**
 * `StaffConfig` — `GET /config/staff`, docs/api-design.md §6.9. **Agent and above**; a Customer
 * calling it gets `403`, and that is correct rather than AP-4's `404` — it is a capability denial the
 * caller can infer from their own role (§4.2, AP-17).
 *
 * **Which is why nothing under `features/portal/` may read it** (UI-11). Story 08 needs
 * `quickReplies`; the other three groups are typed because the endpoint returns them, not because a
 * screen reads them yet.
 */
export interface StaffConfig {
    priorities: string[];
    quickReplies: QuickReply[];
    slaTargets: { priority: string; firstResponseHours: number; resolutionHours: number }[];
    categoryDepartmentMap: { categoryCode: string; departmentId: string }[];
}

/**
 * The two platform endpoints Story 01 delivers, plus both configuration tiers — `GET /config`, which
 * Story 05's ticket list reads for its category filter options (docs/ui-design.md §5.2), and
 * `GET /config/staff`, which Story 08's quick-reply control reads.
 */
@Injectable({ providedIn: 'root' })
export class PlatformApiService extends ApiClientBase {
    private customerConfig$?: Observable<CustomerConfig>;
    private staffConfig$?: Observable<StaffConfig>;

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

    /**
     * **Cached for the session**, for the same reason as the customer tier: quick replies and SLA
     * targets are configuration with no write endpoint (T2-I), so they change only by redeploy.
     * Every ticket an agent opens would otherwise re-fetch the same three canned responses.
     */
    getStaffConfig(): Observable<StaffConfig> {
        this.staffConfig$ ??= this.get<StaffConfig>('config/staff').pipe(
            shareReplay({ bufferSize: 1, refCount: false })
        );

        return this.staffConfig$;
    }
}
