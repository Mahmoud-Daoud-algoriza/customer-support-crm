import { Injectable, computed, signal } from '@angular/core';
import { Identity, UserRole, roleRankAtLeast } from './identity.model';

const TOKEN_STORAGE_KEY = 'supportcrm.token';

/**
 * Holds the bearer token and the resolved identity for the session.
 *
 * The token is persisted so a reload does not sign the user out; **the identity is not**. It is
 * re-fetched from `GET /auth/me` on every load, because role, department and active status are
 * authoritative on the server and a cached copy is exactly the staleness AD-15 removes.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
    private readonly tokenSignal = signal<string | null>(readStoredToken());
    private readonly identitySignal = signal<Identity | null>(null);

    readonly token = this.tokenSignal.asReadonly();
    readonly identity = this.identitySignal.asReadonly();

    /** A token exists. It does not follow that it is still valid — only the server knows that. */
    readonly hasToken = computed(() => this.tokenSignal() !== null);
    readonly isSignedIn = computed(() => this.identitySignal() !== null);
    readonly role = computed<UserRole | undefined>(() => this.identitySignal()?.role);

    readonly isAtLeast = (minimum: UserRole) => roleRankAtLeast(this.role(), minimum);

    setToken(token: string): void {
        this.tokenSignal.set(token);
        writeStoredToken(token);
    }

    setIdentity(identity: Identity | null): void {
        this.identitySignal.set(identity);
    }

    clear(): void {
        this.tokenSignal.set(null);
        this.identitySignal.set(null);
        writeStoredToken(null);
    }
}

function readStoredToken(): string | null {
    try {
        return localStorage.getItem(TOKEN_STORAGE_KEY);
    } catch {
        // Private windows and blocked site data both throw. Being signed out is a fine answer.
        return null;
    }
}

function writeStoredToken(token: string | null): void {
    try {
        if (token === null) {
            localStorage.removeItem(TOKEN_STORAGE_KEY);
        } else {
            localStorage.setItem(TOKEN_STORAGE_KEY, token);
        }
    } catch {
        // The session simply will not survive a reload.
    }
}
