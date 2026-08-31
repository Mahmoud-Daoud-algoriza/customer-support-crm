import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { TranslocoModule } from '@jsverse/transloco';
import { MessageModule } from 'primeng/message';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { Ticket, TicketStatus, TicketsClient } from '../../../core/api/tickets.client';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { EscalateButtonComponent } from '../../../shared/components/escalate-button/escalate-button.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { PriorityChipComponent } from '../../../shared/components/priority-chip/priority-chip.component';
import { StatusChipComponent } from '../../../shared/components/status-chip/status-chip.component';
import { TransitionMenuComponent } from '../../../shared/components/transition-menu/transition-menu.component';
import { isTerminal } from '../../../shared/lifecycle/transition-matrix';
import { TicketActivityRegionComponent } from './ticket-activity-region.component';
import { TicketAssignComponent } from './ticket-assign.component';
import { TicketCustomerPanelComponent } from './ticket-customer-panel.component';
import { TicketThreadRegionComponent } from './ticket-thread-region.component';

/**
 * Ticket detail — `/workspace/tickets/:id` (docs/ui-design.md §5.3). Agent+.
 *
 * <h3>Story 05 built the header, Story 06 the lifecycle, Story 07 the thread</h3>
 * The `Transition ▾` menu, the `Escalate` control and the **activity region** are Story 06's; the
 * **thread and reply composer** are Story 07's. Internal notes, tasks, suggested articles and the AI
 * panel are still to come, from Stories 11, 12 and 14. Regions load **independently**, so a slow
 * call never blanks the screen — and the thread is not chat: nothing on this screen polls (T3-B).
 *
 * <h3>Assignment does not change status (A-18)</h3>
 * **After assigning an unassigned `New` ticket the status chip must still read `New`.** The header
 * renders assignee and status as **two independent facts**, side by side and never derived from one
 * another — this is the detail §5.3 flags as most likely to be got wrong, and
 * `TicketCreationTests.Assigning_a_new_ticket_leaves_the_status_new` pins the server half.
 *
 * <h3>Lifecycle controls, and the two rules behind them</h3>
 * The menu offers **legal ∧ permitted** (UI-3), computed from
 * `shared/lifecycle/transition-matrix.ts` — the one file finding **F-1** confines that duplication
 * to. **`Escalate` is a separate control, never inside the menu** (AP-7).
 *
 * **No optimistic UI (UI-8):** the status chip changes only *after* the server confirms, because a
 * transition can be refused. A `409` renders contextually from the problem **`type`**, never from the
 * server's `detail` (§9).
 *
 * **`Closed` and `Cancelled` disable the lifecycle controls with a reason line** rather than leaving
 * silently inert buttons on screen (§5.3).
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
    imports: [DatePipe, ErrorStateComponent, EscalateButtonComponent, LoadingStateComponent, MessageModule, PriorityChipComponent, RouterLink, StatusChipComponent, TicketActivityRegionComponent, TicketAssignComponent, TicketCustomerPanelComponent, TicketThreadRegionComponent, TransitionMenuComponent, TranslocoModule],
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

                            <!-- Lifecycle controls. The menu and Escalate are separate (AP-7). -->
                            <div class="app-ticket-actions">
                                <app-transition-menu [status]="row.status" [busy]="working()" (transition)="transition($event)" />

                                <app-escalate-button [status]="row.status" [busy]="working()" (escalate)="escalate()" />
                            </div>

                            <!-- A refused transition renders inline, from the problem TYPE (§9). -->
                            @if (lifecycleProblem(); as failure) {
                                <p-message severity="error" [text]="errorKey(failure) | transloco" />
                            }

                            <!-- Terminal tickets say why the controls are gone (§5.3). Assignment
                                 goes with them: there is no work left to hand to anyone. -->
                            @if (terminal()) {
                                <p class="app-page__meta">{{ 'tickets.terminalNoActions' | transloco }}</p>
                            } @else {
                                <app-ticket-assign [departmentId]="row.departmentId" [assigneeId]="row.assignee?.id ?? null" [busy]="assigning()" [problem]="assignProblem()" (assign)="assign($event)" />
                            }

                            <section class="app-region">
                                <h2 class="app-region__title">{{ 'tickets.description' | transloco }}</h2>
                                <p class="app-ticket-description">{{ row.description }}</p>
                            </section>

                            <!-- The THREAD, above the history: §5.3 puts the conversation in the
                                 middle of the screen and the change log below it. A reply bumps the
                                 activity token, because a MessagePosted row has just been written
                                 and the first outbound one also stamped firstRespondedAt. -->
                            <app-ticket-thread-region [ticketId]="row.id" [status]="row.status" [reloadToken]="activityToken()" (replied)="onReplied()" />

                            <!-- The activity region. It reloads when a lifecycle action lands,
                                 which is what makes the new history entry visible immediately. -->
                            <app-ticket-activity-region [ticketId]="row.id" [reloadToken]="activityToken()" />

                            <!-- Story 11: the AI panel. Story 12: suggested articles. Story 14:
                                 internal notes and tasks. Each lands in this column. -->
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

    /** A transition or an escalation is in flight. Both disable both controls. */
    protected readonly working = signal(false);

    /** The inline failure for a refused lifecycle action — `403`, `409` or `404`. */
    protected readonly lifecycleProblem = signal<ApiProblem | null>(null);

    /** Bumped after a lifecycle action so the activity region reloads. */
    protected readonly activityToken = signal(0);

    protected readonly terminal = computed(() => {
        const row = this.ticket();

        return row !== null && isTerminal(row.status);
    });

    protected errorKey = problemTranslationKey;

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
    /**
     * **No optimistic UI (UI-8).** The chip changes only when the server's response arrives, and the
     * whole ticket is replaced from it — so what the screen shows is what the server said rather than
     * what the menu assumed. A refusal leaves the ticket exactly as it was.
     *
     * A `409 illegal-transition` means the menu offered something the server rejected — possible
     * because the client matrix is a duplicate (**F-1**) and the ticket may have moved in another
     * tab. It renders from the problem `type`, and the ticket is reloaded so the menu re-derives
     * from the real status.
     */
    protected transition(targetStatus: TicketStatus): void {
        this.runLifecycle(this.api.transition(this.ticketId, targetStatus));
    }

    /** AP-7 — its own endpoint. Priority rises one level; the status chip must not move. */
    protected escalate(): void {
        this.runLifecycle(this.api.escalate(this.ticketId));
    }

    private runLifecycle(call: Observable<Ticket>): void {
        if (this.working()) {
            return;
        }

        this.working.set(true);
        this.lifecycleProblem.set(null);

        call.subscribe({
            next: (row) => {
                this.working.set(false);
                this.ticket.set(row);
                this.activityToken.update((token) => token + 1);
            },
            error: (failure: ApiProblem) => {
                this.working.set(false);
                this.lifecycleProblem.set(failure);

                // Re-read: a refusal usually means this screen's idea of the status is stale, and
                // the menu is computed from it.
                this.api.getTicket(this.ticketId).subscribe({ next: (row) => this.ticket.set(row) });
            }
        });
    }

    /**
     * A reply landed. The header is re-read because the **first** outbound message sets
     * `firstRespondedAt` (§2.8), and the activity token is bumped because the same request wrote a
     * `MessagePosted` row.
     *
     * <p>The thread itself already has the new message from its own response — it does not need
     * this token, and bumping it does no harm because the region simply re-reads what it has.</p>
     */
    protected onReplied(): void {
        this.activityToken.update((token) => token + 1);

        this.api.getTicket(this.ticketId).subscribe({ next: (row) => this.ticket.set(row) });
    }

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
