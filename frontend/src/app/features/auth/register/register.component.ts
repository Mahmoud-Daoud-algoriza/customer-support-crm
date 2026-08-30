import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { AuthService } from '../../../core/auth/auth.service';
import { AuthStore } from '../../../core/auth/auth.store';

/**
 * Customer self-registration — `/auth/register` (docs/ui-design.md §2), the centred card of the auth
 * shell (§4.3). **The form Story 02 scaffolded, now enabled**: `POST /auth/register` arrives with
 * Story 04 (finding S9-7), and this screen is what closes it.
 *
 * **Four fields, one of them optional, and no others.** There is **no branch selector and no role
 * selector**, and neither may be added: A-15 fixes both server-side — the role is always `Customer`
 * and the branch is always the configured default, which is why the request type has nowhere to put
 * either. A body carrying one is a `400` (AP-10).
 *
 * **`409 user-already-exists` invites the user to sign in** rather than reporting a failure: an
 * address that already has a login is not an error the customer can fix by trying again (PF-6,
 * docs/api-design.md §5.2). The other two A-15 outcomes are both `201` and look identical from
 * here — an address with an agent-created profile is *linked* to it, never duplicated — so this
 * screen distinguishes exactly one case, which is exactly what the contract distinguishes.
 *
 * A successful registration returns an `AuthToken`, so the new customer is **signed in** and routed
 * by role rather than bounced to the sign-in form (docs/api-design.md §6.1).
 */
@Component({
    selector: 'app-register',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, FormsModule, InputTextModule, MessageModule, PasswordModule, RouterLink, TranslocoModule],
    template: `
        <form class="app-login" (ngSubmit)="submit()">
            <h1 class="app-login__title">{{ 'auth.register' | transloco }}</h1>

            @if (problem(); as failure) {
                @if (existingAccount()) {
                    <!-- Not "something went wrong": the address already has a login, and signing in
                         is the action that works (docs/ui-design.md §9's contextual 409). -->
                    <p-message severity="warn" [text]="'auth.alreadyRegistered' | transloco" />
                } @else {
                    <p-message severity="error" [text]="errorKey(failure) | transloco" />
                }
            }

            <label class="app-field">
                <span class="app-field__label">{{ 'auth.fullName' | transloco }}</span>
                <input pInputText name="fullName" autocomplete="name" required [(ngModel)]="fullName" />
            </label>

            <label class="app-field">
                <span class="app-field__label">{{ 'auth.email' | transloco }}</span>
                <input pInputText name="email" type="email" autocomplete="username" required [(ngModel)]="email" />
            </label>

            <label class="app-field">
                <span class="app-field__label">{{ 'auth.password' | transloco }}</span>
                <p-password name="password" autocomplete="new-password" [feedback]="false" [toggleMask]="true" required [(ngModel)]="password" />
            </label>

            <label class="app-field">
                <span class="app-field__label">{{ 'auth.phoneOptional' | transloco }}</span>
                <input pInputText name="phone" type="tel" autocomplete="tel" [(ngModel)]="phone" />
            </label>

            <p-button type="submit" [label]="'auth.register' | transloco" [loading]="busy()" [disabled]="busy()" />

            <a class="app-login__register" routerLink="/auth/login">{{ 'auth.backToSignIn' | transloco }}</a>
        </form>
    `
})
export class RegisterComponent {
    private readonly auth = inject(AuthService);
    private readonly store = inject(AuthStore);
    private readonly router = inject(Router);

    protected fullName = '';
    protected email = '';
    protected password = '';
    protected phone = '';

    protected readonly busy = signal(false);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected errorKey = problemTranslationKey;

    protected existingAccount(): boolean {
        return this.problem()?.type === EXISTING_ACCOUNT_TYPE;
    }

    protected submit(): void {
        if (this.busy()) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        this.auth
            .register({
                email: this.email,
                password: this.password,
                fullName: this.fullName,
                // An empty box is "no phone", not an empty phone — the field is optional (§5.2).
                phone: this.phone.trim() === '' ? null : this.phone
            })
            .subscribe({
                next: () => {
                    this.busy.set(false);

                    // Let the root route redirect by role (docs/ui-design.md §2), rather than naming
                    // `/portal` here — the server decides the role, and it is always `Customer`.
                    void this.router.navigateByUrl('/');
                },
                error: (failure: ApiProblem) => {
                    this.busy.set(false);
                    this.problem.set(failure);
                    this.store.clear();
                }
            });
    }
}

/** PF-6's slug, for PF-6's rule: `User.email` is unique case-insensitively across all users. */
const EXISTING_ACCOUNT_TYPE = 'user-already-exists';
