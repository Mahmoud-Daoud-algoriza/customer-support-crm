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
 * `requests` (the list) and `help` are **not** routed yet: Story 13 adds the first and Story 12 the
 * second. **A route is added by the story that builds its screen, so no route is ever a dead link.**
 */
export const portalRoutes: Routes = [
    {
        path: 'requests/new',
        loadComponent: () =>
            import('./portal-submit-request.component').then((m) => m.PortalSubmitRequestComponent)
    },
    {
        path: 'requests/:id',
        loadComponent: () =>
            import('./portal-request-detail.component').then((m) => m.PortalRequestDetailComponent)
    }
];

export default portalRoutes;
