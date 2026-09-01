import { Routes } from '@angular/router';

/**
 * The Customer area (docs/ui-design.md §2): my requests, submit a request, request detail, help.
 * Lazily loaded behind a Customer guard (AD-14).
 *
 * <h3>Story 07's two routes are stubs, and that is deliberate</h3>
 * Story 07 owns the **endpoints** of docs/api-design.md §5.7 that submission and messaging need;
 * **Story 13 owns the designed screens** of §7.2 and §7.3 and replaces both components in place,
 * including the cancel and reopen controls, attachments and the feedback control. They are routed
 * now so the two endpoints are exercised end to end by the story that publishes them, rather than
 * first being called by the story that also has to get the layout right.
 *
 * `help` and `help/:id` are **Story 12's**, and are routed here now that the screens exist.
 * `requests` (the list) is still Story 13's. **A route is added by the story that builds its screen,
 * so no route is ever a dead link.**
 */
export const portalRoutes: Routes = [
    {
        // The area landing route, for the same reason the workspace one exists: `/portal` had no
        // empty-path child, so the root redirect of §2 sent customers here and the navigation fell
        // through to the root `**` — every customer sign-in landed on `/404`.
        //
        // It points at *submit a request* because that is the only customer screen that exists:
        // `requests` (the list) is Story 13's and is deliberately still unrouted. **Story 13
        // repoints this at `requests`**, which is the §7.1 landing the design actually specifies.
        path: '',
        pathMatch: 'full',
        redirectTo: 'requests/new'
    },
    {
        path: 'requests/new',
        loadComponent: () =>
            import('./portal-submit-request.component').then((m) => m.PortalSubmitRequestComponent)
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
    },
    {
        path: 'requests/:id',
        loadComponent: () =>
            import('./portal-request-detail.component').then((m) => m.PortalRequestDetailComponent)
    }
];

export default portalRoutes;
