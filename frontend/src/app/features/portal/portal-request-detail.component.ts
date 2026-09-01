import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { ApiProblem, problemTranslationKey } from '../../core/api/api-problem';
import { AttachmentMetadata } from '../../core/api/attachments.client';
import { CustomerConfig, PlatformApiService } from '../../core/api/platform-api.service';
import { PortalClient, PortalMessage, PortalTicket, PortalTransitionTarget } from '../../core/api/portal.client';
import { AttachmentListComponent } from '../../shared/components/attachment-list/attachment-list.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { MessageThreadComponent } from '../../shared/components/message-thread/message-thread.component';
import { RatingInputComponent } from '../../shared/components/rating-input/rating-input.component';
import { ReplyComposerComponent } from '../../shared/components/reply-composer/reply-composer.component';
import { StatusChipComponent } from '../../shared/components/status-chip/status-chip.component';

/**
 * Request detail — `/portal/requests/:id` (docs/ui-design.md §7.3). **Replaces Story 07's stub.**
 *
 * Five regions, exactly the ones §7.3 names: header, thread, reply composer, attachments and the
 * feedback block.
 *
 * <h3>The one status side effect, shown in place</h3>
 * **R-13.** Replying to a `Pending` request returns it to `Open` automatically, and the reply's own
 * response carries `statusChanged` and `ticketStatus` — **so the chip updates from that answer
 * rather than from a re-fetch or a guess** (§6.4, §7.3). The *"reopened"* cue is driven by exactly
 * that flag, and by nothing else: it is not shown whenever the status happens to be `Open`.
 *
 * <h3>No manual reopen for a `Pending` request</h3>
 * §7.3 is explicit: **the UI must not offer one.** A-16 gives a customer no direct `Pending → Open`
 * — which is precisely why the reply does it automatically — so `Reopen` is rendered on `Resolved`
 * and on nothing else. `PortalTransitionTarget` narrows the client to the two targets A-16 allows,
 * so the mistake is not even spellable here.
 *
 * <h3>Cancel closes with the window, and says why</h3>
 * §7.3, A-16: cancel is offered **only while the status is `New`** — a window **A-18** keeps
 * genuinely open, because a request that has already been auto-assigned is still `New`. Once `Open`
 * the control **disappears with a line explaining that work has started**, rather than sitting there
 * disabled with no explanation. The confirmation names the effect (**UI-12**).
 *
 * <h3>No staff vocabulary</h3>
 * **UI-11, AP-16.** No department, no priority, no assignee, no SLA — and `PortalTicket` carries
 * none of them, so this screen could not render one. **This file must not import `PriorityChip`,
 * `SlaIndicator` or the customer panel**; they are staff components by ui-design §8.
 *
 * <h3>Internal notes are unreachable</h3>
 * **T2-C, AP-5, UI-5.** Not filtered out — they come from an endpoint `PortalClient` has no method
 * for. There is no merged list here to remember to narrow.
 *
 * <h3>This is not chat</h3>
 * **T3-B.** One `GET` per region on load, one `POST` per action. No interval, no socket, no presence,
 * and no wording anywhere that calls it real-time.
 */
