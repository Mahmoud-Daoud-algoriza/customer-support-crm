import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiProblem, NETWORK_PROBLEM_TYPE } from '../api/api-problem';

/**
 * Normalizes every failed response into a typed {@link ApiProblem} so no caller ever inspects a
 * raw `HttpErrorResponse`.
 *
 * The `type` slug is what the UI maps to a translated string; the server's `detail` is carried for
 * diagnostics but never rendered raw (docs/ui-design.md §9, T2-J).
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) =>
    next(request).pipe(catchError((error: HttpErrorResponse) => throwError(() => toApiProblem(error))));

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
