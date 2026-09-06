import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Type } from '@angular/core';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { ConfirmationService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { PortalRequestDetailComponent } from '../features/portal/portal-request-detail.component';
import { AgentQueueComponent } from '../features/workspace/tickets/agent-queue.component';
import { TicketDetailComponent } from '../features/workspace/tickets/ticket-detail.component';

/**
 * Story 17 Part B task 5 — the regression check T3-F actually cares about.
 *
 * `docs/ui-design.md` §10.3 and `docs/architecture.md` §2.3 both say the same thing: wide content
 * scrolls **inside its own container**, so *"the page body never scrolls sideways."* That is one
 * measurement, and it is cheap: at phone width, `document.body.scrollWidth` must not exceed
 * `window.innerWidth`.
 *
 * <h3>How the phone viewport is obtained</h3>
 * Karma runs each spec inside its context iframe, so `window` here **is** the iframe's window and
 * its width is the viewport the CSS media queries are evaluated against. Narrowing that iframe to
 * **390 px** therefore puts the real phone rules into effect — the stacked cards, the filter sheet,
 * the single-column accordion — rather than measuring a desktop layout squeezed into a narrow box,
 * which would fail for the wrong reason. If a runner ever denies access to the frame element the
 * suite **fails loudly rather than passing vacuously**, because a green check that measured nothing
 * is worse than a red one.
 *
 * <h3>Both directions</h3>
 * Every surface is measured in `ltr` and in `rtl`. A layout that overflows only in Arabic is the
 * failure mode logical properties exist to prevent, and it is invisible if only one direction is
 * measured.
 */
describe('Phone width — the page body never scrolls sideways (T3-F, A-1)', () => {
    const PHONE_WIDTH = 390;
    const TICKET_ID = '11111111-1111-1111-1111-111111111111';

    let restoreViewport: () => void;
    let dictionaries: { en: object; ar: object };

    beforeAll(async () => {
        restoreViewport = narrowViewportTo(PHONE_WIDTH);

        // **The real dictionaries, not stubs.** An untranslated key renders as the key itself —
        // `tickets.slaOverdue` is far longer than "overdue" — so stubbing them would measure text
        // the application never shows and fail for a reason that does not exist.
        dictionaries = {
            en: await (await fetch('/assets/i18n/en.json')).json(),
            ar: await (await fetch('/assets/i18n/ar.json')).json()
        };
    });

    afterAll(() => {
        restoreViewport();
        setDirection('ltr');
    });

    afterEach(() => {
        setDirection('ltr');
        TestBed.resetTestingModule();
    });

    it('really is running at phone width', () => {
        expect(window.innerWidth)
            .withContext('the iframe did not narrow — every measurement below would be meaningless')
            .toBeLessThanOrEqual(PHONE_WIDTH);
    });

    for (const direction of ['ltr', 'rtl'] as const) {
        describe(direction, () => {
            /** **Agent queue** — the table becomes stacked cards and the filters a sheet (§10.3). */
            it('agent queue', async () => {
                setDirection(direction);

                const fixture = await mount(AgentQueueComponent);
                flushAll(queueResponses());
                fixture.detectChanges();

                expectNoBodyOverflow(fixture);
            });

            /**
             * **Ticket detail** — regions become a single-column accordion, the customer panel a
             * drawer and the composer docks to the bottom (UI-4, §10.3). It is the densest staff
             * screen and therefore the one most likely to push the body wide.
             */
            it('ticket detail', async () => {
                setDirection(direction);

                const fixture = await mount(TicketDetailComponent, [
                    { provide: ActivatedRoute, useValue: routeWithId(TICKET_ID) }
                ]);
                flushAll(ticketDetailResponses());
                fixture.detectChanges();

                expectNoBodyOverflow(fixture);
            });

            /** **Customer portal** — single column throughout, cards and never tables (§10.3). */
            it('portal request detail', async () => {
                setDirection(direction);

                const fixture = await mount(PortalRequestDetailComponent, [
                    { provide: ActivatedRoute, useValue: routeWithId(TICKET_ID) },
                    ConfirmationService
                ]);
                flushAll(portalDetailResponses());
                fixture.detectChanges();

                expectNoBodyOverflow(fixture);
            });
        });
    }

    // ---------------------------------------------------------------------------------- harness

    async function mount<T>(component: Type<T>, extraProviders: unknown[] = []): Promise<ComponentFixture<T>> {
        await TestBed.configureTestingModule({
            imports: [
                component,
                TranslocoTestingModule.forRoot({
                    langs: dictionaries,
                    translocoConfig: { availableLangs: ['en', 'ar'], defaultLang: direction() === 'rtl' ? 'ar' : 'en' }
                })
            ],
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                provideNoopAnimations(),
                provideRouter([]),
                providePrimeNG({}),
                ...(extraProviders as never[])
            ]
        }).compileComponents();

        const fixture = TestBed.createComponent(component);
        fixture.detectChanges();

        return fixture;
    }

    /**
     * Answers every request the screen made, matching on the URL. A screen loads its regions
     * independently (§9), so the set and the order vary — matching on the URL rather than asserting
     * a sequence keeps this spec about layout, which is what it is for. **An unmatched request is
     * flushed with an empty object rather than left pending**, so a region that would otherwise sit
     * in its loading state still renders something to measure.
     */
    function flushAll(responses: Record<string, object>): void {
        const controller = TestBed.inject(HttpTestingController);

        // Longest first: `/tickets/{id}/activity` must not be answered by the `/tickets/` fixture.
        const paths = Object.keys(responses).sort((a, b) => b.length - a.length);

        // A region loads only once the payload it depends on has arrived, so answering the first
        // wave produces a second. Drain until quiet, with a bound so a mistake here cannot hang the
        // suite.
        for (let round = 0; round < 6; round++) {
            const pending = controller.match(() => true);

            if (pending.length === 0) {
                return;
            }

            for (const request of pending) {
                const key = paths.find((path) => request.request.url.includes(path));
                request.flush(key ? responses[key] : {});
            }

            fixtureTick();
        }
    }

    /** Lets the just-flushed responses reach the template before the next drain round. */
    function fixtureTick(): void {
        TestBed.tick();
    }

    /**
     * The measurement, with the runner's own chrome hidden. Karma's HTML reporter renders into the
     * same document, and its width has nothing to do with the application's layout — leaving it
     * visible would measure the reporter, not the screen.
     */
    function expectNoBodyOverflow(fixture: ComponentFixture<unknown>): void {
        const restore = hideEverythingExcept(fixture.nativeElement as HTMLElement);

        try {
            expect(document.body.scrollWidth)
                .withContext(`${direction()} · body scrollWidth vs viewport · widest: ${describeOffenders()}`)
                .toBeLessThanOrEqual(window.innerWidth);
        } finally {
            restore();
        }
    }
});

