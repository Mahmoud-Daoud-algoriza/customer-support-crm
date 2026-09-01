import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { ApiProblem } from '../../core/api/api-problem';
import { PortalArticle, PortalKnowledgeClient } from '../../core/api/knowledge.client';
import { Paged } from '../../core/api/paged';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';

/**
 * Portal help — `/portal/help` (docs/ui-design.md §7.4). Customer.
 *
 * <h3>Public, published articles only — and the server is what enforces it</h3>
 * There is no visibility control on this screen and no filter that could ask for one: the rule lives
 * in `PortalArticleService.PortalVisible` (docs/data-model.md §5 constraint 19), and this screen
 * simply renders what comes back. **An internal article cannot appear here even if a link to one is
 * pasted into the address bar** — that read is a `404`.
 *
 * <h3>Prominent search, and a card list</h3>
 * §7.4. Cards rather than a dense table: the portal speaks no staff vocabulary and shows no
 * priority, department, assignee or SLA (UI-11) — a card carries a title, a type and when it was
 * last updated, and nothing else the payload does not have (§6.5).
 *
 * <h3>Article text is never translated</h3>
 * **A-11.** Titles render as authored while the chrome switches language.
 */
@Component({
    selector: 'app-portal-help',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, EmptyStateComponent, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, PaginatorModule, RouterLink, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'portal.help.title' | transloco }}</h1>
            </header>

            <p class="app-page__meta">{{ 'portal.help.intro' | transloco }}</p>

            <div class="app-filters app-help-search">
                <input
                    pInputText
                    type="search"
                    class="app-help-search__input"
                    [placeholder]="'portal.help.searchPlaceholder' | transloco"
                    [(ngModel)]="q"
                    (keyup.enter)="applySearch()"
                />

                <p-button [label]="'portal.help.search' | transloco" (onClick)="applySearch()" />
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <!-- No results is never an error (§9). -->
                        <app-empty-state
                            [title]="'portal.help.emptyTitle' | transloco"
                            [message]="'portal.help.emptyMessage' | transloco"
                            icon="pi-book"
                        />
                    } @else {
                        <ul class="app-help-cards">
                            @for (article of result.items; track article.id) {
                                <li class="app-help-card">
                                    <!-- Authored text, rendered as authored (A-11). -->
                                    <a class="app-help-card__title" [routerLink]="['/portal/help', article.id]">
                                        {{ article.title }}
                                    </a>

                                    <p class="app-help-card__meta">
                                        {{ 'knowledge.articleType.' + article.type | transloco }}
                                    </p>
                                </li>
                            }
                        </ul>

                        <p-paginator
                            [first]="(result.page - 1) * result.pageSize"
                            [rows]="result.pageSize"
                            [totalRecords]="result.totalItems"
                            (onPageChange)="goToPage($event.page)"
                        />
                    }
                } @else {
                    <app-loading-state [rowCount]="4" />
                }
            }
        </section>
    `
})
export class PortalHelpComponent {
    private readonly api = inject(PortalKnowledgeClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly page = signal<Paged<PortalArticle> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected q: string | null = null;

    constructor() {
        // UI-9: the search term lives in the URL, so a result list is shareable and survives a
        // reload — the same rule every staff list follows.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.q = params.get('q');
            this.load(params);
        });
    }

    protected applySearch(): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { q: this.q || null, page: null }
        });
    }

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
            .search({
                q: params.get('q'),
                page: Number.isFinite(page) && page > 1 ? page : undefined
            })
            .subscribe({
                next: (result) => this.page.set(result),
                error: (failure: ApiProblem) => this.problem.set(failure)
            });
    }
}
