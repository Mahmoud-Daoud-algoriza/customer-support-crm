import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../core/api/api-problem';
import { CustomerConfig, PlatformApiService } from '../../../core/api/platform-api.service';
import { Branch, Department, OrganizationClient } from '../../../core/api/organization.client';
import { DashboardReport, ReportsClient } from '../../../core/api/reports.client';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * The management dashboard — `/workspace/reports` (docs/ui-design.md §5.7), Story 15.
 *
 * <h3>Manager+ only, and the guard is not the protection</h3>
 * The route carries a `Manager+` guard and the nav entry is hidden below that role — but
 * `GET /reports/dashboard` returns `403` to an Agent **regardless of what the router allowed**
 * (§5.7). Hiding is convenience; the server is the authority.
 *
 * <h3>"Empty is not zero" is the rule this screen exists to respect</h3>
 * Three separate values arrive as `null` and **must not be rendered as numbers**: no ratings is not
 * `0.0` satisfaction, no resolutions is not `0` hours, and a priority with no tickets is not `100%`
 * attainment. Each renders as **"—"** or as its own empty sentence. A zero in any of those places
 * would be a claim the data does not support, and a manager might act on it.
 *
 * <h3>"Tickets assigned" carries no tooltip</h3>
 * ui-design §11 requires the column labelled exactly as T2-G words it, with **no clarifying
 * tooltip**. **PF-4 was decided on 2026-09-06 — it means *currently* assigned** — and the label
 * still stands as T2-G writes it, because that is the wording the scope fixes.
 *
 * <h3>No export, and no charting dependency</h3>
 * No export button, no print view, no scheduling control and no date-range picker beyond the two
 * approved filters (T2-G, product-scope §8) — and the endpoint refuses an unknown query parameter
 * with a `400`, so there is nothing to add here that would work. Rendering uses **PrimeNG table and
 * tag components only**; no charting library was added.
 *
 * <h3>Filters live in the URL (UI-9)</h3>
 * `departmentId` and `branchId` are query parameters, so a filtered dashboard is shareable and
 * survives a reload. Both are enabled for Manager+, unlike the ticket list's department filter.
 */
