import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { ApiProblem } from '../../../core/api/api-problem';
import { Paged } from '../../../core/api/paged';
import { TicketListFilter, TicketListItem, TicketsClient } from '../../../core/api/tickets.client';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { PriorityChipComponent } from '../../../shared/components/priority-chip/priority-chip.component';
import { StatusChipComponent } from '../../../shared/components/status-chip/status-chip.component';
import { TicketFilterBarComponent } from '../../../shared/components/ticket-filter-bar/ticket-filter-bar.component';

/**
 * Ticket list — `/workspace/tickets` (docs/ui-design.md §5.2). Agent+.
 *
 * **Table on desktop, stacked cards below the table breakpoint** (UI-10): a horizontally scrolling
 * table is not usable on a phone for the primary work surface. Both render the same rows from the
 * same signal — the breakpoint switches presentation, never content.
 *
 * **Filters live in the URL** (UI-9), under the API's own names (§5.6), so a filtered queue is
 * shareable and survives a reload.
 *
 * **Scoping is visible, not hidden.** An Agent's department filter is fixed and disabled with a
 * hint; a Manager's is enabled across all departments. That rule lives in
 * `shared/components/department-filter/` from Story 03 and is not re-implemented here — the server
 * narrows the results either way (docs/architecture.md §4.3), and the disabled control exists to
 * make that legible rather than mysterious. **Guards hide; they do not protect.**
 *
 * **Default order is SLA urgency** — the server sorts `resolutionDueAt:asc` with breached first,
 * and this screen does not re-sort. Under **A-20** the due dates freeze, so escalating a ticket
 * does **not** re-order the queue.
 */