@Component({
    selector: 'app-portal-request-detail',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        AttachmentListComponent, ButtonModule, ConfirmDialogModule, DatePipe, ErrorStateComponent,
        FormsModule, LoadingStateComponent, MessageModule, MessageThreadComponent,
        RatingInputComponent, ReplyComposerComponent, RouterLink, StatusChipComponent,
        TextareaModule, TranslocoModule,
    ],
    providers: [ConfirmationService],
    template: `
        <section class="app-page">
            <p-confirmDialog />

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else if (request()) {
                @let current = request()!;

                <!-- ------------------------------------------------------------- Header -->
                <header class="app-page__header">
                    <h1 class="app-page__title">{{ current.subject }}</h1>
                    <app-status-chip [status]="current.status" />
                </header>

                <p class="app-page__meta app-ltr-numeric">
                    {{ 'portal.requests.submitted' | transloco }} {{ current.createdAt | date: 'medium' }}
                </p>

                <p class="app-request-detail__description">{{ current.description }}</p>

                <!-- The "reopened" cue, driven by statusChanged (§7.3). Shown only when the
                     automatic transition actually fired — not whenever the status is Open. -->
                @if (reopened()) {
                    <p-message severity="info" [text]="'portal.reopened' | transloco" />
                }

                <!-- ------------------------------------------------- Lifecycle controls -->
                <div class="app-request-actions">
                    @if (current.status === 'New') {
                        <!-- A-16 + A-18: still cancellable, even if an agent has been assigned. -->
                        <p-button
                            severity="secondary"
                            icon="pi pi-times"
                            [label]="'portal.detail.cancel' | transloco"
                            [loading]="transitioning()"
                            [disabled]="transitioning()"
                            (onClick)="confirmCancel()" />
                    } @else if (current.status === 'Open' || current.status === 'Pending') {
                        <!-- §7.3: the control disappears WITH A LINE, not silently. -->
                        <p class="app-page__meta">{{ 'portal.detail.cancelClosed' | transloco }}</p>
                    }

                    @if (current.status === 'Resolved') {
                        <p-button
                            severity="secondary"
                            icon="pi pi-refresh"
                            [label]="'portal.detail.reopen' | transloco"
                            [loading]="transitioning()"
                            [disabled]="transitioning()"
                            (onClick)="transition('Open')" />
                    }

                    @if (readOnly()) {
                        <p class="app-page__meta">
                            {{ 'portal.detail.readOnly' | transloco: { status: statusLabel(current.status) } }}
                        </p>
                    }
                </div>

                @if (transitionProblem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                <!-- ------------------------------------------------------------- Thread -->
                <section class="app-region">
                    <h2 class="app-region__title">{{ 'portal.detail.conversation' | transloco }}</h2>

                    @if (threadProblem(); as failure) {
                        <app-error-state [problem]="failure" (retry)="loadThread()" />
                    } @else {
                        @if (messages(); as rows) {
                            <!-- The PORTAL configuration: showChannel stays at its default false,
                                 so neither the channel nor the author's role is rendered
                                 (§6.4, UI-11). -->
                            <app-message-thread [messages]="rows" />
                        } @else {
                            <app-loading-state [rowCount]="3" />
                        }
                    }

                    <!-- The portal configuration of the composer: NO quick replies and NO AI
                         insert (UI-7). quickReplies is left false, which also keeps a Customer
                         from firing the Agent-only GET /config/staff (AP-17). -->
                    <app-reply-composer
                        [busy]="sending()"
                        [problem]="sendProblem()"
                        [disabledReasonKey]="composerDisabledKey()"
                        placeholderKey="portal.detail.replyPlaceholder"
                        (send)="reply($event)" />
                </section>

                <!-- -------------------------------------------------------- Attachments -->
                <section class="app-region">
                    <h2 class="app-region__title">{{ 'portal.detail.attachments' | transloco }}</h2>

                    @if (attachmentsProblem(); as failure) {
                        <app-error-state [problem]="failure" (retry)="loadAttachments()" />
                    } @else {
                        @if (attachments(); as files) {
                            <!-- Upload is offered here rather than on the form (§7.2): a file needs
                                 a request to belong to. It closes with the request itself. -->
                            <app-attachment-list
                                [attachments]="files"
                                [canUpload]="!readOnly()"
                                [uploading]="uploading()"
                                [uploadProblem]="uploadProblem()"
                                (upload)="upload($event)" />
                        } @else {
                            <app-loading-state [rowCount]="2" />
                        }
                    }
                </section>

                <!-- ----------------------------------------------------------- Feedback -->
                @if (feedbackOffered()) {
                    <section class="app-region app-feedback">
                        <h2 class="app-region__title">{{ 'portal.feedback.title' | transloco }}</h2>

                        @if (feedbackSubmitted()) {
                            <p class="app-page__meta">{{ 'portal.feedback.thanks' | transloco }}</p>
                        } @else {
                            @if (scale(); as range) {
                                <!-- ⚠ OQ-1. Rendered FROM the configured range — no star widget,
                                     no 1..5 array, no thumbs pair anywhere in this file. -->
                                <app-rating-input
                                    [min]="range.min"
                                    [max]="range.max"
                                    [value]="rating()"
                                    [disabled]="ratingBusy()"
                                    (valueChange)="rating.set($event)" />

                                <label class="app-form__field">
                                    <span>{{ 'portal.feedback.commentLabel' | transloco }}</span>
                                    <textarea
                                        pTextarea
                                        rows="3"
                                        name="feedbackComment"
                                        [disabled]="ratingBusy()"
                                        [(ngModel)]="comment"></textarea>
                                </label>

                                @if (ratingProblem(); as failure) {
                                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                                }

                                <div class="app-form__actions">
                                    <p-button
                                        [label]="'portal.feedback.submit' | transloco"
                                        [loading]="ratingBusy()"
                                        [disabled]="ratingBusy() || rating() === null"
                                        (onClick)="submitFeedback()" />
                                </div>
                            } @else {
                                <app-loading-state [rowCount]="1" />
                            }
                        }
                    </section>
                }

                <p><a routerLink="/portal/requests">{{ 'portal.detail.backToRequests' | transloco }}</a></p>
            } @else {
                <app-loading-state [rowCount]="5" />
            }
        </section>
    `
})
export class PortalRequestDetailComponent {
    private readonly api = inject(PortalClient);
    private readonly platform = inject(PlatformApiService);
    private readonly route = inject(ActivatedRoute);
    private readonly confirmation = inject(ConfirmationService);
    private readonly transloco = inject(TranslocoService);

