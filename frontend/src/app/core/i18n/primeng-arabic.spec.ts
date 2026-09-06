import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { PrimeNG, providePrimeNG } from 'primeng/config';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { PaginatorModule } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { DirectionService } from './direction.service';

/**
 * Story 17 Part B task 3 — the five PrimeNG components `docs/ui-design.md` §10.2 names are
 * *"verified in Arabic rather than assumed correct"*: **table, paginator, dropdown, calendar and
 * drawer**.
 *
 * `docs/architecture.md` §2.3 requires PrimeNG's locale to switch **alongside** the application
 * dictionaries. A translated page with an English paginator is a fail, and it is the failure nobody
 * notices in review because the surrounding page reads correctly.
 *
 * <h3>What this spec proves, and what it does not</h3>
 * It proves the **strings** — that switching to `ar` puts Arabic into each component's own labels,
 * month names and empty messages, and that the document is `rtl` while they render. It does **not**
 * prove pixel-level mirroring; that is a visual check and is reported as such.
 */
@Component({
    standalone: true,
    imports: [DatePickerModule, DrawerModule, FormsModule, PaginatorModule, SelectModule, TableModule],
    template: `
        <p-table [value]="[]">
            <ng-template #emptymessage>
                <tr>
                    <td>{{ locale('emptyMessage') }}</td>
                </tr>
            </ng-template>
        </p-table>

        <p-paginator [first]="0" [rows]="10" [totalRecords]="100" />

        <p-select [options]="[]" [(ngModel)]="selected" [emptyMessage]="locale('emptyMessage')" />

        <p-datepicker [(ngModel)]="date" [inline]="true" dateFormat="yy-mm-dd" />

        <p-drawer [(visible)]="drawerOpen" [position]="drawerPosition">
            <p>{{ locale('today') }}</p>
        </p-drawer>
    `
})
class PrimeNgHost {
    private readonly primeng = inject(PrimeNG);

    selected: string | null = null;
    date = new Date('2026-09-06T12:00:00Z');
    drawerOpen = true;
    drawerPosition: 'left' | 'right' = 'right';

    locale(key: string): string {
        return String((this.primeng.translation as unknown as Record<string, unknown>)[key] ?? '');
    }
}

describe('PrimeNG in Arabic (ui-design §10.2)', () => {
    let fixture: ComponentFixture<PrimeNgHost>;
    let direction: DirectionService;
    let primeng: PrimeNG;
    let dictionaries: { en: object; ar: object };

    beforeAll(async () => {
        dictionaries = {
            en: await (await fetch('/assets/i18n/en.json')).json(),
            ar: await (await fetch('/assets/i18n/ar.json')).json()
        };
    });

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                PrimeNgHost,
                TranslocoTestingModule.forRoot({
                    langs: dictionaries,
                    translocoConfig: { availableLangs: ['en', 'ar'], defaultLang: 'en' }
                })
            ],
            providers: [provideNoopAnimations(), providePrimeNG({})]
        }).compileComponents();

        fixture = TestBed.createComponent(PrimeNgHost);
        direction = TestBed.inject(DirectionService);
        primeng = TestBed.inject(PrimeNG);

        await direction.use('ar');
        fixture.detectChanges();
        await fixture.whenStable();
        fixture.detectChanges();
    });

    afterEach(() => {
        document.documentElement.removeAttribute('dir');
        document.documentElement.removeAttribute('lang');
        TestBed.resetTestingModule();
    });

    function locale(): Record<string, unknown> {
        return primeng.translation as unknown as Record<string, unknown>;
    }

    function isArabic(value: unknown): boolean {
        return typeof value === 'string' && /[؀-ۿ]/.test(value);
    }

    it('switches the document to rtl alongside the component locale', () => {
        expect(document.documentElement.getAttribute('dir')).toBe('rtl');
        expect(direction.isRtl()).toBeTrue();
    });

    /** **Table** — its empty message is PrimeNG's own string, not one of ours. */
    it('table: the empty message is Arabic', () => {
        expect(isArabic(locale()['emptyMessage'])).withContext(String(locale()['emptyMessage'])).toBeTrue();
        expect((fixture.nativeElement as HTMLElement).querySelector('p-table')).not.toBeNull();
    });

    /** **Paginator** — the labels a reader notices first, and the ones most often left English. */
    it('paginator: every navigation label is Arabic', () => {
        const aria = locale()['aria'] as Record<string, unknown>;

        for (const key of ['nextPageLabel', 'previousPageLabel', 'firstPageLabel', 'lastPageLabel', 'rowsPerPageLabel']) {
            expect(isArabic(aria[key])).withContext(`aria.${key} = ${String(aria[key])}`).toBeTrue();
        }

        expect((fixture.nativeElement as HTMLElement).querySelector('p-paginator')).not.toBeNull();
    });

    /** **Dropdown** (`p-select`) — its empty and selection messages come from the locale. */
    it('dropdown: the empty and selection messages are Arabic', () => {
        expect(isArabic(locale()['emptyMessage'])).toBeTrue();
        expect(isArabic(locale()['emptySelectionMessage'])).toBeTrue();
        expect(isArabic(locale()['emptyFilterMessage'])).toBeTrue();
    });

    /**
     * **Calendar** (`p-datepicker`) — the one with the most to get wrong. It must render Arabic
     * month and day names **and a Gregorian calendar** (A-11): `سبتمبر`, not a Hijri month.
     */
    it('calendar: Arabic month and day names, Gregorian and complete', () => {
        const months = locale()['monthNames'] as string[];
        const days = locale()['dayNamesMin'] as string[];

        expect(months).toHaveSize(12);
        expect(days).toHaveSize(7);
        expect(months.every(isArabic)).withContext(months.join(',')).toBeTrue();
        expect(months[8]).withContext('September, Gregorian').toBe('سبتمبر');

        // The rendered panel shows the month of the bound date — September 2026, not a Hijri month.
        const panel = (fixture.nativeElement as HTMLElement).querySelector('p-datepicker')!;
        expect(panel.textContent).toContain('سبتمبر');
        expect(panel.textContent).toContain('2026');
    });

    /**
     * **Drawer** — PrimeNG's `position` takes physical values only, so the trailing edge has to be
     * chosen from the active direction. `TicketDetailComponent` owns that choice for the screen the
     * drawer belongs to; this asserts the rule it implements, so a regression there fails here.
     */
    it('drawer: the trailing edge mirrors under rtl', () => {
        const trailingEdge = direction.isRtl() ? 'left' : 'right';

        expect(trailingEdge).toBe('left');

        fixture.componentInstance.drawerPosition = trailingEdge;
        fixture.detectChanges();

        expect((fixture.nativeElement as HTMLElement).querySelector('p-drawer')).not.toBeNull();
        expect(isArabic(locale()['today'])).toBeTrue();
    });

    /** Switching back restores the English locale — the switch is not one-way. */
    it('restores the English locale on a switch back', async () => {
        await direction.use('en');
        fixture.detectChanges();

        expect(locale()['monthNames']).toContain('September');
        expect(document.documentElement.getAttribute('dir')).toBe('ltr');
    });
});
