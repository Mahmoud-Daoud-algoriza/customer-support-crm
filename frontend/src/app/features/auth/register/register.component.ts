import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';

/**
 * Customer self-registration — **scaffolded, submit deliberately disabled.**
 *
 * `POST /auth/register` is delivered by Story 04, not here: registration creates a `Customer` with
 * the **configured default branch** (A-15), and neither the entity nor the configuration key exists
 * yet. Calling a non-existent endpoint, or inventing a placeholder branch, would both be worse than
 * saying so plainly. Recorded as finding S9-7.
 */
@Component({
    selector: 'app-register',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, MessageModule, RouterLink, TranslocoModule],
    template: `
        <div class="app-login">
            <h1 class="app-login__title">{{ 'auth.register' | transloco }}</h1>

            <p-message severity="info" [text]="'auth.registerComingSoon' | transloco" />

            <p-button [label]="'auth.backToSignIn' | transloco" severity="secondary" routerLink="/auth/login" />
        </div>
    `
})
export class RegisterComponent {}
