import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import { AppDatePipe } from './app-date.pipe';

/**
 * Story 17 Part B task 4 — **dates render in the Gregorian calendar in both languages** (A-11), and
 * **no Hijri and no dual calendar** anywhere (intake, Out of scope).
 *
 * The instant used throughout is `2026-09-06T14:30:00Z`. In the Hijri (Umm al-Qura) calendar that
 * day falls in **Rabiʿ al-awwal 1448** — so a year that is not 2026 is the single clearest signal
 * that the calendar slipped, which is what the assertions below key on.
 */
@Component({
    standalone: true,
    imports: [AppDatePipe],
    template: `<span class="short">{{ instant | appDate }}</span>
        <span class="medium">{{ instant | appDate: 'medium' }}</span>
        <span class="dateOnly">{{ instant | appDate: 'shortDate' }}</span>
        <span class="empty">{{ nothing | appDate }}</span>
        <span class="garbage">{{ garbage | appDate }}</span>`
})
class DateHost {
    readonly instant = '2026-09-06T14:30:00Z';
    readonly nothing: string | null = null;
    readonly garbage = 'not a date';
}

describe('AppDatePipe', () => {
    let fixture: ComponentFixture<DateHost>;
    let transloco: TranslocoService;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                DateHost,
                TranslocoTestingModule.forRoot({
                    langs: { en: {}, ar: {} },
                    translocoConfig: { availableLangs: ['en', 'ar'], defaultLang: 'en' }
                })
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(DateHost);
        transloco = TestBed.inject(TranslocoService);
        fixture.detectChanges();
    });

    afterEach(() => TestBed.resetTestingModule());

    function text(selector: string): string {
        return (fixture.nativeElement as HTMLElement).querySelector(selector)!.textContent!.trim();
    }

    it('renders the Gregorian year and month in English', () => {
        expect(text('.short')).toContain('2026');
        expect(text('.short')).toContain('09');
        expect(text('.dateOnly')).toContain('2026');
    });

    /**
     * **The assertion that matters.** `Intl` under a bare `ar` locale resolves to
     * `islamic-umalqura` in several runtimes, which would render `1448` here. The pipe pins
     * `u-ca-gregory`, so the year stays `2026` — in Arabic-Indic digits or Latin ones, whichever the
     * runtime picks, which is why the check normalises the digits rather than matching a literal.
     */
    it('renders the Gregorian calendar in Arabic, never Hijri', async () => {
        transloco.setActiveLang('ar');
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        const gregorian = toLatinDigits(text('.short'));

        expect(gregorian).withContext('Gregorian year').toContain('2026');
        expect(gregorian).withContext('a Hijri year would be in the 1440s').not.toMatch(/\b14[0-9]{2}\b/);

        expect(new Intl.DateTimeFormat('ar-u-ca-gregory').resolvedOptions().calendar).toBe('gregory');
    });

    /** Switching language re-renders the date in place — no reload, no state lost (T2-J, AD-9). */
    it('re-renders on a language switch without the input changing', async () => {
        const before = text('.medium');

        transloco.setActiveLang('ar');
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();

        expect(text('.medium')).not.toBe(before);
        expect(toLatinDigits(text('.medium'))).toContain('2026');
    });

    /** A null timestamp renders nothing rather than `Invalid Date` or the epoch. */
    it('renders an empty string for a missing or unparseable value', () => {
        expect(text('.empty')).toBe('');
        expect(text('.garbage')).toBe('');
    });
});

/** Arabic locales may render Arabic-Indic digits; the year is the same number either way. */
function toLatinDigits(value: string): string {
    return value.replace(/[٠-٩۰-۹]/g, (digit) => String(digit.charCodeAt(0) & 0xf));
}
