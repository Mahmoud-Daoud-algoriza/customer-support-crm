import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { AiClassification, AiClient } from '../../../core/api/ai.client';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { CustomerListItem, CustomersClient } from '../../../core/api/customers.client';
import { CustomerConfig, PlatformApiService } from '../../../core/api/platform-api.service';
import { TICKET_PRIORITIES, TicketPriority, TicketsClient } from '../../../core/api/tickets.client';

/**
 * Create a ticket on a customer's behalf — `/workspace/tickets/new`. Agent+.
 *
 * <h3>Where this screen comes from</h3>
 * **Story 11 task 6** specifies `features/workspace/ticket-create/` as the home of §7.3's
 * categorization-at-creation, which is what puts an agent-side creation form in an approved plan.
 * T1-B requires creating a ticket *"by an agent on behalf of a customer"*, and `POST /tickets`
 * (api-design §5.6) has existed since Story 05 — this is its first screen.
 *
 * <h3>The AI suggestion is a pre-selection, never an auto-fill</h3>
 * On blur of **subject** or **description** the screen calls `POST /ai/classification-suggestion` —
 * the endpoint that takes **no ticket id**, because the ticket does not exist yet.
 *
 * The result **pre-selects** the category and priority selectors and shows the **AI-generated** label
 * beside them. Both remain **freely overridable**, and the label persists after an override so the
 * agent can see what was suggested next to what they chose (A-8, UI-6).
 *
 * **No inline ghost text** (UI-6): the suggestion fills two selectors the agent was going to use
 * anyway, not the free-text fields they are writing. Their words are never touched.
 *
 * <h3>`503` leaves the form exactly as it was before Story 11</h3>
 * An unavailable AI leaves both selectors **empty and enabled** with a quiet note. The agent picks
 * manually and creation works unchanged — the visible half of T1-F, and the intake's own acceptance
 * criterion.
 *
 * <h3>What this form does not have</h3>
 * **No `isUrgent`.** It is customer input only (A-17), so the staff creation body has no such field
 * and a body carrying one is a `400` (AP-10). The portal's own form has it.
 *
 * **No `departmentId`.** It is optional on the contract, and omitting it lets the server derive the
 * department from the category (A-14) — which is the behaviour every other creation path uses. An
 * override selector here would be contract surface for a case no requirement asks for.
 *
 * **No status.** Status is server-derived; a new ticket is `New` (AP-1).
 */
@Component({
    selector: 'app-ticket-create',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, FormsModule, InputTextModule, MessageModule, RouterLink, SelectModule,
        TextareaModule, TranslocoModule
    ],
    template: `
        <section class="app-page">
            <a routerLink="/workspace/tickets">{{ 'actions.back' | transloco }}</a>

            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'tickets.createTitle' | transloco }}</h1>
            </header>

            @if (problem(); as failure) {
                <p-message severity="error" [text]="errorKey(failure) | transloco" />
            }

            <form class="app-form" (ngSubmit)="submit()">
                <label class="app-form__field">
                    <span>{{ 'tickets.customer' | transloco }}</span>
                    <p-select
                        name="customer"
                        [options]="customers()"
                        optionLabel="fullName"
                        optionValue="id"
                        [filter]="true"
                        filterBy="fullName,email"
                        [showClear]="true"
                        [placeholder]="'tickets.choosecustomer' | transloco"
                        [(ngModel)]="customerId"
                        [disabled]="busy()"
                    />
                </label>

                <label class="app-form__field">
                    <span>{{ 'tickets.subject' | transloco }}</span>
                    <!-- The blur is what asks for a suggestion: the agent has finished a thought, and
                         nothing is requested while they are still typing. -->
                    <input
                        pInputText
                        name="subject"
                        [(ngModel)]="subject"
                        [disabled]="busy()"
                        (blur)="requestSuggestion()"
                    />
                </label>

                <label class="app-form__field">
                    <span>{{ 'tickets.description' | transloco }}</span>
                    <textarea
                        pTextarea
                        rows="6"
                        name="description"
                        [(ngModel)]="description"
                        [disabled]="busy()"
                        (blur)="requestSuggestion()"
                    ></textarea>
                </label>

                <!-- The AI-generated label, always visible while a suggestion stands (A-8, UI-6).
                     It stays after an override, so the agent can see what was suggested beside what
                     they chose. -->
                @if (suggestion(); as offered) {
                    <p class="app-ai-inline">
                        <span class="app-ai-inline__label">{{ 'ai.generatedLabel' | transloco }}</span>
                        <span>{{ 'ai.suggestedClassification' | transloco }}</span>
                        <span class="app-ai-inline__value">{{ offered.categoryCode }} · {{ 'tickets.priority.' + offered.priority | transloco }}</span>
                        @if (overridden()) {
                            <span class="app-ai-inline__overridden">{{ 'ai.overridden' | transloco }}</span>
                        }
                    </p>
                }

                <!-- 503 leaves both selectors empty and ENABLED. The form works as it did before. -->
                @if (aiUnavailable()) {
                    <p class="app-page__meta">{{ 'ai.unavailableInline' | transloco }}</p>
                }

                <label class="app-form__field">
                    <span>{{ 'tickets.categoryLabel' | transloco }}</span>
                    <p-select
                        name="category"
                        [options]="categories()"
                        optionLabel="name"
                        optionValue="code"
                        [showClear]="true"
                        [placeholder]="'tickets.anyCategory' | transloco"
                        [(ngModel)]="categoryCode"
                        [disabled]="busy()"
                    />
                </label>

                <label class="app-form__field">
                    <span>{{ 'tickets.priorityLabel' | transloco }}</span>
                    <p-select
                        name="priority"
                        [options]="priorityOptions"
                        [showClear]="true"
                        [placeholder]="'tickets.anyPriority' | transloco"
                        [(ngModel)]="priority"
                        [disabled]="busy()"
                    />
                </label>

                <div class="app-form__actions">
                    <p-button
                        type="submit"
                        [label]="'actions.create' | transloco"
                        [loading]="busy()"
                        [disabled]="busy() || !canSubmit()"
                    />
                </div>
            </form>
        </section>
    `
})
export class TicketCreateComponent {
    private readonly tickets = inject(TicketsClient);
    private readonly customersApi = inject(CustomersClient);
    private readonly platform = inject(PlatformApiService);
    private readonly ai = inject(AiClient);
    private readonly router = inject(Router);