@Component({
    selector: 'app-ticket-list',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, LoadingStateComponent, PaginatorModule, PriorityChipComponent, RouterLink, StatusChipComponent, TableModule, TicketFilterBarComponent, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'tickets.title' | transloco }}</h1>

                <!-- Story 11 task 6 gives POST /tickets its first screen; this is the way in. -->
                <p-button
                    icon="pi pi-plus"
                    [label]="'tickets.createTitle' | transloco"
                    routerLink="/workspace/tickets/new"
                />
            </header>

            <app-ticket-filter-bar [value]="filter()" (filterChange)="applyFilters($event)" />

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <app-empty-state [title]="'tickets.emptyTitle' | transloco" [message]="'tickets.emptyMessage' | transloco" icon="pi-ticket" />
                    } @else {
                        <!-- Desktop: the table. Hidden below the breakpoint by CSS, not by a
                             second data path — both views render the same rows. -->
                        <div class="app-scroll-x app-table-view">
                            <p-table [value]="result.items" [tableStyle]="{ 'min-width': '60rem' }">
                                <ng-template pTemplate="header">
                                    <tr>
                                        <th>{{ 'tickets.subject' | transloco }}</th>
                                        <th>{{ 'tickets.customer' | transloco }}</th>
                                        <th>{{ 'tickets.statusLabel' | transloco }}</th>
                                        <th>{{ 'tickets.priorityLabel' | transloco }}</th>
                                        <th>{{ 'tickets.categoryLabel' | transloco }}</th>
                                        <th>{{ 'tickets.assigneeLabel' | transloco }}</th>
                                        <th>{{ 'tickets.resolutionDue' | transloco }}</th>
                                        <th></th>
                                    </tr>
                                </ng-template>
                                <ng-template pTemplate="body" let-ticket>
                                    <tr>
                                        <td>{{ ticket.subject }}</td>
                                        <td>{{ ticket.customer.fullName }}</td>
                                        <td><app-status-chip [status]="ticket.status" /></td>
                                        <td><app-priority-chip [priority]="ticket.priority" /></td>
                                        <td>{{ ticket.categoryCode }}</td>
                                        <!-- Assignee and status are two independent facts (A-18). -->
                                        <td>{{ ticket.assignee?.displayName ?? ('tickets.unassigned' | transloco) }}</td>
                                        <td class="app-ltr-numeric">
                                            {{ ticket.resolutionDueAt | date: 'short' }}
                                            @if (ticket.resolutionBreached || ticket.firstResponseBreached) {
                                                <span class="app-breach">{{ 'tickets.breached' | transloco }}</span>
                                            }
                                        </td>
                                        <td>
                                            <a [routerLink]="['/workspace/tickets', ticket.id]">{{ 'actions.open' | transloco }}</a>
                                        </td>
                                    </tr>
                                </ng-template>
                            </p-table>
                        </div>

                        <!-- Phone width: stacked cards, each leading with subject, status and SLA
                             (UI-10, docs/ui-design.md §10.3). -->
                        <ul class="app-card-view">
                            @for (ticket of result.items; track ticket.id) {
                                <li class="app-ticket-card">
                                    <a class="app-ticket-card__subject" [routerLink]="['/workspace/tickets', ticket.id]">{{ ticket.subject }}</a>

                                    <div class="app-ticket-card__chips">
                                        <app-status-chip [status]="ticket.status" />
                                        <app-priority-chip [priority]="ticket.priority" />
                                    </div>

                                    <p class="app-ticket-card__meta">
                                        {{ ticket.customer.fullName }} ·
                                        {{ ticket.assignee?.displayName ?? ('tickets.unassigned' | transloco) }}
                                    </p>

                                    <p class="app-ticket-card__meta app-ltr-numeric">
                                        {{ 'tickets.resolutionDue' | transloco }}: {{ ticket.resolutionDueAt | date: 'short' }}
                                        @if (ticket.resolutionBreached || ticket.firstResponseBreached) {
                                            <span class="app-breach">{{ 'tickets.breached' | transloco }}</span>
                                        }
                                    </p>
                                </li>
                            }
                        </ul>

                        <p-paginator [first]="(result.page - 1) * result.pageSize" [rows]="result.pageSize" [totalRecords]="result.totalItems" (onPageChange)="goToPage($event.page)" />
                    }
                } @else {
                    <app-loading-state [rowCount]="6" />
                }
            }
        </section>
    `
})
export class TicketListComponent {
    private readonly api = inject(TicketsClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly page = signal<Paged<TicketListItem> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly filter = signal<TicketListFilter>({});

    constructor() {
        // UI-9: the URL drives the screen. A filter change navigates; the navigation is what loads.
        // A deep link and a typed search therefore take exactly the same path.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.filter.set(readFilter(params));
            this.load(params);
        });
    }

    /** A changed filter starts again at page 1 — page 3 of a different query is not a page. */
    protected applyFilters(filter: TicketListFilter): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { ...toQueryParams(filter), page: null }
        });
    }

    /** The paginator reports a **0-based** index; the API's `page` is 1-based (§2.1). */
    protected goToPage(zeroBasedPage: number | undefined): void {
        const page = (zeroBasedPage ?? 0) + 1;

        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { page: page > 1 ? page : null },
            queryParamsHandling: 'merge'
        });
    }

    protected reload(): void {
        this.load(this.route.snapshot.queryParamMap);
    }

    private load(params: ParamMap): void {
        this.page.set(null);
        this.problem.set(null);

        const page = Number(params.get('page'));

        this.api
            .list({
                ...readFilter(params),
                page: Number.isFinite(page) && page > 1 ? page : undefined
            })
            .subscribe({
                next: (result) => this.page.set(result),
                error: (failure: ApiProblem) => this.problem.set(failure)
            });
    }
}

/**
 * The URL is the source of truth for what is displayed. The parameter names are the API's own
 * (§5.6), so this reads them straight across with no translation step.
 */
function readFilter(params: ParamMap): TicketListFilter {
    const breached = params.get('breached');

    return {
        q: params.get('q'),
        status: params.get('status') as TicketListFilter['status'],
        priority: params.get('priority') as TicketListFilter['priority'],
        categoryCode: params.get('categoryCode'),
        assigneeId: params.get('assigneeId'),
        departmentId: params.get('departmentId'),
        breached: breached === null ? null : breached === 'true'
    };
}

/** An absent filter is an absent parameter, never an empty one — the server would reject `status=`. */
function toQueryParams(filter: TicketListFilter): Record<string, string | null> {
    return {
        q: filter.q || null,
        status: filter.status || null,
        priority: filter.priority || null,
        categoryCode: filter.categoryCode || null,
        assigneeId: filter.assigneeId || null,
        departmentId: filter.departmentId || null,
        breached: filter.breached === null || filter.breached === undefined ? null : String(filter.breached)
    };
}
