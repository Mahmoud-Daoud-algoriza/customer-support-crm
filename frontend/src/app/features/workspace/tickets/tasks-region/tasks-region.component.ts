import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiProblem, problemTranslationKey } from '../../../../core/api/api-problem';
import { IdentityClient } from '../../../../core/api/identity.client';
import { TicketTask, TicketsClient } from '../../../../core/api/tickets.client';
import { AuthStore } from '../../../../core/auth/auth.store';
import { UserRow } from '../../../../core/auth/identity.model';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';

/**
 * The **tasks region** of the ticket detail screen — docs/ui-design.md §5.3, Story 14 task 10.
 *
 * A table of **title, due date, assignee and a done checkbox**, with **overdue rows visually
 * distinct**, and a small dialog to add one.
 *
 * <h3>No calendar, no recurrence, no reminders</h3>
 * None of the three exists server-side, and rendering a control for one would promise behaviour the
 * product does not have (T2-C, the intake's Out of scope). The date picker below picks **one** date;
 * it is not a calendar view.
 *
 * <h3>The assignee picker reuses finding I-16's resolution, it does not re-open it</h3>
 * **No approved endpoint lets an Agent list the staff in their department** — `GET /users` is
 * Administrator-only (docs/api-design.md §5.3). Story 05 hit this for ticket assignment and recorded
 * it as **I-16**; the same constraint applies to a task assignee, and the same shape is used rather
 * than inventing a second answer:
 *
 * - **Every caller can assign the task to themselves.** It needs no directory and is always valid —
 *   the ticket was loaded through the department scope, so a caller reaching it is by construction
 *   in its department, which is the server's own rule for a legal assignee (§5 constraint 10).
 * - **An Administrator also gets a real picker**, filtered to active agents in the ticket's
 *   department. The directory is requested only when the caller may read it, because
 *   `errorInterceptor` routes any `403` to `/403` and a speculative call would bounce an Agent off
 *   the ticket.
 *
 * **The guard hides; it does not protect.** The server answers `422 assignee-out-of-department` for
 * an ineligible assignee whatever this component offered.
 *
 * <h3>Gregorian in both languages</h3>
 * The picker is `[dateFormat]="'yy-mm-dd'"` with no Hijri option anywhere — **A-11**, and the
 * intake's Out of scope. Story 17 Part B owns the shared date pipe that will format the *rendered*
 * dates in one place; this region uses the same `| date` the other regions use until then, so it
 * changes with them rather than becoming a second convention.
 */
