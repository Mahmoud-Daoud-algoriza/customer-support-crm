import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { SlaIndicatorComponent } from './sla-indicator.component';

/**
 * The three display rules the design fixes for `SlaIndicator` (docs/ui-design.md §8, §11) — the only
 * new logic in the My queue slice, and the part a reader cannot verify by looking at it.
 *
 * The clock is injected through `now`, so nothing here depends on when the suite runs.
 */
describe('SlaIndicatorComponent', () => {
    const now = new Date('2026-08-31T12:00:00Z');

    let fixture: ComponentFixture<SlaIndicatorComponent>;

    function render(dueAt: string | null, breached = false): string {
        fixture.componentRef.setInput('dueAt', dueAt);
        fixture.componentRef.setInput('breached', breached);
        fixture.componentRef.setInput('now', now);
        fixture.detectChanges();

        return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ').trim();
    }

    function breachedClassApplied(): boolean {
        return (fixture.nativeElement as HTMLElement).querySelector('.app-breach') !== null;
    }

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                SlaIndicatorComponent,
                TranslocoTestingModule.forRoot({
                    langs: {
                        en: {
                            tickets: {
                                slaRemaining: 'left',
                                slaOverdue: 'overdue',
                                slaUnknown: 'No resolution target',
                                slaUnit: { day: 'd', hour: 'h', minute: 'm' }
                            }
                        }
                    },
                    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' }
                })
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(SlaIndicatorComponent);
    });

    it('renders an em dash for a null due date, not "breached" and not "0"', () => {
        const text = render(null);

        expect(text).toBe('—');
        expect(text).not.toContain('0');
        expect(breachedClassApplied()).toBeFalse();
    });

    it('renders remaining time for a future deadline, without the breached style', () => {
        expect(render('2026-08-31T14:30:00Z')).toBe('2h 30m left');
        expect(breachedClassApplied()).toBeFalse();
    });

    it('renders a past deadline as overdue, with the breached style', () => {
        expect(render('2026-08-31T09:00:00Z')).toBe('3h overdue');
        expect(breachedClassApplied()).toBeTrue();
    });

    /**
     * **The latched flag wins over the clock.** A ticket the server has flagged breached reads as
     * breached even when its deadline is still in the future — the flag is the fact, the clock is
     * only a derivation. This is the case a clock-only implementation gets wrong.
     */
    it('honours the latched breach flag even when the deadline has not passed', () => {
        expect(render('2026-08-31T18:00:00Z', true)).toBe('6h overdue');
        expect(breachedClassApplied()).toBeTrue();
    });

    it('drops to the largest useful unit for long and short spans alike', () => {
        expect(render('2026-09-03T16:00:00Z')).toBe('3d 4h left');
        expect(render('2026-08-31T12:08:00Z')).toBe('8m left');
    });
});
