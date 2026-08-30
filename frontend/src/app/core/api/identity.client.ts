import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Paged, PageRequest } from './paged';
import { ApiClientBase, QueryValue } from './api-client.base';
import { AuthToken, Identity, UserRole, UserRow } from '../auth/identity.model';

/** `POST /users` — docs/api-design.md §5.3. */
export interface CreateUserRequest {
    email: string;
    password: string;
    displayName: string;
    role: UserRole;
    departmentId: string;
    branchId?: string | null;
}

/** `PATCH /users/{id}` — exactly four patchable fields (docs/api-design.md §5.3). */
export interface PatchUserRequest {
    displayName?: string;
    role?: UserRole;
    departmentId?: string;
    branchId?: string | null;
}

/**
 * `POST /auth/register` — docs/api-design.md §5.2. **Exactly four fields, one of them optional.**
 *
 * There is **no branch and no role**, and neither may be added: A-15 fixes both server-side — role
 * is always `Customer`, branch is always the configured default. A request carrying either is a
 * `400` (AP-10), so the omission here is the contract, not a convenience.
 */
export interface RegisterRequest {
    email: string;
    password: string;
    fullName: string;
    phone?: string | null;
}

/** `GET /users` filters — docs/api-design.md §5.3. Names mirror the API exactly. */
export interface UserListFilter extends PageRequest {
    role?: UserRole | null;
    departmentId?: string | null;
    isActive?: boolean | null;
    q?: string | null;
}

/**
 * The typed client for the identity endpoints. Feature components never call `HttpClient`
 * directly (docs/architecture.md §2.2) — this is the one place these contracts are absorbed.
 */
@Injectable({ providedIn: 'root' })
export class IdentityClient extends ApiClientBase {
    login(email: string, password: string): Observable<AuthToken> {
        return this.post<AuthToken>('auth/login', { email, password });
    }

    /**
     * `POST /auth/register` — anonymous customer self-registration (A-15).
     *
     * The response is an `AuthToken`, so a new customer is signed in rather than bounced back to
     * the sign-in form (docs/api-design.md §6.1). An email that already has a login is `409`
     * `user-already-exists` (**PF-6**); an email that already has an agent-created profile and no
     * login is a `201` that **links** to that profile rather than duplicating it — all three
     * outcomes are the server's, and the client distinguishes only the `409`.
     */
    register(request: RegisterRequest): Observable<AuthToken> {
        return this.post<AuthToken>('auth/register', request);
    }

    /** The resolved identity. There is no logout endpoint (AP-8) — the client discards the token. */
    me(): Observable<Identity> {
        return this.get<Identity>('auth/me');
    }

    listUsers(filter: UserListFilter): Observable<Paged<UserRow>> {
        return this.get<Paged<UserRow>>('users', filter as Record<string, QueryValue>);
    }

    getUser(id: string): Observable<UserRow> {
        return this.get<UserRow>(`users/${id}`);
    }

    createUser(request: CreateUserRequest): Observable<UserRow> {
        return this.post<UserRow>('users', request);
    }

    patchUser(id: string, request: PatchUserRequest): Observable<UserRow> {
        return this.patch<UserRow>(`users/${id}`, request);
    }

    deactivateUser(id: string): Observable<void> {
        return this.post<void>(`users/${id}/deactivate`);
    }
}
