import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { AgentQueueComponent } from './agent-queue.component';

/**
 * My queue's **request contract**, which is where this screen can be wrong in a way the rendering
 * would not reveal: the queue's whole promise depends on sending `assigneeId=me` and on **not**
 * sending a `sort`, so the server's SLA-urgency default applies (docs/ui-design.md §5.1).
 *
 * The ordering itself is the server's and is proven server-side by `QueueOrderingTests`; nothing
 * here re-asserts it, because a client-side ordering assertion would pass even if the screen sorted
 * the rows itself — which is exactly the mistake §5.1 forbids.
 */
describe('AgentQueueComponent', () => {
    const TICKETS = '/api/v1/tickets';

    let fixture: ComponentFixture<AgentQueueComponent>;
    let controller: HttpTestingController;

    function page(items: unknown[]) {
        return { items, page: 1, pageSize: 25, totalItems: items.length, totalPages: 1 };
    }

    function row(overrides: Record<string, unknown> = {}) {
        return {
            id: '11111111-1111-1111-1111-111111111111',
            subject: 'Card declined at checkout',
            customer: { id: 'c1', fullName: 'Ada Lovelace' },
            status: 'Open',
            priority: 'High',
            categoryCode: 'billing',
            departmentId: 'd1',
            assignee: { id: 'u1', displayName: 'Bilal Haddad' },
            createdAt: '2026-08-30T09:00:00Z',
            resolutionDueAt: '2026-09-01T09:00:00Z',
            firstResponseBreached: false,
            resolutionBreached: false,
            ...overrides
        };
    }

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [
                AgentQueueComponent,
                TranslocoTestingModule.forRoot({
                    langs: { en: {} },
                    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' }
                })
            ],
            // `app-error-state` renders PrimeNG's `p-message`, which declares animations.
            providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([])]
        }).compileComponents();

        fixture = TestBed.createComponent(AgentQueueComponent);
        controller = TestBed.inject(HttpTestingController);
        fixture.detectChanges();
    });

    afterEach(() => {
        TestBed.resetTestingModule();
    });

    function text(): string {
        return (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
    }

    /**
     * **The two halves of the contract, asserted together.** `assigneeId=me` is the literal the
     * server resolves from the caller — the screen never reads its own user id — and the absence of
     * `sort` is what hands ordering to the API.
     */
    it('requests the caller’s own queue and sends no sort', () => {
        const request = controller.expectOne((r) => r.url === TICKETS);

        expect(request.request.params.get('assigneeId')).toBe('me');
        expect(request.request.params.has('sort')).toBeFalse();

        request.flush(page([row()]));
    });

    it('renders the returned rows in the order the server gave them', () => {
        controller.expectOne((r) => r.url === TICKETS).flush(
            page([
                row({ id: 'a', subject: 'Breached and later' }),
                row({ id: 'b', subject: 'Sooner but fine' })
            ])
        );

        fixture.detectChanges();

        const body = text();

        expect(body).toContain('Breached and later');
        expect(body).toContain('Sooner but fine');
        expect(body.indexOf('Breached and later')).toBeLessThan(body.indexOf('Sooner but fine'));
    });

    /**
     * **An empty queue is an expected state, not an error** (§9). It must offer the action that would
     * fill the region rather than reading as a failure.
     */
    it('shows the empty state, not an error, when nothing is assigned', () => {
        controller.expectOne((r) => r.url === TICKETS).flush(page([]));

        fixture.detectChanges();

        expect((fixture.nativeElement as HTMLElement).querySelector('app-empty-state')).not.toBeNull();
        expect((fixture.nativeElement as HTMLElement).querySelector('app-error-state')).toBeNull();
    });

    it('shows an inline error with a retry when the request fails', () => {
        controller.expectOne((r) => r.url === TICKETS).flush(
            { type: 'server-error', title: 'Server error', status: 500 },
            { status: 500, statusText: 'Server Error' }
        );

        fixture.detectChanges();

        expect((fixture.nativeElement as HTMLElement).querySelector('app-error-state')).not.toBeNull();
    });
});
