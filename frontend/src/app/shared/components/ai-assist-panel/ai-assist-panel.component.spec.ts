import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { AiAssistPanelComponent } from './ai-assist-panel.component';

/**
 * The two A-8 guardrails this panel is responsible for, and the T1-F degradation rule.
 *
 * **Inserting is not sending** and **the label is always visible** are acceptance criteria, not
 * styling choices — so they get assertions rather than a review.
 */
describe('AiAssistPanelComponent', () => {
    const TICKET = '11111111-1111-1111-1111-111111111111';
    const SUMMARY_URL = `/api/v1/tickets/${TICKET}/ai/summary`;
    const REPLY_URL = `/api/v1/tickets/${TICKET}/ai/suggested-reply`;

    let fixture: ComponentFixture<AiAssistPanelComponent>;
    let controller: HttpTestingController;
    let inserted: string[];

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                AiAssistPanelComponent,
                TranslocoTestingModule.forRoot({
                    langs: { en: { ai: { generatedLabel: 'AI-generated' } } },
                    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' }
                })
            ],
            providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()]
        }).compileComponents();

        fixture = TestBed.createComponent(AiAssistPanelComponent);
        controller = TestBed.inject(HttpTestingController);

        fixture.componentRef.setInput('ticketId', TICKET);

        inserted = [];
        fixture.componentInstance.insertDraft.subscribe((text: string) => inserted.push(text));

        fixture.detectChanges();
    });

    afterEach(() => {
        TestBed.resetTestingModule();
    });

    function text(): string {
        return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
    }

    function buttons(): HTMLButtonElement[] {
        return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
    }

    function click(label: RegExp): void {
        const target = buttons().find((b) => label.test(b.textContent ?? ''));

        if (!target) {
            throw new Error(`No button matching ${label}. Found: ${buttons().map((b) => b.textContent).join(' | ')}`);
        }

        target.click();
        fixture.detectChanges();
    }

    it('makes no request until an action is taken', () => {
        // The panel is an aid, not a cost imposed on every ticket that is opened.
        controller.expectNone(SUMMARY_URL);
        controller.expectNone(REPLY_URL);
    });

    /**
     * **The label is always visible** (A-8, UI-6) — a rendered element, not a tooltip and not a
     * hover state.
     */
    it('renders a summary with a visible AI-generated label', () => {
        fixture.componentInstance['summarize']();
        fixture.detectChanges();

        controller.expectOne(SUMMARY_URL).flush({
            summary: 'The customer cannot pay.',
            generatedBy: 'ai',
            generatedAt: '2026-08-31T12:00:00Z'
        });
        fixture.detectChanges();

        expect(text()).toContain('AI-generated');
        expect(text()).toContain('The customer cannot pay.');

        // Present in the DOM, not merely in an attribute a user would have to hover to see.
        expect((fixture.nativeElement as HTMLElement).querySelector('.app-ai-result__label')).not.toBeNull();
    });

    /**
     * **The whole point of the panel.** A suggested reply is emitted for insertion, and the panel has
     * no path that could send it — there is no send here, and the composer's Send remains the agent's.
     */
    it('emits the draft for insertion and never sends anything', () => {
        fixture.componentInstance['suggestReply']();
        fixture.detectChanges();

        controller.expectOne(REPLY_URL).flush({
            draft: 'Thank you for getting in touch.',
            generatedBy: 'ai',
            generatedAt: '2026-08-31T12:00:00Z'
        });
        fixture.detectChanges();

        expect(inserted).toEqual([]);

        click(/Insert/i);

        expect(inserted).toEqual(['Thank you for getting in touch.']);

        // No further HTTP call of any kind — insertion is local, and nothing was posted.
        controller.verify();
    });

    /**
     * **The label survives insertion.** Clearing the suggestion on insert would remove the
     * AI-generated marker at the moment authorship matters most.
     */
    it('keeps the suggestion and its label visible after insertion', () => {
        fixture.componentInstance['suggestReply']();
        fixture.detectChanges();

        controller.expectOne(REPLY_URL).flush({
            draft: 'A draft.',
            generatedBy: 'ai',
            generatedAt: '2026-08-31T12:00:00Z'
        });
        fixture.detectChanges();

        click(/Insert/i);

        expect(text()).toContain('AI-generated');
        expect(text()).toContain('A draft.');
    });

    /**
     * **`503` degrades this panel and leaves its controls usable** (T1-F). The buttons must not
     * disable, or a transient outage would strand the agent with no retry.
     */
    it('shows unavailable on 503 and keeps its buttons enabled', () => {
        fixture.componentInstance['summarize']();
        fixture.detectChanges();

        controller.expectOne(SUMMARY_URL).flush(
            { type: 'ai-unavailable', title: 'Service unavailable', status: 503 },
            { status: 503, statusText: 'Service Unavailable' }
        );
        fixture.detectChanges();

        expect((fixture.nativeElement as HTMLElement).querySelector('p-message')).not.toBeNull();

        // Every button is still clickable, so a retry costs one click.
        expect(buttons().every((b) => !b.disabled)).toBeTrue();
    });
});
