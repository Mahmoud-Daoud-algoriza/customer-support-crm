import { UserRole } from '../../../core/auth/identity.model';

/**
 * The roles selectable in user administration.
 *
 * **`Customer` is deliberately absent** (docs/ui-design.md §6, DM-1). Customers arrive through
 * registration or by an agent creating a profile, never through `POST /users` — and the server
 * rejects the role outright, so this list is a convenience that matches the rule rather than a
 * control that implements it.
 */
export const STAFF_ROLES: readonly UserRole[] = ['Agent', 'Manager', 'Administrator'];

export const STAFF_ROLE_OPTIONS = STAFF_ROLES.map((value) => ({ label: value, value }));
