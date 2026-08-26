import { Routes } from '@angular/router';
import { AuthShellComponent } from './app/layout/auth-shell/auth-shell.component';
import { PortalShellComponent } from './app/layout/portal-shell/portal-shell.component';
import { StaffShellComponent } from './app/layout/staff-shell/staff-shell.component';
import { StatusPageComponent } from './app/shared/components/status-page/status-page.component';

/**
 * The route tree of docs/ui-design.md §2. Every area is a **lazy** child tree behind its own shell
 * (AD-14), so a customer never loads agent screens and an agent never loads administration screens.
 *
 * Story 01 ships the tree and the three status screens; the area route files are empty and are
 * filled by the stories that own each screen. Guards arrive with Story 02 — and a guard only ever
 * hides, it never protects: everything it hides is independently refused by the server.
 */
export const appRoutes: Routes = [
    {
        // TODO Story 02: replace this with the role redirect — Customer -> /portal, staff -> /workspace.
        path: '',
        component: AuthShellComponent,
        children: [
            {
                path: '',
                loadComponent: () => import('./app/features/platform/health-check.component').then((m) => m.HealthCheckComponent)
            }
        ]
    },
    {
        path: 'auth',
        component: AuthShellComponent,
        loadChildren: () => import('./app/features/auth/auth.routes')
    },
    {
        // TODO Story 02: [guard: Agent+]
        path: 'workspace',
        component: StaffShellComponent,
        loadChildren: () => import('./app/features/workspace/workspace.routes')
    },
    {
        // TODO Story 02: [guard: Administrator]
        path: 'admin',
        component: StaffShellComponent,
        loadChildren: () => import('./app/features/admin/admin.routes')
    },
    {
        // TODO Story 02: [guard: Customer]
        path: 'portal',
        component: PortalShellComponent,
        loadChildren: () => import('./app/features/portal/portal.routes')
    },
    { path: '403', component: StatusPageComponent, data: { kind: 'forbidden' } },
    { path: '404', component: StatusPageComponent, data: { kind: 'notFound' } },
    { path: 'error', component: StatusPageComponent, data: { kind: 'error' } },
    { path: '**', redirectTo: '/404' }
];
