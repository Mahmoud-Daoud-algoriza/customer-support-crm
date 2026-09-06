import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { PrimeNG } from 'primeng/config';

/** Languages that read right to left. Arabic is the only one this product ships (A-11). */
const RTL_LANGUAGES = new Set(['ar']);

const STORAGE_KEY = 'supportcrm.language';

/**
 * Owns the active language, the document direction and the PrimeNG locale — all three switch
 * together, at runtime, without a reload and without losing application state (T2-J, AD-9).
 *
 * Mirroring itself is CSS: logical properties only, enforced by .stylelintrc.json
 * (docs/ui-design.md §10.2).
 */
@Injectable({ providedIn: 'root' })
export class DirectionService {
    private readonly transloco = inject(TranslocoService);
    private readonly primeng = inject(PrimeNG);

    private readonly language = signal(this.transloco.getDefaultLang());

    readonly activeLanguage = this.language.asReadonly();
    readonly direction = computed<'ltr' | 'rtl'>(() => (RTL_LANGUAGES.has(this.language()) ? 'rtl' : 'ltr'));
    readonly isRtl = computed(() => this.direction() === 'rtl');

    /** The language the user last chose, if it is still one the server offers. */
    storedLanguage(available: readonly string[]): string | null {
        const stored = readStoredLanguage();
        return stored && available.includes(stored) ? stored : null;
    }

    async use(lang: string): Promise<void> {
        await this.transloco.load(lang).toPromise();
        this.transloco.setActiveLang(lang);
        this.language.set(lang);
        this.applyToDocument(lang);
        this.applyPrimeNgLocale();
        writeStoredLanguage(lang);
    }

    private applyToDocument(lang: string): void {
        const root = document.documentElement;
        root.setAttribute('lang', lang);
        root.setAttribute('dir', RTL_LANGUAGES.has(lang) ? 'rtl' : 'ltr');
    }

    /**
     * PrimeNG carries its own component strings; its locale switches alongside the application
     * dictionaries so both change together (docs/architecture.md §2.3). A translated page with an
     * English paginator or an English month name is a fail (Story 17 Part B task 1).
     *
     * **The array restore is load-bearing.** Transloco flattens a loaded dictionary to dot keys and
     * its `unflatten` rebuilds plain objects only — `dayNames` comes back as
     * `{ '0': 'Sunday', … }`, which PrimeNG's date picker cannot iterate. {@link restoreArrays}
     * turns every densely 0-indexed object back into the array PrimeNG's `Translation` declares.
     */
    private applyPrimeNgLocale(): void {
        const locale = this.transloco.translateObject('primeng');

        if (locale && typeof locale === 'object') {
            this.primeng.setTranslation(restoreArrays(locale as Record<string, unknown>) as Record<string, unknown>);
        }
    }
}

/**
 * Rebuilds the arrays Transloco's flatten/unflatten round trip loses. An object is treated as an
 * array only when its keys are exactly `0 … n-1` — so `aria` and the other named groups are left
 * alone, and a dictionary that never had an array is unchanged.
 */
export function restoreArrays(value: unknown): unknown {
    if (value === null || typeof value !== 'object' || Array.isArray(value)) {
        return value;
    }

    const entries = Object.entries(value as Record<string, unknown>).map(([key, item]) => [key, restoreArrays(item)] as const);
    const keys = entries.map(([key]) => key);
    const isDenseIndex = keys.length > 0 && keys.every((key, index) => key === String(index));

    return isDenseIndex ? entries.map(([, item]) => item) : Object.fromEntries(entries);
}

function readStoredLanguage(): string | null {
    try {
        return localStorage.getItem(STORAGE_KEY);
    } catch {
        // Private windows and blocked site data both throw; the default language is a fine answer.
        return null;
    }
}

function writeStoredLanguage(lang: string): void {
    try {
        localStorage.setItem(STORAGE_KEY, lang);
    } catch {
        // The choice simply does not persist. Not worth surfacing.
    }
}
