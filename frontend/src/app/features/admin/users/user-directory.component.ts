import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../core/api/api-problem';
import { IdentityClient, UserListFilter } from '../../../core/api/identity.client';
import { Paged } from '../../../core/api/paged';
import { UserRow } from '../../../core/auth/identity.model';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { CreateUserDialogComponent } from './create-user-dialog.component';
import { STAFF_ROLE_OPTIONS } from './staff-roles';

/**
 * User directory — `/admin/users` (docs/ui-design.md §6). Filters mirror the API exactly:
 * `role`, `departmentId`, `isActive`, `q` (docs/api-design.md §5.3).
 */
@Component({
    selector: 'app-user-directory',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, CreateUserDialogComponent, EmptyStateComponent, ErrorStateComponent,
        FormsModule, InputTextModule, LoadingStateComponent, RouterLink, SelectModule, TableModule,
        TagModule, TranslocoModule,
    ],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'admin.users.title' | transloco }}</h1>
                <p-button [label]="'admin.users.create' | transloco" icon="pi pi-plus" (onClick)="createOpen.set(true)" />
            </header>

            <div class="app-filters">
                <input pInputText [placeholder]="'admin.users.search' | transloco" [(ngModel)]="filter.q" (keyup.enter)="load()" />

                <p-select
                    [options]="roleOptions"
                    [(ngModel)]="filter.role"
                    optionLabel="label"
                    optionValue="value"
                    [showClear]="true"
                    [placeholder]="'admin.users.anyRole' | transloco"
                    (onChange)="load()"
                />

                <p-select
                    [options]="activeOptions"
                    [(ngModel)]="filter.isActive"
                    optionLabel="label"
                    optionValue="value"
                    [showClear]="true"
                    [placeholder]="'admin.users.anyState' | transloco"
                    (onChange)="load()"
                />

                <p-button [label]="'actions.apply' | transloco" severity="secondary" (onClick)="load()" />
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                    <app-empty-state [title]="'admin.users.emptyTitle' | transloco" [message]="'admin.users.emptyMessage' | transloco" icon="pi-users" />
                } @else {
                    <div class="app-scroll-x">
                        <p-table [value]="result.items" [tableStyle]="{ 'min-width': '48rem' }">
                            <ng-template pTemplate="header">
                                <tr>
                                    <th>{{ 'admin.users.name' | transloco }}</th>
                                    <th>{{ 'admin.users.email' | transloco }}</th>
                                    <th>{{ 'admin.users.role' | transloco }}</th>
                                    <th>{{ 'admin.users.state' | transloco }}</th>
                                    <th></th>
                                </tr>
                            </ng-template>
                            <ng-template pTemplate="body" let-user>
                                <tr>
                                    <td>{{ user.displayName }}</td>
                                    <td>{{ user.email }}</td>
                                    <td>{{ 'roles.' + user.role | transloco }}</td>
                                    <td>
                                        <p-tag
                                            [severity]="user.isActive ? 'success' : 'danger'"
                                            [value]="(user.isActive ? 'admin.users.active' : 'admin.users.inactive') | transloco"
                                        />
                                    </td>
                                    <td>
                                        <a [routerLink]="['/admin/users', user.id]">{{ 'actions.open' | transloco }}</a>
                                    </td>
                                </tr>
                            </ng-template>
                        </p-table>
                    </div>

                    <p class="app-page__count app-ltr-numeric">
                        {{ result.totalItems }} · {{ result.page }}/{{ result.totalPages }}
                    </p>
                    }
                } @else {
                    <app-loading-state [rowCount]="5" />
                }
            }

            <app-create-user-dialog [(visible)]="createOpen" (created)="load()" />
        </section>
    `
})
export class UserDirectoryComponent {
    private readonly api = inject(IdentityClient);

    /** The `Customer` role is absent — it cannot be filtered for here because it cannot exist here. */
    protected readonly roleOptions = STAFF_ROLE_OPTIONS;

    protected readonly activeOptions = [
        { label: 'Active', value: true },
        { label: 'Inactive', value: false },
    ];

    protected filter: UserListFilter = {};
    protected readonly page = signal<Paged<UserRow> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly createOpen = signal(false);

    constructor() {
        this.load();
    }

    protected load(): void {
        this.page.set(null);
        this.problem.set(null);

        this.api.listUsers(this.filter).subscribe({
            next: (result) => this.page.set(result),
            error: (failure: ApiProblem) => this.problem.set(failure),
        });
    }
}
