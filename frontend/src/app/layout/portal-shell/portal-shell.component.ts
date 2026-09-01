import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { AuthService } from '../../core/auth/auth.service';
import { AuthStore } from '../../core/auth/auth.store';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';

/**
 * The Customer portal shell — docs/ui-design.md §4.2.
 *
 * ```
 * ┌──────────────────────────────────────────────────────────┐
 * │ [brand]   My requests   Help      [EN|ع]   [avatar ▾]     │
 * ├──────────────────────────────────────────────────────────┤
 * │                    routed content                        │
 * └──────────────────────────────────────────────────────────┘
 * ```
 *
 * <h3>Two destinations, and no sidebar</h3>
 * §4.2 draws exactly this: *"Two destinations, no sidebar, no notification bell."* Story 01 stood the
 * shell up on the shared staff chrome because the portal had no screens yet; **Story 13 replaces it
 * with the shell the design specifies.** The staff `AppLayout` brings a collapsible sidebar and a
 * menu whose whole purpose is role-based sections — none of which a two-destination portal has any
 * use for, and all of which would put agent chrome in front of a customer (AD-14).
 *
 * <h3>No notification bell, and that is a requirement rather than a simplification</h3>
 * §4.2: A-13's four events — assignment, breach, escalation, customer reply — are **staff-facing**,
 * and *"no requirement gives a customer an in-app notification feed."* There is no bell here and no
 * `NotificationStore` injected, so there is nothing to switch on by accident.
 *
 * <h3>No staff vocabulary anywhere in this area</h3>
 * **UI-11.** Nothing in this shell or in any screen it hosts names a department, a priority, an
 * assignee or an SLA. The avatar menu shows the display name and the role label, and deliberately
 * **not** the department line the staff topbar shows — a customer has none (DM-1), and the portal
 * ticket payload carries no department at all.
 *
 * **Review rule for `features/portal/**`:** it must not import `PriorityChipComponent`,
 * `SlaIndicatorComponent` or `TicketCustomerPanelComponent`. Those are staff components by
 * ui-design §8, and an import of one is the first symptom of UI-11 being lost.
 *
 * <h3>Single column at every width</h3>
 * ui-design §10.3. The shell is a header and one content column; there is no second region to
 * collapse, which is why the portal needs no responsive layout switch at all.
 */
@Component({
    selector: 'app-portal-shell',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, LanguageSwitcherComponent, PopoverModule, RouterLink, RouterLinkActive,
        RouterOutlet, TranslocoModule,
    ],
    template: `
        <div class="app-portal">
            <header class="app-portal__bar">
                <a class="app-portal__brand" routerLink="/portal/requests">
                    @if (config.logoUrl()) {
                        <img [src]="config.logoUrl()" [alt]="config.productName()" height="28" />
                    }
                    <span>{{ config.productName() }}</span>
                </a>

                <!-- The two destinations of §4.2, and there are no others to add. -->
                <nav class="app-portal__nav" [attr.aria-label]="'nav.portal' | transloco">
                    <a routerLink="/portal/requests" routerLinkActive="app-portal__link--active">
                        {{ 'nav.myRequests' | transloco }}
                    </a>
                    <a routerLink="/portal/help" routerLinkActive="app-portal__link--active">
                        {{ 'nav.help' | transloco }}
                    </a>
                </nav>

                <div class="app-portal__actions">
                    <app-language-switcher />

                    @if (store.identity(); as identity) {
                        <button
                            type="button"
                            class="app-portal__avatar"
                            [attr.aria-label]="identity.displayName"
                            (click)="avatar.toggle($event)">
                            <i class="pi pi-user"></i>
                        </button>

                        <p-popover #avatar>
                            <div class="app-avatar-menu">
                                <p class="app-avatar-menu__name">{{ identity.displayName }}</p>
                                <p class="app-avatar-menu__meta">{{ 'roles.' + identity.role | transloco }}</p>

                                <p-button
                                    [label]="'auth.signOut' | transloco"
                                    severity="secondary"
                                    icon="pi pi-sign-out"
                                    (onClick)="signOut()" />
                            </div>
                        </p-popover>
                    }
                </div>
            </header>

            <main class="app-portal__content">
                <router-outlet />
            </main>
        </div>
    `
})
export class PortalShellComponent {
    protected readonly config = inject(RuntimeConfigService);
    protected readonly store = inject(AuthStore);

    private readonly auth = inject(AuthService);

    /**
     * Sign-out is entirely client-side: the token is discarded and left to expire. There is no
     * logout endpoint to call (AP-8).
     */
    protected signOut(): void {
        this.auth.logout();
    }
}
