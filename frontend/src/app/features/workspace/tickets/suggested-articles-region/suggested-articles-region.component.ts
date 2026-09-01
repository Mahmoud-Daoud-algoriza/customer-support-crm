import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../../core/api/api-problem';
import { KnowledgeClient, SuggestedArticle } from '../../../../core/api/knowledge.client';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../../shared/components/loading-state/loading-state.component';

/**
 * The **suggested articles** region of the ticket detail — requirements §7.4,
 * docs/ui-design.md §5.3. It fills the slot Story 05 left, below the AI panel.
 *
 * <h3>It sits next to the AI panel, which is exactly why it must not look like one</h3>
 * **AP-14 and T2-E.** These are **existing articles retrieved by keyword** from the ticket's subject
 * and description (AD-13) — nothing was written for this ticket and nothing was generated. The
 * heading says *related articles*, a line under it says they were **found by keyword**, and each row
 * shows the **match score** the database produced. There is **no sparkle icon, no "AI-generated"
 * label and no dismiss-a-suggestion affordance**, because none of those would be true here: the
 * *AI-generated* label belongs to `AiAssistPanel` and marks something this component never produces.
 *
 * <h3>Rows link to the reader, not into the composer</h3>
 * A suggestion is something to read, so a row is a link to `/workspace/knowledge/:id`. There is
 * deliberately no *insert into reply*: pasting an article body into a customer reply is not what
 * §7.4 asks for, and the one insertion point belongs to the AI draft (UI-7).
 *
 * <h3>It loads independently</h3>
 * Its own loading, empty and error states (§9), so a slow retrieval never blanks the ticket. No
 * matches is an ordinary empty state — an empty region is never an error.
 */
@Component({
    selector: 'app-suggested-articles-region',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [EmptyStateComponent, ErrorStateComponent, LoadingStateComponent, RouterLink, TagModule, TranslocoModule],
    template: `
        <section class="app-region app-suggested">
            <h2 class="app-region__title">{{ 'knowledge.suggested.title' | transloco }}</h2>

            <!-- The sentence that keeps this region honest: retrieved, not generated (AP-14). -->
            <p class="app-suggested__note">{{ 'knowledge.suggested.retrievedNote' | transloco }}</p>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (articles(); as rows) {
                    @if (rows.length === 0) {
                        <app-empty-state
                            [title]="'knowledge.suggested.emptyTitle' | transloco"
                            [message]="'knowledge.suggested.emptyMessage' | transloco"
                            icon="pi-book"
                        />
                    } @else {
                        <ul class="app-suggested__list">
                            @for (article of rows; track article.id) {
                                <li class="app-suggested__item">
                                    <a [routerLink]="['/workspace/knowledge', article.id]">{{ article.title }}</a>

                                    <span class="app-suggested__meta">
                                        {{ 'knowledge.articleType.' + article.type | transloco }}
                                        ·
                                        <!-- The database's own ranking, shown as what it is. -->
                                        <span class="app-ltr-numeric">
                                            {{ 'knowledge.suggested.matchScore' | transloco }}: {{ article.matchScore }}
                                        </span>
                                    </span>
                                </li>
                            }
                        </ul>
                    }
                } @else {
                    <app-loading-state [rowCount]="3" />
                }
            }
        </section>
    `
})
export class SuggestedArticlesRegionComponent {
    private readonly api = inject(KnowledgeClient);

    readonly ticketId = input.required<string>();

    protected readonly articles = signal<SuggestedArticle[] | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    constructor() {
        effect(() => {
            // Reading the input inside the effect is what re-runs the retrieval when the screen is
            // pointed at a different ticket.
            this.ticketId();
            this.load();
        });
    }

    protected load(): void {
        this.articles.set(null);
        this.problem.set(null);

        this.api.suggested(this.ticketId()).subscribe({
            next: (rows) => this.articles.set(rows),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
