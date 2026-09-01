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
import {
    ArticleListFilter,
    ArticleListItem,
    ArticleType,
    ArticleVisibility,
    KnowledgeClient
} from '../../../core/api/knowledge.client';
import { Paged } from '../../../core/api/paged';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * Knowledge base (staff) — `/workspace/knowledge` (docs/ui-design.md §5.6). Agent+.
 *
 * <h3>Search is presented as search, never as an AI answer</h3>
 * §5.6 is explicit about it, and this screen keeps the promise structurally: **no sparkle icon, no
 * "AI" wording, no generated summary above the results**. What comes back is a list of articles that
 * already existed, ordered by the database's own keyword ranking (AD-13). The AI panel is a
 * different component, on a different screen, with a permanent *AI-generated* label — the contrast
 * is the point (UI-6, AP-14).
 *
 * <h3>Staff see internal articles, and they are badged</h3>
 * An Agent's search returns internal **and** public articles (§5.9); the internal ones carry an
 * **internal** badge (§5.6) so nothing is quoted to a customer by mistake. The badge is rendered
 * from the payload's `visibility`, never inferred.
 *
 * <h3>Filters live in the URL, under the API's own names</h3>
 * **UI-9**: `q`, `type`, `visibility` and `isPublished` are query parameters, so a filtered view is
 * shareable and survives a reload, and their names mirror `GET /kb/articles` exactly.
 *
 * <h3>Article text is never translated</h3>
 * **A-11.** Titles are rendered as authored; only the chrome around them switches language.
 */
@Component({
    selector: 'app-knowledge-search',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, PaginatorModule, RouterLink, SelectModule, TableModule, TagModule, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'knowledge.title' | transloco }}</h1>
            </header>

            <!-- Plain search. No sparkle, no "ask" wording, no generated answer above the list. -->
            <div class="app-filters">
                <input
                    pInputText
                    type="search"
                    [placeholder]="'knowledge.searchPlaceholder' | transloco"
                    [(ngModel)]="q"
                    (keyup.enter)="applyFilters()"
                />

                <p-select
                    [options]="typeOptions()"
                    [(ngModel)]="type"
                    optionLabel="label"
                    optionValue="value"
                    [placeholder]="'knowledge.anyType' | transloco"
                    [showClear]="true"
                />

                <p-select
                    [options]="visibilityOptions()"
                    [(ngModel)]="visibility"
                    optionLabel="label"
                    optionValue="value"
                    [placeholder]="'knowledge.anyVisibility' | transloco"
                    [showClear]="true"
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
                        <!-- An empty search result is never an error (§9). -->
                        <app-empty-state
                            [title]="'knowledge.emptyTitle' | transloco"
                            [message]="'knowledge.emptyMessage' | transloco"
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
                                <ng-template pTemplate="body" let-article>
                                    <tr>
                                        <!-- Authored text, rendered as authored (A-11). -->
                                        <td><a [routerLink]="['/workspace/knowledge', article.id]">{{ article.title }}</a></td>
                                        <td>{{ 'knowledge.articleType.' + article.type | transloco }}</td>
                                        <td>
                                            @if (article.visibility === 'Internal') {
                                                <!-- §5.6: internal articles carry an "internal" badge. -->
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
export class KnowledgeSearchComponent {
    private readonly api = inject(KnowledgeClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly page = signal<Paged<ArticleListItem> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected q: string | null = null;
    protected type: ArticleType | null = null;
    protected visibility: ArticleVisibility | null = null;
    protected isPublished: boolean | null = null;

    private readonly transloco = inject(TranslocoService);

    /**
     * The three type codes of T2-E, **labelled** through the dictionary. The values are the API's
     * stable string codes and are never translated (docs/api-design.md §2) — only what a person
     * reads is. Reading the active language makes the labels re-translate on a switch without a
     * reload (T2-J), the same way the shell menu does it.
     */
    protected readonly typeOptions = computed(() => {
        this.transloco.getActiveLang();

        return (['Faq', 'HelpArticle', 'SolutionGuide'] as const).map((value) => ({
            value,
            label: this.transloco.translate(`knowledge.articleType.${value}`)
        }));
    });

    protected readonly visibilityOptions = computed(() => {
        this.transloco.getActiveLang();

        return (['Public', 'Internal'] as const).map((value) => ({
            value,
            label: this.transloco.translate(`knowledge.visibilityValue.${value}`)
        }));
    });

    protected readonly publishedOptions = computed(() => {
        this.transloco.getActiveLang();

        return [
            { value: true, label: this.transloco.translate('knowledge.published') },
            { value: false, label: this.transloco.translate('knowledge.draft') }
        ];
    });

    constructor() {
        // UI-9: the URL drives the screen.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.readFilterInto(params);
            this.load(params);
        });
    }

    protected applyFilters(): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: {
                q: this.q || null,
                type: this.type,
                visibility: this.visibility,
                isPublished: this.isPublished,
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

    private readFilterInto(params: ParamMap): void {
        this.q = params.get('q');
        this.type = params.get('type') as ArticleType | null;
        this.visibility = params.get('visibility') as ArticleVisibility | null;
        this.isPublished = params.has('isPublished') ? params.get('isPublished') === 'true' : null;
    }

    private load(params: ParamMap): void {
        this.page.set(null);
        this.problem.set(null);

        const page = Number(params.get('page'));

        const filter: ArticleListFilter = {
            q: params.get('q'),
            type: params.get('type') as ArticleType | null,
            visibility: params.get('visibility') as ArticleVisibility | null,
            isPublished: params.has('isPublished') ? params.get('isPublished') === 'true' : null,
            page: Number.isFinite(page) && page > 1 ? page : undefined
        };

        this.api.search(filter).subscribe({
            next: (result) => this.page.set(result),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
