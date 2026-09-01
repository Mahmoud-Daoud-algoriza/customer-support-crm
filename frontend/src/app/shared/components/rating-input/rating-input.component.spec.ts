import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { RatingInputComponent } from './rating-input.component';

/**
 * **The one property this control must have while OQ-1 is open: it renders the *configured* range
 * and nothing else.**
 *
 * ui-design §11 forbids hardcoding a star widget, and data-model §2.15 forbids inferring any range
 * into a UI control. A hardcoded `1..5` would be invisible in a screenshot and would keep working
 * for as long as configuration happened to say 1–5 — so the assertion is made with a range that is
 * deliberately **not** the placeholder in `appsettings.json`. If someone replaces this control with
 * five stars, this spec fails.
 */
describe('RatingInputComponent', () => {
    let fixture: ComponentFixture<RatingInputComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                RatingInputComponent,
                TranslocoTestingModule.forRoot({
                    langs: { en: {} },
                    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' }
                })
            ]
        }).compileComponents();

        fixture = TestBed.createComponent(RatingInputComponent);
    });

    afterEach(() => {
        TestBed.resetTestingModule();
    });

    function steps(): HTMLButtonElement[] {
        return Array.from(
            (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('.app-rating__step')
        );
    }

    function render(min: number, max: number): void {
        fixture.componentRef.setInput('min', min);
        fixture.componentRef.setInput('max', max);
        fixture.detectChanges();
    }

    /** A three-step range renders three steps — not five, and not a fixed widget. */
    it('renders exactly the configured range', () => {
        render(2, 4);

        expect(steps().map((step) => step.textContent!.trim())).toEqual(['2', '3', '4']);
    });

    /**
     * **The binary shape ui-design §11 names as the other candidate.** A two-value range renders two
     * controls, so the component already covers that answer without anyone hardcoding a thumbs pair.
     */
    it('renders a two-value range as two controls', () => {
        render(0, 1);

        expect(steps().length).toBe(2);
    });

    /**
     * **Nothing is selected until the customer selects it.** §7.3: declining is normal, so a default
     * would record an opinion nobody gave, and the submit button is what reads this to stay disabled.
     */
    it('starts with nothing selected and emits only what was chosen', () => {
        render(1, 3);

        expect(steps().some((step) => step.classList.contains('app-rating__step--selected'))).toBeFalse();

        const emitted: number[] = [];
        fixture.componentInstance.valueChange.subscribe((value) => emitted.push(value));

        steps()[2].click();

        expect(emitted).toEqual([3]);
    });

    /**
     * An inverted or absurd configured range renders **no scale**, rather than a guessed one:
     * startup validation already refuses `Min >= Max`, so this is a fault the server does not start
     * with — and the control must not paper over one by inventing a range of its own.
     */
    it('renders no scale for an impossible range rather than inventing one', () => {
        render(5, 1);

        expect(steps().length).toBe(0);
    });
});