@Component({
    selector: 'app-reports',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        EmptyStateComponent, ErrorStateComponent, FormsModule, LoadingStateComponent,
        SelectModule, TableModule, TagModule, TranslocoModule
    ],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'reports.title' | transloco }}</h1>
            </header>

            <!-- UI-9: both filters are URL-bound, so a filtered view is shareable and survives a
                 reload. Both are enabled for Manager+ — the department filter is a choice of view
                 here, never a scope (architecture §4.3). -->
            <div class="app-filter-bar">
                <p-select
                    [options]="departments()"
                    [ngModel]="departmentId()"
                    optionLabel="name"
                    optionValue="id"
                    [showClear]="true"
                    [placeholder]="'reports.allDepartments' | transloco"
                    [ariaLabel]="'reports.department' | transloco"
                    (ngModelChange)="applyFilter({ departmentId: $event })" />

                <p-select
                    [options]="branches()"
                    [ngModel]="branchId()"
                    optionLabel="name"
                    optionValue="id"
                    [showClear]="true"
                    [placeholder]="'reports.allBranches' | transloco"
                    [ariaLabel]="'reports.branch' | transloco"
                    (ngModelChange)="applyFilter({ branchId: $event })" />
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (report(); as data) {
                    <div class="app-report-grid">
                        <!-- ------------------------------------------------ §9.1 ticket counts -->
                        <section class="app-region">
                            <h2 class="app-region__title">{{ 'reports.ticketCounts' | transloco }}</h2>

                            @if (totalTickets(data) === 0) {
                                <app-empty-state
                                    [title]="'reports.empty.tickets' | transloco"
                                    [message]="'reports.empty.ticketsMessage' | transloco" />
                            } @else {
                                <div class="app-report-breakdowns">
                                    <div class="app-report-breakdown">
                                        <h3>{{ 'tickets.statusLabel' | transloco }}</h3>
                                        @for (row of data.ticketCounts.byStatus; track row.key) {
                                            <p class="app-report-line">
                                                <span>{{ 'tickets.status.' + row.key | transloco }}</span>
                                                <span class="app-ltr-numeric">{{ row.count }}</span>
                                            </p>
                                        }
                                    </div>

                                    <div class="app-report-breakdown">
                                        <h3>{{ 'tickets.priorityLabel' | transloco }}</h3>
                                        @for (row of data.ticketCounts.byPriority; track row.key) {
                                            <p class="app-report-line">
                                                <span>{{ 'tickets.priority.' + row.key | transloco }}</span>
                                                <span class="app-ltr-numeric">{{ row.count }}</span>
                                            </p>
                                        }
                                    </div>

                                    <div class="app-report-breakdown">
                                        <h3>{{ 'tickets.categoryLabel' | transloco }}</h3>
                                        @for (row of data.ticketCounts.byCategory; track row.key) {
                                            <p class="app-report-line">
                                                <!-- The configured display name, not the stable code
                                                     (§6.4) — the same lookup the ticket list uses. -->
                                                <span>{{ categoryName(row.key) }}</span>
                                                <span class="app-ltr-numeric">{{ row.count }}</span>
                                            </p>
                                        }
                                    </div>

                                    <div class="app-report-breakdown">
                                        <h3>{{ 'reports.department' | transloco }}</h3>
                                        @for (row of data.ticketCounts.byDepartment; track row.key) {
                                            <p class="app-report-line">
                                                <!-- Department is the one breakdown carrying a name:
                                                     its key is a GUID. -->
                                                <span>{{ row.name }}</span>
                                                <span class="app-ltr-numeric">{{ row.count }}</span>
                                            </p>
                                        }
                                    </div>
                                </div>
                            }
                        </section>

                        <!-- ------------------------------------------------ §9.2 SLA attainment -->
                        <section class="app-region">
                            <h2 class="app-region__title">{{ 'reports.slaAttainment' | transloco }}</h2>

                            @if (totalTickets(data) === 0) {
                                <app-empty-state
                                    [title]="'reports.empty.sla' | transloco"
                                    [message]="'reports.empty.slaMessage' | transloco" />
                            } @else {
                                <div class="app-table-scroll">
                                    <p-table [value]="data.slaAttainment" styleClass="p-datatable-sm">
                                        <ng-template #header>
                                            <tr>
                                                <th>{{ 'tickets.priorityLabel' | transloco }}</th>
                                                <th>{{ 'reports.met' | transloco }}</th>
                                                <th>{{ 'reports.breached' | transloco }}</th>
                                                <th>{{ 'reports.attainment' | transloco }}</th>
                                            </tr>
                                        </ng-template>
                                        <ng-template #body let-row>
                                            <tr>
                                                <td>{{ 'tickets.priority.' + row.priority | transloco }}</td>
                                                <td class="app-ltr-numeric">{{ row.met }}</td>
                                                <td class="app-ltr-numeric">{{ row.breached }}</td>
                                                <!-- null renders "—", NEVER 100%: a priority with no
                                                     tickets has no attainment to report. -->
                                                <td class="app-ltr-numeric">
                                                    {{ row.attainmentPercent === null
                                                        ? '—'
                                                        : row.attainmentPercent + '%' }}
                                                </td>
                                            </tr>
                                        </ng-template>
                                    </p-table>
                                </div>
                            }
                        </section>

                        <!-- ------------------------------------------------ §9.3 agent performance -->
                        <section class="app-region">
                            <h2 class="app-region__title">{{ 'reports.agentPerformance' | transloco }}</h2>

                            @if (data.agentPerformance.length === 0) {
                                <app-empty-state
                                    [title]="'reports.empty.agents' | transloco"
                                    [message]="'reports.empty.agentsMessage' | transloco" />
                            } @else {
                                <div class="app-table-scroll">
                                    <p-table [value]="data.agentPerformance" styleClass="p-datatable-sm">
                                        <ng-template #header>
                                            <tr>
                                                <th>{{ 'reports.agent' | transloco }}</th>
                                                <!-- Labelled exactly as T2-G words it, with NO
                                                     clarifying tooltip (ui-design §11). -->
                                                <th>{{ 'reports.ticketsAssigned' | transloco }}</th>
                                                <th>{{ 'reports.ticketsResolved' | transloco }}</th>
                                                <th>{{ 'reports.averageResolution' | transloco }}</th>
                                            </tr>
                                        </ng-template>
                                        <ng-template #body let-row>
                                            <tr>
                                                <td>{{ row.agent.displayName }}</td>
                                                <td class="app-ltr-numeric">{{ row.assignedCount }}</td>
                                                <td class="app-ltr-numeric">{{ row.resolvedCount }}</td>
                                                <!-- null renders "—", never "0": an agent who has
                                                     resolved nothing has no average, and a zero
                                                     would read as instantaneous resolution. -->
                                                <td class="app-ltr-numeric">
                                                    {{ row.averageResolutionHours === null
                                                        ? '—'
                                                        : (row.averageResolutionHours + 'h') }}
                                                </td>
                                            </tr>
                                        </ng-template>
                                    </p-table>
                                </div>
                            }
                        </section>

                        <!-- ------------------------------------------------ §9.4 satisfaction -->
                        <section class="app-region">
                            <h2 class="app-region__title">{{ 'reports.satisfaction' | transloco }}</h2>

                            <!-- The whole rule of this tile: no ratings is "no ratings", never
                                 "0.0", which would read as universal dissatisfaction. -->
                            @if (data.satisfaction.responseCount === 0) {
                                <app-empty-state
                                    [title]="'reports.empty.ratings' | transloco"
                                    [message]="'reports.empty.ratingsMessage' | transloco" />
                            } @else {
                                <p class="app-report-metric app-ltr-numeric">
                                    <!-- The configured scale travels with the average, so the
                                         denominator is never hardcoded (OQ-1 is not answered). -->
                                    {{ 'reports.averageOf' | transloco: {
                                        average: data.satisfaction.averageRating,
                                        max: data.satisfaction.ratingScale.max
                                    } }}
                                </p>

                                <p class="app-report-line">
                                    <span>{{ 'reports.responseCount' | transloco }}</span>
                                    <span class="app-ltr-numeric">{{ data.satisfaction.responseCount }}</span>
                                </p>
                            }
                        </section>
                    </div>
                } @else {
                    <app-loading-state [rowCount]="4" />
                }
            }
        </section>
    `,
    styles: `
        .app-report-grid {
            display: grid;
            gap: 1rem;
            grid-template-columns: repeat(auto-fit, minmax(20rem, 1fr));
        }

        .app-report-breakdowns {
            display: grid;
            gap: 1rem;
            grid-template-columns: repeat(auto-fit, minmax(9rem, 1fr));
        }

        .app-report-breakdown h3 {
            margin-block: 0 0.35rem;
            font-size: 0.8125rem;
            font-weight: 600;
            color: var(--p-text-muted-color);
        }

        .app-report-line {
            display: flex;
            gap: 0.75rem;
            justify-content: space-between;
            margin-block: 0.2rem;
        }

        .app-report-metric {
            margin-block: 0 0.5rem;
            font-size: 1.75rem;
            font-weight: 600;
        }

        /* Wide content scrolls inside its own container, so the page body never scrolls
           sideways (architecture §2.3, ui-design §10.3). */
        .app-table-scroll {
            overflow-x: auto;
        }
    `
})
export class ReportsComponent {
    private readonly api = inject(ReportsClient);
    private readonly organization = inject(OrganizationClient);
    private readonly platform = inject(PlatformApiService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly report = signal<DashboardReport | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly departments = signal<Department[]>([]);
    protected readonly branches = signal<Branch[]>([]);
    protected readonly departmentId = signal<string | null>(null);
    protected readonly branchId = signal<string | null>(null);

    private readonly categories = signal<CustomerConfig['categories']>([]);

    constructor() {
        // UI-9: the URL drives the screen. A filter change navigates, and the navigation is what
        // loads — so a deep link and a filter click take exactly the same path.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.departmentId.set(params.get('departmentId'));
            this.branchId.set(params.get('branchId'));
            this.load(params);
        });

        this.organization.getDepartments().subscribe((rows) => this.departments.set(rows));
        this.organization.getBranches().subscribe((rows) => this.branches.set(rows));
        this.platform.getCustomerConfig().subscribe((config) => this.categories.set(config.categories));
    }

    /** The category's configured display name — never the raw code, which is not meant to be read. */
    protected categoryName(code: string): string {
        return this.categories().find((c) => c.code === code)?.name ?? code;
    }

    /** The four breakdowns are one set sliced four ways, so any of them gives the total. */
    protected totalTickets(report: DashboardReport): number {
        return report.ticketCounts.byStatus.reduce((sum, row) => sum + row.count, 0);
    }

    protected applyFilter(change: { departmentId?: string | null; branchId?: string | null }): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            // A cleared select sends null, which strips the parameter from the URL rather than
            // sending an empty one the endpoint would reject.
            queryParams: change,
            queryParamsHandling: 'merge'
        });
    }

    protected reload(): void {
        this.load(this.route.snapshot.queryParamMap);
    }

    private load(params: ParamMap): void {
        this.report.set(null);
        this.problem.set(null);

        this.api
            .getDashboard({
                departmentId: params.get('departmentId'),
                branchId: params.get('branchId')
            })
            .subscribe({
                next: (data) => this.report.set(data),
                error: (failure: ApiProblem) => this.problem.set(failure)
            });
    }
}
