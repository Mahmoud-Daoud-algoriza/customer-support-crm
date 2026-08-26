import { Routes } from '@angular/router';

/**
 * The Agent and Manager area (docs/ui-design.md §2): queue, tickets, customers, knowledge, reports,
 * notifications. Lazily loaded behind an Agent+ guard (AD-14) — the guard arrives with Story 02.
 *
 * Empty in Story 01. Filled by Stories 05–09, 12, 14, 15.
 */
export const workspaceRoutes: Routes = [];

export default workspaceRoutes;
