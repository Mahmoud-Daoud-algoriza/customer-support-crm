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
     * The primary colour reaches the UI as a CSS custom property, so no component and no stylesheet
     * carries a branding value.
     */
    private applyBranding(config: BootstrapConfig): void {
        document.documentElement.style.setProperty('--app-brand-primary', config.primaryColor);
        document.title = config.productName;
    }
}
