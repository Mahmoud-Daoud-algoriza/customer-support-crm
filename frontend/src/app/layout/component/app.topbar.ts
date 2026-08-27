import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { StyleClassModule } from 'primeng/styleclass';
import { Department, OrganizationClient } from '../../core/api/organization.client';
import { AuthService } from '../../core/auth/auth.service';
import { AuthStore } from '../../core/auth/auth.store';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';
import { LayoutService } from '../service/layout.service';
import { AppConfigurator } from './app.configurator';

/**
 * Sakai's topbar, with the template's own brand mark replaced by the runtime brand block from
 * `/config/bootstrap` — no branding value is hardcoded in a component (T3-E,
 * docs/architecture.md §6.3).
 *
 * The language switcher lives in every shell (docs/ui-design.md §10.1).
 *
 * The avatar menu shows the signed-in user's **department name**, resolved through
 * `GET /departments` (Story 03 task 7). Story 02 showed the raw id here because that endpoint did
 * not exist yet.
 */
@Component({
    selector: 'app-topbar',
    standalone: true,
    imports: [
        RouterModule, CommonModule, StyleClassModule, AppConfigurator, LanguageSwitcherComponent,
        ButtonModule, PopoverModule, TranslocoModule,
    ],
    template: ` <div class="layout-topbar">
        <div class="layout-topbar-logo-container">
            <button class="layout-menu-button layout-topbar-action" (click)="layoutService.onMenuToggle()">
                <i class="pi pi-bars"></i>
            </button>
            <a class="layout-topbar-logo" routerLink="/">
                @if (config.logoUrl()) {
                    <img [src]="config.logoUrl()" [alt]="config.productName()" height="32" />
                }
                <span>{{ config.productName() }}</span>
            </a>
        </div>

        <div class="layout-topbar-actions">
            <div class="layout-config-menu">
                <app-language-switcher />
                <button type="button" class="layout-topbar-action" (click)="toggleDarkMode()">
                    <i [ngClass]="{ 'pi ': true, 'pi-moon': layoutService.isDarkTheme(), 'pi-sun': !layoutService.isDarkTheme() }"></i>
                </button>
                <div class="relative">
                    <button
                        class="layout-topbar-action layout-topbar-action-highlight"
                        pStyleClass="@next"
                        enterFromClass="hidden"
                        enterActiveClass="animate-scalein"
                        leaveToClass="hidden"
                        leaveActiveClass="animate-fadeout"
                        [hideOnOutsideClick]="true"
                    >
                        <i class="pi pi-palette"></i>
                    </button>
                    <app-configurator />
                </div>
            </div>

            <!-- TODO Story 09: the notification bell and its unread count (A-13). Its slot is left
                 here deliberately, between the theme controls and the avatar. -->

            @if (store.identity(); as identity) {
                <button
                    type="button"
                    class="layout-topbar-action"
                    [attr.aria-label]="identity.displayName"
                    (click)="avatar.toggle($event)"
                >
                    <i class="pi pi-user"></i>
                </button>

                <p-popover #avatar>
                    <div class="app-avatar-menu">
                        <p class="app-avatar-menu__name">{{ identity.displayName }}</p>
                        <p class="app-avatar-menu__meta">{{ 'roles.' + identity.role | transloco }}</p>

                        @if (departmentName(); as department) {
                            <p class="app-avatar-menu__meta">{{ department }}</p>
                        }

                        <p-button
                            [label]="'auth.signOut' | transloco"
                            severity="secondary"
                            icon="pi pi-sign-out"
                            (onClick)="signOut()"
                        />
                    </div>
                </p-popover>
            }
        </div>
    </div>`
})
export class AppTopbar {
    readonly layoutService = inject(LayoutService);
    protected readonly config = inject(RuntimeConfigService);
    protected readonly store = inject(AuthStore);
    private readonly auth = inject(AuthService);
    private readonly organization = inject(OrganizationClient);

    private readonly departments = signal<Department[]>([]);

    /**
     * The department's name, or `null` when there is nothing to show. A Customer has no department
     * (DM-1), and the list has not necessarily arrived yet — neither is an error, and neither
     * renders a row.
     */
    protected readonly departmentName = computed(() => {
        const departmentId = this.store.identity()?.departmentId;

        return this.departments().find((d) => d.id === departmentId)?.name ?? null;
    });

    constructor() {
        // Guarded on the id, not on the role. `GET /departments` is Agent+, and a Customer's
        // departmentId is always null (DM-1) — so the guard that renders nothing is the same guard
        // that keeps a Customer from ever making a request that would be a 403. This shell serves
        // both surfaces.
        effect(() => {
            if (!this.store.identity()?.departmentId || this.departments().length > 0) {
                return;
            }

            this.organization.getDepartments().subscribe((departments) => this.departments.set(departments));
        });
    }

    /**
     * Sign-out is entirely client-side: the token is discarded and left to expire. There is no
     * logout endpoint to call (AP-8).
     */
    protected signOut(): void {
        this.auth.logout();
    }

    toggleDarkMode() {
        this.layoutService.layoutConfig.update((state) => ({ ...state, darkTheme: !state.darkTheme }));
    }
}
