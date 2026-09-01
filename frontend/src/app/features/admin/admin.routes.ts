import { Routes } from '@angular/router';

/**
 * The Administrator area (docs/ui-design.md §2, §6). Lazily loaded behind an Administrator guard
 * (AD-14) declared in app.routes.ts.
 *
 * Story 02 delivers the user directory, user detail and the create-user dialog. Story 16 Part B
 * delivers the audit log and the read-only configuration view. Knowledge authoring (Story 12)
 * arrives with its own story.
 */
export const adminRoutes: Routes = [
    { path: '', pathMatch: 'full', redirectTo: 'users' },
    {
        path: 'users',
        loadComponent: () =>
            import('./users/user-directory.component').then((m) => m.UserDirectoryComponent),
    },
    {
        path: 'users/:id',
        loadComponent: () => import('./users/user-detail.component').then((m) => m.UserDetailComponent),
    },
    {
        path: 'audit',
        loadComponent: () => import('./audit/audit-log.component').then((m) => m.AuditLogComponent),
    },
    {
        path: 'configuration',
        loadComponent: () =>
            import('./configuration/configuration.component').then((m) => m.ConfigurationComponent),
    },
];

export default adminRoutes;
