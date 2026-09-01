import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ApiProblem } from '../../core/api/api-problem';
import { PortalArticle, PortalKnowledgeClient } from '../../core/api/knowledge.client';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';
import { MarkdownViewComponent } from '../../shared/components/markdown-view/markdown-view.component';

/**
 * The portal article reader — `/portal/help/:id` (docs/ui-design.md §7.4). Customer.
 *
 * <h3>`404` reads as "Not found", identically in all three cases</h3>
 * A missing article, an **internal** article and an **unpublished** one all answer `404` (AP-4), and
 * this screen renders one wording for all of them (§9). **That is the whole reason AP-4 exists** —
 * distinguishing them here would tell a customer that an article they may not read is nevertheless
 * there. The message comes from the problem `type` through `ErrorStateComponent`, so this component
 * has no branch that could tell the cases apart even if it wanted to.
 *
 * <h3>Read-only, and no staff vocabulary</h3>
 * The payload carries `{ id, title, body, type, updatedAt }` and nothing else (§6.5): no
 * `visibility`, no `isPublished`, no author. There is nothing to render that a customer should not
 * see, because the contract never sends it (UI-11).
 *
 * <h3>The body renders markdown and is never translated</h3>
 * **A-11**, through the same reader the staff screens use.
 */
@Component({
    selector: 'app-portal-help-article',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe, ErrorStateComponent, LoadingStateComponent, MarkdownViewComponent, RouterLink, TranslocoModule],
    template: `
        <section class="app-page">
            <a routerLink="/portal/help">{{ 'actions.back' | transloco }}</a>

            @if (problem(); as failure) {
                <!-- One wording for missing, internal and unpublished (AP-4, §9). -->
                <app-error-state [problem]="failure" [retryable]="false" />
            } @else {
                @if (article(); as row) {
                    <header class="app-page__header">
                        <h1 class="app-page__title">{{ row.title }}</h1>
                    </header>

                    <p class="app-page__meta">
                        {{ 'knowledge.articleType.' + row.type | transloco }}
                        · <span class="app-ltr-numeric">{{ 'knowledge.updated' | transloco }}: {{ row.updatedAt | date: 'short' }}</span>
                    </p>

                    <app-markdown-view [source]="row.body" />
                } @else {
                    <app-loading-state [rowCount]="5" />
                }
            }
        </section>
    `
})
export class PortalHelpArticleComponent {
    private readonly api = inject(PortalKnowledgeClient);
    private readonly route = inject(ActivatedRoute);

    private readonly articleId = this.route.snapshot.paramMap.get('id') ?? '';

    protected readonly article = signal<PortalArticle | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    constructor() {
        this.api.read(this.articleId).subscribe({
            next: (row) => this.article.set(row),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
