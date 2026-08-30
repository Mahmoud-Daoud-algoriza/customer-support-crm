import { Routes } from '@angular/router';

/**
 * The Agent and Manager area (docs/ui-design.md §2): queue, tickets, customers, knowledge, reports,
 * notifications. Lazily loaded behind the Agent+ guard the parent route applies (AD-14).
 *
 * **Guards hide; they do not protect.** Every endpoint behind these screens independently returns
 * `403` to a caller whose role is insufficient (docs/architecture.md §4.2) — a Customer cannot
 * browse the customer directory because `GET /customers` refuses them, not because this file does.
 *
 * Story 04 adds `customers` and `customers/:id`. The rest arrive with Stories 05–09, 12, 14, 15,
 * each added by the story that builds the screen so no route is ever a dead link.
 */
export const workspaceRoutes: Routes = [
    {
        path: 'customers',
        loadComponent: () => import('./customers/customer-directory.component').then((m) => m.CustomerDirectoryComponent)
    },
    {
        path: 'customers/:id',
        loadComponent: () => import('./customers/customer-detail.component').then((m) => m.CustomerDetailComponent)
    }
];

export default workspaceRoutes;
