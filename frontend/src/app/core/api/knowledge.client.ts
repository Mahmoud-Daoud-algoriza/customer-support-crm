import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { UserSummary } from '../auth/identity.model';
import { ApiClientBase, QueryValue } from './api-client.base';
import { Paged, PageRequest } from './paged';

/** The one article concept, distinguished by a type (T2-E) — never three subsystems. */
export type ArticleType = 'Faq' | 'HelpArticle' | 'SolutionGuide';

/**
 * `Public` reaches the portal **once published**; `Internal` never does, published or not
 * (docs/data-model.md §5 constraint 19). The two are independent facts.
 */
export type ArticleVisibility = 'Public' | 'Internal';

/** `Article` — docs/api-design.md §6.5, the shape of `GET /kb/articles/{id}`. */
export interface Article {
    id: string;
    title: string;
    body: string;
    type: ArticleType;
    visibility: ArticleVisibility;
    isPublished: boolean;
    author: UserSummary;
    createdAt: string;
    updatedAt: string;
}

/** `ArticleListItem` — §6.5. **No body**, so a list does not ship every article's full text. */
export interface ArticleListItem {
    id: string;
    title: string;
    type: ArticleType;
    visibility: ArticleVisibility;
    isPublished: boolean;
    updatedAt: string;
}

/**
 * `Portal article` — §6.5. **No `visibility`, no `isPublished`, no author.**
 *
 * **This is not `Article` with fields omitted, on purpose.** AP-5 separates the path spaces, and one
 * shared interface would make the narrowing an optional field a portal component could read anyway.
 */
export interface PortalArticle {
    id: string;
    title: string;
    body: string;
    type: ArticleType;
    updatedAt: string;
}

/**
 * `SuggestedArticle` — `GET /tickets/{id}/suggested-articles`, §6.5.
 *
 * **`matchScore` is the database's own text-match ranking** (AD-13), exposed so a screen can order
 * results — a query artefact, not a stored field. **There is no `generatedBy` here**, because
 * nothing was generated: these are existing articles retrieved by keyword (AP-14).
 */
export interface SuggestedArticle {
    id: string;
    title: string;
    type: ArticleType;
    matchScore: number;
}

/** Filters for `GET /kb/articles` — §5.9. Their names mirror the API exactly (UI-9). */
export interface ArticleListFilter extends PageRequest {
    q?: string | null;
    type?: ArticleType | null;
    visibility?: ArticleVisibility | null;
    isPublished?: boolean | null;
}

/** `POST /kb/articles` — §6.11. `isPublished` is **not** sent: a new article is always a draft. */
export interface CreateArticleRequest {
    title: string;
    body: string;
    type: ArticleType;
    visibility: ArticleVisibility;
}

/**
 * `PATCH /kb/articles/{id}` — §6.11. **`isPublished` is absent, and its absence is the contract**:
 * publication changes through {@link KnowledgeClient.publish} and
 * {@link KnowledgeClient.unpublish} alone, and a body carrying it is a `400`.
 */
export interface PatchArticleRequest {
    title?: string;
    body?: string;
    type?: ArticleType;
    visibility?: ArticleVisibility;
}

/**
 * The typed client for the knowledge base — docs/api-design.md §5.9.
 *
 * <h3>Search is search, not an AI answer</h3>
 * Every method here reads or writes articles. **`suggested()` belongs to this client and not to
 * `AiClient`** (**AP-14**): suggested solutions *retrieve* existing articles by keyword (AD-13)
 * rather than generating text, so putting them beside the assists would misdescribe them in the one
 * place a developer looks first.
 *
 * <h3>There is no delete method, and there must never be one</h3>
 * No delete endpoint exists server-side (T2-E, docs/ui-design.md §6), and neither does versioning,
 * a review workflow or scheduled publishing.
 *
 * <h3>The portal reads are a different client</h3>
 * `PortalKnowledgeClient` below — AP-5 keeps the two path spaces apart, and **no DTO is shared
 * between them**.
 */
@Injectable({ providedIn: 'root' })
export class KnowledgeClient extends ApiClientBase {
    /** `GET /kb/articles` — **all** articles, internal included: staff see both (§5.9). */
    search(filter: ArticleListFilter): Observable<Paged<ArticleListItem>> {
        return this.get<Paged<ArticleListItem>>('kb/articles', filter as Record<string, QueryValue>);
    }

    /** `GET /kb/articles/{id}`. */
    read(id: string): Observable<Article> {
        return this.get<Article>(`kb/articles/${id}`);
    }

    /** `POST /kb/articles` — Administrator only (A-4). The result is **unpublished** (§6.11). */
    create(request: CreateArticleRequest): Observable<Article> {
        return this.post<Article>('kb/articles', request);
    }

    /** `PATCH /kb/articles/{id}` — Administrator only. Never carries `isPublished`. */
    update(id: string, request: PatchArticleRequest): Observable<Article> {
        return this.patch<Article>(`kb/articles/${id}`, request);
    }

    /** `POST /kb/articles/{id}/publish` — the one path publication changes through (AP-1). */
    publish(id: string): Observable<Article> {
        return this.post<Article>(`kb/articles/${id}/publish`);
    }

    /** `POST /kb/articles/{id}/unpublish`. */
    unpublish(id: string): Observable<Article> {
        return this.post<Article>(`kb/articles/${id}/unpublish`);
    }

    /**
     * `GET /tickets/{id}/suggested-articles` — requirements §7.4, **retrieval, not generation**.
     *
     * It is on this client because it is a Knowledge endpoint (**AP-14**). It returns a short ranked
     * list rather than a paged envelope (§6.5), and no matches is an empty array, not an error.
     */
    suggested(ticketId: string): Observable<SuggestedArticle[]> {
        return this.get<SuggestedArticle[]>(`tickets/${ticketId}/suggested-articles`);
    }
}

/**
 * The **customer** knowledge path space — `GET /portal/kb/articles` and
 * `/portal/kb/articles/{id}` (docs/api-design.md §5.9, AP-5).
 *
 * <h3>Public and published only, and the server decides</h3>
 * There is no `visibility` or `isPublished` parameter here because a customer has no choice to make:
 * the rule is server-side, in one place, and an internal or unpublished article answers **`404`, not
 * `403`** (AP-4). A screen must render that `404` with the **same wording as a missing article**
 * (docs/ui-design.md §9) — distinguishing them in the UI would undo the reason AP-4 exists.
 *
 * <h3>No write method of any kind</h3>
 * Authoring is Administrator-only (A-4). Not "not yet" — never.
 */
@Injectable({ providedIn: 'root' })
export class PortalKnowledgeClient extends ApiClientBase {
    /** `GET /portal/kb/articles` — the customer's search over public, published articles. */
    search(filter: { q?: string | null } & PageRequest): Observable<Paged<PortalArticle>> {
        return this.get<Paged<PortalArticle>>(
            'portal/kb/articles',
            filter as Record<string, QueryValue>
        );
    }

    /** `GET /portal/kb/articles/{id}` — missing, internal and unpublished are one answer: `404`. */
    read(id: string): Observable<PortalArticle> {
        return this.get<PortalArticle>(`portal/kb/articles/${id}`);
    }
}
