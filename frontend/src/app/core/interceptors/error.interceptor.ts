import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { ApiProblem, NETWORK_PROBLEM_TYPE, problemTranslationKey } from '../api/api-problem';
import { AuthStore } from '../auth/auth.store';

/**
 * The one place a failed HTTP response is turned into a decision.
 *
 * Two jobs, in order:
 *
 * 1. **Normalize** every failure into a typed {@link ApiProblem}, so no caller ever inspects a raw
 *    `HttpErrorResponse`. The `type` slug is what the UI maps to a translated string; the server's
 *    `detail` is carried for diagnostics but never rendered raw (docs/ui-design.md §9, T2-J, AP-2).
 * 2. **Apply the cross-cutting half of the docs/ui-design.md §9 status table** — the rows that are
 *    the same on every screen — and *only* that half.
 *
 * | Status | Handled here | Why |
 * |---|---|---|
 * | `401` | Clear the session, redirect to sign-in with the return URL | The session ended; no screen can do anything useful |
 * | `403` | Route to `/403`. **The session is untouched** | A role denial is not an expired session — signing the user out would be wrong and infuriating |
 * | `400`, `409`, `413`, `422`, `404` | **Nothing.** Passed through untouched | §9 puts each of these *inline, in context*: on the offending field, on the uploader, on the ticket. Only the feature knows where |
 * | `503` | **Nothing.** Passed through untouched | §9 scopes it to the AI panel — *"the rest of the screen stays live"* — so a global surface would contradict it |
 * | other `5xx`, network | One translated toast | The request never produced a usable answer, and every screen would otherwise re-implement the same sentence |
 *
 * **The problem is always rethrown**, including in the rows this interceptor acts on. Centralizing
 * the reaction must not swallow the error: a feature form still needs the `409` slug and the
 * `errors` dictionary to render the message beside the field that caused it.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
    const store = inject(AuthStore);
    const router = inject(Router);
    const messages = inject(MessageService);
    const transloco = inject(TranslocoService);

    return next(request).pipe(
        catchError((error: HttpErrorResponse) => {
            const problem = toApiProblem(error);

            if (problem.status === 401) {
                handleSessionEnded(request, store, router);
            } else if (problem.status === 403) {
                handleForbidden(router);
            } else if (isUnusableResponse(problem)) {
                showToast(problem, messages, transloco);
            }

            // Always. See the class remarks: centralizing the reaction must not swallow the error.
            return throwError(() => problem);
        })
    );
};

/**
 * The session ended — an expired token, or a user deactivated or deleted since it was issued
 * (docs/api-design.md §4.1, AD-15). Clear the token and send them to sign-in, preserving where they
 * were (docs/ui-design.md §9).
 *
 * **Anonymous auth endpoints are excluded.** A wrong password on `POST /auth/login` is a `401`, but
 * it is a *form error to render in place* — there is no session to end, and redirecting to the page
 * the user is already on would replace the message with a blank form. The same holds for any other
 * anonymous auth endpoint, which is why the check is a list rather than a single URL.
 */
function handleSessionEnded(request: HttpRequest<unknown>, store: AuthStore, router: Router): void {
    if (isAnonymousAuthEndpoint(request.url)) {
        return;
    }

    store.clear();

    const returnUrl = router.url;

    // Already on the sign-in screen: clearing was right, navigating again is not. Without this a
    // failed background call while sitting on /auth/login would discard a half-typed form.
    if (returnUrl.startsWith('/auth/')) {
        return;
    }

    void router.navigate(['/auth/login'], {
        queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : undefined
    });
}

/**
 * A role denial the user can understand (docs/ui-design.md §9), shown through the `/403` route that
 * already exists — the same destination `roleAtLeast` sends them to, so a denial reads identically
 * whether the router caught it or the server did.
 *
 * **The session is deliberately not cleared.** `403` means "authenticated, and not allowed"; the
 * token is still perfectly valid, and signing the user out would lose their session over a link
 * they should not have been shown.
 */
function handleForbidden(router: Router): void {
    // Guard against a redirect loop if the /403 screen itself ever issues a request.
    if (router.url.startsWith('/403')) {
        return;
    }

    void router.navigate(['/403']);
}

/**
 * A response no screen can render something meaningful from: the server broke, or it was never
 * reached at all.
 *
 * **`503` is excluded on purpose.** docs/ui-design.md §9 gives it exactly one presentation — *"only
 * the AI panel; the rest of the screen stays live"* — and AP-12 makes graceful degradation of one
 * panel the visible form of that. A global toast would contradict both.
 */
function isUnusableResponse(problem: ApiProblem): boolean {
    return problem.status === 0 || (problem.status >= 500 && problem.status !== 503);
}

/**
 * The translated sentence, never the server's prose (T2-J). An unmapped slug — `http-502`, say —
 * falls back to the generic server-error string rather than surfacing a code to the user.
 */
function showToast(problem: ApiProblem, messages: MessageService, transloco: TranslocoService): void {
    const key = problemTranslationKey(problem);
    const translated = transloco.translate(key);

    messages.add({
        severity: 'error',
        detail: translated === key ? transloco.translate(GENERIC_ERROR_KEY) : translated,
        life: 6000
    });
}

/** Present in every dictionary, so the fallback above always resolves to a sentence. */
const GENERIC_ERROR_KEY = 'errors.internal-error';

/**
 * Endpoints reachable without a session, where a `401` is the endpoint's answer rather than the end
 * of a session. Matched on the path so the API base URL can change without breaking the rule.
 */
const ANONYMOUS_AUTH_PATHS = ['/auth/login', '/auth/register'] as const;

function isAnonymousAuthEndpoint(url: string): boolean {
    const path = url.split('?')[0].replace(/\/$/, '');

    return ANONYMOUS_AUTH_PATHS.some((suffix) => path.endsWith(suffix));
}

function toApiProblem(error: HttpErrorResponse): ApiProblem {
    // status 0 means the request never reached the server, so there is no Problem Details body.
    if (error.status === 0) {
        return {
            type: NETWORK_PROBLEM_TYPE,
            title: 'The server could not be reached.',
            status: 0
        };
    }

    const body = (error.error ?? {}) as Partial<ApiProblem>;

    return {
        type: typeof body.type === 'string' && body.type.length > 0 ? body.type : `http-${error.status}`,
        title: body.title ?? error.statusText,
        status: body.status ?? error.status,
        detail: body.detail,
        instance: body.instance,
        errors: body.errors,
        allowedTransitions: body.allowedTransitions
    };
}
