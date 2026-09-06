import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';
import { ApiProblem } from '../../../../core/api/api-problem';
import { InternalNote, TicketsClient } from '../../../../core/api/tickets.client';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';
import { LtrEmbedDirective } from '../../../../shared/directives/ltr-embed.directive';
import { AppDatePipe } from '../../../../shared/pipes/app-date.pipe';

/**
 * The **internal notes region** of the ticket detail screen — docs/ui-design.md §5.3 and **UI-5**,
 * Story 14 task 9.
 *
 * <h3>The persistent marker is the whole point of UI-5</h3>
 * *"Internal notes live in a visually distinct region with a persistent 'not visible to the
 * customer' marker."* The marker is **always rendered** — not a hover state, not a per-item badge,
 * and **not conditional on the list being non-empty**. It sits in the region header above both the
 * list and the composer, so it is on screen at the moment an agent is typing, which is the moment
 * it matters. The rejected alternative — a merged thread with per-item badges — is recorded in UI-5.
 *
 * <h3>Its own endpoint, so there is nothing to filter</h3>
 * The region calls `GET /tickets/{id}/internal-notes` and renders exactly what comes back. **There
 * is no merged list and no client-side filter**, so a rendering bug cannot leak a note into the
 * customer thread and a filter bug cannot leak a message into here (§5.3). The two regions read two
 * endpoints.
 *
 * <h3>No edit and no delete control</h3>
 * Neither exists server-side (§5 constraint 16), and rendering one would promise behaviour the
 * product does not have. A correction is a new note.
 *
 * <h3>Terminal tickets</h3>
 * `Closed` and `Cancelled` disable the note field with a reason line rather than leaving a control
 * that fails on submit (§5.3) — the same treatment the reply composer gets, because it is the same
 * rule (§5 constraint 8).
 */
@Component({
    selector: 'app-internal-notes-region',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        AppDatePipe, LtrEmbedDirective, ButtonModule, EmptyStateComponent, ErrorStateComponent, FormsModule,
        LoadingStateComponent, TextareaModule, TranslocoModule
    ],
    template: `
        <section class="app-region app-internal-notes">
            <header class="app-internal-notes__header">
                <h2 class="app-region__title">{{ 'tickets.internalNotes.title' | transloco }}</h2>

                <!-- UI-5: ALWAYS rendered. Not a hover state, not per-item, and not conditional on
                     there being notes — an empty region is exactly when someone is about to write
                     one. -->
                <p class="app-internal-notes__marker">
                    <i class="pi pi-eye-slash" aria-hidden="true"></i>
                    {{ 'tickets.internalNotes.notVisibleToCustomer' | transloco }}
                </p>
            </header>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (notes(); as rows) {
                    @if (rows.length === 0) {
                        <app-empty-state
                            [title]="'tickets.internalNotes.emptyTitle' | transloco"
                            [message]="'tickets.internalNotes.emptyMessage' | transloco" />
                    } @else {
                        <ol class="app-internal-notes__list">
                            @for (note of rows; track note.id) {
                                <li class="app-internal-notes__item">
                                    <p class="app-internal-notes__meta">
                                        <span class="app-internal-notes__author">{{ note.author.displayName }}</span>
                                        · <span appLtrEmbed>{{ note.createdAt | appDate: 'short' }}</span>
                                    </p>

                                    <!-- No edit and no delete control: neither exists server-side. -->
                                    <p class="app-internal-notes__body">{{ note.body }}</p>
                                </li>
                            }
                        </ol>
                    }
                } @else {
                    <app-loading-state [rowCount]="2" />
                }
            }

            @if (isTerminal()) {
                <p class="app-internal-notes__closed">
                    {{ 'tickets.internalNotes.closedReason' | transloco }}
                </p>
            } @else {
                <form class="app-internal-notes__composer" (ngSubmit)="submit()">
                    <label class="app-visually-hidden" for="internal-note-body">
                        {{ 'tickets.internalNotes.placeholder' | transloco }}
                    </label>

                    <textarea
                        pTextarea
                        id="internal-note-body"
                        rows="3"
                        [(ngModel)]="draft"
                        [ngModelOptions]="{ standalone: true }"
                        [disabled]="saving()"
                        [placeholder]="'tickets.internalNotes.placeholder' | transloco"></textarea>

                    @if (submitProblem(); as failure) {
                        <app-error-state [problem]="failure" />
                    }

                    <p-button
                        type="submit"
                        [label]="'tickets.internalNotes.add' | transloco"
                        [disabled]="saving() || draft.trim().length === 0"
                        [loading]="saving()" />
                </form>
            }
        </section>
    `,
    styles: `
        /* A different colour block from the thread (UI-5, §5.3) — logical properties only. */
        .app-internal-notes {
            border-inline-start: 3px solid var(--p-amber-500);
            background: var(--p-amber-50);
            padding-inline: 1rem;
            padding-block: 0.75rem;
        }

        :host-context(.app-dark) .app-internal-notes {
            background: color-mix(in srgb, var(--p-amber-500) 12%, transparent);
        }

        .app-internal-notes__header {
            display: flex;
            flex-wrap: wrap;
            gap: 0.5rem;
            align-items: baseline;
            justify-content: space-between;
        }

        .app-internal-notes__marker {
            display: inline-flex;
            gap: 0.35rem;
            align-items: center;
            margin-block: 0;
            font-weight: 600;
            font-size: 0.8125rem;
            color: var(--p-amber-700);
        }

        :host-context(.app-dark) .app-internal-notes__marker {
            color: var(--p-amber-300);
        }

        .app-internal-notes__list {
            margin-block: 0.75rem;
            padding-inline-start: 0;
            list-style: none;
        }

        .app-internal-notes__item + .app-internal-notes__item {
            margin-block-start: 0.75rem;
            border-block-start: 1px solid var(--p-content-border-color);
            padding-block-start: 0.75rem;
        }

        .app-internal-notes__meta {
            margin-block: 0 0.25rem;
            font-size: 0.8125rem;
            color: var(--p-text-muted-color);
        }

        .app-internal-notes__author {
            font-weight: 600;
        }

        .app-internal-notes__body {
            margin-block: 0;
            white-space: pre-wrap;
        }

        .app-internal-notes__composer {
            display: flex;
            flex-direction: column;
            gap: 0.5rem;
            margin-block-start: 0.75rem;
        }

        .app-internal-notes__composer p-button {
            align-self: flex-end;
        }

        .app-internal-notes__closed {
            margin-block: 0.75rem 0;
            font-size: 0.8125rem;
            color: var(--p-text-muted-color);
        }
    `
})
export class InternalNotesRegionComponent {
    private readonly api = inject(TicketsClient);