    private readonly composer = viewChild(ReplyComposerComponent);

    private readonly ticketId = this.route.snapshot.paramMap.get('id') ?? '';

    protected readonly request = signal<PortalTicket | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected readonly messages = signal<PortalMessage[] | null>(null);
    protected readonly threadProblem = signal<ApiProblem | null>(null);

    protected readonly attachments = signal<AttachmentMetadata[] | null>(null);
    protected readonly attachmentsProblem = signal<ApiProblem | null>(null);
    protected readonly uploading = signal(false);
    protected readonly uploadProblem = signal<ApiProblem | null>(null);

    protected readonly sending = signal(false);
    protected readonly sendProblem = signal<ApiProblem | null>(null);

    protected readonly transitioning = signal(false);
    protected readonly transitionProblem = signal<ApiProblem | null>(null);

    /** True only when R-13's automatic `Pending → Open` fired on the last reply. */
    protected readonly reopened = signal(false);

    /**
     * The configured rating scale — `feedback.ratingScale` from `GET /config` (**OQ-1**). Null until
     * it arrives, and **the control is not rendered until then**: guessing a range while the real one
     * is in flight is exactly the hardcoding ui-design §11 forbids.
     */
    protected readonly scale = signal<CustomerConfig['feedback']['ratingScale'] | null>(null);

    protected readonly rating = signal<number | null>(null);
    protected readonly ratingBusy = signal(false);
    protected readonly ratingProblem = signal<ApiProblem | null>(null);

    /** Set once this session's submission succeeds, so the block thanks rather than re-prompts. */
    protected readonly feedbackSubmitted = signal(false);

    protected comment = '';

    protected errorKey = problemTranslationKey;

    /**
     * `Closed` and `Cancelled` requests are **read-only, with a line saying so** (§7.3). It is the
     * same pair A-5 makes terminal — no outgoing edge, and no reply either (`409 ticket-terminal`).
     */
    protected readonly readOnly = computed(() => {
        const status = this.request()?.status;

        return status === 'Closed' || status === 'Cancelled';
    });

    /**
     * **Feedback appears when the request has reached `Resolved` and has not been rated** (§7.3,
     * T2-F).
     *
     * *Reached*, not *is* — which is why this reads `resolvedAt` rather than the status. It is the
     * same signal the server's precondition uses: `resolvedAt` is stamped on the way to `Resolved`
     * and never cleared, so a request that was resolved and then reopened, or resolved and then
     * closed, is still ratable. A status check would disagree with the endpoint in both directions.
     *
     * **Declining is normal** (§7.3): there is no nag, no re-prompt and no blocking anywhere — the
     * block is simply present, and a customer who ignores it has answered.
     */
    protected readonly feedbackOffered = computed(() => {
        const current = this.request();

        return current !== null && !!current.resolvedAt && (!current.hasFeedback || this.feedbackSubmitted());
    });

    /**
     * The composer's reason line when replying is unavailable. Terminal requests accept no message
     * (A-5, `409 ticket-terminal`), so the box is replaced by the sentence rather than left to fail
     * on send.
     */
    protected readonly composerDisabledKey = computed(() =>
        this.readOnly() ? 'portal.detail.replyClosed' : null
    );

    constructor() {
        this.load();

        // The scale is fetched once and cached for the session by PlatformApiService — it is
        // configuration, and configuration changes only by redeploy (T2-I).
        this.platform.getCustomerConfig().subscribe((config) => this.scale.set(config.feedback.ratingScale));
    }

    protected statusLabel(status: string): string {
        return this.transloco.translate(`tickets.status.${status}`);
    }

