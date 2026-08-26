import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthStore } from '../auth/auth.store';

/**
 * Attaches `Authorization: Bearer <token>` when one is held (AD-7).
 *
 * Anonymous endpoints are unaffected: sending a header they ignore is harmless, and omitting it
 * for them would mean this interceptor had to know the endpoint catalogue.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
    const token = inject(AuthStore).token();

    if (!token) {
        return next(request);
    }

    return next(
        request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    );
};