    readonly ticketId = input.required<string>();

    /**
     * `Closed` and `Cancelled` accept no further notes (§5 constraint 8). The parent passes it
     * rather than this region re-fetching the ticket to learn a fact the parent already has.
     */
    readonly isTerminal = input(false);

    /** Bumped by the parent so the region reloads without owning a subscription to parent state. */
    readonly reloadToken = input(0);

    protected readonly notes = signal<InternalNote[] | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly submitProblem = signal<ApiProblem | null>(null);
    protected readonly saving = signal(false);

    protected draft = '';

    constructor() {
        // An effect rather than ngOnInit, for the reason finding I-18 records: reading a required
        // signal input in a constructor throws NG0950, while an effect first runs after the inputs
        // are set. It also covers the parent's reload bumps.
        effect(() => {
            this.reloadToken();

            this.load();
        });
    }

    protected load(): void {
        this.notes.set(null);
        this.problem.set(null);

        // pageSize 100 is the contract's cap (AP-3), and a ticket's notes are few by nature — the
        // same reasoning the activity region records.
        this.api.internalNotes(this.ticketId(), { pageSize: 100 }).subscribe({
            next: (page) => this.notes.set(page.items),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }

    protected submit(): void {
        const body = this.draft.trim();

        if (body.length === 0 || this.saving()) {
            return;
        }

        this.saving.set(true);
        this.submitProblem.set(null);

        this.api.postInternalNote(this.ticketId(), body).subscribe({
            next: (created) => {
                // Appended locally rather than re-fetching: the response IS the created note, and a
                // reload would cost a round trip to learn what we were just told. Oldest-first, so
                // the new note goes last.
                this.notes.update((rows) => [...(rows ?? []), created]);
                this.draft = '';
                this.saving.set(false);
            },
            error: (failure: ApiProblem) => {
                this.submitProblem.set(failure);
                this.saving.set(false);
            }
        });
    }
}
