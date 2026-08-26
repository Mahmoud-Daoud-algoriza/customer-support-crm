import { Routes } from '@angular/router';
import { authenticatedGuard, roleAtLeast } from './app/core/guards/auth.guards';
import { roleRedirectGuard } from './app/core/guards/role-redirect.guard';
import { AuthShellComponent } from './app/layout/auth-shell/auth-shell.component';
import { PortalShellComponent } from './app/layout/portal-shell/portal-shell.component';
import { StaffShellComponent } from './app/layout/staff-shell/staff-shell.component';
import { StatusPageComponent } from './app/shared/components/status-page/status-page.component';

/**
 * The route tree of docs/ui-design.md §2. Every area is a **lazy** child tree behind its own shell
 * (AD-14), so a customer never loads agent screens and an agent never loads administration screens.
 *
 * **Every guard here mirrors a server rule that is independently enforced.** The guards decide what
 * is *shown*; the endpoints decide what is *allowed*, and they return `403` or `401` regardless of
 * what the router permitted (docs/architecture.md §2.2, §4.2).
 */
export const appRoutes: Routes = [
    {
        // Redirect by role: Customer -> /portal, staff -> /workspace (docs/ui-design.md §2).
        // This replaces Story 01's temporary health-check landing route.
        path: '',
        pathMatch: 'full',
        canActivate: [roleRedirectGuard],
        children: [],
    },
    {
        path: 'auth',
        component: AuthShellComponent,
        loadChildren: () => import('./app/features/auth/auth.routes'),
    },
    {
        path: 'workspace',
        component: StaffShellComponent,
        canActivate: [roleAtLeast('Agent')],
        loadChildren: () => import('./app/features/workspace/workspace.routes'),
    },
    {
        path: 'admin',
        component: StaffShellComponent,
        canActivate: [roleAtLeast('Administrator')],
        loadChildren: () => import('./app/features/admin/admin.routes'),
    },
    {
        path: 'portal',
        component: PortalShellComponent,
        canActivate: [authenticatedGuard],
        loadChildren: () => import('./app/features/portal/portal.routes'),
    },

    // The temporary Story 01 diagnostic, kept reachable as a named route rather than deleted: it is
    // the only screen that reports database connectivity, and it costs one lazy chunk.
    {
        path: 'status',
        component: StaffShellComponent,
        canActivate: [roleAtLeast('Administrator')],
        children: [
            {
                path: '',
                loadComponent: () =>
                    import('./app/features/platform/health-check.component').then((m) => m.HealthCheckComponent),
            },
        ],
    },

    { path: '403', component: StatusPageComponent, data: { kind: 'forbidden' } },
    { path: '404', component: StatusPageComponent, data: { kind: 'notFound' } },
    { path: 'error', component: StatusPageComponent, data: { kind: 'error' } },
    { path: '**', redirectTo: '/404' },
];
