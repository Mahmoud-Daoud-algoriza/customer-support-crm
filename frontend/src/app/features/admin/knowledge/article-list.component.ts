import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../core/api/api-problem';
import { ArticleListFilter, ArticleListItem, KnowledgeClient } from '../../../core/api/knowledge.client';
import { Paged } from '../../../core/api/paged';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * Article authoring list — `/admin/knowledge` (docs/ui-design.md §6). Administrator only.
 *
 * <h3>The author's list, with publish state</h3>
 * It reads the same `GET /kb/articles` the staff search reads — §6's own API column says so — and
 * differs in what it is *for*: publication state is the column that matters, and every row leads to
 * the editor rather than to the reader.
 *
 * <h3>No delete control, no version history, anywhere</h3>
 * **Neither exists server-side** (T2-E, §6), so neither appears here. Their absence is the design,
 * not an unfinished screen.
 *
 * <h3>Guards hide; they do not protect</h3>
 * The route is Administrator-guarded and `POST`/`PATCH`/`publish`/`unpublish` independently return
 * `403` to every other role (A-4, docs/architecture.md §4.2).
 */
@Component({
    selector: 'app-admin-article-list',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, PaginatorModule, RouterLink, SelectModule, TableModule, TagModule, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'admin.knowledge.title' | transloco }}</h1>

                <p-button
                    [label]="'admin.knowledge.create' | transloco"
                    icon="pi pi-plus"
                    routerLink="/admin/knowledge/new"
                />
            </header>

            <div class="app-filters">
                <input
                    pInputText
                    type="search"
                    [placeholder]="'knowledge.searchPlaceholder' | transloco"
                    [(ngModel)]="q"
                    (keyup.enter)="applyFilters()"
                />

                <p-select
                    [options]="publishedOptions()"
                    [(ngModel)]="isPublished"
                    optionLabel="label"
                    optionValue="value"
                    [placeholder]="'knowledge.anyState' | transloco"
                    [showClear]="true"
                />

                <p-button [label]="'actions.apply' | transloco" severity="secondary" (onClick)="applyFilters()" />
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <app-empty-state
                            [title]="'admin.knowledge.emptyTitle' | transloco"
                            [message]="'admin.knowledge.emptyMessage' | transloco"
                            icon="pi-book"
                        />
                    } @else {
                        <div class="app-scroll-x">
                            <p-table [value]="result.items" [tableStyle]="{ 'min-width': '48rem' }">
                                <ng-template pTemplate="header">
                                    <tr>
                                        <th>{{ 'knowledge.articleTitle' | transloco }}</th>
                                        <th>{{ 'knowledge.type' | transloco }}</th>
                                        <th>{{ 'knowledge.visibility' | transloco }}</th>
                                        <th>{{ 'knowledge.state' | transloco }}</th>
                                        <th>{{ 'knowledge.updated' | transloco }}</th>
                                    </tr>
                                </ng-template>
                                <!-- No row action column: there is no delete and no version history
                                     (T2-E, §6). Editing is the link on the title. -->
                                <ng-template pTemplate="body" let-article>
                                    <tr>
                                        <td><a [routerLink]="['/admin/knowledge', article.id]">{{ article.title }}</a></td>
                                        <td>{{ 'knowledge.articleType.' + article.type | transloco }}</td>
                                        <td>
                                            @if (article.visibility === 'Internal') {
                                                <p-tag severity="warn" [value]="'knowledge.internalBadge' | transloco" />
                                            } @else {
                                                {{ 'knowledge.visibilityValue.Public' | transloco }}
                                            }
                                        </td>
                                        <td>
                                            <p-tag
                                                [severity]="article.isPublished ? 'success' : 'secondary'"
                                                [value]="(article.isPublished ? 'knowledge.published' : 'knowledge.draft') | transloco"
                                            />
                                        </td>
                                        <td class="app-ltr-numeric">{{ article.updatedAt | date: 'short' }}</td>
                                    </tr>
                                </ng-template>
                            </p-table>
                        </div>

                        <p-paginator
                            [first]="(result.page - 1) * result.pageSize"
                            [rows]="result.pageSize"
                            [totalRecords]="result.totalItems"
                            (onPageChange)="goToPage($event.page)"
                        />
                    }
                } @else {
                    <app-loading-state [rowCount]="6" />
                }
            }
        </section>
    `
})
export class AdminArticleListComponent {
    private readonly api = inject(KnowledgeClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly transloco = inject(TranslocoService);

    protected readonly page = signal<Paged<ArticleListItem> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected q: string | null = null;
    protected isPublished: boolean | null = null;

    protected readonly publishedOptions = computed(() => {
        this.transloco.getActiveLang();

        return [
            { value: true, label: this.transloco.translate('knowledge.published') },
            { value: false, label: this.transloco.translate('knowledge.draft') }
        ];
    });

    constructor() {
        // UI-9, and the filter names mirror the API.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.q = params.get('q');
            this.isPublished = params.has('isPublished') ? params.get('isPublished') === 'true' : null;
            this.load(params);
        });
    }

    protected applyFilters(): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { q: this.q || null, isPublished: this.isPublished, page: null }
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

        const filter: ArticleListFilter = {
            q: params.get('q'),
            isPublished: params.has('isPublished') ? params.get('isPublished') === 'true' : null,
            page: Number.isFinite(page) && page > 1 ? page : undefined
        };

        this.api.search(filter).subscribe({
            next: (result) => this.page.set(result),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