// -------------------------------------------------------------------------------------- fixtures

function routeWithId(id: string) {
    return { snapshot: { paramMap: new Map([['id', id]]) as unknown as { get(name: string): string | null } } };
}

function queueResponses(): Record<string, object> {
    return {
        '/tickets': paged([
            {
                id: '11111111-1111-1111-1111-111111111111',
                subject: 'A subject long enough to need wrapping rather than widening the page body',
                customer: { id: 'c1', fullName: 'Ada Lovelace' },
                status: 'Open',
                priority: 'High',
                categoryCode: 'billing',
                departmentId: 'd1',
                assignee: { id: 'u1', displayName: 'Bilal Haddad' },
                createdAt: '2026-08-30T09:00:00Z',
                resolutionDueAt: '2026-09-01T09:00:00Z',
                firstResponseBreached: false,
                resolutionBreached: false
            }
        ]),
        '/config': customerConfig()
    };
}

function ticketDetailResponses(): Record<string, object> {
    return {
        '/config': customerConfig(),
        '/config/staff': staffConfig(),
        '/activity': paged([]),
        '/messages': paged([]),
        '/attachments': [],
        '/notes': paged([]),
        '/tasks': paged([]),
        '/suggested': [],
        '/timeline': paged([]),
        '/customers/': customer(),
        '/tickets/': ticket()
    };
}

function portalDetailResponses(): Record<string, object> {
    return {
        '/config': customerConfig(),
        '/messages': paged([]),
        '/attachments': [],
        'portal/tickets/': ticket()
    };
}

