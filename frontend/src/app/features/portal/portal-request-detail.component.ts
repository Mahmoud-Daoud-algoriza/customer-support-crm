import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { MessageModule } from 'primeng/message';
import { ApiProblem } from '../../core/api/api-problem';
import { PortalClient, PortalMessage } from '../../core/api/portal.client';
import { TicketStatus } from '../../core/api/tickets.client';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { MessageThreadComponent } from '../../shared/components/message-thread/message-thread.component';
import { ReplyComposerComponent } from '../../shared/components/reply-composer/reply-composer.component';
import { StatusChipComponent } from '../../shared/components/status-chip/status-chip.component';

/**
 * Request detail — `/portal/requests/:id` (docs/ui-design.md §7.3).
 *
 * <h3>A Story 07 stub, and Story 13 replaces it</h3>
 * Story 07 owns the **two endpoints**; Story 13 owns the designed screen, the cancel and reopen
 * controls, the attachments and the feedback control. This exists so the thread and the reply are
 * exercised end to end now.
 *
 * <h3>The one status side effect, shown in place</h3>
 * **R-13.** Replying to a `Pending` request returns it to `Open` automatically, and the response
 * carries `statusChanged` and `ticketStatus` — **so the chip updates from the reply's own answer
 * rather than from a re-fetch or a guess** (§6.4, §7.3). The *"reopened"* cue below is driven by
 * exactly that flag.
 *
 * <h3>No manual reopen for a `Pending` request</h3>
 * §7.3 is explicit: **the UI must not offer one.** A-16 gives a customer no direct `Pending → Open`
 * — which is precisely why the reply does it automatically — so there is no such control here, and
 * Story 13 must not add one either. Reopen belongs to a `Resolved` request, and is Story 13's.
 *
 * <h3>This is not chat</h3>
 * **T3-B.** One `GET` on load, one `POST` per reply. No interval, no socket, no presence, and no
 * wording anywhere that calls it real-time.
 *
 * <h3>The portal configuration of the shared thread</h3>
 * `showChannel` is left at its default `false`, so the customer sees neither the channel nor the
 * author's role (§6.4, UI-11) — and **internal notes are unreachable**: they come from an endpoint
 * `PortalClient` has no method for (T2-C, AP-5).
 */
@Component({
    selector: 'app-portal-request-detail',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ErrorStateComponent, LoadingStateComponent, MessageModule, MessageThreadComponent, ReplyComposerComponent, StatusChipComponent, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'portal.requestTitle' | transloco }}</h1>

                @if (status(); as current) {
                    <app-status-chip [status]="current" />
                }
            </header>

            <!-- The "reopened" cue, driven by statusChanged (§7.3). It is shown only when the
                 automatic transition actually fired — not whenever the status happens to be Open. -->
            @if (reopened()) {
                <p-message severity="info" [text]="'portal.reopened' | transloco" />
            }

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (messages(); as rows) {
                    <app-message-thread [messages]="rows" />
                } @else {
                    <app-loading-state [rowCount]="3" />
                }
            }

            <app-reply-composer [busy]="sending()" [problem]="sendProblem()" (send)="reply($event)" />
        </section>
    `
})
export class PortalRequestDetailComponent {
    private readonly api = inject(PortalClient);
    private readonly route = inject(ActivatedRoute);

    private readonly composer = viewChild(ReplyComposerComponent);

    private readonly ticketId = this.route.snapshot.paramMap.get('id') ?? '';

    protected readonly messages = signal<PortalMessage[] | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly sending = signal(false);
    protected readonly sendProblem = signal<ApiProblem | null>(null);

    /**
     * The status as the **reply's own response** reported it. Null until a reply lands, because
     * `GET /portal/tickets/{id}` is Story 13's — this stub reads the thread, not the request.
     */
    protected readonly status = signal<TicketStatus | null>(null);

    /** True only when R-13's automatic `Pending → Open` fired on the last reply. */
    protected readonly reopened = signal(false);

    constructor() {
        this.load();
    }

    protected load(): void {
        this.messages.set(null);
        this.problem.set(null);

        this.api.messages(this.ticketId, { pageSize: 100 }).subscribe({
            next: (page) => this.messages.set(page.items),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }

    protected reply(body: string): void {
        if (this.sending()) {
            return;
        }

        this.sending.set(true);
        this.sendProblem.set(null);
        this.reopened.set(false);

        this.api.postMessage(this.ticketId, body).subscribe({
            next: (posted) => {
                this.sending.set(false);

                // Cleared only now: a refusal leaves the customer's words where they wrote them.
                this.composer()?.clear();

                this.messages.update((rows) => [...(rows ?? []), posted.message]);

                // Straight from the envelope — no re-fetch, and no inference from the status alone.
                this.status.set(posted.ticketStatus);
                this.reopened.set(posted.statusChanged);
            },
            error: (failure: ApiProblem) => {
                this.sending.set(false);
                this.sendProblem.set(failure);
            }
        });
    }
}
