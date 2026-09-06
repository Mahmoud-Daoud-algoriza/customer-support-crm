import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { BootstrapConfig, PlatformApiService } from '../api/platform-api.service';

/**
 * Loads `GET /config/bootstrap` before the first screen renders, so branding is read at runtime and
 * never compiled into a component or a stylesheet (docs/architecture.md §6.3, T3-E).
 *
 * Wired through `provideAppInitializer` in app.config.ts.
 */
@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
    private readonly api = inject(PlatformApiService);
    private readonly config = signal<BootstrapConfig | null>(null);

    readonly productName = computed(() => this.config()?.productName ?? '');
    readonly logoUrl = computed(() => this.config()?.logoUrl ?? '');
    readonly primaryColor = computed(() => this.config()?.primaryColor ?? '');
    readonly languages = computed(() => this.config()?.languages ?? []);
    readonly defaultLanguage = computed(() => this.config()?.defaultLanguage ?? '');
    readonly isLoaded = computed(() => this.config() !== null);

    async load(): Promise<void> {
        const config = await firstValueFrom(this.api.getBootstrapConfig());
        this.config.set(config);
        this.applyBranding(config);
    }

    /**
     * The primary colour reaches the UI as CSS custom properties, so no component and no stylesheet
     * carries a branding value (T3-E, docs/architecture.md §6.3).
     *
     * <h3>It reaches PrimeNG's theme, and that is the point</h3>
     * Story 17 Part B task 6 asks for the seam to be **proved rather than asserted**, and proving it
     * showed `--app-brand-primary` was being set and read by nothing: the configured colour changed
     * no pixel. The three PrimeNG tokens below are where it becomes visible — buttons, links, focus
     * rings and chips all resolve their accent from them.
     *
     * **Hover and active are derived with `color-mix`, not a generated palette.** One configured
     * value in, a fixed set of tokens out — no preset list, no picker, no per-tenant variant. A ramp
     * generator would be the **theming engine T3-E excludes**; how far the configured colour ought to
     * propagate is not decided by any approved document, and is recorded as finding **I-41**.
     *
     * When no colour is configured nothing is set at all, so the shipped theme's own colours stand
     * rather than being overwritten with an empty string.
     */
    private applyBranding(config: BootstrapConfig): void {
        const root = document.documentElement.style;

        if (config.primaryColor) {
            root.setProperty('--app-brand-primary', config.primaryColor);
            root.setProperty('--p-primary-color', config.primaryColor);
            root.setProperty('--p-primary-hover-color', `color-mix(in srgb, ${config.primaryColor} 85%, black)`);
            root.setProperty('--p-primary-active-color', `color-mix(in srgb, ${config.primaryColor} 72%, black)`);
        }

        // index.html ships a neutral, non-branding title so the product name is never compiled in;
        // this is where the configured one takes over.
        if (config.productName) {
            document.title = config.productName;
        }
    }
}
