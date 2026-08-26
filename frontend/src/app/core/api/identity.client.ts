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
