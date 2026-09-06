import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { providePrimeNG } from 'primeng/config';
import { ReplyComposerComponent } from '../../shared/components/reply-composer/reply-composer.component';
import { DirectionService } from './direction.service';

/**
 * Story 17 Part B task 7 — **switching language must not lose application state**.
 *
 * This is the acceptance criterion a late change is most likely to break, and the one AD-9 chose a
 * runtime library over `@angular/localize` for: a compile-time locale means one bundle per language
 * and a full reload, which loses everything below by definition.
 *
 * The subject is the **reply composer**, because the story names it: *"open a ticket, type a partial
 * reply into the composer, switch to العربية — the draft is intact … and no network request
 * re-fetches the ticket."* The composer is the shared one both the workspace and the portal mount
 * (UI-7), so proving it here proves it on both surfaces.
 */
describe('Language switch — application state survives (T2-J, AD-9)', () => {
    const DRAFT = 'Thank you for getting in touch, we are looking into';

    let fixture: ComponentFixture<ReplyComposerComponent>;
    let direction: DirectionService;
    let controller: HttpTestingController;
    let sent: string[];

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                ReplyComposerComponent,
                TranslocoTestingModule.forRoot({
                    langs: {
                        en: { 'tickets.reply': 'Reply' },
                        ar: { 'tickets.reply': 'رد' }
                    },
                    translocoConfig: { availableLangs: ['en', 'ar'], defaultLang: 'en' }
                })
            ],
            providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), providePrimeNG({})]
        }).compileComponents();

        fixture = TestBed.createComponent(ReplyComposerComponent);
        direction = TestBed.inject(DirectionService);
        controller = TestBed.inject(HttpTestingController);

        sent = [];
        fixture.componentInstance.send.subscribe((body: string) => sent.push(body));

        fixture.detectChanges();
    });

    afterEach(() => {
        document.documentElement.removeAttribute('dir');
        document.documentElement.removeAttribute('lang');
        TestBed.resetTestingModule();
    });

    function textarea(): HTMLTextAreaElement {
        return (fixture.nativeElement as HTMLElement).querySelector('textarea')!;
    }

    async function type(value: string): Promise<void> {
        const box = textarea();
        box.value = value;
        box.dispatchEvent(new Event('input'));
        fixture.detectChanges();
        await fixture.whenStable();
    }

    /**
     * The whole criterion in one spec: a half-typed reply is still there after the switch, the
     * interface is in Arabic and right-to-left, **and the switch issued no request** — nothing was
     * re-fetched and nothing was reloaded.
     */
    it('keeps an in-progress draft, and issues no request, when the language changes', async () => {
        await type(DRAFT);

        // Anything outstanding from mounting the composer is not what this spec is about.
        controller.verify();

        await direction.use('ar');
        fixture.detectChanges();
        await fixture.whenStable();

        expect(textarea().value).withContext('the draft survives the switch').toBe(DRAFT);
        expect(direction.activeLanguage()).toBe('ar');
        expect(direction.isRtl()).toBeTrue();
        expect(document.documentElement.getAttribute('dir')).toBe('rtl');
        expect(document.documentElement.getAttribute('lang')).toBe('ar');

        // No re-fetch: the switch is a client-side re-render, not a reload (AD-9).
        controller.verify();

        // And nothing was sent on the user's behalf, in either language (T1-C).
        expect(sent).toEqual([]);
    });

    /** Switching back is the same operation in the other direction, and loses no more state. */
    it('keeps the draft across a switch back to English', async () => {
        await type(DRAFT);

        await direction.use('ar');
        await direction.use('en');
        fixture.detectChanges();
        await fixture.whenStable();

        expect(textarea().value).toBe(DRAFT);
        expect(direction.isRtl()).toBeFalse();
        expect(document.documentElement.getAttribute('dir')).toBe('ltr');

        controller.verify();
        expect(sent).toEqual([]);
    });

    /**
     * The choice persists per user in browser storage (`docs/ui-design.md` §10.1), and a stored
     * value the server no longer offers is ignored rather than applied.
     */
    it('persists the chosen language and ignores one the server does not offer', async () => {
        await direction.use('ar');

        expect(direction.storedLanguage(['en', 'ar'])).toBe('ar');
        expect(direction.storedLanguage(['en'])).withContext('no longer offered').toBeNull();
    });
});
