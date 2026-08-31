import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';

/**
 * `ReplyComposer` — **one component**, used by the staff ticket detail and the portal request
 * detail (docs/ui-design.md §8, **UI-7**).
 *
 * <h3>One draft, one insertion point</h3>
 * Story 07 delivers plain text. **Story 08 adds the quick-reply insert and Story 11 the AI *Insert
 * into reply* — into this same draft**, through {@link insert}. That single insertion point is what
 * keeps A-8's *"never auto-sent"* true **by construction** rather than by policy: there is one place
 * text can arrive from, and it is the same place the user types.
 *
 * <h3>Nothing sends itself</h3>
 * The component emits {@link send} only from the button. No timer, no autosave, no auto-send, and —
 * **T3-B** — no polling and nothing described as chat.
 *
 * <h3>The draft survives a refusal</h3>
 * On error the text stays in the box. A composer that clears a rejected reply loses the user's
 * words for them, and a `409 ticket-terminal` is exactly the case where they would want to copy it
 * elsewhere.
 *
 * <h3>Disabled with a reason, never silently inert</h3>
 * `Closed` and `Cancelled` disable the composer **with a line saying why** (docs/ui-design.md §5.3),
 * which the parent supplies through {@link disabledReasonKey}. **The guard hides; it does not
 * protect** — the server independently answers `409` whatever this renders.
 */
@Component({
    selector: 'app-reply-composer',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, FormsModule, MessageModule, TextareaModule, TranslocoModule],
    template: `
        <section class="app-composer">
            @if (disabledReasonKey(); as reason) {
                <p class="app-page__meta">{{ reason | transloco }}</p>
            } @else {
                <label class="app-composer__label" [attr.for]="'reply-body'">
                    {{ 'tickets.replyLabel' | transloco }}
                </label>

                <textarea
                    id="reply-body"
                    pTextarea
                    rows="4"
                    class="app-composer__input"
                    [placeholder]="'tickets.replyPlaceholder' | transloco"
                    [disabled]="busy()"
                    [ngModel]="draft()"
                    (ngModelChange)="draft.set($event)"></textarea>

                @if (problem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                <div class="app-composer__actions">
                    <!-- Story 08 inserts a quick reply here, and Story 11 the AI draft. Both go
                         through insert(), into this same draft — never straight to send(). -->
                    <p-button
                        [label]="'tickets.reply' | transloco"
                        icon="pi pi-send"
                        [loading]="busy()"
                        [disabled]="busy() || !canSend()"
                        (onClick)="submit()" />
                </div>
            }
        </section>
    `
})
export class ReplyComposerComponent {
    /** A send is in flight. The button and the box both disable; the text stays put. */
    readonly busy = input(false);

    /**
     * Present means the composer is unavailable, and this is the **translation key** of the sentence
     * that says why. A key rather than prose, because every other user-facing string in this
     * codebase is translated at the point of rendering (T2-J) — and the parent is the one that knows
     * the status, so it chooses the key.
     */
    readonly disabledReasonKey = input<string | null>(null);

    /**
     * A refused send, rendered from the problem **`type`** (docs/ui-design.md §9). The server's
     * `detail` is never shown raw (T2-J).
     */
    readonly problem = input<ApiProblem | null>(null);

    readonly send = output<string>();

    protected readonly draft = signal('');

    /** Whitespace is not a reply. The server refuses it too, with a `400`. */
    protected readonly canSend = computed(() => this.draft().trim().length > 0);

    protected errorKey = problemTranslationKey;

    /**
     * **The one insertion point** (UI-7, A-8). Story 08's quick replies and Story 11's AI draft both
     * land here, appended to whatever the user has already written rather than replacing it — a
     * suggestion must not silently delete their words. Nothing is sent as a result.
     */
    insert(text: string): void {
        const current = this.draft();

        this.draft.set(current.length === 0 ? text : `${current}\n\n${text}`);
    }

    /** Called by the parent once the server has accepted the reply. */
    clear(): void {
        this.draft.set('');
    }

    protected submit(): void {
        if (!this.canSend()) {
            return;
        }

        // The draft is deliberately NOT cleared here: the parent clears it on success, so a refusal
        // leaves the user's words where they wrote them.
        this.send.emit(this.draft().trim());
    }
}
