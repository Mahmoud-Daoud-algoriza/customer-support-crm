import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ApiProblem } from '../../../core/api/api-problem';
import { Ticket, TicketsClient } from '../../../core/api/tickets.client';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { PriorityChipComponent } from '../../../shared/components/priority-chip/priority-chip.component';
import { StatusChipComponent } from '../../../shared/components/status-chip/status-chip.component';
import { TicketAssignComponent } from './ticket-assign.component';
import { TicketCustomerPanelComponent } from './ticket-customer-panel.component';

/**
 * Ticket detail — `/workspace/tickets/:id` (docs/ui-design.md §5.3). Agent+.
 *
 * <h3>Story 05 builds the header region and the customer panel — and nothing else</h3>
 * The thread, internal notes, activity, tasks, suggested articles and the AI panel are added into
 * the region slots left here by Stories 06, 07, 11, 12 and 14. Regions load **independently**, so a
 * slow call never blanks the screen.
 *
 * <h3>Assignment does not change status (A-18)</h3>
 * **After assigning an unassigned `New` ticket the status chip must still read `New`.** The header
 * renders assignee and status as **two independent facts**, side by side and never derived from one
 * another — this is the detail §5.3 flags as most likely to be got wrong, and
 * `TicketCreationTests.Assigning_a_new_ticket_leaves_the_status_new` pins the server half.
 *
 * <h3>What is deliberately not rendered</h3>
 * **`Transition ▾` and `Escalate` are absent.** Story 06 adds them with their A-5 legality and A-16
 * authority rules; a control that looks disabled and does nothing is worse than none. Their absence
 * here is the shaped hole, not an oversight.
 *
 * <h3>The SLA line and A-20</h3>
 * Both due timestamps are shown as the server computed them at creation. **A priority change does
 * not move them** (A-20, closing OQ-2), so this screen never recomputes a deadline locally — it
 * displays what the payload carries.
 */
@Component({
    selector: 'app-ticket-detail',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe, ErrorStateComponent, LoadingStateComponent, PriorityChipComponent, RouterLink, StatusChipComponent, TicketAssignComponent, TicketCustomerPanelComponent, TranslocoModule],
    template: `
        <section class="app-page">
            <a routerLink="/workspace/tickets">{{ 'actions.back' | transloco }}</a>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (ticket(); as row) {
                    <div class="app-ticket-layout">
                        <div class="app-ticket-main">
                            <!-- ------------------------------------------------ Header -->
                            <header class="app-page__header">
                                <h1 class="app-page__title">{{ row.subject }}</h1>

                                <div class="app-ticket-card__chips">
                                    <!-- Two independent facts (A-18): the chip below is the STATUS,
                                         and it does not change when an assignee is set. -->
                                    <app-status-chip [status]="row.status" />
                                    <app-priority-chip [priority]="row.priority" />
                                </div>
                            </header>

                            <p class="app-page__meta">
                                {{ 'tickets.categoryLabel' | transloco }}: {{ row.categoryCode }} · {{ 'tickets.assigneeLabel' | transloco }}:
                                {{ row.assignee?.displayName ?? ('tickets.unassigned' | transloco) }}
                                @if (row.isUrgent) {
                                    · <span class="app-breach">{{ 'tickets.urgentFlag' | transloco }}</span>
                                }
                            </p>

                            <!-- The SLA line. Frozen at creation (A-20). -->
                            <p class="app-page__meta app-ltr-numeric">
                                {{ 'tickets.firstResponseDue' | transloco }}: {{ row.firstResponseDueAt | date: 'short' }}
                                @if (row.firstResponseBreached) {
                                    <span class="app-breach">{{ 'tickets.breached' | transloco }}</span>
                                }
                                · {{ 'tickets.resolutionDue' | transloco }}: {{ row.resolutionDueAt | date: 'short' }}
                                @if (row.resolutionBreached) {
                                    <span class="app-breach">{{ 'tickets.breached' | transloco }}</span>
                                }
                            </p>

                            <!-- Assign. Transition and Escalate are Story 06's and are absent. -->
                            <app-ticket-assign [departmentId]="row.departmentId" [assigneeId]="row.assignee?.id ?? null" [busy]="assigning()" [problem]="assignProblem()" (assign)="assign($event)" />

                            <section class="app-region">
                                <h2 class="app-region__title">{{ 'tickets.description' | transloco }}</h2>
                                <p class="app-ticket-description">{{ row.description }}</p>
                            </section>

                            <!-- Story 06: the ACTIVITY region. Story 07: the THREAD and the reply
                                 composer. Story 11: the AI panel. Story 12: suggested articles.
                                 Story 14: internal notes and tasks. Each lands in this column. -->
                        </div>

                        <app-ticket-customer-panel [customerId]="row.customer.id" />
                    </div>
                } @else {
                    <app-loading-state [rowCount]="5" />
                }
            }
        </section>
    `
})
export class TicketDetailComponent {
    private readonly api = inject(TicketsClient);
    private readonly route = inject(ActivatedRoute);

    private readonly ticketId = this.route.snapshot.paramMap.get('id') ?? '';

    protected readonly ticket = signal<Ticket | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly assigning = signal(false);
    protected readonly assignProblem = signal<ApiProblem | null>(null);

    constructor() {
        this.load();
    }

    protected load(): void {
        this.ticket.set(null);
        this.problem.set(null);

        this.api.getTicket(this.ticketId).subscribe({
            next: (row) => this.ticket.set(row),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }

    /**
     * `422 assignee-out-of-department` renders **inline on the assign control** (docs/ui-design.md
     * §9), never as a toast and never as a page-level error: the failure belongs to the control that
     * caused it.
     *
     * On success the whole ticket is replaced from the response, which is what keeps the status chip
     * honest — the server says the status is unchanged, and the screen shows what the server said
     * rather than what it assumed.
     */
    protected assign(assignedUserId: string): void {
        if (this.assigning()) {
            return;
        }

        this.assigning.set(true);
        this.assignProblem.set(null);

        this.api.assign(this.ticketId, assignedUserId).subscribe({
            next: (row) => {
                this.assigning.set(false);
                this.ticket.set(row);
            },
            error: (failure: ApiProblem) => {
                this.assigning.set(false);
                this.assignProblem.set(failure);
            }
        });
    }
}
