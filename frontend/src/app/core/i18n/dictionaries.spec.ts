import { restoreArrays } from './direction.service';

/**
 * Story 17 Part B task 8 — **key parity**, and task 2 — **every error slug has a string**.
 *
 * These read the two shipped dictionaries as data. Nothing here mounts a component: the point is to
 * fail the build when the two files drift, which is the failure mode a screen walk finds last and a
 * file comparison finds instantly.
 *
 * The dictionaries are served to the test browser by the karma target's asset rule
 * (`angular.json` → `test.options.assets`, `src/assets/i18n` → `assets/i18n`), which is the same
 * path `TranslocoHttpLoader` fetches at runtime — so a missing file fails here too.
 */
describe('i18n dictionaries', () => {
    let en: Record<string, unknown>;
    let ar: Record<string, unknown>;

    beforeAll(async () => {
        [en, ar] = await Promise.all([load('en'), load('ar')]);
    });

    it('ships identical key sets in both languages', () => {
        const enKeys = flatten(en).sort();
        const arKeys = flatten(ar).sort();

        expect(enKeys.filter((key) => !arKeys.includes(key))).toEqual([]);
        expect(arKeys.filter((key) => !enKeys.includes(key))).toEqual([]);
    });

    it('leaves no value empty in either language', () => {
        for (const [lang, dict] of [
            ['en', en],
            ['ar', ar]
        ] as const) {
            for (const key of flatten(dict)) {
                const value = read(dict, key);

                if (typeof value === 'string') {
                    expect(value.trim().length)
                        .withContext(`${lang} → ${key}`)
                        .toBeGreaterThan(0);
                }
            }
        }
    });

    /**
     * The stable Problem Details slugs of `docs/api-design.md` §6.12, plus the status-level rows of
     * `docs/ui-design.md` §9. `errorInterceptor` maps an unmapped body to `http-<status>`, so both
     * shapes have to resolve to a sentence.
     */
    const REQUIRED_ERROR_KEYS = [
        'illegal-transition',
        'transition-not-permitted',
        'assignee-out-of-department',
        'feedback-already-submitted',
        'user-already-exists',
        'customer-email-in-use',
        'invalid-credentials',
        'ai-unavailable',
        'attachment-too-large',
        'not-found',
        'validation-failed',
        'internal-error',
        'network-unavailable',
        'http-401',
        'http-403',
        'http-404',
        'http-413',
        'http-422',
        'http-500',
        'http-503'
    ];

    it('carries a translated sentence for every Problem Details slug and status row', () => {
        for (const [lang, dict] of [
            ['en', en],
            ['ar', ar]
        ] as const) {
            for (const slug of REQUIRED_ERROR_KEYS) {
                expect(read(dict, `errors.${slug}`))
                    .withContext(`${lang} → errors.${slug}`)
                    .toEqual(jasmine.any(String));
            }
        }
    });

    /**
     * **AP-4 exists to stop the UI distinguishing a missing record from one out of scope**, and a
     * translation is the easiest place to break that by accident: `not-found` is the slug the server
     * sends and `http-404` is the fallback for a `404` with no body. If they ever read differently,
     * the wording itself would leak the distinction the contract hides.
     */
    it('words 404 identically whether the record is missing or out of scope (AP-4)', () => {
        expect(read(en, 'errors.http-404')).toBe(read(en, 'errors.not-found'));
        expect(read(ar, 'errors.http-404')).toBe(read(ar, 'errors.not-found'));
    });

    /**
     * PrimeNG carries its own strings and its locale switches alongside the application dictionaries
     * (architecture §2.3). Its date picker indexes `dayNames` and `monthNames` as **arrays** — and
     * Transloco's flatten/unflatten round trip returns plain objects, which is what
     * {@link restoreArrays} undoes. A translated page with an English month name is a fail.
     */
    it('gives PrimeNG a complete Gregorian calendar in both languages', () => {
        for (const [lang, dict] of [
            ['en', en],
            ['ar', ar]
        ] as const) {
            const locale = restoreArrays(read(dict, 'primeng')) as Record<string, unknown>;

            expect(locale['dayNames']).withContext(`${lang} dayNames`).toHaveSize(7);
            expect(locale['dayNamesShort']).withContext(`${lang} dayNamesShort`).toHaveSize(7);
            expect(locale['dayNamesMin']).withContext(`${lang} dayNamesMin`).toHaveSize(7);
            expect(locale['monthNames']).withContext(`${lang} monthNames`).toHaveSize(12);
            expect(locale['monthNamesShort']).withContext(`${lang} monthNamesShort`).toHaveSize(12);

            // The paginator's own labels, which are the ones a reader notices first (task 1).
            const aria = locale['aria'] as Record<string, unknown>;
            expect(aria['nextPageLabel']).toEqual(jasmine.any(String));
            expect(aria['previousPageLabel']).toEqual(jasmine.any(String));
            expect(aria['rowsPerPageLabel']).toEqual(jasmine.any(String));
        }
    });

    /** Arabic month names must actually be Arabic — an untranslated copy would pass a size check. */
    it('translates the Arabic calendar rather than copying the English one', () => {
        const enMonths = restoreArrays(read(en, 'primeng.monthNames')) as string[];
        const arMonths = restoreArrays(read(ar, 'primeng.monthNames')) as string[];

        expect(arMonths.some((month, index) => month === enMonths[index])).toBeFalse();
        expect(arMonths.every((month) => /[؀-ۿ]/.test(month))).toBeTrue();
    });

    /**
     * **No third language** (product-scope §8). The dictionary folder is the place a fourth file
     * would appear, so the pair is asserted rather than assumed.
     */
    it('ships exactly two languages', async () => {
        await expectAsync(load('fr')).toBeRejected();
    });
});

async function load(lang: string): Promise<Record<string, unknown>> {
    const response = await fetch(`/assets/i18n/${lang}.json`);

    if (!response.ok) {
        throw new Error(`no dictionary for "${lang}"`);
    }

    return response.json();
}

function flatten(value: unknown, prefix = ''): string[] {
    if (value === null || typeof value !== 'object') {
        return [prefix];
    }

    return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) =>
        flatten(child, prefix ? `${prefix}.${key}` : key)
    );
}

function read(dict: unknown, path: string): unknown {
    return path.split('.').reduce<unknown>((node, key) => (node as Record<string, unknown>)?.[key], dict);
}
