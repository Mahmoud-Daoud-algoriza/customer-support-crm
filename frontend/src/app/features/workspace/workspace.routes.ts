import { Routes } from '@angular/router';

/**
 * The Agent and Manager area (docs/ui-design.md §2): queue, tickets, customers, knowledge, reports,
 * notifications. Lazily loaded behind the Agent+ guard the parent route applies (AD-14).
 *
 * **Guards hide; they do not protect.** Every endpoint behind these screens independently returns
 * `403` to a caller whose role is insufficient (docs/architecture.md §4.2) — a Customer cannot
 * browse the customer directory because `GET /customers` refuses them, not because this file does.
 *
 * Story 04 adds `customers` and `customers/:id`; Story 05 adds `tickets` and `tickets/:id`;
 * Story 08 adds `queue` and makes it the area landing. The rest arrive with Stories 09, 12, 14, 15,
 * each added by the story that builds the screen so no route is ever a dead link.
 */
export const workspaceRoutes: Routes = [
    {
        // **The staff landing route** (UI-2, docs/ui-design.md §5.1). It pointed at `tickets` as a
        // corrective measure while My queue did not exist; now that it does, this is the
        // destination the design always specified.
        path: '',
        pathMatch: 'full',
        redirectTo: 'queue'
    },
    {
        path: 'queue',
        loadComponent: () => import('./tickets/agent-queue.component').then((m) => m.AgentQueueComponent)
    },
    {
        path: 'tickets',
        loadComponent: () => import('./tickets/ticket-list.component').then((m) => m.TicketListComponent)
    },
    {
        path: 'tickets/:id',
        loadComponent: () => import('./tickets/ticket-detail.component').then((m) => m.TicketDetailComponent)
    },
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
