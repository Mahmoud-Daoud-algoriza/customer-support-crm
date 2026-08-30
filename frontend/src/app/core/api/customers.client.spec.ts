import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AttachmentsClient } from './attachments.client';
import { CustomersClient } from './customers.client';

/**
 * The typed clients of story 04 task 11.
 *
 * These tests are about the **wire**, because the wire is where this layer can be wrong in a way no
 * screen would reveal until it reached a server: a filter name that does not mirror the API's, a
 * download built from something other than an id, a multipart part the controller does not bind.
 */
describe('CustomersClient / AttachmentsClient', () => {
    const BASE = '/api/v1';
    const CUSTOMER = '33333333-3333-3333-3333-333333333333';
    const ATTACHMENT = '44444444-4444-4444-4444-444444444444';

    let customers: CustomersClient;
    let attachments: AttachmentsClient;
    let controller: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [provideHttpClient(), provideHttpClientTesting(), CustomersClient, AttachmentsClient]
        });

        customers = TestBed.inject(CustomersClient);
        attachments = TestBed.inject(AttachmentsClient);
        controller = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        controller.verify();
        TestBed.resetTestingModule();
    });

    it('sends the list filters under the API’s own names', () => {
        customers.list({ q: 'ada', branchId: 'b1', page: 2 }).subscribe();

        const request = controller.expectOne((r) => r.url === `${BASE}/customers`);

        // docs/api-design.md §5.5 names them `q` and `branchId`; the directory puts those same names
        // in the URL (UI-9), so a mismatch here would break the screen and the shareable link both.
        expect(request.request.params.get('q')).toBe('ada');
        expect(request.request.params.get('branchId')).toBe('b1');
        expect(request.request.params.get('page')).toBe('2');
        request.flush({ items: [], page: 2, pageSize: 25, totalItems: 0, totalPages: 0 });
    });

    it('omits a filter that is null rather than sending an empty one', () => {
        customers.list({ q: null, branchId: null }).subscribe();

        const request = controller.expectOne((r) => r.url === `${BASE}/customers`);

        // "No branch filter" is the absence of the parameter, not `branchId=`. The server would
        // reject the latter as an unparseable Guid.
        expect(request.request.params.has('q')).toBeFalse();
        expect(request.request.params.has('branchId')).toBeFalse();
        request.flush({ items: [], page: 1, pageSize: 25, totalItems: 0, totalPages: 0 });
    });

    it('patches only what it is given, and never a server-derived field', () => {
        customers.patchCustomer(CUSTOMER, { email: 'new@x.local' }).subscribe();

        const request = controller.expectOne(`${BASE}/customers/${CUSTOMER}`);

        expect(request.request.method).toBe('PATCH');

        // AP-10: a PATCH carries only what changes, and `externalReference` is settable through no
        // endpoint (DM-6). The server answers `400` to a body carrying one, so a client that sent it
        // would fail every save.
        expect(Object.keys(request.request.body as object)).toEqual(['email']);
        request.flush({});
    });

    it('posts a note with the body alone — author and timestamp are the server’s', () => {
        customers.addNote(CUSTOMER, 'Called back').subscribe();

        const request = controller.expectOne(`${BASE}/customers/${CUSTOMER}/notes`);

        expect(request.request.body).toEqual({ body: 'Called back' });
        request.flush({});
    });

    it('uploads multipart under the part name the controller binds', () => {
        const file = new File(['x'], 'note.txt', { type: 'text/plain' });

        customers.upload(CUSTOMER, file).subscribe();

        const request = controller.expectOne(`${BASE}/customers/${CUSTOMER}/attachments`);
        const body = request.request.body as FormData;

        const part = body.get('file') as File;

        expect(body instanceof FormData).toBeTrue();

        // The original client-supplied name is what survives the round trip and comes back in the
        // metadata; the name on disk never leaves the server (docs/api-design.md §6.7).
        expect(part.name).toBe('note.txt');

        // HttpClient must set the multipart boundary itself; a Content-Type set here would make the
        // body unparseable server-side (AP-13).
        expect(request.request.headers.has('Content-Type')).toBeFalse();
        request.flush({});
    });

    it('builds the download from the attachment id, never from a path', () => {
        expect(attachments.downloadUrl(ATTACHMENT)).toBe(`attachments/${ATTACHMENT}/content`);
    });

    it('fetches the bytes through HttpClient, so the bearer token is sent (AP-19, I-13)', () => {
        attachments.download(ATTACHMENT).subscribe();

        const request = controller.expectOne(`${BASE}/attachments/${ATTACHMENT}/content`);

        // A browser navigation to the same path would carry no Authorization header and be answered
        // `401` — the token lives in localStorage, not in a cookie (AD-7).
        expect(request.request.responseType).toBe('blob');
        request.flush(new Blob(['x']));
    });
});