    protected load(): void {
        this.request.set(null);
        this.problem.set(null);

        this.api.getRequest(this.ticketId).subscribe({
            next: (ticket) => this.request.set(ticket),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });

        // The three regions load independently, so one slow or failed call never blanks the screen
        // (docs/ui-design.md §9) — the same arrangement the staff customer detail uses.
        this.loadThread();
        this.loadAttachments();
    }

    protected loadThread(): void {
        this.messages.set(null);
        this.threadProblem.set(null);

        this.api.messages(this.ticketId, { pageSize: 100 }).subscribe({
            next: (page) => this.messages.set(page.items),
            error: (failure: ApiProblem) => this.threadProblem.set(failure)
        });
    }

    protected loadAttachments(): void {
        this.attachments.set(null);
        this.attachmentsProblem.set(null);

        this.api.attachments(this.ticketId).subscribe({
            next: (page) => this.attachments.set(page.items),
            error: (failure: ApiProblem) => this.attachmentsProblem.set(failure)
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

                // **Straight from the envelope — no re-fetch and no inference from the status.**
                // This is the whole reason §6.4 puts `ticketStatus` and `statusChanged` on the reply
                // response, and why §7.3 can say the chip updates "in place".
                this.request.update((current) =>
                    current === null ? current : { ...current, status: posted.ticketStatus });

                this.reopened.set(posted.statusChanged);
            },
            error: (failure: ApiProblem) => {
                this.sending.set(false);
                this.sendProblem.set(failure);
            }
        });
    }

    /**
     * **UI-12: the confirmation names the effect.** Cancelling is not reversible — `Cancelled` is
     * terminal in A-5 — so the dialog says what will happen rather than asking "are you sure?".
     */
    protected confirmCancel(): void {
        this.confirmation.confirm({
            header: this.transloco.translate('portal.detail.cancel'),
            message: this.transloco.translate('portal.detail.cancelConfirm'),
            acceptLabel: this.transloco.translate('primeng.accept'),
            rejectLabel: this.transloco.translate('primeng.reject'),
            accept: () => this.transition('Cancelled')
        });
    }

    /**
     * **No optimistic UI (UI-8).** The screen sends and waits, then replaces the request with the
     * server's answer — a transition can be refused, and a chip that had already moved would be
     * lying.
     */
    protected transition(target: PortalTransitionTarget): void {
        if (this.transitioning()) {
            return;
        }

        this.transitioning.set(true);
        this.transitionProblem.set(null);
        this.reopened.set(false);

        this.api.transition(this.ticketId, target).subscribe({
            next: (ticket) => {
                this.transitioning.set(false);
                this.request.set(ticket);
            },
            error: (failure: ApiProblem) => {
                this.transitioning.set(false);
                this.transitionProblem.set(failure);
            }
        });
    }

    /** `413 attachment-too-large` comes back here and renders inline on the uploader (§9, T2-A). */
    protected upload(file: File): void {
        if (this.uploading()) {
            return;
        }

        this.uploading.set(true);
        this.uploadProblem.set(null);

        this.api.uploadAttachment(this.ticketId, file).subscribe({
            next: (created) => {
                this.uploading.set(false);
                this.attachments.update((files) => [created, ...(files ?? [])]);
            },
            error: (failure: ApiProblem) => {
                this.uploading.set(false);
                this.uploadProblem.set(failure);
            }
        });
    }

    /**
     * **Write-once, and the UI does not pretend otherwise.** A second submission is
     * `409 feedback-already-submitted`; on success the block switches to a thank-you and offers no
     * way to change the answer, because the server has none.
     *
     * **⚠ No range is checked here (OQ-1).** The control cannot produce a value outside the
     * configured range because it renders from it, and the server validates independently — adding a
     * client-side bound would be a second, quieter copy of a number nobody has decided.
     */
    protected submitFeedback(): void {
        const rating = this.rating();

        if (this.ratingBusy() || rating === null) {
            return;
        }

        this.ratingBusy.set(true);
        this.ratingProblem.set(null);

        this.api.submitFeedback(this.ticketId, rating, this.comment.trim() || null).subscribe({
            next: () => {
                this.ratingBusy.set(false);
                this.feedbackSubmitted.set(true);

                // `hasFeedback` is a server projection; reflecting it here keeps the offer from
                // re-appearing without a second read of a request that has not otherwise changed.
                this.request.update((current) =>
                    current === null ? current : { ...current, hasFeedback: true });
            },
            error: (failure: ApiProblem) => {
                this.ratingBusy.set(false);
                this.ratingProblem.set(failure);
            }
        });
    }
}
