import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { ApiProblem } from '../../core/api/api-problem';
import { Paged } from '../../core/api/paged';
import { PortalClient, PortalTicket } from '../../core/api/portal.client';
import { TICKET_STATUSES } from '../../core/api/tickets.client';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { StatusChipComponent } from '../../shared/components/status-chip/status-chip.component';

/**
 * My requests — `/portal/requests` (docs/ui-design.md §7.1). The portal's landing screen.
 *
 * <h3>Cards, not a dense table</h3>
 * §7.1 says so, and the reason is the payload: a customer's request has a subject, a status and a
 * date, and **nothing else the contract returns** (§6.4). A table of three columns with no priority,
 * no assignee and no SLA would be a staff screen with the interesting columns removed — a card that
 * carries what there is reads as a request instead of as a stripped ticket.
 *
 * **Single column at every width** (ui-design §10.3): the list is one column of cards on a phone and
 * one column of cards on a desktop. There is no grid to reflow and no table to turn into cards.
 *
 * <h3>No staff vocabulary</h3>
 * **UI-11, AP-16.** No department, no priority, no assignee, no SLA — and none is available to
 * render: `PortalTicket` has no such member, so the omission is structural rather than a decision
 * this template makes.
 *
 * <h3>The "response needed from you" cue</h3>
 * §7.1. Shown when the status is `Pending`, which is the one status that means *the agent is waiting
 * on the customer*. It is a **cue on the card, not a badge count**: A-13's notifications are
 * staff-facing and the portal has no feed (§4.2).
 *
 * <h3>⚠ "Last update" — finding I-35</h3>
 * §7.1 asks each card to show *"subject, status, last update"*. **The contract carries no
 * `updatedAt`**: `Ticket (portal)` (§6.4) has `createdAt` and `resolvedAt`, and
 * [data-model.md] §2.6 defines no modification timestamp on `Ticket` at all — so there is no such
 * value on any payload, staff or portal. Rather than invent one (a new column, a new response field,
 * or a guess derived from the newest message), **the card shows the timestamps the payload does
 * carry**, each under its own honest label: *submitted*, and *resolved* where the request has been.
 * Recorded as a finding for the user to settle; **it is not a design decision taken here.**
 */
@Component({
    selector: 'app-portal-requests',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, FormsModule,
        LoadingStateComponent, PaginatorModule, RouterLink, SelectModule, StatusChipComponent,
        TranslocoModule,
    ],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'portal.requests.title' | transloco }}</h1>

                <p-button
                    icon="pi pi-plus"
                    [label]="'portal.requests.submit' | transloco"
                    routerLink="/portal/requests/new" />
            </header>

            <div class="app-filters">
                <p-select
                    [options]="statusOptions"
                    [(ngModel)]="status"
                    [showClear]="true"
                    [placeholder]="'portal.requests.anyStatus' | transloco"
                    [ariaLabel]="'portal.requests.statusLabel' | transloco"
                    (onChange)="applyStatus()">
                    <ng-template #selectedItem let-code>{{ 'tickets.status.' + code | transloco }}</ng-template>
                    <ng-template #item let-code>{{ 'tickets.status.' + code | transloco }}</ng-template>
                </p-select>
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <!-- §7.1's empty state, with the submit action on it. An empty list is never
                             an error (§9) — and for a new customer it is the normal first screen. -->
                        <app-empty-state
                            [title]="emptyTitleKey() | transloco"
                            [message]="'portal.requests.emptyMessage' | transloco"
                            icon="pi-inbox"
                            [actionLabel]="'portal.requests.submit' | transloco"
                            (action)="goToSubmit()" />
                    } @else {
                        <ul class="app-request-cards">
                            @for (request of result.items; track request.id) {
                                <li class="app-request-card">
                                    <div class="app-request-card__head">
                                        <a class="app-request-card__subject" [routerLink]="['/portal/requests', request.id]">
                                            {{ request.subject }}
                                        </a>

                                        <!-- The SHARED chip and the same A-5 vocabulary as staff:
                                             ui-design §8 authorized no separate customer wording. -->
                                        <app-status-chip [status]="request.status" />
                                    </div>

                                    <!-- §7.1's cue. Pending is the one status that means the agent is
                                         waiting on the customer. -->
                                    @if (request.status === 'Pending') {
                                        <p class="app-request-card__cue">
                                            <i class="pi pi-exclamation-circle" aria-hidden="true"></i>
                                            {{ 'portal.requests.responseNeeded' | transloco }}
                                        </p>
                                    }

                                    <p class="app-request-card__meta app-ltr-numeric">
                                        {{ 'portal.requests.submitted' | transloco }}
                                        {{ request.createdAt | date: 'medium' }}
                                    </p>

                                    @if (request.resolvedAt) {
                                        <p class="app-request-card__meta app-ltr-numeric">
                                            {{ 'portal.requests.resolved' | transloco }}
                                            {{ request.resolvedAt | date: 'medium' }}
                                        </p>
                                    }
                                </li>
                            }
                        </ul>

                        <p-paginator
                            [first]="(result.page - 1) * result.pageSize"
                            [rows]="result.pageSize"
                            [totalRecords]="result.totalItems"
                            (onPageChange)="goToPage($event.page)" />
                    }
                } @else {
                    <app-loading-state [rowCount]="4" />
                }
            }
        </section>
    `
})
export class PortalRequestsComponent {
    private readonly api = inject(PortalClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly page = signal<Paged<PortalTicket> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    /** The six statuses of A-5 — the same set the staff filter offers, from the same constant. */
    protected readonly statusOptions = [...TICKET_STATUSES];

    protected status: string | null = null;

    constructor() {
        // UI-9: the filter lives in the URL, so a filtered list is shareable and survives a reload —
        // the same rule every staff list follows.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.status = params.get('status');
            this.load(params);
        });
    }

    /**
     * **"No requests at all" and "none match this filter" are different sentences** (ui-design §9).
     * Telling a customer who filtered to `Closed` that they have never submitted anything would be
     * false, and the submit action beside it would be the wrong suggestion.
     */
    protected emptyTitleKey(): string {
        return this.status ? 'portal.requests.emptyFilteredTitle' : 'portal.requests.emptyTitle';
    }

    protected applyStatus(): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { status: this.status || null, page: null }
        });
    }

    protected goToPage(zeroBasedPage: number | undefined): void {
        const page = (zeroBasedPage ?? 0) + 1;

        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { page: page > 1 ? page : null },
            queryParamsHandling: 'merge'
        });
    }

    protected goToSubmit(): void {
        void this.router.navigate(['/portal/requests/new']);
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
                status: params.get('status'),
                page: Number.isFinite(page) && page > 1 ? page : undefined
            })
            .subscribe({
                next: (result) => this.page.set(result),
                error: (failure: ApiProblem) => this.problem.set(failure)
            });
    }
}
