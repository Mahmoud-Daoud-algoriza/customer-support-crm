import { ChangeDetectionStrategy, Component, inject, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { Customer, CustomersClient } from '../../../core/api/customers.client';
import { Branch, OrganizationClient } from '../../../core/api/organization.client';

/**
 * `POST /customers` — docs/api-design.md §5.5, the create half of story 04's *"an agent can create,
 * view, edit and list customers"*.
 *
 * **The branch is chosen here, and required.** An agent creating a profile picks the branch (A-2);
 * only a *self-registering* customer is given the configured default and never asked (A-15), which
 * is a different endpoint on a different screen.
 *
 * **`409 customer-email-in-use` renders inline on the email field**, the same placement the detail
 * screen uses for the same collision (docs/ui-design.md §5.5, §9) — a duplicate is rejected, never
 * reconciled, because there is no merge or dedupe tooling (A-10, T2-A).
 */
@Component({
    selector: 'app-create-customer-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DialogModule, FormsModule, InputTextModule, MessageModule, SelectModule, TranslocoModule],
    template: `
        <p-dialog [visible]="visible()" (visibleChange)="visible.set($event)" [modal]="true" [style]="{ 'max-inline-size': '32rem', 'inline-size': '90vw' }" [header]="'customers.create' | transloco">
            <form class="app-form" (ngSubmit)="submit()">
                <label class="app-field">
                    <span class="app-field__label">{{ 'customers.name' | transloco }}</span>
                    <input pInputText name="fullName" required [(ngModel)]="fullName" />
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'customers.email' | transloco }}</span>
                    <input pInputText name="email" type="email" required [(ngModel)]="email" />

                    @if (emailProblem(); as failure) {
                        <p-message severity="error" [text]="errorKey(failure) | transloco" />
                    }
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'customers.phone' | transloco }}</span>
                    <input pInputText name="phone" [(ngModel)]="phone" />
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'customers.branch' | transloco }}</span>
                    <p-select name="branchId" [options]="branches()" [(ngModel)]="branchId" optionLabel="name" optionValue="id" [placeholder]="'customers.selectBranch' | transloco" />
                </label>

                @if (otherProblem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                <div class="app-form__actions">
                    <p-button type="button" severity="secondary" [label]="'actions.cancel' | transloco" (onClick)="visible.set(false)" />
                    <p-button type="submit" [label]="'actions.create' | transloco" [loading]="busy()" [disabled]="busy() || branchId === null" />
                </div>
            </form>
        </p-dialog>
    `
})
export class CreateCustomerDialogComponent {
    private readonly api = inject(CustomersClient);
    private readonly organization = inject(OrganizationClient);

    readonly visible = model(false);
    readonly created = output<Customer>();

    protected readonly branches = signal<Branch[]>([]);
    protected readonly busy = signal(false);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected fullName = '';
    protected email = '';
    protected phone = '';

    /** `null` when unset — a `p-select` with nothing selected binds `null`. */
    protected branchId: string | null = null;

    protected errorKey = problemTranslationKey;

    constructor() {
        // Cached for the session by the client, so the dialog and the filter cost one request.
        this.organization.getBranches().subscribe((branches) => this.branches.set(branches));
    }

    /**
     * The email collision, and only that, belongs beside the email field (docs/ui-design.md §9:
     * *"`409` … 'That email is already in use'"*). Anything else is a form-level message.
     */
    protected emailProblem(): ApiProblem | null {
        const failure = this.problem();

        return failure?.type === 'customer-email-in-use' ? failure : null;
    }

    protected otherProblem(): ApiProblem | null {
        const failure = this.problem();

        return failure && failure.type !== 'customer-email-in-use' ? failure : null;
    }

    protected submit(): void {
        if (this.busy() || this.branchId === null) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        this.api
            .create({
                fullName: this.fullName,
                email: this.email,
                // An empty box is "no phone", not an empty phone: the field is optional and the
                // server stores null (docs/data-model.md §2.4).
                phone: this.phone.trim() === '' ? null : this.phone,
                branchId: this.branchId
            })
            .subscribe({
                next: (customer) => {
                    this.busy.set(false);
                    this.reset();
                    this.visible.set(false);
                    this.created.emit(customer);
                },
                error: (failure: ApiProblem) => {
                    this.busy.set(false);
                    this.problem.set(failure);
                }
            });
    }

    private reset(): void {
        this.fullName = '';
        this.email = '';
        this.phone = '';
        this.branchId = null;
        this.problem.set(null);
    }
}
