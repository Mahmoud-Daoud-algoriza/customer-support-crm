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
import { SlaIndicatorComponent } from '../../../shared/components/sla-indicator/sla-indicator.component';
import { StatusChipComponent } from '../../../shared/components/status-chip/status-chip.component';
import { TicketFilterBarComponent } from '../../../shared/components/ticket-filter-bar/ticket-filter-bar.component';

/**
 * My queue — `/workspace/queue` (docs/ui-design.md §5.1). Agent+. **The staff landing screen**
 * (UI-2): `/workspace` redirects here.
 *
 * <h3>`assigneeId=me`, and the client never sends its own id</h3>
 * The request is `GET /tickets?assigneeId=me` with **no `sort` parameter**. The literal `me` is
 * resolved server-side from the authenticated caller, because a caller-supplied identity is never
 * trusted (docs/architecture.md §4.3 point 1) — this screen does not read `AuthStore.identity()` to
 * build a filter.
 *
 * **`assigneeId` is a property of the screen, not of the URL.** The quick filters are in the query
 * string (UI-9) so a filtered queue is shareable and survives a reload, but `assigneeId` is not:
 * *my* queue is whose queue it is, and putting `me` in the URL would invite someone to edit it into
 * another agent's id — which the server would refuse, but the control should not exist. It is merged
 * in at request time only.
 *
 * <h3>The server owns urgency</h3>
 * **No `sort` is sent, so the API's default applies** — `resolutionBreached DESC,
 * resolutionDueAt ASC`: breached first, then soonest due. **This screen does not re-sort what it
 * receives** (§5.1). `QueueOrderingTests` pins both halves server-side.
 *
 * Under **A-20** the due dates freeze at creation, so escalating a ticket does not re-order the
 * queue — the same rule the ticket list already documents.
 *
 * <h3>Reused, not rebuilt</h3>
 * The two presentations are Story 05's `.app-table-view` / `.app-card-view` pair (UI-10), the chips
 * are Stories 05–06's, the states are Story 04's three shared components, and the filters are the
 * shared `app-ticket-filter-bar` in its `quickOnly` mode. Nothing here is a second implementation of
 * something that already exists.
 *
 * <h3>The empty state is an expected state, not an error</h3>
 * An agent with nothing assigned is normal (§9), so it offers the action that would fill the
 * region — a link to all tickets in their department, per §5.1.
 */
