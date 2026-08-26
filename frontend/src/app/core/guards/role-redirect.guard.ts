import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AuthStore } from '../auth/auth.store';

/**
 * The root redirect of docs/ui-design.md §2: Customer -> `/portal`, staff -> `/workspace`.
 *
 * This replaces Story 01's temporary `HealthCheckComponent` route, exactly as that story said it
 * would.
 *
 * It resolves the identity first for the same reason `authenticatedGuard` does: on a cold load the
 * router's initial navigation can reach here before the bootstrap `loadMe()` has finished.
 */
export const roleRedirectGuard: CanActivateFn = async () => {
    const store = inject(AuthStore);
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!store.isSignedIn() && store.hasToken()) {
        await auth.loadMe();
    }

    if (!store.isSignedIn()) {
        return router.createUrlTree(['/auth/login']);
    }

    return router.createUrlTree([store.role() === 'Customer' ? '/portal' : '/workspace']);
};
