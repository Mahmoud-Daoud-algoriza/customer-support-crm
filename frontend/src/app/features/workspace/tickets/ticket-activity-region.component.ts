import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../core/api/api-problem';
import { TicketActivityEntry, TicketsClient } from '../../../core/api/tickets.client';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * The **activity region** of the ticket detail screen — docs/ui-design.md §5.3, Story 06 task 10.
 *
 * A chronological list with **actor, timestamp and before/after values**, which is the intake's
 * acceptance criterion word for word.
 *
 * <h3>Actor rendering, and the R-14 trap</h3>
 * A `System`-actor row renders as **"System"** — those come from Story 09's SLA breach sweep. **The
 * automatic `Pending → Open` row renders the customer as the actor**, because it carries
 * `actorKind: 'User'` (**R-14**): attributing a customer-caused change to the system would make the
 * history less truthful. **No "system" label appears on it**, and this component has no special case
 * for it — it reads `actorKind` and nothing else, which is what makes the trap unspringable.
 *
 * <h3>Internal entries are shown here</h3>
 * This is the **staff** read (`GET /tickets/{id}/activity`, *"full history, internal entries
 * included"*), and the route is staff-only. They carry a visible marker (**UI-5**) so an agent can
 * see at a glance that the customer cannot. The customer-facing exclusion lives in the server's
 * timeline projection, not here.
 *
 * <h3>It loads independently</h3>
 * Its own loading, empty and error states (§9), so a slow history never blanks the screen.
 */
@Component({
    selector: 'app-ticket-activity-region',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe, EmptyStateComponent, ErrorStateComponent, LoadingStateComponent, TagModule, TranslocoModule],
    template: `
        <section class="app-region">
            <h2 class="app-region__title">{{ 'tickets.activity' | transloco }}</h2>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (entries(); as rows) {
                @if (rows.length === 0) {
                    <app-empty-state [title]="'tickets.activityEmptyTitle' | transloco" [message]="'tickets.activityEmptyMessage' | transloco" />
                } @else {
                    <ol class="app-activity">
                        @for (entry of rows; track entry.id) {
                            <li class="app-activity__item">
                                <p class="app-activity__meta app-ltr-numeric">
                                    <!-- actorKind decides the label. No special case for the
                                         automatic Pending -> Open row: it is a User entry (R-14). -->
                                    <span class="app-activity__actor">
                                        {{ entry.actor?.displayName ?? ('tickets.systemActor' | transloco) }}
                                    </span>
                                    · {{ entry.occurredAt | date: 'short' }}

                                    @if (entry.visibility === 'Internal') {
                                        <p-tag severity="warn" [value]="'tickets.internalOnly' | transloco" />
                                    }
                                </p>

                                <p class="app-activity__body">
                                    {{ 'tickets.activityType.' + entry.activityType | transloco }}

                                    @if (entry.oldValue || entry.newValue) {
                                        <span class="app-activity__change">
                                            {{ entry.oldValue ?? '—' }} → {{ entry.newValue ?? '—' }}
                                        </span>
                                    }
                                </p>
                            </li>
                        }
                    </ol>
                }
                } @else {
                    <app-loading-state [rowCount]="3" />
                }
            }
        </section>
    `
})
export class TicketActivityRegionComponent {
    private readonly api = inject(TicketsClient);

    readonly ticketId = input.required<string>();

    /**
     * Bumped by the parent after a transition or an escalation, so the history reloads without the
     * region owning a subscription to the parent's state.
     */
    readonly reloadToken = input(0);

    protected readonly entries = signal<TicketActivityEntry[] | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    constructor() {
        // **An effect, not `ngOnInit`.** It covers both jobs at once: the first load, and every
        // reload the parent asks for by bumping `reloadToken`. Reading a *required* signal input is
        // safe here because an effect first runs after change detection has set the inputs —
        // whereas reading one in a constructor throws `NG0950` (finding **I-18**).
        effect(() => {
            // Tracked so a bump re-runs this. `ticketId` is tracked too, which is correct: a
            // different ticket is a different history.
            this.reloadToken();

            this.load();
        });
    }

    protected load(): void {
        this.entries.set(null);
        this.problem.set(null);

        // pageSize 100 is the contract's cap (AP-3). A ticket's history is short by nature; paging
        // controls here would be furniture for a case that does not arise in this scope.
        this.api.activity(this.ticketId(), { pageSize: 100 }).subscribe({
            next: (page) => this.entries.set(page.items),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
