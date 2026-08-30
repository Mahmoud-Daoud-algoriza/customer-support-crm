/** The four fixed roles of A-4. The array order is the hierarchy. */
export const ROLES = ['Customer', 'Agent', 'Manager', 'Administrator'] as const;

export type UserRole = (typeof ROLES)[number];

/**
 * `Identity` — docs/api-design.md §6.1. The per-request resolved values (AP-9), not token claims:
 * a role change made by an Administrator shows up here without a new token.
 */
export interface Identity {
    id: string;
    displayName: string;
    email: string;
    role: UserRole;
    departmentId?: string | null;
    branchId?: string | null;
    customerId?: string | null;
    isActive: boolean;
}

/** `AuthToken` — docs/api-design.md §6.1. */
export interface AuthToken {
    accessToken: string;
    expiresAt: string;
    user: Identity;
}

/** `User` — docs/api-design.md §6.1. Note the absence of `passwordHash`; it is never returned. */
export interface UserRow {
    id: string;
    email: string;
    displayName: string;
    role: UserRole;
    departmentId?: string | null;
    branchId?: string | null;
    isActive: boolean;
    createdAt: string;
}

/**
 * `UserSummary` — docs/api-design.md §6.1: *"embedded wherever a person is referenced"*. A note's
 * author, an attachment's uploader, a timeline entry's actor — all of them are this and nothing
 * more, because §6 says a display name beside an id is a projection, not a second call and not a
 * second entity.
 */
export interface UserSummary {
    id: string;
    displayName: string;
}

/**
 * The A-4 hierarchy check, mirroring `UserRole.RankAtLeast` on the server.
 * It decides what to *show*; the server independently decides what is *allowed*.
 */
export function roleRankAtLeast(role: UserRole | undefined, minimum: UserRole): boolean {
    if (!role) {
        return false;
    }

    return ROLES.indexOf(role) >= ROLES.indexOf(minimum);
}