    protected customerId: string | null = null;
    protected subject = '';
    protected description = '';
    protected categoryCode: string | null = null;
    protected priority: TicketPriority | null = null;

    protected readonly priorityOptions = [...TICKET_PRIORITIES];

    protected readonly customers = signal<CustomerListItem[]>([]);
    protected readonly categories = signal<CustomerConfig['categories']>([]);
    protected readonly busy = signal(false);
    protected readonly problem = signal<ApiProblem | null>(null);

    /** The standing suggestion, kept so its label can stay visible (A-8). */
    protected readonly suggestion = signal<AiClassification | null>(null);
    protected readonly aiUnavailable = signal(false);

    protected errorKey = problemTranslationKey;

    /** What was last asked for, so a blur that changed nothing does not re-ask. */
    private lastAsked = '';

    constructor() {
        // pageSize 100 is the contract's cap (AP-3). A picker over a demo-sized directory needs no
        // paging; a real one would need a typeahead against `GET /customers?q=`, which is a change to
        // this screen and not to the contract.
        this.customersApi.list({ pageSize: 100 }).subscribe({
            next: (page) => this.customers.set(page.items),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });

        // Categories come from `GET /config` — customer-safe configuration, cached for the session.
        this.platform.getCustomerConfig().subscribe((config) => this.categories.set(config.categories));
    }

    protected canSubmit(): boolean {
        return (
            this.customerId !== null &&
            this.subject.trim().length > 0 &&
            this.description.trim().length > 0 &&
            this.categoryCode !== null &&
            this.priority !== null
        );
    }

    /**
     * Asks for a category and priority suggestion. **Both fields must have content** — a suggestion
     * from a subject alone would be worse than none, and the endpoint requires both.
     */
    protected requestSuggestion(): void {
        const subject = this.subject.trim();
        const description = this.description.trim();

        if (subject.length === 0 || description.length === 0) {
            return;
        }

        const key = `${subject} ${description}`;

        if (key === this.lastAsked) {
            // Tabbing between the two fields must not re-ask for the same answer.
            return;
        }

        this.lastAsked = key;
        this.aiUnavailable.set(false);

        // **No isUrgent from this form** — it is customer input only (A-17). The endpoint's parameter
        // is optional and is deliberately left off rather than sent as false, which would imply this
        // screen had asked the question.
        this.ai.suggestClassification(subject, description).subscribe({
            next: (result) => {
                this.suggestion.set(result);

                // **A pre-selection, not an overwrite.** A value the agent has already chosen is left
                // alone: a suggestion arriving after a deliberate choice must not undo it.
                this.categoryCode ??= result.categoryCode;
                this.priority ??= result.priority;
            },
            error: () => {
                // 503, or anything else. Both selectors stay empty and enabled, and the agent
                // proceeds exactly as they would have before Story 11 (T1-F).
                this.suggestion.set(null);
                this.aiUnavailable.set(true);
            }
        });
    }

    /** True when a suggestion stands and the agent has changed either value away from it. */
    protected overridden(): boolean {
        const offered = this.suggestion();

        if (!offered) {
            return false;
        }

        return this.categoryCode !== offered.categoryCode || this.priority !== offered.priority;
    }

    protected submit(): void {
        if (this.busy() || !this.canSubmit()) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        // **No `departmentId`**: omitted, so the server derives it from the category (A-14).
        this.tickets
            .create({
                customerId: this.customerId!,
                subject: this.subject.trim(),
                description: this.description.trim(),
                categoryCode: this.categoryCode!,
                priority: this.priority!
            })
            .subscribe({
                next: (ticket) => {
                    // S9-4 — BLOCKED on a Stage 7 decision. The suggestion that was offered and
                    // whether the agent kept it are known right here, in `suggestion()` and
                    // `overridden()`, and `data-model.md` §2.7 has the activity types for them
                    // (`AiSuggestionOffered`, `AiSuggestionResolved`) — but **no request field and no
                    // endpoint exists to carry them**: `POST /tickets` accepts six fields and none of
                    // them is this (api-design §5.6, §6.11, §7).
                    //
                    // **Do NOT attach an undocumented field here.** The server rejects an unknown
                    // member with a 400 (AP-10, `UnmappedMemberHandling.Disallow`), so a client that
                    // "sent it anyway" would break creation outright — which is precisely what AP-10
                    // exists to prevent. When the contract is extended, send the captured suggestion
                    // and outcome from this call site.
                    this.busy.set(false);

                    void this.router.navigate(['/workspace/tickets', ticket.id]);
                },
                error: (failure: ApiProblem) => {
                    this.busy.set(false);
                    this.problem.set(failure);
                }
            });
    }
}