function ticket(): Record<string, unknown> {
    return {
        id: '11111111-1111-1111-1111-111111111111',
        subject: 'A subject long enough to need wrapping rather than widening the page body',
        description: 'A description with a very long unbroken run of prose that must wrap inside its column.',
        customer: { id: 'c1', fullName: 'Ada Lovelace', email: 'ada@example.com' },
        status: 'Open',
        priority: 'High',
        categoryCode: 'billing',
        departmentId: 'd1',
        assignee: { id: 'u1', displayName: 'Bilal Haddad' },
        createdAt: '2026-08-30T09:00:00Z',
        firstResponseDueAt: '2026-08-30T13:00:00Z',
        resolutionDueAt: '2026-09-01T09:00:00Z',
        firstResponseBreached: false,
        resolutionBreached: false,
        isUrgent: false,
        allowedTransitions: []
    };
}

function staffConfig(): Record<string, unknown> {
    return {
        priorities: ['Low', 'Medium', 'High', 'Urgent'],
        quickReplies: [{ id: 'ack', title: 'Acknowledge', body: 'Thank you for getting in touch.' }],
        slaTargets: [],
        categoryDepartmentMap: []
    };
}

function customer(): Record<string, unknown> {
    return {
        id: 'c1',
        fullName: 'Ada Lovelace',
        email: 'ada@example.com',
        phone: '+966 55 000 0000',
        branch: { id: 'b1', name: 'Riyadh' },
        externalReference: null,
        createdAt: '2026-01-04T09:00:00Z',
        openTicketCount: 2
    };
}

function customerConfig(): Record<string, unknown> {
    return {
        categories: [{ code: 'billing', name: 'Billing' }],
        feedback: { ratingScale: { min: 1, max: 5 } }
    };
}

function paged(items: unknown[]): Record<string, unknown> {
    return { items, page: 1, pageSize: 25, totalItems: items.length, totalPages: 1 };
}

// --------------------------------------------------------------------------------------- the DOM

function direction(): 'ltr' | 'rtl' {
    return document.documentElement.getAttribute('dir') === 'rtl' ? 'rtl' : 'ltr';
}

function setDirection(value: 'ltr' | 'rtl'): void {
    document.documentElement.setAttribute('dir', value);
    document.documentElement.setAttribute('lang', value === 'rtl' ? 'ar' : 'en');
}

/**
 * Narrows the runner's own frame so the viewport — and therefore every media query — really is at
 * phone width. Returns the undo.
 */
function narrowViewportTo(width: number): () => void {
    const frame = window.frameElement as HTMLElement | null;

    if (!frame) {
        throw new Error('This runner does not expose window.frameElement, so the viewport cannot be narrowed. ' + 'The phone-width check must not be reported as passing — run it in the Karma browser target.');
    }

    const previous = frame.getAttribute('style');
    frame.style.width = `${width}px`;
    frame.style.maxWidth = `${width}px`;

    return () => {
        if (previous === null) {
            frame.removeAttribute('style');
        } else {
            frame.setAttribute('style', previous);
        }
    };
}

/**
 * Names the elements sticking out past the viewport. A bare "397 > 390" says nothing about which
 * rule to fix, and this check is only worth having if a failure is actionable.
 */
function describeOffenders(): string {
    const viewport = window.innerWidth;

    return Array.from(document.body.querySelectorAll<HTMLElement>('*'))
        .map((element) => ({ element, box: element.getBoundingClientRect() }))
        .filter(({ box }) => box.width > 0 && (box.right > viewport + 1 || box.left < -1))
        .sort((a, b) => b.box.width - a.box.width)
        .slice(0, 5)
        .map(({ element, box }) => `${element.tagName.toLowerCase()}.${element.className || '(no class)'}@${Math.round(box.left)}..${Math.round(box.right)}`)
        .join(' | ') || '(nothing measurable)';
}

/** Hides every top-level node except the one holding the screen under test. Returns the undo. */
function hideEverythingExcept(element: HTMLElement): () => void {
    const host = element.closest('body > *') ?? element;
    const hidden: HTMLElement[] = [];

    for (const child of Array.from(document.body.children)) {
        if (child !== host && child instanceof HTMLElement && !child.hidden) {
            child.hidden = true;
            hidden.push(child);
        }
    }

    return () => hidden.forEach((child) => (child.hidden = false));
}
