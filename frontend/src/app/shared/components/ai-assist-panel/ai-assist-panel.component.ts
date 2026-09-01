import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { AiClient } from '../../../core/api/ai.client';
import { ApiProblem } from '../../../core/api/api-problem';

/**
 * `AiAssistPanel` — the AI region of the ticket detail (docs/ui-design.md §5.3, **UI-6**). It fills
 * the slot Story 05 left.
 *
 * <h3>Two actions, and a result that says what it is</h3>
 * `Summarize` and `Suggest reply`. **Every result renders inside the panel with an always-visible
 * "AI-generated" label** — not a tooltip, not a hover state, not a subtle icon (A-8, UI-6). The label
 * is rendered from the response's own `generatedBy` field, so it cannot drift from what the server
 * actually produced.
 *
 * <h3>No inline ghost text, no auto-fill</h3>
 * **UI-6:** a suggestion never flows straight into a field, *"because that would blur authorship."*
 * The draft sits in this panel until the agent presses **Insert into reply** — an explicit human
 * action, which is A-8's requirement rather than a UX preference. The summary gets a **dismiss**,
 * because a summary is read and finished with.
 *
 * <h3>Insertion goes through the one composer</h3>
 * **UI-7.** This component does not touch the composer: it emits {@link insertDraft} and the ticket
 * detail hands the text to the same `insert()` Story 08's quick replies use. **One draft, one
 * insertion point, one send action** — which is what keeps *"never auto-sent"* true by construction
 * rather than by discipline. There is no code path from here to a send.
 *
 * <h3>`503` degrades this panel and nothing else</h3>
 * On `ai-unavailable` the panel says so and its two buttons stay usable for a retry. **Every other
 * control on the screen keeps working** — the thread, the composer, the transition menu, assignment —
 * because this component owns no state any of them read. That is the visible half of T1-F.
 *
 * <h3>Staff only</h3>
 * **A-8 excludes customer-facing generation entirely.** This component is never imported by a
 * `features/portal/` component, and `GET`/`POST` on `/ai` answers a Customer `403` regardless.
 */
@Component({
    selector: 'app-ai-assist-panel',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, MessageModule, TranslocoModule],
    template: `
        <aside class="app-ai-panel">
            <h2 class="app-region__title">{{ 'ai.title' | transloco }}</h2>

            <div class="app-ai-panel__actions">
                <p-button
                    severity="secondary"
                    [outlined]="true"
                    icon="pi pi-sparkles"
                    [label]="'ai.summarize' | transloco"
                    [loading]="busy() === 'summary'"
                    [disabled]="busy() !== null"
                    (onClick)="summarize()"
                />

                <p-button
                    severity="secondary"
                    [outlined]="true"
                    icon="pi pi-comment"
                    [label]="'ai.suggestReply' | transloco"
                    [loading]="busy() === 'reply'"
                    [disabled]="busy() !== null"
                    (onClick)="suggestReply()"
                />
            </div>

            <!-- The 503 state. It replaces the results, never the buttons: the agent can retry, and
                 nothing else on the screen is affected (T1-F, §5.3). -->
            @if (unavailable()) {
                <p-message severity="warn" [text]="'ai.unavailable' | transloco" />
            }

            @if (summary(); as result) {
                <section class="app-ai-result">
                    <!-- **Always visible, never a tooltip** (A-8, UI-6). Rendered from the response's
                         own generatedBy field. -->
                    <p class="app-ai-result__label">{{ 'ai.generatedLabel' | transloco }}</p>

                    <p class="app-ai-result__body">{{ result.summary }}</p>

                    <div class="app-ai-result__actions">
                        <p-button
                            severity="secondary"
                            [text]="true"
                            [label]="'ai.dismiss' | transloco"
                            (onClick)="summary.set(null)"
                        />
                    </div>
                </section>
            }

            @if (draft(); as result) {
                <section class="app-ai-result">
                    <p class="app-ai-result__label">{{ 'ai.generatedLabel' | transloco }}</p>

                    <p class="app-ai-result__body">{{ result.draft }}</p>

                    <div class="app-ai-result__actions">
                        <!-- The explicit human action A-8 requires. It inserts; it does not send. -->
                        <p-button
                            [label]="'ai.insertIntoReply' | transloco"
                            icon="pi pi-arrow-down-left"
                            (onClick)="insert(result.draft)"
                        />

                        <p-button
                            severity="secondary"
                            [text]="true"
                            [label]="'ai.dismiss' | transloco"
                            (onClick)="draft.set(null)"
                        />
                    </div>
                </section>
            }

            <!-- The suggested-articles region sits BELOW this panel, as its own component
                 (Story 12). It is a KNOWLEDGE endpoint, not an AI one (AP-14) — retrieval, not
                 generation — so it is not part of this panel and must never be folded into it. -->
        </aside>
    `
})
export class AiAssistPanelComponent {
    private readonly api = inject(AiClient);

    readonly ticketId = input.required<string>();

    /**
     * The draft the agent chose to insert. **The parent routes it to the composer's one insertion
     * point** (UI-7) — this component never reaches into the composer itself, so there is exactly one
     * place text can enter a reply.
     */
    readonly insertDraft = output<string>();

    protected readonly summary = signal<{ summary: string } | null>(null);
    protected readonly draft = signal<{ draft: string } | null>(null);
    protected readonly unavailable = signal(false);

    /** Which call is in flight, so the two buttons can disable together without a second flag. */
    protected readonly busy = signal<'summary' | 'reply' | null>(null);

    protected summarize(): void {
        this.busy.set('summary');
        this.unavailable.set(false);

        this.api.summarize(this.ticketId()).subscribe({
            next: (result) => {
                this.summary.set(result);
                this.busy.set(null);
            },
            error: (failure: ApiProblem) => this.fail(failure)
        });
    }

    protected suggestReply(): void {
        this.busy.set('reply');
        this.unavailable.set(false);

        this.api.suggestReply(this.ticketId()).subscribe({
            next: (result) => {
                this.draft.set(result);
                this.busy.set(null);
            },
            error: (failure: ApiProblem) => this.fail(failure)
        });
    }

    /**
     * Emits the draft for the parent to insert. **The panel keeps showing the suggestion** rather than
     * clearing it: the agent may want to see what was suggested next to what they are now writing, and
     * clearing on insert would make the label disappear at the moment authorship matters most.
     */
    protected insert(text: string): void {
        this.insertDraft.emit(text);
    }

    /**
     * Every failure reads as "AI unavailable" for this panel. **The buttons stay enabled** so a retry
     * costs one click, and no error state is raised that could cover the rest of the screen — the
     * thread and the composer are the features, and this is an aid to them.
     */
    private fail(failure: ApiProblem): void {
        this.busy.set(null);
        this.unavailable.set(true);

        // The `503 ai-unavailable` slug is the expected case (AP-12); anything else — a `404` on a
        // ticket that moved out of scope, a network failure — reads the same way here, because the
        // agent's next action is identical and the panel is not where a ticket error belongs.
        void failure;
    }
}
