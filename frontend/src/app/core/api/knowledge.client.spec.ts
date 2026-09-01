import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { KnowledgeClient, PortalKnowledgeClient } from './knowledge.client';

/**
 * The typed knowledge clients of story 12.
 *
 * These tests are about the **wire**, for the reason `CustomersClient`'s suite gives: a filter name
 * that does not mirror the API's, or a body carrying a field the contract refuses, is wrong in a way
 * no screen reveals until it reaches a server.
 *
 * Two of them are about a rule rather than a name — **`isPublished` never appears in a `PATCH`
 * body** (docs/api-design.md §6.11) and **suggested articles are not an `/ai` path** (AP-14).
 */
describe('KnowledgeClient / PortalKnowledgeClient', () => {
    const BASE = '/api/v1';
    const ARTICLE = '55555555-5555-5555-5555-555555555555';
    const TICKET = '44444444-4444-4444-4444-444444444444';

    let knowledge: KnowledgeClient;
    let portal: PortalKnowledgeClient;
    let controller: HttpTestingController;

    const emptyPage = { items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 };

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [provideHttpClient(), provideHttpClientTesting(), KnowledgeClient, PortalKnowledgeClient]
        });

        knowledge = TestBed.inject(KnowledgeClient);
        portal = TestBed.inject(PortalKnowledgeClient);
        controller = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        controller.verify();
        TestBed.resetTestingModule();
    });

    it('sends the search filters under the API’s own names', () => {
        knowledge.search({ q: 'invoice', type: 'Faq', visibility: 'Internal', isPublished: true }).subscribe();

        const request = controller.expectOne((r) => r.url === `${BASE}/kb/articles`);

        // docs/api-design.md §5.9 names them exactly these four, and the staff screen puts the same
        // names in the URL (UI-9) — a mismatch would break the screen and the shareable link both.
        expect(request.request.params.get('q')).toBe('invoice');
        expect(request.request.params.get('type')).toBe('Faq');
        expect(request.request.params.get('visibility')).toBe('Internal');
        expect(request.request.params.get('isPublished')).toBe('true');
        request.flush(emptyPage);
    });

    it('omits a filter that is null rather than sending an empty one', () => {
        knowledge.search({ q: null, type: null, visibility: null, isPublished: null }).subscribe();

        const request = controller.expectOne((r) => r.url === `${BASE}/kb/articles`);

        expect(request.request.params.has('q')).toBeFalse();
        expect(request.request.params.has('type')).toBeFalse();
        expect(request.request.params.has('visibility')).toBeFalse();
        expect(request.request.params.has('isPublished')).toBeFalse();
        request.flush(emptyPage);
    });

    it('never sends isPublished in a create or a patch body', () => {
        knowledge
            .create({ title: 'T', body: 'B', type: 'Faq', visibility: 'Public' })
            .subscribe();

        const created = controller.expectOne((r) => r.url === `${BASE}/kb/articles` && r.method === 'POST');

        // §6.11: `isPublished` defaults to false server-side and is not a create field here; the
        // server would answer `400` if it were sent (AP-10).
        expect(Object.keys(created.request.body as object)).not.toContain('isPublished');
        created.flush({});

        knowledge.update(ARTICLE, { title: 'T2' }).subscribe();

        const patched = controller.expectOne((r) => r.url === `${BASE}/kb/articles/${ARTICLE}`);

        // The rule this test exists for: publication changes through the action pair, never a field.
        expect(patched.request.method).toBe('PATCH');
        expect(Object.keys(patched.request.body as object)).not.toContain('isPublished');
        patched.flush({});
    });

    it('publishes and unpublishes through the dedicated action pair', () => {
        knowledge.publish(ARTICLE).subscribe();
        const published = controller.expectOne(`${BASE}/kb/articles/${ARTICLE}/publish`);
        expect(published.request.method).toBe('POST');
        published.flush({});

        knowledge.unpublish(ARTICLE).subscribe();
        const unpublished = controller.expectOne(`${BASE}/kb/articles/${ARTICLE}/unpublish`);
        expect(unpublished.request.method).toBe('POST');
        unpublished.flush({});
    });

    it('asks a Knowledge path for suggested articles, never an /ai one', () => {
        knowledge.suggested(TICKET).subscribe();

        const request = controller.expectOne(`${BASE}/tickets/${TICKET}/suggested-articles`);

        // **AP-14.** Suggested solutions retrieve rather than generate, so they are a Knowledge
        // endpoint. A `GET` also says so: the three AI assists are all `POST` because they perform
        // work (§5.8), and this one simply reads.
        expect(request.request.method).toBe('GET');
        expect(request.request.url).not.toContain('/ai/');
        request.flush([]);
    });

    it('reads the portal path space, with no visibility parameter to send', () => {
        portal.search({ q: 'refund' }).subscribe();

        const search = controller.expectOne((r) => r.url === `${BASE}/portal/kb/articles`);

        expect(search.request.params.get('q')).toBe('refund');

        // The portal has no visibility choice to make: the rule is server-side, in one place
        // (docs/data-model.md §5 constraint 19). A parameter here would imply otherwise.
        expect(search.request.params.has('visibility')).toBeFalse();
        expect(search.request.params.has('isPublished')).toBeFalse();
        search.flush(emptyPage);

        portal.read(ARTICLE).subscribe();
        controller.expectOne(`${BASE}/portal/kb/articles/${ARTICLE}`).flush({});
    });
});
