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
 * Story 08 adds `queue` and makes it the area landing;
 * Story 09 adds `notifications`. The rest arrive with Stories 12, 14, 15,
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
        // **Before `tickets/:id`**, or the router would read "new" as a ticket id and the detail
        // screen would request a GUID that does not exist. Story 11 task 6.
        path: 'tickets/new',
        loadComponent: () => import('./tickets/ticket-create.component').then((m) => m.TicketCreateComponent)
    },
    {
        path: 'tickets/:id',
        loadComponent: () => import('./tickets/ticket-detail.component').then((m) => m.TicketDetailComponent)
    },
    {
        path: 'notifications',
        loadComponent: () =>
            import('./notifications/notification-list.component').then((m) => m.NotificationListComponent)
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
