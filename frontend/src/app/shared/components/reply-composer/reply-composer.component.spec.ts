import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { ReplyComposerComponent } from './reply-composer.component';

/**
 * The quick-reply library Story 08 adds to the **shared** composer (UI-7), and the two rules that
 * make it safe.
 *
 * **Inserting is not sending** is a T1-C acceptance criterion, and **the portal must not call
 * `GET /config/staff`** is AP-17 plus UI-11 — the same component renders on both surfaces, so the
 * second is a real risk rather than a theoretical one.
 */
describe('ReplyComposerComponent — quick replies', () => {
    const STAFF_CONFIG = '/api/v1/config/staff';

    let fixture: ComponentFixture<ReplyComposerComponent>;
    let controller: HttpTestingController;
    let sent: string[];

    const library = {
        priorities: ['Low', 'Medium', 'High', 'Urgent'],
        quickReplies: [
            { id: 'ack', title: 'Acknowledge', body: 'Thank you for getting in touch.' },
            { id: 'need-info', title: 'Request more information', body: 'Could you tell us more?' }
        ],
        slaTargets: [],
        categoryDepartmentMap: []
    };

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                ReplyComposerComponent,
                TranslocoTestingModule.forRoot({
                    langs: { en: {} },
                    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' }
                })
            ],
            providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()]
        }).compileComponents();

        fixture = TestBed.createComponent(ReplyComposerComponent);
        controller = TestBed.inject(HttpTestingController);

        sent = [];
        fixture.componentInstance.send.subscribe((body: string) => sent.push(body));
    });

    afterEach(() => {
        TestBed.resetTestingModule();
    });

    function withQuickReplies(): void {
        fixture.componentRef.setInput('quickReplies', true);
        fixture.detectChanges();
        controller.expectOne(STAFF_CONFIG).flush(library);
        fixture.detectChanges();
    }

    /**
     * **The rule the whole control exists under** (T1-C, and A-8's discipline applied to canned
     * text). Insertion goes through the same `insert` the AI draft will use in Story 11, so
     * "never auto-sent" holds by construction.
     */
    it('inserts editable text into the draft and sends nothing', async () => {
        withQuickReplies();

        fixture.componentInstance.insert(library.quickReplies[0].body);
        fixture.detectChanges();
        await fixture.whenStable();

        const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea')!;

        expect(textarea.value).toBe('Thank you for getting in touch.');

        // The whole point: the text is in the box and the server has heard nothing.
        expect(sent).toEqual([]);
        controller.verify();
    });

    /** A suggestion must not silently delete what the agent already wrote. */
    it('appends to an existing draft rather than replacing it', async () => {
        withQuickReplies();

        fixture.componentInstance.insert('Hello Ada,');
        fixture.componentInstance.insert(library.quickReplies[0].body);
        fixture.detectChanges();
        await fixture.whenStable();

        const textarea = (fixture.nativeElement as HTMLElement).querySelector('textarea')!;

        expect(textarea.value).toContain('Hello Ada,');
        expect(textarea.value).toContain('Thank you for getting in touch.');
        expect(sent).toEqual([]);
    });

    /**
     * **The portal never asks for staff configuration.** `GET /config/staff` answers a Customer with
     * `403` (AP-17), and the control must not appear on their screen at all (UI-11). Off is the
     * default, so this is the portal's configuration of the shared component.
     */
    it('makes no request and shows no control when quick replies are off', () => {
        fixture.detectChanges();

        controller.expectNone(STAFF_CONFIG);
        expect((fixture.nativeElement as HTMLElement).querySelector('p-select')).toBeNull();
    });

    /**
     * A failed library must not put an error state over a composer that still works — quick replies
     * are a convenience, and the reply box is the feature.
     */
    it('still composes when the library cannot be loaded', async () => {
        fixture.componentRef.setInput('quickReplies', true);
        fixture.detectChanges();

        controller.expectOne(STAFF_CONFIG).flush(
            { type: 'server-error', title: 'Server error', status: 500 },
            { status: 500, statusText: 'Server Error' }
        );
        fixture.detectChanges();

        expect((fixture.nativeElement as HTMLElement).querySelector('textarea')).not.toBeNull();

        fixture.componentInstance.insert('Typed by hand.');
        fixture.detectChanges();
        await fixture.whenStable();

        expect((fixture.nativeElement as HTMLElement).querySelector('textarea')!.value).toBe('Typed by hand.');
    });

    /** Sending remains the button's job alone, and it trims. */
    it('emits the draft only when send is invoked', () => {
        withQuickReplies();

        fixture.componentInstance.insert('  A reply.  ');
        fixture.detectChanges();

        expect(sent).toEqual([]);

        (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('p-button button')!.click();

        expect(sent).toEqual(['A reply.']);
    });
});
