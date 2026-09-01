import { Routes } from '@angular/router';

/**
 * The Customer area (docs/ui-design.md §2): my requests, submit a request, request detail, help.
 * Lazily loaded behind a Customer guard (AD-14).
 *
 * **Story 13 completed this area.** Story 07 routed two screens as stubs so its endpoints were
 * exercised end to end; Story 12 added `help` and `help/:id`; Story 13 replaced both stubs with the
 * designed screens of §7.2 and §7.3 and added the `requests` list of §7.1. **Every route here now
 * points at a screen the design specifies, and §7 names no fifth one.**
 *
 * **A route is added by the story that builds its screen**, so no route is ever a dead link.
 */
export const portalRoutes: Routes = [
    {
        // **My requests is the landing screen** (docs/ui-design.md §7.1). Story 07 pointed this at
        // `requests/new` because the list did not exist and a customer signing in fell through to
        // the root `**` and landed on `/404`; Story 13 repoints it where the design says, which is
        // the list — a returning customer is far more often tracking a request than opening one.
        path: '',
        pathMatch: 'full',
        redirectTo: 'requests'
    },
    {
        path: 'requests',
        loadComponent: () =>
            import('./portal-requests.component').then((m) => m.PortalRequestsComponent)
    },
    {
        // Before `requests/:id`, so the literal wins over the parameter.
        path: 'requests/new',
        loadComponent: () =>
            import('./portal-submit-request.component').then((m) => m.PortalSubmitRequestComponent)
    },
    {
        path: 'requests/:id',
        loadComponent: () =>
            import('./portal-request-detail.component').then((m) => m.PortalRequestDetailComponent)
    },
    {
        // Story 12 — the customer's knowledge base (docs/ui-design.md §7.4). Public, published
        // articles only; an internal or unpublished id answers `404` (AP-4).
        path: 'help',
        loadComponent: () => import('./portal-help.component').then((m) => m.PortalHelpComponent)
    },
    {
        path: 'help/:id',
        loadComponent: () =>
            import('./portal-help-article.component').then((m) => m.PortalHelpArticleComponent)
    }
];

export default portalRoutes;
