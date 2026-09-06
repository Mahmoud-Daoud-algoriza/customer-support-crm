import { ChangeDetectorRef, DestroyRef, Pipe, PipeTransform, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';

/**
 * The one date formatter every screen uses (Story 17 Part B task 4).
 *
 * <h3>Gregorian in both languages, and that is the whole point</h3>
 * **A-11**: *"dates display in the Gregorian calendar in both languages."* Arabic formats through
 * `ar-u-ca-gregory` — the `u-ca-gregory` extension pins the calendar, because `Intl` under a plain
 * `ar` locale resolves to `islamic-umalqura` in several runtimes and would silently render Hijri.
 * **No Hijri and no dual calendar** is an explicit exclusion of the story intake, so it is pinned
 * here, once, rather than trusted to a default.
 *
 * `docs/ui-design.md` §10.2 also requires numerals and timestamps to stay **LTR-embedded** inside
 * RTL text. That is presentation, not formatting, and it belongs to
 * `shared/directives/ltr-embed.directive.ts` — a pipe cannot set a style on its host.
 *
 * <h3>Why it is impure</h3>
 * Switching language must re-render every date **without a reload** (T2-J, AD-9). A pure pipe would
 * not recompute, because the input instant has not changed — only the locale has. So the pipe is
 * impure and, exactly as Transloco's own pipe does, marks its host for check when the active
 * language changes; the two together are what make an `OnPush` screen redraw its dates on a switch.
 *
 * <h3>Formats</h3>
 * The three Angular `DatePipe` presets the screens already used, kept by name so a call site reads
 * the same as it did:
 *
 * | Format | Renders |
 * |---|---|
 * | `short` | date and time, numeric — the table and timeline default |
 * | `medium` | a longer date with time, for a single prominent timestamp |
 * | `shortDate` | date only |
 */
@Pipe({
    name: 'appDate',
    standalone: true,
    // See "Why it is impure" above: the instant does not change on a language switch, the locale does.
    pure: false
})
export class AppDatePipe implements PipeTransform {
    private readonly transloco = inject(TranslocoService);

    constructor() {
        const cdr = inject(ChangeDetectorRef);

        this.transloco.langChanges$.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(() => cdr.markForCheck());
    }

    transform(value: string | Date | null | undefined, format: AppDateFormat = 'short'): string {
        if (value === null || value === undefined || value === '') {
            return '';
        }

        const instant = value instanceof Date ? value : new Date(value);

        if (Number.isNaN(instant.getTime())) {
            return '';
        }

        return formatterFor(this.transloco.getActiveLang(), format).format(instant);
    }
}

export type AppDateFormat = 'short' | 'medium' | 'shortDate';

const OPTIONS: Record<AppDateFormat, Intl.DateTimeFormatOptions> = {
    short: { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' },
    medium: { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' },
    shortDate: { year: 'numeric', month: '2-digit', day: '2-digit' }
};

/**
 * `Intl.DateTimeFormat` construction is the expensive half of formatting, and a table renders the
 * same pair hundreds of times, so the formatters are built once per language-and-format.
 */
const FORMATTERS = new Map<string, Intl.DateTimeFormat>();

function formatterFor(lang: string, format: AppDateFormat): Intl.DateTimeFormat {
    const cacheKey = `${lang}|${format}`;
    const cached = FORMATTERS.get(cacheKey);

    if (cached) {
        return cached;
    }

    const formatter = new Intl.DateTimeFormat(intlLocale(lang), OPTIONS[format]);
    FORMATTERS.set(cacheKey, formatter);

    return formatter;
}

/**
 * The BCP 47 tag to format with. **Every language gets `-u-ca-gregory` explicitly** rather than only
 * Arabic: it costs nothing where Gregorian is already the default, and it means adding a language to
 * `availableLangs` can never introduce a non-Gregorian calendar by accident (A-11).
 */
function intlLocale(lang: string): string {
    return `${lang}-u-ca-gregory`;
}
