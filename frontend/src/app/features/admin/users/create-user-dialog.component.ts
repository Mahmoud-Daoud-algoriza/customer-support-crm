import { ChangeDetectionStrategy, Component, computed, inject, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { IdentityClient } from '../../../core/api/identity.client';
import { UserRole } from '../../../core/auth/identity.model';
import { STAFF_ROLE_OPTIONS } from './staff-roles';

/**
 * Create-user dialog — `/admin/users` (docs/ui-design.md §6).
 *
 * The form enforces "a staff role requires a department" for immediate feedback, **and the server
 * re-validates it** (DM-1). The client check is UX; the server check is the rule.
 *
 * The department field is a free-text id in this story: `GET /departments` is delivered by Story 03
 * task 5, and a selector cannot be populated before it exists. Story 03 task 7 replaces this input
 * with that selector.
 */
@Component({
    selector: 'app-create-user-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, DialogModule, FormsModule, InputTextModule, MessageModule, PasswordModule,
        SelectModule, TranslocoModule,
    ],
    template: `
        <p-dialog
            [visible]="visible()"
            (visibleChange)="visible.set($event)"
            [modal]="true"
            [style]="{ width: '32rem' }"
            [header]="'admin.users.create' | transloco"
        >
            <form class="app-form" (ngSubmit)="submit()">
                @if (problem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                <label class="app-field">
                    <span class="app-field__label">{{ 'admin.users.email' | transloco }}</span>
                    <input pInputText name="email" type="email" required [(ngModel)]="email" />
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'admin.users.name' | transloco }}</span>
                    <input pInputText name="displayName" required [(ngModel)]="displayName" />
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'auth.password' | transloco }}</span>
                    <p-password name="password" [feedback]="false" [toggleMask]="true" required [(ngModel)]="password" />
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'admin.users.role' | transloco }}</span>
                    <p-select
                        name="role"
                        [options]="roleOptions"
                        [(ngModel)]="role"
                        optionLabel="label"
                        optionValue="value"
                        [placeholder]="'admin.users.selectRole' | transloco"
                    />
                </label>

                <label class="app-field">
                    <span class="app-field__label">{{ 'admin.users.departmentId' | transloco }}</span>
                    <input pInputText name="departmentId" required [(ngModel)]="departmentId" />
                </label>

                @if (departmentMissing()) {
                    <p-message severity="warn" [text]="'admin.users.departmentRequired' | transloco" />
                }

                <div class="app-form__actions">
                    <p-button type="button" [label]="'actions.cancel' | transloco" severity="secondary" (onClick)="visible.set(false)" />
                    <p-button type="submit" [label]="'actions.create' | transloco" [loading]="busy()" [disabled]="busy() || departmentMissing()" />
                </div>
            </form>
        </p-dialog>
    `
})
export class CreateUserDialogComponent {
    private readonly api = inject(IdentityClient);

    readonly visible = model(false);
    readonly created = output<void>();

    protected readonly roleOptions = STAFF_ROLE_OPTIONS;

    protected email = '';
    protected displayName = '';
    protected password = '';
    protected role: UserRole = 'Agent';
    protected departmentId = '';

    protected readonly busy = signal(false);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly touched = signal(false);

    protected errorKey = problemTranslationKey;

    /** Every role in this dialog is a staff role, so a department is always required (DM-1). */
    protected readonly departmentMissing = computed(() => this.touched() && this.departmentId.trim() === '');

    protected submit(): void {
        this.touched.set(true);

        if (this.departmentId.trim() === '' || this.busy()) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        this.api
            .createUser({
                email: this.email,
                password: this.password,
                displayName: this.displayName,
                role: this.role,
                departmentId: this.departmentId.trim(),
            })
            .subscribe({
                next: () => {
                    this.busy.set(false);
                    this.visible.set(false);
                    this.reset();
                    this.created.emit();
                },
                error: (failure: ApiProblem) => {
                    this.busy.set(false);
                    this.problem.set(failure);
                },
            });
    }

    private reset(): void {
        this.email = '';
        this.displayName = '';
        this.password = '';
        this.role = 'Agent';
        this.departmentId = '';
        this.touched.set(false);
    }
}
