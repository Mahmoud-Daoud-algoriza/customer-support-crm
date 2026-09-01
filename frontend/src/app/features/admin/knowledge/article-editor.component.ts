import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import {
    Article,
    ArticleType,
    ArticleVisibility,
    KnowledgeClient
} from '../../../core/api/knowledge.client';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';
import { MarkdownViewComponent } from '../../../shared/components/markdown-view/markdown-view.component';

/**
 * Article editor — `/admin/knowledge/new` and `/admin/knowledge/:id` (docs/ui-design.md §6).
 * Administrator only (A-4).
 *
 * <h3>A plain-text / markdown editor with a preview — and nothing else</h3>
 * **T2-E, §6: no rich text toolbar, no media library, no image upload.** The body is a
 * `<textarea>`; the preview renders the same basic markdown the reader does, through the same
 * component, so what an author sees is what a reader gets. There is no file input on this screen and
 * no endpoint one could post to.
 *
 * <h3>Publish and unpublish are explicit buttons, separate from Save</h3>
 * They mirror the API's action pair exactly (AP-1, docs/api-design.md §6.11): **Save** sends
 * `PATCH` with the four editable fields, and **Publish** and **Unpublish** are their own calls. There is
 * no publish *checkbox*, because `isPublished` is not patchable — sending it would be a `400`, and a
 * control that implied otherwise would be a lie about the contract.
 *
 * <h3>A new article is created as a draft</h3>
 * `POST` never carries `isPublished`, so an article is drafted before it is visible (§6.11). The
 * publish control appears once the article exists, which is also when publishing it means anything.
 *
 * <h3>No version history and no delete control</h3>
 * **Neither exists server-side** (T2-E), so neither is offered here — not disabled, absent.
 *
 * <h3>The body is authored content and is never translated</h3>
 * **A-11.** The editor chrome follows the interface language; the text in the box does not.
 */
