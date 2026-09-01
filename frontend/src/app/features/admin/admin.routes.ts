import { Routes } from '@angular/router';

/**
 * The Administrator area (docs/ui-design.md §2, §6). Lazily loaded behind an Administrator guard
 * (AD-14) declared in app.routes.ts.
 *
 * Story 02 delivers the user directory, user detail and the create-user dialog. Story 16 Part B
 * delivers the audit log and the read-only configuration view. Story 12 delivers knowledge
 * authoring — a list, a create route and an editor, with **no delete route and no version-history
 * route**, because neither exists server-side (T2-E, docs/ui-design.md §6).
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
        path: 'knowledge',
        loadComponent: () =>
            import('./knowledge/article-list.component').then((m) => m.AdminArticleListComponent),
    },
    {
        // **Before `knowledge/:id`**, or the router would read "new" as an article id and the
        // editor would request a GUID that does not exist.
        path: 'knowledge/new',
        loadComponent: () =>
            import('./knowledge/article-editor.component').then((m) => m.AdminArticleEditorComponent),
    },
    {
        path: 'knowledge/:id',
        loadComponent: () =>
            import('./knowledge/article-editor.component').then((m) => m.AdminArticleEditorComponent),
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
