import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withEnabledBlockingInitialNavigation, withInMemoryScrolling } from '@angular/router';
import { provideTransloco } from '@jsverse/transloco';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { appRoutes } from './app.routes';
import { RuntimeConfigService } from './app/core/config/runtime-config.service';
import { DirectionService } from './app/core/i18n/direction.service';
import { TranslocoHttpLoader } from './app/core/i18n/transloco-http-loader';
import { AuthService } from './app/core/auth/auth.service';
import { authInterceptor } from './app/core/interceptors/auth.interceptor';
import { errorInterceptor } from './app/core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
    providers: [
        provideRouter(appRoutes, withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' }), withEnabledBlockingInitialNavigation()),
        provideHttpClient(withFetch(), withInterceptors([authInterceptor, errorInterceptor])),
        provideAnimationsAsync(),
        providePrimeNG({ theme: { preset: Aura, options: { darkModeSelector: '.app-dark' } } }),

        // The toast channel errorInterceptor writes to, and <p-toast> in AppComponent reads from.
        // Provided once at the root rather than per component, because an interceptor has no
        // component to be provided by.
        MessageService,

        // Runtime translation (AD-9): switching language must not reload the app or lose state, so
        // compile-time @angular/localize is rejected. Dictionaries are static assets.
        provideTransloco({
            config: {
                availableLangs: ['en', 'ar'],
                defaultLang: 'en',
                fallbackLang: 'en',
                reRenderOnLangChange: true,
                prodMode: false
            },
            loader: TranslocoHttpLoader
        }),

        // Branding and the language set are resolved before the first screen renders
        // (docs/architecture.md §6.3).
        provideAppInitializer(async () => {
            // Every inject() must happen before the first await: the injection context is gone
            // afterwards, and calling inject() late throws NG0203 at bootstrap.
            const runtimeConfig = inject(RuntimeConfigService);
            const direction = inject(DirectionService);
            const auth = inject(AuthService);

            try {
                await runtimeConfig.load();
            } catch {
                // A shell that renders and reports the failure beats a blank page. The health screen
                // shows what went wrong; the default language below keeps the UI readable.
            }

            const available = runtimeConfig.languages();
            const chosen = direction.storedLanguage(available) ?? (runtimeConfig.defaultLanguage() || 'en');
            await direction.use(chosen);

            // A stored token is re-validated against the server before the first screen renders, so
            // role, department and active status are the authoritative ones rather than whatever was
            // true when the token was minted (AD-15). An expired or revoked token simply clears.
            await auth.loadMe();
        })
    ]
};
