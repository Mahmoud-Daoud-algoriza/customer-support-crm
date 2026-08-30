import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { ApiProblem } from '../../../core/api/api-problem';
import { Customer, CustomersClient, TimelineEntry } from '../../../core/api/customers.client';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * The **customer panel** of docs/ui-design.md §5.3 — *"reachable without leaving the screen (T1-C):
 * a side region on desktop, a drawer at phone width. Never a navigation away from an unsent
 * draft."*
 *
 * It is rendered **in place** rather than as a route, which is the whole point: Story 07's reply
 * composer will sit on the same screen, and navigating to the customer profile to check a detail
 * must never cost an agent their unsent draft. The *"open profile"* link is the deliberate
 * exception, and it is a link the agent chooses.
 *
 * <h3>Recent tickets — finding S9-6</h3>
 * The recent-ticket list is derived from **`GET /customers/{id}/timeline`**, not from a customer
 * filter on the ticket list: **`GET /tickets` has no `customerId` filter, so do not invent one.**
 * The timeline is empty until Story 06 fills it, so this region shows its empty state today — which
 * is correct rather than a gap.
 */
@Component({
    selector: 'app-ticket-customer-panel',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, LoadingStateComponent, RouterLink, TranslocoModule],
    template: `
        <aside class="app-customer-panel">
            <h2 class="app-region__title">{{ 'tickets.customerPanel' | transloco }}</h2>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (customer(); as row) {
                    <p class="app-customer-panel__name">{{ row.fullName }}</p>
                    <p class="app-customer-panel__meta">{{ row.email }}</p>
                    @if (row.phone) {
                        <p class="app-customer-panel__meta app-ltr-numeric">{{ row.phone }}</p>
                    }
                    <p class="app-customer-panel__meta">{{ row.branch.name }}</p>

                    <p-button severity="secondary" [text]="true" [label]="'tickets.openProfile' | transloco" [routerLink]="['/workspace/customers', row.id]" />
                } @else {
                    <app-loading-state [rowCount]="3" />
                }
            }

            <h3 class="app-customer-panel__subtitle">{{ 'tickets.recentActivity' | transloco }}</h3>

            @if (timelineProblem(); as failure) {
                <app-error-state [problem]="failure" [retryable]="false" />
            } @else {
                @if (timeline(); as entries) {
                    @if (entries.length === 0) {
                        <!-- Not an error: the projection fills as Story 06 lands. -->
                        <app-empty-state [title]="'customers.timeline.emptyTitle' | transloco" icon="pi-clock" />
                    } @else {
                        <ul class="app-timeline">
                            @for (entry of entries; track $index) {
                                <li class="app-timeline__entry">
                                    <span class="app-timeline__when app-ltr-numeric">{{ entry.occurredAt | date: 'short' }}</span>
                                    <span class="app-timeline__what">{{ entry.ticketSubject }}</span>
                                </li>
                            }
                        </ul>
                    }
                } @else {
                    <app-loading-state [rowCount]="2" />
                }
            }
        </aside>
    `
})
export class TicketCustomerPanelComponent implements OnInit {
    private readonly api = inject(CustomersClient);

    readonly customerId = input.required<string>();

    protected readonly customer = signal<Customer | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly timeline = signal<TimelineEntry[] | null>(null);
    protected readonly timelineProblem = signal<ApiProblem | null>(null);

    /**
     * **`ngOnInit`, not the constructor.** `customerId` is a *required* signal input, and a required
     * input is not set until the first change detection runs — reading one in a constructor throws
     * `NG0950`, which aborts the parent's render block and leaves the screen blank with no error
     * state to explain it. Found by driving the real screen; recorded as finding **I-18**.
     */
    ngOnInit(): void {
        this.load();
    }

    /** Two independent loads, so a slow timeline never blanks the profile (docs/ui-design.md §5.3). */
    protected load(): void {
        this.customer.set(null);
        this.problem.set(null);

        this.api.getCustomer(this.customerId()).subscribe({
            next: (row) => this.customer.set(row),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });

        this.timeline.set(null);
        this.timelineProblem.set(null);

        this.api.timeline(this.customerId(), { pageSize: 5 }).subscribe({
            next: (result) => this.timeline.set(result.items),
            error: (failure: ApiProblem) => this.timelineProblem.set(failure)
        });
    }
}
