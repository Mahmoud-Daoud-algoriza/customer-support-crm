import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';

/**
 * `DashboardReport` — the response of `GET /reports/dashboard` (docs/api-design.md §6.8), group for
 * group.
 *
 * **There is no ticket array in this type, and there must never be one.** The intake's rule is
 * *"aggregate on the server; do not ship raw ticket lists to the browser to be counted there"*, so
 * every number here arrives computed. A backend test asserts the payload does not grow with the
 * ticket count.
 */
export interface DashboardReport {
    ticketCounts: TicketCounts;
    slaAttainment: SlaAttainmentRow[];
    agentPerformance: AgentPerformanceRow[];
    satisfaction: Satisfaction;
}

/** §9.1 — four breakdowns of the same filtered set. */
export interface TicketCounts {
    byStatus: CountByKey[];
    byPriority: CountByKey[];
    byCategory: CountByKey[];
    byDepartment: CountByNamedKey[];
}

/**
 * `key` is a **stable string code**, never a display label (docs/api-design.md §2) — the front end
 * translates it, because the API returns codes and the client owns display text.
 */
export interface CountByKey {
    key: string;
    count: number;
}

/**
 * Department is the one breakdown carrying a `name`: its key is a GUID, which is not renderable.
 * The others are codes with translations.
 */
export interface CountByNamedKey extends CountByKey {
    name: string;
}

/**
 * §9.2 — one row per priority.
 *
 * **`attainmentPercent` is `null` when the priority has no tickets** — the region renders "—", and
 * **never 100%**, which would claim perfect attainment on no evidence.
 */
export interface SlaAttainmentRow {
    priority: string;
    met: number;
    breached: number;
    attainmentPercent: number | null;
}

/**
 * §9.3 — one row per agent.
 *
 * **`averageResolutionHours` is `null` when the agent has resolved nothing** — rendered "—", never
 * "0" (docs/api-design.md §6.8).
 *
 * **`assignedCount` means *currently* assigned** — the PF-4 decision taken 2026-09-06. The column is
 * labelled exactly as T2-G words it, *"Tickets assigned"*, **with no clarifying tooltip**
 * (ui-design §11).
 */
export interface AgentPerformanceRow {
    agent: UserSummary;
    assignedCount: number;
    resolvedCount: number;
    averageResolutionHours: number | null;
}

/**
 * §9.4 — the CSAT summary.
 *
 * **`averageRating` is `null` when `responseCount` is 0**, and the tile must read *"No ratings
 * submitted yet"* rather than "0.0" — declining is a normal outcome, so the absence of ratings is
 * "no response", not dissatisfaction (data-model §2.15).
 *
 * **`ratingScale` travels with the average** so the tile can render "4.2 of 5" **without hardcoding
 * a denominator** — OQ-1 is not answered, and nothing on this screen may assume a scale.
 */
export interface Satisfaction {
    averageRating: number | null;
    responseCount: number;
    ratingScale: RatingScale;
}

export interface RatingScale {
    min: number;
    max: number;
}

/**
 * The two filters of docs/api-design.md §5.11 — and **only** those two.
 *
 * **The endpoint refuses an unknown query parameter with a `400`** (AP-15), so there is no
 * `format`, no date range and no agent filter to add here: sending one would break the request
 * rather than be ignored. That is deliberate — T2-G has no export.
 */
export interface DashboardFilter {
    departmentId?: string | null;
    branchId?: string | null;
}

/**
 * The typed client for the one reporting endpoint (docs/api-design.md §5.11). Feature components
 * never call `HttpClient` directly (architecture §2.2).
 *
 * **One method, because there is one endpoint.** No export, no per-metric call, no second report —
 * T2-G is one dashboard with a fixed metric set (product-scope §8).
 */
@Injectable({ providedIn: 'root' })
export class ReportsClient extends ApiClientBase {
    getDashboard(filter: DashboardFilter): Observable<DashboardReport> {
        return this.get<DashboardReport>('reports/dashboard', filter as Record<string, QueryValue>);
    }
}
