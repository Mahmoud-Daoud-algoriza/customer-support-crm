import { Routes } from '@angular/router';

/**
 * Unauthenticated routes — `/auth/login` and `/auth/register` (docs/ui-design.md §2).
 *
 * Registration is scaffolded with its submit disabled; the endpoint arrives with Story 04 (S9-7).
 */
export const authRoutes: Routes = [
    { path: '', pathMatch: 'full', redirectTo: 'login' },
    {
        path: 'login',
        loadComponent: () => import('./login/login.component').then((m) => m.LoginComponent),
    },
    {
        path: 'register',
        loadComponent: () => import('./register/register.component').then((m) => m.RegisterComponent),
    },
];

export default authRoutes;
