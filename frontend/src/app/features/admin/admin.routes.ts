import { Routes } from '@angular/router';

/**
 * The Administrator area (docs/ui-design.md §2, §6). Lazily loaded behind an Administrator guard
 * (AD-14) declared in app.routes.ts.
 *
 * Story 02 delivers the user directory, user detail and the create-user dialog. Knowledge authoring
 * (Story 12), the audit log read surface (Story 16 Part B) and the read-only configuration view
 * (Story 16) arrive with their own stories.
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
];

export default adminRoutes;
