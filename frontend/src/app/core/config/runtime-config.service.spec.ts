import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RuntimeConfigService } from './runtime-config.service';

/**
 * Story 17 Part B task 6 — **the branding seam, proved rather than asserted** (T3-E).
 *
 * The acceptance criterion is *"changing the configured values changes the running application
 * without code edits."* This spec is the code-level half of that: it feeds the service two
 * different `GET /config/bootstrap` payloads and shows the application takes its product name,
 * logo and primary colour from whichever one it was given — **nothing is compiled in**.
 *
 * The other half — editing `appsettings.json`, restarting the API and reloading the browser with an
 * unchanged bundle — is a live-stack check and is reported separately.
 */
describe('RuntimeConfigService — branding comes from configuration (T3-E)', () => {
    const BOOTSTRAP = '/api/v1/config/bootstrap';

    let service: RuntimeConfigService;
    let controller: HttpTestingController;
    let originalTitle: string;
    let originalStyle: string | null;

    function bootstrapConfig(overrides: Record<string, unknown> = {}) {
        return {
            productName: 'Contoso Care',
            logoUrl: '/assets/branding/contoso.svg',
            primaryColor: '#7A1FA2',
            languages: ['en', 'ar'],
            defaultLanguage: 'en',
            ...overrides
        };
    }

    beforeEach(() => {
        originalTitle = document.title;
        originalStyle = document.documentElement.getAttribute('style');

        TestBed.configureTestingModule({
            providers: [provideHttpClient(), provideHttpClientTesting()]
        });

        service = TestBed.inject(RuntimeConfigService);
        controller = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        document.title = originalTitle;

        if (originalStyle === null) {
            document.documentElement.removeAttribute('style');
        } else {
            document.documentElement.setAttribute('style', originalStyle);
        }

        TestBed.resetTestingModule();
    });

    async function load(config: Record<string, unknown>): Promise<void> {
        const loading = service.load();
        controller.expectOne(BOOTSTRAP).flush(config);
        await loading;
    }

    function cssVariable(name: string): string {
        return document.documentElement.style.getPropertyValue(name).trim();
    }

    /**
     * **The whole seam in one spec.** A payload naming a different product, logo and colour produces
     * a different running application — the product name in the tab, the logo the shells render, and
     * the primary colour every accent resolves from.
     */
    it('takes product name, logo and primary colour from the payload', async () => {
        await load(bootstrapConfig());

        expect(service.productName()).toBe('Contoso Care');
        expect(service.logoUrl()).toBe('/assets/branding/contoso.svg');
        expect(service.primaryColor()).toBe('#7A1FA2');
        expect(document.title).toBe('Contoso Care');
    });

    /**
     * The colour has to reach something. Before this story it was written to `--app-brand-primary`
     * and read by nothing, so a configured colour changed no pixel — see finding **I-41**.
     */
    it('applies the primary colour to PrimeNG’s own accent tokens', async () => {
        await load(bootstrapConfig());

        expect(cssVariable('--app-brand-primary')).toBe('#7A1FA2');
        expect(cssVariable('--p-primary-color')).toBe('#7A1FA2');
        expect(cssVariable('--p-primary-hover-color')).toContain('#7A1FA2');
        expect(cssVariable('--p-primary-active-color')).toContain('#7A1FA2');
    });

    /** A second, different payload proves nothing was baked in by the first. */
    it('follows a changed configuration without a code edit', async () => {
        await load(bootstrapConfig());

        TestBed.resetTestingModule();
        TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
        service = TestBed.inject(RuntimeConfigService);
        controller = TestBed.inject(HttpTestingController);

        await load(bootstrapConfig({ productName: 'Northwind Desk', primaryColor: '#0F7B4A' }));

        expect(service.productName()).toBe('Northwind Desk');
        expect(document.title).toBe('Northwind Desk');
        expect(cssVariable('--p-primary-color')).toBe('#0F7B4A');
    });

    /**
     * **Two languages, from the server.** The switcher offers what `/config/bootstrap` lists and
     * hardcodes nothing, which is also what keeps *"no third language"* (product-scope §8) a
     * configuration fact rather than a component one.
     */
    it('publishes the offered languages and the default', async () => {
        await load(bootstrapConfig());

        expect(service.languages()).toEqual(['en', 'ar']);
        expect(service.defaultLanguage()).toBe('en');
        expect(service.isLoaded()).toBeTrue();
    });

    /** No configured colour leaves the shipped theme alone rather than blanking its tokens. */
    it('leaves the theme untouched when no colour is configured', async () => {
        await load(bootstrapConfig({ primaryColor: '' }));

        expect(cssVariable('--p-primary-color')).toBe('');
        expect(service.productName()).toBe('Contoso Care');
    });
});
