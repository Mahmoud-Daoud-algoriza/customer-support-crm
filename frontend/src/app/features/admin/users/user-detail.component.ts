import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { IdentityClient } from '../../../core/api/identity.client';
import { Department, OrganizationClient } from '../../../core/api/organization.client';
import { UserRole, UserRow } from '../../../core/auth/identity.model';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { STAFF_ROLE_OPTIONS } from './staff-roles';

/**
 * User detail — `/admin/users/:id` (docs/ui-design.md §6). Deep-linkable: it loads its own data and
 * depends on nothing carried from the directory.
 *
 * Deactivate confirms first (UI-12). The patchable fields are exactly those of
 * docs/api-design.md §5.3 — email and password are not among them.
 *
 * The department field is a **selector populated from `GET /departments`** (Story 03 task 7),
 * replacing the free-text id input Story 02 carried while that endpoint did not exist.
 */
@Component({
    selector: 'app-user-detail',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [ConfirmationService],
    imports: [
        ButtonModule, ConfirmDialogModule, ErrorStateComponent, FormsModule, InputTextModule,
        LoadingStateComponent, MessageModule, RouterLink, SelectModule, TagModule, TranslocoModule,
    ],
    template: `
        <section class="app-page">
            <a routerLink="/admin/users">{{ 'actions.back' | transloco }}</a>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (user(); as row) {
                <header class="app-page__header">
                    <h1 class="app-page__title">{{ row.displayName }}</h1>
                    <p-tag
                        [severity]="row.isActive ? 'success' : 'danger'"
                        [value]="(row.isActive ? 'admin.users.active' : 'admin.users.inactive') | transloco"
                    />
                </header>

                <p class="app-page__meta">{{ row.email }}</p>

                @if (saved()) {
                    <p-message severity="success" [text]="'admin.users.saved' | transloco" />
                }

                <form class="app-form" (ngSubmit)="save()">
                    <label class="app-field">
                        <span class="app-field__label">{{ 'admin.users.name' | transloco }}</span>
                        <input pInputText name="displayName" [(ngModel)]="displayName" />
                    </label>

                    <label class="app-field">
                        <span class="app-field__label">{{ 'admin.users.role' | transloco }}</span>
                        <p-select name="role" [options]="roleOptions" [(ngModel)]="role" optionLabel="label" optionValue="value" />
                    </label>

                    <label class="app-field">
                        <span class="app-field__label">{{ 'admin.users.department' | transloco }}</span>
                        <p-select
                            name="departmentId"
                            [options]="departments()"
                            [(ngModel)]="departmentId"
                            optionLabel="name"
                            optionValue="id"
                            [placeholder]="'organization.department.select' | transloco"
                        />
                    </label>

                    @if (departmentId === null) {
                        <p-message severity="warn" [text]="'admin.users.departmentRequired' | transloco" />
                    }

                    <div class="app-form__actions">
                        <p-button
                            type="submit"
                            [label]="'actions.save' | transloco"
                            [loading]="busy()"
                            [disabled]="busy() || departmentId === null"
                        />

                        @if (row.isActive) {
                            <p-button
                                type="button"
                                severity="danger"
                                [label]="'admin.users.deactivate' | transloco"
                                (onClick)="confirmDeactivate(row)"
                            />
                        }
                    </div>
                </form>

                <p-confirmDialog />
                } @else {
                    <app-loading-state [rowCount]="4" />
                }
            }
        </section>
    `
})
export class UserDetailComponent {
    private readonly api = inject(IdentityClient);
    private readonly organization = inject(OrganizationClient);
    private readonly route = inject(ActivatedRoute);
    private readonly confirm = inject(ConfirmationService);

    protected readonly roleOptions = STAFF_ROLE_OPTIONS;
    protected readonly departments = signal<Department[]>([]);

    protected readonly user = signal<UserRow | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly busy = signal(false);
    protected readonly saved = signal(false);

    protected displayName = '';
    protected role: UserRole = 'Agent';

    /** `null` when unset — a `p-select` with nothing selected binds `null`. */
    protected departmentId: string | null = null;

    protected errorKey = problemTranslationKey;

    constructor() {
        this.organization.getDepartments().subscribe((departments) => this.departments.set(departments));
        this.load();
    }

    protected load(): void {
        this.user.set(null);
        this.problem.set(null);
        this.saved.set(false);

        const id = this.route.snapshot.paramMap.get('id');
        if (!id) {
            return;
        }

        this.api.getUser(id).subscribe({
            next: (row) => {
                this.user.set(row);
                this.displayName = row.displayName;
                this.role = row.role;
                this.departmentId = row.departmentId ?? null;
            },
            error: (failure: ApiProblem) => this.problem.set(failure),
        });
    }

    protected save(): void {
        const row = this.user();
        if (!row || this.busy() || this.departmentId === null) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        this.api
            .patchUser(row.id, {
                displayName: this.displayName,
                role: this.role,
                departmentId: this.departmentId,
            })
            .subscribe({
                next: (updated) => {
                    this.busy.set(false);
                    this.user.set(updated);
                    this.saved.set(true);
                },
                error: (failure: ApiProblem) => {
                    this.busy.set(false);
                    this.problem.set(failure);
                },
            });
    }

    /** UI-12: a destructive action confirms first. */
    protected confirmDeactivate(row: UserRow): void {
        this.confirm.confirm({
            header: 'admin.users.deactivate',
            message: 'admin.users.deactivateConfirm',
            accept: () => this.deactivate(row),
        });
    }

    private deactivate(row: UserRow): void {
        this.busy.set(true);

        this.api.deactivateUser(row.id).subscribe({
            next: () => {
                this.busy.set(false);
                this.load();
            },
            error: (failure: ApiProblem) => {
                this.busy.set(false);
                this.problem.set(failure);
            },
        });
    }
}
