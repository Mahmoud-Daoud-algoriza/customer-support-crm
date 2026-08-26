import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { ApiProblem, NETWORK_PROBLEM_TYPE } from '../api/api-problem';
import { AuthStore } from '../auth/auth.store';

/**
 * Normalizes every failed response into a typed {@link ApiProblem} so no caller ever inspects a
 * raw `HttpErrorResponse`.
 *
 * The `type` slug is what the UI maps to a translated string; the server's `detail` is carried for
 * diagnostics but never rendered raw (docs/ui-design.md §9, T2-J).
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
    const store = inject(AuthStore);
    const router = inject(Router);

    return next(request).pipe(
        catchError((error: HttpErrorResponse) => {
            const problem = toApiProblem(error);

            // 401 means the session ended — an expired token, or a user deactivated or deleted
            // since it was issued (docs/api-design.md §4.1). Clear the token and send them to
            // sign-in preserving the return URL (docs/ui-design.md §9).
            //
            // The sign-in request itself is excluded: a wrong password is a form error to render in
            // place, not a session that ended.
            if (problem.status === 401 && !isLoginRequest(request.url)) {
                store.clear();

                const returnUrl = router.url;
                void router.navigate(['/auth/login'], {
                    queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : undefined,
                });
            }

            return throwError(() => problem);
        })
    );
};

function isLoginRequest(url: string): boolean {
    return url.endsWith('/auth/login');
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
