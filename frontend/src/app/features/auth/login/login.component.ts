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
 * Sign in — the centred card of the auth shell (docs/ui-design.md §4.3).
 *
 * The error message is the **translated** string chosen by the Problem Details `type` slug; the
 * server's `detail` is never rendered raw (docs/ui-design.md §9, T2-J).
 */
@Component({
    selector: 'app-login',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, FormsModule, InputTextModule, MessageModule, PasswordModule, RouterLink, TranslocoModule],
    template: `
        <form class="app-login" (ngSubmit)="submit()">
            <h1 class="app-login__title">{{ 'auth.signIn' | transloco }}</h1>

            @if (problem(); as failure) {
                <p-message severity="error" [text]="errorKey(failure) | transloco" />
            }

            <label class="app-field">
                <span class="app-field__label">{{ 'auth.email' | transloco }}</span>
                <input pInputText name="email" type="email" autocomplete="username" required [(ngModel)]="email" />
            </label>

            <label class="app-field">
                <span class="app-field__label">{{ 'auth.password' | transloco }}</span>
                <p-password name="password" autocomplete="current-password" [feedback]="false" [toggleMask]="true" required [(ngModel)]="password" />
            </label>

            <p-button type="submit" [label]="'auth.signIn' | transloco" [loading]="busy()" [disabled]="busy()" />

            <a class="app-login__register" routerLink="/auth/register">{{ 'auth.registerPrompt' | transloco }}</a>
        </form>
    `
})
export class LoginComponent {
    private readonly auth = inject(AuthService);
    private readonly store = inject(AuthStore);
    private readonly router = inject(Router);

    protected email = '';
    protected password = '';
    protected readonly busy = signal(false);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected errorKey = problemTranslationKey;

    protected submit(): void {
        if (this.busy()) {
            return;
        }

        this.busy.set(true);
        this.problem.set(null);

        this.auth.login(this.email, this.password).subscribe({
            next: () => {
                this.busy.set(false);

                // Honour a return URL captured by the guard or the 401 handler, otherwise let the
                // root route redirect by role (docs/ui-design.md §2).
                const returnUrl = new URLSearchParams(window.location.search).get('returnUrl');
                void this.router.navigateByUrl(returnUrl ?? '/');
            },
            error: (failure: ApiProblem) => {
                this.busy.set(false);
                this.problem.set(failure);
                this.store.clear();
            }
        });
    }
}
