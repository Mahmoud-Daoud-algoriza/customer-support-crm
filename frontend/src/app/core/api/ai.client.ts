import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientBase } from './api-client.base';
import { TicketPriority } from './tickets.client';

/**
 * The label every AI response carries (docs/api-design.md §6.10). **A-8 requires the result to be
 * visibly labelled AI-generated**, and this is the field a screen labels from — never an inference
 * about which endpoint was called.
 */
export type GeneratedBy = 'ai';

/** `POST /tickets/{id}/ai/summary` — §6.10. */
export interface AiSummary {
    summary: string;
    generatedBy: GeneratedBy;
    generatedAt: string;
}

/**
 * `POST /tickets/{id}/ai/suggested-reply` — §6.10.
 *
 * **It is a `draft`, and no endpoint accepts this shape.** The only way it becomes a message is the
 * agent editing it in the composer and pressing Send (A-8, UI-7).
 */
export interface AiSuggestedReply {
    draft: string;
    generatedBy: GeneratedBy;
    generatedAt: string;
}

/** `POST /ai/classification-suggestion` — §6.10. */
export interface AiClassification {
    categoryCode: string;
    priority: TicketPriority;
    generatedBy: GeneratedBy;
    generatedAt: string;
}

/**
 * The three AI assist endpoints of docs/api-design.md §5.8 — and **only** those three.
 *
 * **All three are `POST` because they perform work, not because they mutate**: none of them changes a
 * ticket (§5.8, AD-12). There is deliberately no method here that applies a suggestion — a suggestion
 * is applied by the agent, through the ordinary ticket endpoints.
 *
 * **`503 ai-unavailable` is the contract's unavailability answer** (AP-12), and it is the only place
 * this API uses `503`. A caller renders it as "AI assistance is unavailable" for that one feature and
 * keeps every other control working (T1-F).
 *
 * **There is no `suggested-solutions` method, and there must never be one** — AP-14 puts §7.4 under
 * Knowledge as `GET /tickets/{id}/suggested-articles` (Story 12), because it retrieves rather than
 * generates.
 *
 * **Staff only.** A Customer gets `403`; A-8 excludes customer-facing generation entirely, so nothing
 * under `features/portal/` may import this.
 */
@Injectable({ providedIn: 'root' })
export class AiClient extends ApiClientBase {
    summarize(ticketId: string): Observable<AiSummary> {
        return this.post<AiSummary>(`tickets/${ticketId}/ai/summary`, {});
    }

    suggestReply(ticketId: string): Observable<AiSuggestedReply> {
        return this.post<AiSuggestedReply>(`tickets/${ticketId}/ai/suggested-reply`, {});
    }

    /**
     * **Callable before a ticket exists** — it takes no ticket id, which is what makes "suggest at
     * creation" possible.
     *
     * `isUrgent` is an optional **input** to the suggestion and never sets a priority (A-17).
     */
    suggestClassification(
        subject: string,
        description: string,
        isUrgent?: boolean
    ): Observable<AiClassification> {
        return this.post<AiClassification>('ai/classification-suggestion', {
            subject,
            description,
            ...(isUrgent === undefined ? {} : { isUrgent })
        });
    }
}
