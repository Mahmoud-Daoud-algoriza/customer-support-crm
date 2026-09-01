import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ApiProblem, problemTranslationKey } from '../../core/api/api-problem';
import { PortalClient } from '../../core/api/portal.client';
import { CustomerConfig, PlatformApiService } from '../../core/api/platform-api.service';

/**
 * Submit a request — `/portal/requests/new` (docs/ui-design.md §7.2).
 *
 * <h3>Exactly four inputs, and that is the contract, not a simplification</h3>
 * Subject, description, **category** and the **"this is urgent"** checkbox (§7.2). There is no
 * department field — **the customer chooses a category and the server derives the department**
 * (A-14) — and no priority field, because customers do not set priority (A-6). The checkbox is
 * labelled as an *indication*, never as a priority (A-17).
 *
 * <h3>No staff vocabulary</h3>
 * **UI-11.** Nothing here names a department, an assignee, an SLA or a priority, and the category
 * list comes from `GET /config`, which publishes `code` and `name` only — the routing map is
 * staff-only (AP-17), so this screen could not reveal the department even if it tried.
 *
 * <h3>Attachments are offered after submission, not here</h3>
 * §7.2 says so, and the reason is structural: an attachment belongs to a request, and there is no
 * request until this form is sent. On success the customer lands on the detail screen, where the
 * uploader is.
 *
 * <h3>Single column at every width</h3>
 * ui-design §10.3. The form is one column of full-width fields on a phone and the same column on a
 * desktop; there is no second column to collapse.
 */
@Component({
    selector: 'app-portal-submit-request',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, CheckboxModule, FormsModule, InputTextModule, MessageModule, RouterLink, SelectModule, TextareaModule, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'portal.submitTitle' | transloco }}</h1>
            </header>

            <form class="app-form app-form--portal" (ngSubmit)="submit()">
                <label class="app-form__field">
                    <span>{{ 'portal.subject' | transloco }}</span>
                    <input pInputText name="subject" [(ngModel)]="subject" [disabled]="busy()" />
                </label>

                <label class="app-form__field">
                    <span>{{ 'portal.description' | transloco }}</span>
                    <textarea pTextarea rows="5" name="description" [(ngModel)]="description" [disabled]="busy()"></textarea>
                </label>

                <label class="app-form__field">
                    <!-- A CATEGORY, never a department (A-14). The department is derived server-side
                         and never appears in this form. -->
                    <span>{{ 'portal.category' | transloco }}</span>
                    <p-select
                        name="categoryCode"
                        optionLabel="name"
                        optionValue="code"
                        [options]="categories()"
                        [(ngModel)]="categoryCode"
                        [disabled]="busy()"
                        [placeholder]="'portal.categoryPlaceholder' | transloco" />
                </label>

                <label class="app-form__field app-form__field--inline">
                    <p-checkbox name="isUrgent" [binary]="true" [(ngModel)]="isUrgent" [disabled]="busy()" />
                    <!-- A-17: an INDICATION, not a priority. The wording must not imply otherwise. -->
                    <span>{{ 'portal.urgentLabel' | transloco }}</span>
                </label>

                @if (problem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                <p-button
                    type="submit"
                    [label]="'portal.submit' | transloco"
                    [loading]="busy()"
                    [disabled]="busy() || !canSubmit()" />
            </form>

            <p><a routerLink="/portal/requests">{{ 'portal.detail.backToRequests' | transloco }}</a></p>
        </section>
    `
})
export class PortalSubmitRequestComponent {
    private readonly api = inject(PortalClient);
    private readonly platform = inject(PlatformApiService);
    private readonly router = inject(Router);

    protected subject = '';
    protected description = '';
    protected categoryCode: string | null = null;
    protected isUrgent = false;

    protected readonly busy = signal(false);
    protected readonly problem = signal<ApiProblem | null>(null);

    /**
     * `GET /config` publishes `code` and `name` only — **never the department behind them** (AP-17,
     * A-14), so this screen could not name a department even if it tried. The same cached call the
     * staff filter bar uses.
     */
    protected readonly categories = signal<CustomerConfig['categories']>([]);

    constructor() {
        this.platform.getCustomerConfig().subscribe((config) => this.categories.set(config.categories));
    }

    protected errorKey = problemTranslationKey;

    protected canSubmit(): boolean {
        return this.subject.trim().length > 0
            && this.description.trim().length > 0
            && this.categoryCode !== null;
    }

    protected submit(): void {
        if (this.busy() || !this.canSubmit()) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        this.api
            .submit({
                subject: this.subject.trim(),
                description: this.description.trim(),
                categoryCode: this.categoryCode!,
                isUrgent: this.isUrgent
            })
            .subscribe({
                next: (ticket) => {
                    this.busy.set(false);
                    void this.router.navigate(['/portal/requests', ticket.id]);
                },
                error: (failure: ApiProblem) => {
                    this.busy.set(false);
                    this.problem.set(failure);
                }
            });
    }
}
