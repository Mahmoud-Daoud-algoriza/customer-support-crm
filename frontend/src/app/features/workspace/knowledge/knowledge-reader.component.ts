import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../core/api/api-problem';
import { Article, KnowledgeClient } from '../../../core/api/knowledge.client';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { MarkdownViewComponent } from '../../../shared/components/markdown-view/markdown-view.component';

/**
 * The staff article reader — `/workspace/knowledge/:id` (docs/ui-design.md §5.6). Agent+.
 *
 * <h3>Read-only, and that is the whole of it</h3>
 * Authoring is Administrator-only (A-4) and lives at `/admin/knowledge/:id`. **There is no edit
 * control here, no publish control and no delete control** — the last one exists nowhere, because
 * no delete endpoint exists server-side (T2-E, §6).
 *
 * <h3>Internal articles are readable here, and badged</h3>
 * Staff see both visibilities (§5.9). The **internal** badge is the same one the list shows, so an
 * agent about to quote a paragraph to a customer can see that they must not.
 *
 * <h3>The body renders markdown, and is never translated</h3>
 * **A-11**: the body is displayed as authored, in whatever language it was written, while the
 * chrome around it follows the interface language. It is not passed through a translation pipe, and
 * it must not be.
 *
 * **Deep links survive a reload** (§2): the id comes from the route and the screen loads its own
 * data, depending on nothing carried from the list.
 */
@Component({
    selector: 'app-knowledge-reader',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe, ErrorStateComponent, LoadingStateComponent, MarkdownViewComponent, RouterLink, TagModule, TranslocoModule],
    template: `
        <section class="app-page">
            <a routerLink="/workspace/knowledge">{{ 'actions.back' | transloco }}</a>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (article(); as row) {
                    <header class="app-page__header">
                        <h1 class="app-page__title">{{ row.title }}</h1>

                        <div class="app-ticket-card__chips">
                            @if (row.visibility === 'Internal') {
                                <p-tag severity="warn" [value]="'knowledge.internalBadge' | transloco" />
                            }
                            <p-tag
                                [severity]="row.isPublished ? 'success' : 'secondary'"
                                [value]="(row.isPublished ? 'knowledge.published' : 'knowledge.draft') | transloco"
                            />
                        </div>
                    </header>

                    <p class="app-page__meta">
                        {{ 'knowledge.articleType.' + row.type | transloco }}
                        · {{ 'knowledge.author' | transloco }}: {{ row.author.displayName }}
                        · <span class="app-ltr-numeric">{{ 'knowledge.updated' | transloco }}: {{ row.updatedAt | date: 'short' }}</span>
                    </p>

                    <!-- Authored content, rendered as authored (A-11). No translation pipe. -->
                    <app-markdown-view [source]="row.body" />
                } @else {
                    <app-loading-state [rowCount]="5" />
                }
            }
        </section>
    `
})
export class KnowledgeReaderComponent {
    private readonly api = inject(KnowledgeClient);
    private readonly route = inject(ActivatedRoute);

    private readonly articleId = this.route.snapshot.paramMap.get('id') ?? '';

    protected readonly article = signal<Article | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    constructor() {
        this.load();
    }

    protected load(): void {
        this.article.set(null);
        this.problem.set(null);

        this.api.read(this.articleId).subscribe({
            next: (row) => this.article.set(row),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
