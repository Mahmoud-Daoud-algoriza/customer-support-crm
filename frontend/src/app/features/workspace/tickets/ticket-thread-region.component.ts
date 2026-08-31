import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { ApiProblem } from '../../../core/api/api-problem';
import { TicketMessage, TicketStatus, TicketsClient } from '../../../core/api/tickets.client';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { MessageThreadComponent } from '../../../shared/components/message-thread/message-thread.component';
import { ReplyComposerComponent } from '../../../shared/components/reply-composer/reply-composer.component';
import { isTerminal } from '../../../shared/lifecycle/transition-matrix';

/**
 * The **thread region** of the ticket detail screen — docs/ui-design.md §5.3, Story 07 task 9. It
 * fills the slot Story 05 left in `TicketDetailComponent`.
 *
 * <h3>It loads independently</h3>
 * Its own loading, empty and error states (§9), so a slow thread never blanks the screen and a
 * failed thread never hides the header — the same contract the activity region already keeps.
 *
 * <h3>The staff configuration of the shared thread</h3>
 * `showChannel` is `true` here, so an agent sees the channel each message arrived on and the
 * author's role. The portal configuration of the **same component** shows neither (§6.4, UI-11).
 *
 * <h3>Terminal tickets say why the composer is gone</h3>
 * `Closed` and `Cancelled` disable it **with a reason line** rather than leaving an inert box
 * (§5.3). **The guard hides; it does not protect** — the server answers `409 ticket-terminal`
 * whatever this renders, and a refusal surfaces inline on the composer.
 *
 * <h3>Nothing polls</h3>
 * **T3-B.** The thread reloads when this component is asked to and after a reply the user sent.
 * There is no interval and no subscription, and nothing here is chat.
 */
@Component({
    selector: 'app-ticket-thread-region',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ErrorStateComponent, LoadingStateComponent, MessageThreadComponent, ReplyComposerComponent, TranslocoModule],
    template: `
        <section class="app-region">
            <h2 class="app-region__title">{{ 'tickets.thread' | transloco }}</h2>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (messages(); as rows) {
                    <app-message-thread [messages]="rows" [showChannel]="true" />
                } @else {
                    <app-loading-state [rowCount]="3" />
                }
            }

            <app-reply-composer
                [busy]="sending()"
                [problem]="sendProblem()"
                [disabledReasonKey]="composerDisabledKey()"
                (send)="reply($event)" />
        </section>
    `
})
export class TicketThreadRegionComponent {
    private readonly api = inject(TicketsClient);

    private readonly composer = viewChild(ReplyComposerComponent);

    readonly ticketId = input.required<string>();

    readonly status = input.required<TicketStatus>();

    /** Bumped by the parent when something outside this region should refresh the thread. */
    readonly reloadToken = input(0);

    /**
     * Emitted after a reply the server accepted, so the parent can refresh the header — the first
     * outbound message sets `firstRespondedAt` — and the activity trail, which gained a
     * `MessagePosted` row.
     */
    readonly replied = output<TicketMessage>();

    protected readonly messages = signal<TicketMessage[] | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly sending = signal(false);
    protected readonly sendProblem = signal<ApiProblem | null>(null);

    constructor() {
        // An effect, not `ngOnInit`: it covers the first load and every parent-requested reload at
        // once, and reading a required signal input is safe here because an effect first runs after
        // change detection has set the inputs (finding I-18).
        effect(() => {
            this.reloadToken();

            this.load();
        });
    }

    /**
     * The translation key of the reason the composer is unavailable, or null when it is available.
     *
     * <p>Terminal is the only case: A-5 refuses a message on a `Closed` or `Cancelled` ticket, and
     * every other status accepts one.</p>
     */
    protected composerDisabledKey(): string | null {
        // Deliberately a plain method rather than a computed: it reads one input and returns a key
        // the template translates, so there is nothing to memoize.
        return isTerminal(this.status()) ? 'tickets.terminalNoReply' : null;
    }

    protected load(): void {
        this.messages.set(null);
        this.problem.set(null);

        // pageSize 100 is the contract's cap (AP-3). A ticket's thread is short by nature, and
        // paging controls here would be furniture for a case this scope does not produce.
        this.api.messages(this.ticketId(), { pageSize: 100 }).subscribe({
            next: (page) => this.messages.set(page.items),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }

    /**
     * **No optimistic UI (UI-8).** The message appears only after the server confirms it, because a
     * reply can be refused — a `409 ticket-terminal` on a ticket someone else just closed is the
     * ordinary case, and it renders inline on the composer from the problem `type` (§9).
     */
    protected reply(body: string): void {
        if (this.sending()) {
            return;
        }

        this.sending.set(true);
        this.sendProblem.set(null);

        this.api.postMessage(this.ticketId(), body).subscribe({
            next: (message) => {
                this.sending.set(false);

                // Cleared only now: a refusal leaves the user's words where they wrote them.
                this.composer()?.clear();

                this.messages.update((rows) => [...(rows ?? []), message]);

                this.replied.emit(message);
            },
            error: (failure: ApiProblem) => {
                this.sending.set(false);
                this.sendProblem.set(failure);
            }
        });
    }
}
