import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ApiProblem } from '../../../core/api/api-problem';
import { CustomerListItem, CustomersClient } from '../../../core/api/customers.client';
import { Branch, OrganizationClient } from '../../../core/api/organization.client';
import { Paged } from '../../../core/api/paged';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { CreateCustomerDialogComponent } from './create-customer-dialog.component';

/**
 * Customer directory — `/workspace/customers` (docs/ui-design.md §5.4). Agent+.
 *
 * Columns are exactly §5.4's: name, email, phone, branch, **open-ticket count**. `openTicketCount`
 * is an aggregate the server computes (docs/api-design.md §6.3) — it reads `0` for every row until
 * Story 06 creates tickets, which is the true answer, not a placeholder.
 *
 * **Filters live in the URL as query parameters (UI-9)**, so a filtered directory is shareable and
 * survives a reload. Their names are the API's own — `q` and `branchId` (docs/api-design.md §5.5) —
 * with no translation step in between.
 *
 * **Branch is a filter here**, and that is its legitimate reporting use (T2-K, A-2). It appears in
 * no authorization predicate anywhere: narrowing a list the caller may already see in full is not
 * scoping, which is why this control is a plain select and deliberately **not** modelled on
 * `app-department-filter` — that component exists to make *scoping* legible, and there is no branch
 * equivalent of it.
 *
 * **There is no department filter**, because a customer has no department (§5.4).
 */
@Component({
    selector: 'app-customer-directory',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, CreateCustomerDialogComponent, EmptyStateComponent, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, PaginatorModule, RouterLink, SelectModule, TableModule, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'customers.title' | transloco }}</h1>
                <p-button [label]="'customers.create' | transloco" icon="pi pi-plus" (onClick)="createOpen.set(true)" />
            </header>

            <div class="app-filters">
                <input pInputText [placeholder]="'customers.search' | transloco" [(ngModel)]="q" (keyup.enter)="applyFilters()" />

                <p-select
                    [options]="branches()"
                    [(ngModel)]="branchId"
                    optionLabel="name"
                    optionValue="id"
                    [showClear]="true"
                    [placeholder]="'customers.anyBranch' | transloco"
                    [ariaLabel]="'customers.branch' | transloco"
                    (onChange)="applyFilters()"
                />

                <p-button [label]="'actions.apply' | transloco" severity="secondary" (onClick)="applyFilters()" />
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <app-empty-state [title]="'customers.emptyTitle' | transloco" [message]="'customers.emptyMessage' | transloco" icon="pi-users" />
                    } @else {
                        <!-- Wide content scrolls inside its own container, so the page body never
                             scrolls sideways (docs/ui-design.md §10.3). -->
                        <div class="app-scroll-x">
                            <p-table [value]="result.items" [tableStyle]="{ 'min-width': '52rem' }">
                                <ng-template pTemplate="header">
                                    <tr>
                                        <th>{{ 'customers.name' | transloco }}</th>
                                        <th>{{ 'customers.email' | transloco }}</th>
                                        <th>{{ 'customers.phone' | transloco }}</th>
                                        <th>{{ 'customers.branch' | transloco }}</th>
                                        <th>{{ 'customers.openTickets' | transloco }}</th>
                                        <th></th>
                                    </tr>
                                </ng-template>
                                <ng-template pTemplate="body" let-customer>
                                    <tr>
                                        <td>{{ customer.fullName }}</td>
                                        <td>{{ customer.email }}</td>
                                        <!-- Phone is optional and the server omits it when null. -->
                                        <td class="app-ltr-numeric">{{ customer.phone ?? '—' }}</td>
                                        <td>{{ customer.branch.name }}</td>
                                        <td class="app-ltr-numeric">{{ customer.openTicketCount }}</td>
                                        <td>
                                            <a [routerLink]="['/workspace/customers', customer.id]">{{ 'actions.open' | transloco }}</a>
                                        </td>
                                    </tr>
                                </ng-template>
                            </p-table>
                        </div>

                        <!-- The paginator writes to the URL like every other filter (UI-9), so a
                             page is as shareable as a search term. -->
                        <p-paginator [first]="(result.page - 1) * result.pageSize" [rows]="result.pageSize" [totalRecords]="result.totalItems" (onPageChange)="goToPage($event.page)" />
                    }
                } @else {
                    <app-loading-state [rowCount]="5" />
                }
            }

            <app-create-customer-dialog [(visible)]="createOpen" (created)="reload()" />
        </section>
    `
})
export class CustomerDirectoryComponent {
    private readonly api = inject(CustomersClient);
    private readonly organization = inject(OrganizationClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly branches = signal<Branch[]>([]);
    protected readonly page = signal<Paged<CustomerListItem> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly createOpen = signal(false);

    /** Bound to the controls. The URL, not this, is the source of truth for what is displayed. */
    protected q = '';
    protected branchId: string | null = null;

    constructor() {
        this.organization.getBranches().subscribe((branches) => this.branches.set(branches));

        // UI-9: the URL drives the screen. Editing a control navigates; the navigation is what
        // loads. A deep link and a typed search therefore take exactly the same path, and a reload
        // reproduces the list rather than resetting it.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.q = params.get('q') ?? '';
            this.branchId = params.get('branchId');
            this.load(params);
        });
    }

    /** A changed filter starts again at page 1 — page 3 of a different query is not a page. */
    protected applyFilters(): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: {
                q: this.q.trim() === '' ? null : this.q.trim(),
                branchId: this.branchId,
                page: null
            }
        });
    }

    /** The paginator reports a **0-based** index; the API's `page` is 1-based (§2.1). */
    protected goToPage(zeroBasedPage: number | undefined): void {
        const page = (zeroBasedPage ?? 0) + 1;

        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { page: page > 1 ? page : null },
            queryParamsHandling: 'merge'
        });
    }

    protected reload(): void {
        this.load(this.route.snapshot.queryParamMap);
    }

    private load(params: ParamMap): void {
        this.page.set(null);
        this.problem.set(null);

        const page = Number(params.get('page'));

        this.api
            .list({
                q: params.get('q'),
                branchId: params.get('branchId'),
                page: Number.isFinite(page) && page > 1 ? page : undefined
            })
            .subscribe({
                next: (result) => this.page.set(result),
                error: (failure: ApiProblem) => this.problem.set(failure)
            });
    }
}