@Component({
    selector: 'app-tasks-region',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, CheckboxModule, DatePickerModule, DatePipe, DialogModule, EmptyStateComponent,
        ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, MessageModule,
        SelectModule, TranslocoModule
    ],
    template: `
        <section class="app-region app-tasks">
            <header class="app-tasks__header">
                <h2 class="app-region__title">{{ 'tickets.tasks.title' | transloco }}</h2>

                @if (!isTerminal()) {
                    <p-button
                        size="small"
                        severity="secondary"
                        icon="pi pi-plus"
                        [label]="'tickets.tasks.add' | transloco"
                        (onClick)="openDialog()" />
                }
            </header>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (tasks(); as rows) {
                    @if (rows.length === 0) {
                        <app-empty-state
                            [title]="'tickets.tasks.emptyTitle' | transloco"
                            [message]="'tickets.tasks.emptyMessage' | transloco" />
                    } @else {
                        <ul class="app-tasks__list">
                            @for (task of rows; track task.id) {
                                <li class="app-tasks__item" [class.app-tasks__item--overdue]="isOverdue(task)">
                                    <p-checkbox
                                        [binary]="true"
                                        [ngModel]="task.isDone"
                                        [ngModelOptions]="{ standalone: true }"
                                        [disabled]="busyTaskId() === task.id"
                                        [ariaLabel]="'tickets.tasks.markDone' | transloco"
                                        (onChange)="setDone(task, $event.checked)" />

                                    <span class="app-tasks__title" [class.app-tasks__title--done]="task.isDone">
                                        {{ task.title }}
                                    </span>

                                    <span class="app-tasks__due app-ltr-numeric">
                                        {{ task.dueAt | date: 'shortDate' }}

                                        @if (isOverdue(task)) {
                                            <span class="app-tasks__overdue-label">
                                                {{ 'tickets.tasks.overdue' | transloco }}
                                            </span>
                                        }
                                    </span>

                                    <span class="app-tasks__assignee">{{ task.assignee.displayName }}</span>
                                </li>
                            }
                        </ul>
                    }
                } @else {
                    <app-loading-state [rowCount]="2" />
                }
            }

            @if (mutationProblem(); as failure) {
                <p-message severity="error" [text]="errorKey(failure) | transloco" />
            }
        </section>

        <p-dialog
            [visible]="dialogOpen()"
            [modal]="true"
            [draggable]="false"
            [dismissableMask]="true"
            [style]="{ width: '28rem' }"
            [header]="'tickets.tasks.add' | transloco"
            (visibleChange)="dialogOpen.set($event)">
            <form class="app-tasks__form" (ngSubmit)="create()">
                <label for="task-title">{{ 'tickets.tasks.titleLabel' | transloco }}</label>
                <input pInputText id="task-title" [(ngModel)]="draftTitle" [ngModelOptions]="{ standalone: true }" />

                <label for="task-due">{{ 'tickets.tasks.dueLabel' | transloco }}</label>
                <!-- One date. Not a calendar view, and no recurrence control (T2-C).
                     Gregorian in both languages (A-11) — no Hijri option is offered anywhere. -->
                <p-datepicker
                    inputId="task-due"
                    dateFormat="yy-mm-dd"
                    [(ngModel)]="draftDueAt"
                    [ngModelOptions]="{ standalone: true }"
                    [showIcon]="true"
                    [minDate]="today" />

                <label for="task-assignee">{{ 'tickets.tasks.assigneeLabel' | transloco }}</label>
                @if (candidates().length > 0) {
                    <p-select
                        inputId="task-assignee"
                        optionLabel="displayName"
                        optionValue="id"
                        [options]="candidates()"
                        [(ngModel)]="draftAssigneeId"
                        [ngModelOptions]="{ standalone: true }"
                        [placeholder]="'tickets.tasks.assigneePlaceholder' | transloco" />
                } @else {
                    <!-- I-16: no agent-readable directory exists, so self-assignment is the path
                         that always works. -->
                    <p class="app-tasks__self">{{ 'tickets.tasks.assignToMeOnly' | transloco }}</p>
                }

                @if (createProblem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                <div class="app-tasks__actions">
                    <p-button
                        type="button"
                        severity="secondary"
                        [label]="'actions.cancel' | transloco"
                        (onClick)="dialogOpen.set(false)" />

                    <p-button
                        type="submit"
                        [label]="'tickets.tasks.add' | transloco"
                        [loading]="creating()"
                        [disabled]="creating() || !canCreate()" />
                </div>
            </form>
        </p-dialog>
    `,
    styles: `
        .app-tasks__header {
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
            align-items: center;
            justify-content: space-between;
        }

        .app-tasks__list {
            margin-block: 0.75rem 0;
            padding-inline-start: 0;
            list-style: none;
        }

        .app-tasks__item {
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
            align-items: center;
            padding-block: 0.4rem;
        }

        .app-tasks__item + .app-tasks__item {
            border-block-start: 1px solid var(--p-content-border-color);
        }

        /* Overdue rows are visually distinct (task 10) — colour AND a text label, so the cue does
           not depend on colour perception alone. */
        .app-tasks__item--overdue .app-tasks__due {
            color: var(--p-red-600);
            font-weight: 600;
        }

        :host-context(.app-dark) .app-tasks__item--overdue .app-tasks__due {
            color: var(--p-red-400);
        }

        .app-tasks__title {
            flex: 1 1 12rem;
        }

        .app-tasks__title--done {
            text-decoration: line-through;
            color: var(--p-text-muted-color);
        }

        .app-tasks__due,
        .app-tasks__assignee {
            font-size: 0.8125rem;
            color: var(--p-text-muted-color);
        }

        .app-tasks__overdue-label {
            margin-inline-start: 0.35rem;
        }

        .app-tasks__form {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
        }

        .app-tasks__self {
            margin-block: 0;
            font-size: 0.8125rem;
            color: var(--p-text-muted-color);
        }

        .app-tasks__actions {
            display: flex;
            gap: 0.5rem;
            justify-content: flex-end;
            margin-block-start: 0.5rem;
        }
    `
})
export class TasksRegionComponent {
    private readonly api = inject(TicketsClient);
    private readonly identity = inject(IdentityClient);
    private readonly auth = inject(AuthStore);

    readonly ticketId = input.required<string>();

    /** The ticket's department — a candidate must belong to it, or the server answers `422`. */
    readonly departmentId = input.required<string>();

    /** `Closed` and `Cancelled` accept no further tasks (§5 constraint 8). */
    readonly isTerminal = input(false);

