import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AuthStore } from '../auth/auth.store';
import { UserRole, roleRankAtLeast } from '../auth/identity.model';

/**
 * **Guards hide; they do not protect.**
 *
 * Every route rule here mirrors a server rule that is independently enforced: the endpoints behind
 * these routes return `403` to a caller whose role is insufficient, and `401` to one whose account
 * has been deactivated, whatever the router allowed (docs/architecture.md §2.2, §4.2).
 *
 * A statement of the form "the customer can't reach that because the route is guarded" is not an
 * acceptable answer in this codebase. The guards exist so a customer never loads agent screens, not
 * so that agent data is safe.
 */
export const authenticatedGuard: CanActivateFn = async (_route, state) => {
    const store = inject(AuthStore);
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!(await ensureIdentityResolved(store, auth))) {
        // Preserve where they were going, so sign-in can return them there (docs/ui-design.md §9).
        return router.createUrlTree(['/auth/login'], {
            queryParams: state.url && state.url !== '/' ? { returnUrl: state.url } : undefined,
        });
    }

    return true;
};

/**
 * The A-4 hierarchy: a Manager satisfies an `Agent` guard. Mirrors `UserRole.RankAtLeast` on the
 * server so the two cannot disagree about what the hierarchy means.
 */
export function roleAtLeast(minimum: UserRole): CanActivateFn {
    return async (route, state) => {
        const store = inject(AuthStore);
        const router = inject(Router);

        const authenticated = await authenticatedGuard(route, state);
        if (authenticated !== true) {
            return authenticated as UrlTree;
        }

        if (roleRankAtLeast(store.role(), minimum)) {
            return true;
        }

        // A role denial the user can understand, and one the server would issue as 403 anyway
        // (docs/ui-design.md §9).
        return router.createUrlTree(['/403']);
    };
}

/**
 * Resolves the identity if a token is held but the store is empty.
 *
 * **Why the guard does this rather than trusting the app initializer.**
 * `withEnabledBlockingInitialNavigation()` runs the router's initial navigation as an initializer of
 * its own, so on a deep link — a reload straight onto `/admin/users` — a guard can run before the
 * bootstrap `loadMe()` has resolved. Depending on provider ordering for correctness would make a
 * signed-in user bounce to sign-in on any hard refresh. Asking here removes the ordering dependency
 * entirely, and doubles as the recovery path if the identity is ever cleared mid-session.
 */
async function ensureIdentityResolved(store: AuthStore, auth: AuthService): Promise<boolean> {
    if (store.isSignedIn()) {
        return true;
    }

    if (!store.hasToken()) {
        return false;
    }

    // The server decides whether the token is still good — including whether the account is still
    // active (AD-15). A failure clears the store.
    await auth.loadMe();

    return store.isSignedIn();
}
