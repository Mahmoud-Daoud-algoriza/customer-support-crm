import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { StyleClassModule } from 'primeng/styleclass';
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
 */
@Component({
    selector: 'app-topbar',
    standalone: true,
    imports: [RouterModule, CommonModule, StyleClassModule, AppConfigurator, LanguageSwitcherComponent],
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

            <!-- TODO Story 02: notifications bell and the signed-in user menu (docs/ui-design.md §4.1).
                 TODO Story 09: unread notification count on the bell (A-13).
                 Nothing user-specific can render here until authentication exists. -->
        </div>
    </div>`
})
export class AppTopbar {
    readonly layoutService = inject(LayoutService);
    protected readonly config = inject(RuntimeConfigService);

    toggleDarkMode() {
        this.layoutService.layoutConfig.update((state) => ({ ...state, darkTheme: !state.darkTheme }));
    }
}
