import { isDevMode } from '@angular/core';
import { TranslocoMissingHandler, TranslocoMissingHandlerData } from '@jsverse/transloco';

/**
 * Story 17 Part B task 8 — the missing-key sweep.
 *
 * Transloco's default handler returns the key itself, silently. That is exactly the failure the
 * story's acceptance criterion forbids — *"no untranslated key or raw token is visible in either
 * language"* — and it stays invisible until someone reads a screen in Arabic and finds
 * `tickets.slaOverdue` staring back at them.
 *
 * So: **loud in development, quiet in production.** A missing key logs an error naming the key and
 * the language it was missing from, so walking the 24 screens of `docs/ui-design.md` §13 in both
 * languages surfaces every gap in the console rather than in a screenshot.
 *
 * **The returned value is the key, in both modes.** Returning an empty string would hide the gap in
 * development too, and throwing would take a screen down over one missing sentence.
 */
export class AppMissingHandler implements TranslocoMissingHandler {
    handle(key: string, data: TranslocoMissingHandlerData): string {
        if (isDevMode()) {
            // eslint-disable-next-line no-console
            console.error(`[i18n] missing translation key "${key}" in "${data?.activeLang}"`);
        }

        return key;
    }
}
