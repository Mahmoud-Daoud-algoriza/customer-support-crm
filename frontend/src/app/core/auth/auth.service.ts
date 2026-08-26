import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { firstValueFrom } from 'rxjs';
import { IdentityClient } from '../api/identity.client';
import { AuthStore } from './auth.store';
import { AuthToken } from './identity.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly api = inject(IdentityClient);
    private readonly store = inject(AuthStore);
    private readonly router = inject(Router);

    login(email: string, password: string): Observable<AuthToken> {
        return this.api.login(email, password).pipe(
            tap((token) => {
                this.store.setToken(token.accessToken);
                this.store.setIdentity(token.user);
            })
        );
    }

    /**
     * Re-resolves the identity from the server. Called at bootstrap when a stored token exists, so
     * a role or department change made while the user was away is picked up without a new sign-in.
     */
    async loadMe(): Promise<void> {
        if (!this.store.hasToken()) {
            return;
        }

        try {
            this.store.setIdentity(await firstValueFrom(this.api.me()));
        } catch {
            // An expired or revoked token: the error interceptor has already cleared the store.
            this.store.clear();
        }
    }

    /**
     * **There is no logout endpoint** (AP-8). Sign-out is purely client-side: discard the token and
     * let it expire on its own. Nothing is called on the server.
     */
    logout(returnUrl?: string): void {
        this.store.clear();

        void this.router.navigate(['/auth/login'], {
            queryParams: returnUrl ? { returnUrl } : undefined,
        });
    }
}