    readonly reloadToken = input(0);

    protected readonly tasks = signal<TicketTask[] | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly mutationProblem = signal<ApiProblem | null>(null);
    protected readonly createProblem = signal<ApiProblem | null>(null);
    protected readonly busyTaskId = signal<string | null>(null);
    protected readonly candidates = signal<UserRow[]>([]);
    protected readonly dialogOpen = signal(false);
    protected readonly creating = signal(false);

    protected draftTitle = '';
    protected draftDueAt: Date | null = null;
    protected draftAssigneeId: string | null = null;

    /** The picker offers today onwards: a task due in the past is not a thing anyone means to create. */
    protected readonly today = new Date();

    protected errorKey = problemTranslationKey;

    /**
     * All three fields are required by the contract (docs/data-model.md §2.10), so the submit stays
     * disabled until all three are present. A method rather than a `computed`, because the drafts
     * are plain `ngModel` fields — a computed would not track them and would never re-evaluate.
     */
    protected canCreate(): boolean {
        return this.draftTitle.trim().length > 0
            && this.draftDueAt !== null
            && this.draftAssigneeId !== null;
    }

    constructor() {
        // An effect rather than ngOnInit — reading a required signal input in a constructor throws
        // NG0950 (finding I-18). It also covers the parent's reload bumps.
        effect(() => {
            this.reloadToken();

            this.load();
        });

        // The directory, once, and only for the role that may read it — I-16. Asked here rather
        // than on dialog open so the picker is populated the moment it is shown.
        effect(() => {
            const departmentId = this.departmentId();

            if (!this.auth.isAtLeast('Administrator')) {
                return;
            }

            this.identity
                .listUsers({ role: 'Agent', departmentId, isActive: true, pageSize: 100 })
                .subscribe({ next: (result) => this.candidates.set(result.items) });
        });
    }

    /**
     * Overdue is **not done and past due**. A completed task that was finished late is not overdue —
     * it is finished, and colouring it red would report a problem that no longer exists.
     */
    protected isOverdue(task: TicketTask): boolean {
        return !task.isDone && new Date(task.dueAt).getTime() < Date.now();
    }

    protected load(): void {
        this.tasks.set(null);
        this.problem.set(null);

        this.api.tasks(this.ticketId(), { pageSize: 100 }).subscribe({
            next: (page) => this.tasks.set(page.items),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }

    protected openDialog(): void {
        this.draftTitle = '';
        this.draftDueAt = null;
        // Self-assignment is the default because it is the one choice that always works (I-16).
        this.draftAssigneeId = this.auth.identity()?.id ?? null;
        this.createProblem.set(null);
        this.dialogOpen.set(true);
    }

    protected create(): void {
        const title = this.draftTitle.trim();
        const dueAt = this.draftDueAt;
        const assignedUserId = this.draftAssigneeId;

        if (title.length === 0 || dueAt === null || assignedUserId === null || this.creating()) {
            return;
        }

        this.creating.set(true);
        this.createProblem.set(null);

        this.api
            .createTask(this.ticketId(), { title, dueAt: dueAt.toISOString(), assignedUserId })
            .subscribe({
                next: (created) => {
                    // Inserted locally and re-sorted by due date, which is the server's own order —
                    // so the row lands where a reload would have put it, without the round trip.
                    this.tasks.update((rows) =>
                        [...(rows ?? []), created].sort((a, b) => a.dueAt.localeCompare(b.dueAt))
                    );
                    this.creating.set(false);
                    this.dialogOpen.set(false);
                },
                error: (failure: ApiProblem) => {
                    this.createProblem.set(failure);
                    this.creating.set(false);
                }
            });
    }

    protected setDone(task: TicketTask, isDone: boolean): void {
        this.busyTaskId.set(task.id);
        this.mutationProblem.set(null);

        this.api.setTaskDone(this.ticketId(), task.id, isDone).subscribe({
            next: (updated) => {
                // The RESPONSE is written back, not the checkbox's value: `completedAt` is
                // server-set, and the server is the only thing that knows it (§5 constraint 23).
                this.tasks.update((rows) =>
                    (rows ?? []).map((row) => (row.id === updated.id ? updated : row))
                );
                this.busyTaskId.set(null);
            },
            error: (failure: ApiProblem) => {
                this.mutationProblem.set(failure);
                this.busyTaskId.set(null);

                // UI-8: no optimistic update. The server refused, so the list is re-read rather than
                // left showing a state the server does not hold.
                this.load();
            }
        });
    }
}
