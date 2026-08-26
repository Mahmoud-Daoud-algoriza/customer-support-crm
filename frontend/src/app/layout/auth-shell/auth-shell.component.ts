import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { RuntimeConfigService } from '../../core/config/runtime-config.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';

/**
 * Centred card, brand block from `/config/bootstrap`, language switcher. Nothing else
 * (docs/ui-design.md §4.3).
 *
 * The brand block reads its values at runtime, so no branding value is hardcoded here (T3-E).
 */
@Component({
    selector: 'app-auth-shell',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterOutlet, LanguageSwitcherComponent],
    template: `
        <div class="app-centered-card-shell">
            <div class="app-centered-card app-auth-shell">
                <header class="app-auth-shell__brand">
                    @if (config.logoUrl()) {
                        <img class="app-auth-shell__logo" [src]="config.logoUrl()" [alt]="config.productName()" />
                    }
                    <span class="app-auth-shell__name">{{ config.productName() }}</span>
                </header>

                <router-outlet />

                <footer class="app-auth-shell__footer">
                    <app-language-switcher />
                </footer>
            </div>
        </div>
    `
})
export class AuthShellComponent {
    protected readonly config = inject(RuntimeConfigService);
}