@Component({
    selector: 'app-admin-article-editor',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, MarkdownViewComponent, MessageModule, RouterLink, SelectModule, TagModule, TextareaModule, TranslocoModule],
    template: `
        <section class="app-page">
            <a routerLink="/admin/knowledge">{{ 'actions.back' | transloco }}</a>

            @if (loadProblem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else if (loading()) {
                <app-loading-state [rowCount]="5" />
            } @else {
                <header class="app-page__header">
                    <h1 class="app-page__title">
                        {{ (articleId ? 'admin.knowledge.editTitle' : 'admin.knowledge.createTitle') | transloco }}
                    </h1>

                    @if (articleId) {
                        <p-tag
                            [severity]="isPublished() ? 'success' : 'secondary'"
                            [value]="(isPublished() ? 'knowledge.published' : 'knowledge.draft') | transloco"
                        />
                    }
                </header>

                @if (saveProblem(); as failure) {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }

                @if (saved()) {
                    <p-message severity="success" [text]="'admin.knowledge.saved' | transloco" />
                }

                <div class="app-form">
                    <div class="app-field">
                        <label for="article-title">{{ 'knowledge.articleTitle' | transloco }}</label>
                        <input id="article-title" pInputText [(ngModel)]="title" maxlength="200" />
                    </div>

                    <div class="app-field">
                        <label for="article-type">{{ 'knowledge.type' | transloco }}</label>
                        <p-select
                            inputId="article-type"
                            [options]="typeOptions()"
                            [(ngModel)]="type"
                            optionLabel="label"
                            optionValue="value"
                        />
                    </div>

                    <div class="app-field">
                        <label for="article-visibility">{{ 'knowledge.visibility' | transloco }}</label>
                        <p-select
                            inputId="article-visibility"
                            [options]="visibilityOptions()"
                            [(ngModel)]="visibility"
                            optionLabel="label"
                            optionValue="value"
                        />
                        <small class="app-field__help">{{ 'admin.knowledge.visibilityHelp' | transloco }}</small>
                    </div>

                    <!-- Plain markdown. No toolbar, no media library, no upload (T2-E). -->
                    <div class="app-field">
                        <label for="article-body">{{ 'admin.knowledge.body' | transloco }}</label>
                        <textarea
                            id="article-body"
                            pTextarea
                            rows="14"
                            [(ngModel)]="body"
                            (ngModelChange)="draft.set($event)"
                        ></textarea>
                        <small class="app-field__help">{{ 'admin.knowledge.bodyHelp' | transloco }}</small>
                    </div>

                    <div class="app-field">
                        <span class="app-field__label">{{ 'admin.knowledge.preview' | transloco }}</span>
                        <div class="app-markdown-preview">
                            <app-markdown-view [source]="draft()" />
                        </div>
                    </div>

                    <div class="app-form__actions">
                        <p-button
                            [label]="'actions.save' | transloco"
                            [loading]="busy()"
                            [disabled]="busy() || !title.trim() || !draft().trim()"
                            (onClick)="save()"
                        />

                        <!-- The action pair, separate from Save, mirroring the API (AP-1, §6.11). -->
                        @if (articleId) {
                            @if (isPublished()) {
                                <p-button
                                    severity="secondary"
                                    [outlined]="true"
                                    [label]="'admin.knowledge.unpublish' | transloco"
                                    [loading]="busy()"
                                    [disabled]="busy()"
                                    (onClick)="setPublication(false)"
                                />
                            } @else {
                                <p-button
                                    severity="secondary"
                                    [label]="'admin.knowledge.publish' | transloco"
                                    [loading]="busy()"
                                    [disabled]="busy()"
                                    (onClick)="setPublication(true)"
                                />
                            }
                        }
                    </div>
                </div>
            }
        </section>
    `
})
export class AdminArticleEditorComponent {
    private readonly api = inject(KnowledgeClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly transloco = inject(TranslocoService);

    /** Null on `/admin/knowledge/new`; the article's id on the edit route. */
    protected readonly articleId = this.route.snapshot.paramMap.get('id');

    /**
     * Plain fields, not signals: `[(ngModel)]` binds a value, and a signal bound through it would
     * write the signal object rather than its contents. The preview needs a reactive source, so the
     * body has both — the field `ngModel` writes, and {@link draft}, updated from `ngModelChange`.
     */
    protected title = '';
    protected body = '';

    protected readonly draft = signal('');
    protected type: ArticleType = 'Faq';
    protected visibility: ArticleVisibility = 'Public';

    protected readonly isPublished = signal(false);
    protected readonly loading = signal(false);
    protected readonly busy = signal(false);
    protected readonly saved = signal(false);
    protected readonly loadProblem = signal<ApiProblem | null>(null);
    protected readonly saveProblem = signal<ApiProblem | null>(null);

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

    constructor() {
        if (this.articleId) {
            this.load();
        }
    }

    protected load(): void {
        if (!this.articleId) {
            return;
        }

        this.loading.set(true);
        this.loadProblem.set(null);

        this.api.read(this.articleId).subscribe({
            next: (article) => {
                this.apply(article);
                this.loading.set(false);
            },
            error: (failure: ApiProblem) => {
                this.loadProblem.set(failure);
                this.loading.set(false);
            }
        });
    }

    /**
     * `POST` on the create route, `PATCH` on the edit route — **and neither carries
     * `isPublished`** (§6.11). A created article navigates to its own edit route, which is where the
     * publish control lives.
     */
    protected save(): void {
        this.busy.set(true);
        this.saveProblem.set(null);
        this.saved.set(false);

        const payload = {
            title: this.title.trim(),
            body: this.draft(),
            type: this.type,
            visibility: this.visibility
        };

        const request = this.articleId
            ? this.api.update(this.articleId, payload)
            : this.api.create(payload);

        request.subscribe({
            next: (article) => {
                this.busy.set(false);
                this.saved.set(true);

                if (this.articleId) {
                    this.apply(article);
                } else {
                    void this.router.navigate(['/admin/knowledge', article.id]);
                }
            },
            error: (failure: ApiProblem) => {
                this.saveProblem.set(failure);
                this.busy.set(false);
            }
        });
    }

    /** The dedicated action pair — the only way publication state changes (AP-1). */
    protected setPublication(publish: boolean): void {
        if (!this.articleId) {
            return;
        }

        this.busy.set(true);
        this.saveProblem.set(null);
        this.saved.set(false);

        const request = publish
            ? this.api.publish(this.articleId)
            : this.api.unpublish(this.articleId);

        request.subscribe({
            next: (article) => {
                this.apply(article);
                this.busy.set(false);
            },
            error: (failure: ApiProblem) => {
                this.saveProblem.set(failure);
                this.busy.set(false);
            }
        });
    }

    /** The message comes from the problem `type`; the server's `detail` is never shown (§9). */
    protected errorKey(problem: ApiProblem): string {
        return problemTranslationKey(problem);
    }

    private apply(article: Article): void {
        this.title = article.title;
        this.body = article.body;
        this.draft.set(article.body);
        this.type = article.type;
        this.visibility = article.visibility;
        this.isPublished.set(article.isPublished);
    }
}