@Component({
    selector: 'app-agent-queue',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, LoadingStateComponent, PaginatorModule,
        PriorityChipComponent, RouterLink, SlaIndicatorComponent, StatusChipComponent, TableModule,
        TicketFilterBarComponent, TranslocoModule
    ],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'queue.title' | transloco }}</h1>
            </header>

            <!-- Quick filters only: status, priority, breached (§5.1). The assignee and department
                 controls are hidden because both are already decided for this screen — it is one
                 agent's own queue, and TicketScope fixes the department.

                 At phone width the bar collapses behind this toggle — the filter sheet of §10.3 —
                 so the queue itself is what fills a small screen. The toggle is hidden above the
                 breakpoint by CSS, where the bar is always shown. -->
            <p-button
                class="app-filter-toggle"
                severity="secondary"
                [outlined]="true"
                icon="pi pi-filter"
                [label]="'queue.filters' | transloco"
                [attr.aria-expanded]="filtersOpen()"
                (onClick)="filtersOpen.set(!filtersOpen())" />

            <div class="app-filter-sheet" [class.app-filter-sheet--open]="filtersOpen()">
                <app-ticket-filter-bar [value]="filter()" [quickOnly]="true" (filterChange)="applyFilters($event)" />
            </div>

            @if (problem(); as failure) {
                <!-- Inline retry, navigation still usable (§5.1, §9). -->
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <app-empty-state
                            [title]="'queue.emptyTitle' | transloco"
                            [message]="'queue.emptyMessage' | transloco"
                            [actionLabel]="'queue.viewAllTickets' | transloco"
                            icon="pi-inbox"
                            (action)="goToAllTickets()"
                        />
                    } @else {
                        <!-- Desktop: the table. Hidden below the breakpoint by CSS, not by a second
                             data path — both views render the same rows (UI-10). -->
                        <div class="app-scroll-x app-table-view">
                            <p-table [value]="result.items" [tableStyle]="{ 'min-width': '60rem' }">
                                <ng-template pTemplate="header">
                                    <tr>
                                        <th>{{ 'tickets.subject' | transloco }}</th>
                                        <th>{{ 'tickets.customer' | transloco }}</th>
                                        <th>{{ 'tickets.statusLabel' | transloco }}</th>
                                        <th>{{ 'tickets.priorityLabel' | transloco }}</th>
                                        <th>{{ 'tickets.breachLabel' | transloco }}</th>
                                        <th>{{ 'queue.age' | transloco }}</th>
                                        <th>{{ 'tickets.categoryLabel' | transloco }}</th>
                                        <th></th>
                                    </tr>
                                </ng-template>
                                <ng-template pTemplate="body" let-ticket>
                                    <tr>
                                        <td>{{ ticket.subject }}</td>
                                        <td>{{ ticket.customer.fullName }}</td>
                                        <td><app-status-chip [status]="ticket.status" /></td>
                                        <td><app-priority-chip [priority]="ticket.priority" /></td>
                                        <td>
                                            <app-sla-indicator
                                                [dueAt]="ticket.resolutionDueAt"
                                                [breached]="ticket.resolutionBreached || ticket.firstResponseBreached"
                                            />
                                        </td>
                                        <td class="app-ltr-numeric">{{ ticket.createdAt | date: 'short' }}</td>
                                        <td>{{ ticket.categoryCode }}</td>
                                        <td>
                                            <a [routerLink]="['/workspace/tickets', ticket.id]">{{ 'actions.open' | transloco }}</a>
                                        </td>
                                    </tr>
                                </ng-template>
                            </p-table>
                        </div>

                        <!-- Phone width: stacked cards, each leading with subject, status and SLA
                             (UI-10, §10.3). -->
                        <ul class="app-card-view">
                            @for (ticket of result.items; track ticket.id) {
                                <li class="app-ticket-card">
                                    <a class="app-ticket-card__subject" [routerLink]="['/workspace/tickets', ticket.id]">{{ ticket.subject }}</a>

                                    <div class="app-ticket-card__chips">
                                        <app-status-chip [status]="ticket.status" />
                                        <app-priority-chip [priority]="ticket.priority" />
                                        <app-sla-indicator
                                            [dueAt]="ticket.resolutionDueAt"
                                            [breached]="ticket.resolutionBreached || ticket.firstResponseBreached"
                                        />
                                    </div>

                                    <p class="app-ticket-card__meta">{{ ticket.customer.fullName }} · {{ ticket.categoryCode }}</p>

                                    <p class="app-ticket-card__meta app-ltr-numeric">
                                        {{ 'queue.age' | transloco }}: {{ ticket.createdAt | date: 'short' }}
                                    </p>
                                </li>
                            }
                        </ul>

                        <p-paginator [first]="(result.page - 1) * result.pageSize" [rows]="result.pageSize" [totalRecords]="result.totalItems" (onPageChange)="goToPage($event.page)" />
                    }
                } @else {
                    <!-- Skeleton rows matching the final layout, not a spinner over blank space (§9). -->
                    <app-loading-state [rowCount]="6" [label]="'queue.title' | transloco" />
                }
            }

            <!-- Story 14 / S9-1 — the open-and-overdue task region belongs here. No cross-ticket
                 task endpoint exists (api-design §5.6 has only /tickets/{id}/tasks), so none is
                 invented and no placeholder is rendered that would imply data is coming. -->
        </section>
    `
})
export class AgentQueueComponent {
    private readonly api = inject(TicketsClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly page = signal<Paged<TicketListItem> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly filter = signal<TicketListFilter>({});

    /**
     * Whether the phone-width filter sheet is expanded (docs/ui-design.md §10.3). Above the
     * breakpoint it is ignored: CSS shows the bar unconditionally there, so this state costs nothing
     * and there is no second layout rule in TypeScript to keep in step.
     */
    protected readonly filtersOpen = signal(false);

    constructor() {
        // UI-9: the URL drives the screen, exactly as on the ticket list. A filter change navigates;
        // the navigation is what loads.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.filter.set(readQuickFilter(params));
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

    /** The empty state's action — every ticket in the agent's department, which the server scopes. */
    protected goToAllTickets(): void {
        void this.router.navigate(['/workspace/tickets']);
    }

    private load(params: ParamMap): void {
        this.page.set(null);
        this.problem.set(null);

        const page = Number(params.get('page'));

        this.api
            .list({
                ...readQuickFilter(params),
                // The one filter the URL does not carry, and no `sort` — the server's SLA-urgency
                // default is the ordering.
                assigneeId: 'me',
                page: Number.isFinite(page) && page > 1 ? page : undefined
            })
            .subscribe({
                next: (result) => this.page.set(result),
                error: (failure: ApiProblem) => this.problem.set(failure)
            });
    }
}

/**
 * The three quick filters of §5.1, read from the URL under the API's own names (UI-9) so no
 * translation step is needed. `assigneeId` is deliberately absent — see the class comment.
 */
function readQuickFilter(params: ParamMap): TicketListFilter {
    const breached = params.get('breached');

    return {
        status: params.get('status') as TicketListFilter['status'],
        priority: params.get('priority') as TicketListFilter['priority'],
        breached: breached === null ? null : breached === 'true'
    };
}

/** An absent filter is an absent parameter, never an empty one — the server would reject `status=`. */
function toQueryParams(filter: TicketListFilter): Record<string, string | null> {
    return {
        status: filter.status || null,
        priority: filter.priority || null,
        breached: filter.breached === null || filter.breached === undefined ? null : String(filter.breached)
    };
}
