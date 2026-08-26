import { Routes } from '@angular/router';

/**
 * The Administrator area (docs/ui-design.md §2): users, knowledge authoring, audit log, effective
 * configuration. Lazily loaded behind an Administrator guard (AD-14) — the guard arrives with
 * Story 02.
 *
 * Empty in Story 01. Filled by Stories 02, 12, 16.
 */
export const adminRoutes: Routes = [];

export default adminRoutes;
