import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { MessageService } from 'primeng/api';
import { ApiProblem, NETWORK_PROBLEM_TYPE } from '../api/api-problem';
import { AuthStore } from '../auth/auth.store';
import { errorInterceptor } from './error.interceptor';

/**
 * The cross-cutting half of the docs/ui-design.md §9 status table.
 *
 * Every test here asserts **two** things about a status: what the interceptor does, and what it
 * leaves alone. The second half matters as much as the first — the whole risk of centralizing error
 * handling is that it starts swallowing the structured errors feature forms need.
 */
describe('errorInterceptor', () => {
    const URL = '/api/v1/customers';
    const LOGIN_URL = '/api/v1/auth/login';

    let http: HttpClient;
    let controller: HttpTestingController;
    let store: AuthStore;
    let router: jasmine.SpyObj<Router>;
    let messages: jasmine.SpyObj<MessageService>;

    /** Only the members the interceptor touches; `url` is writable so tests can place the user. */
    function routerSpy(url = '/workspace/customers'): jasmine.SpyObj<Router> {
        const spy = jasmine.createSpyObj<Router>('Router', ['navigate']);
        Object.defineProperty(spy, 'url', { value: url, writable: true });
        spy.navigate.and.resolveTo(true);

        return spy;
    }

    function setUp(currentUrl?: string): void {
        router = routerSpy(currentUrl);
        messages = jasmine.createSpyObj<MessageService>('MessageService', ['add']);

        TestBed.configureTestingModule({
            imports: [
                TranslocoTestingModule.forRoot({
                    langs: { en: { errors: { 'internal-error': 'Server error.', 'network-unavailable': 'Unreachable.' } } },
                    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },

                    // Without this the dictionary loads asynchronously and translate() returns the
                    // key — which is exactly the fallback path, so the assertions would pass for
                    // the wrong reason.
                    preloadLangs: true
                })
            ],
            providers: [provideHttpClient(withInterceptors([errorInterceptor])), provideHttpClientTesting(), { provide: Router, useValue: router }, { provide: MessageService, useValue: messages }]
        });

        http = TestBed.inject(HttpClient);
        controller = TestBed.inject(HttpTestingController);
        store = TestBed.inject(AuthStore);
    }

    /** Issues a request, fails it with the given status and body, and returns the thrown problem. */
    async function failWith(status: number, body: string | object = {}, url = URL): Promise<ApiProblem> {
        const promise = new Promise<ApiProblem>((resolve, reject) => {
            http.get(url).subscribe({ next: () => reject(new Error('expected a failure')), error: resolve });
        });

        if (status === 0) {
            controller.expectOne(url).error(new ProgressEvent('network error'), { status: 0, statusText: '' });
        } else {
            controller.expectOne(url).flush(body, { status, statusText: 'x' });
        }

        return promise;
    }

    afterEach(() => controller.verify());

    // ---------------------------------------------------------------- 401

    it('clears the session and redirects to sign-in, preserving the return URL', async () => {
        setUp('/workspace/customers');
        store.setToken('a-token');

        const problem = await failWith(401, { type: 'http-401', title: 'Unauthorized', status: 401 });

        expect(store.hasToken()).toBeFalse();
        expect(router.navigate).toHaveBeenCalledWith(['/auth/login'], {
            queryParams: { returnUrl: '/workspace/customers' }
        });

        // Still delivered to the caller: centralizing the reaction must not swallow the error.
        expect(problem.status).toBe(401);
    });

    it('leaves a sign-in failure to the login form: no session cleared, no redirect', async () => {
        setUp('/auth/login');

        const problem = await failWith(401, { type: 'invalid-credentials', title: 'Invalid credentials', status: 401 }, LOGIN_URL);

        expect(router.navigate).not.toHaveBeenCalled();

        // The slug the form renders beside the password field.
        expect(problem.type).toBe('invalid-credentials');
    });

    it('does not redirect when the user is already inside the auth area', async () => {
        setUp('/auth/login');
        store.setToken('a-token');

        await failWith(401);

        // Clearing was right; navigating again would discard a half-typed form.
        expect(store.hasToken()).toBeFalse();
        expect(router.navigate).not.toHaveBeenCalled();
    });

    // ---------------------------------------------------------------- 403

    it('routes a 403 to /403 and leaves the session signed in', async () => {
        setUp('/admin/users');
        store.setToken('a-token');

        const problem = await failWith(403, { type: 'http-403', title: 'Forbidden', status: 403 });

        expect(router.navigate).toHaveBeenCalledWith(['/403']);

        // The token is still valid — 403 is "allowed to be here, not allowed to do that".
        expect(store.hasToken()).toBeTrue();
        expect(problem.status).toBe(403);
    });

    it('does not loop when the 403 screen itself issues a request', async () => {
        setUp('/403');

        await failWith(403);

        expect(router.navigate).not.toHaveBeenCalled();
    });

    // ------------------------------------------------- 400 / 409 / 413 / 422 / 404 pass through

    it('passes structured client errors through untouched, with their fields intact', async () => {
        setUp();
        store.setToken('a-token');

        const problem = await failWith(400, {
            type: 'validation-failed',
            title: 'Validation failed',
            status: 400,
            errors: { email: ['Email is required.'] }
        });

        expect(router.navigate).not.toHaveBeenCalled();
        expect(messages.add).not.toHaveBeenCalled();
        expect(store.hasToken()).toBeTrue();

        // The per-field dictionary a form needs to render errors in place survives normalization.
        expect(problem.errors).toEqual({ email: ['Email is required.'] });
    });

    it('leaves every inline-rendered status to the feature', async () => {
        for (const [status, type] of [
            [409, 'customer-email-in-use'],
            [413, 'attachment-too-large'],
            [422, 'assignee-out-of-department'],
            [404, 'not-found']
        ] as const) {
            setUp();

            const problem = await failWith(status, { type, title: type, status });

            expect(problem.type).withContext(`${status} should reach the caller with its slug`).toBe(type);
            expect(router.navigate).withContext(`${status} should not navigate`).not.toHaveBeenCalled();
            expect(messages.add).withContext(`${status} should not toast`).not.toHaveBeenCalled();

            TestBed.resetTestingModule();
        }
    });

    // ---------------------------------------------------------------- 5xx and network

    it('shows one translated toast for a server error, and still rethrows', async () => {
        setUp();

        const problem = await failWith(500, { type: 'http-500', title: 'Server error', status: 500 });

        expect(messages.add).toHaveBeenCalledTimes(1);
        expect(problem.status).toBe(500);

        const message = messages.add.calls.mostRecent().args[0];
        expect(message.severity).toBe('error');

        // The translated sentence, never the slug and never the server's prose (T2-J).
        expect(message.detail).toBe('Server error.');
    });

    it('falls back to the generic sentence for an unmapped 5xx slug', async () => {
        setUp();

        await failWith(502, { type: 'http-502', title: 'Bad gateway', status: 502 });

        expect(messages.add.calls.mostRecent().args[0].detail).toBe('Server error.');
    });

    it('reports an unreachable server as a network problem, not an HTTP status', async () => {
        setUp();

        const problem = await failWith(0);

        expect(problem.type).toBe(NETWORK_PROBLEM_TYPE);
        expect(problem.status).toBe(0);
        expect(messages.add.calls.mostRecent().args[0].detail).toBe('Unreachable.');
    });

    it('leaves 503 to the panel that asked for it', async () => {
        setUp();

        // docs/ui-design.md §9 scopes 503 to the AI panel: "the rest of the screen stays live".
        // A global toast would contradict that, so this status is deliberately not handled here.
        const problem = await failWith(503, { type: 'ai-unavailable', title: 'Unavailable', status: 503 });

        expect(messages.add).not.toHaveBeenCalled();
        expect(router.navigate).not.toHaveBeenCalled();
        expect(problem.type).toBe('ai-unavailable');
    });

    // ---------------------------------------------------------------- normalization

    it('never lets the caller see a raw HttpErrorResponse', async () => {
        setUp();

        // A body that is not Problem Details at all — a proxy's HTML error page, say.
        const problem = await failWith(500, 'a wall of html');

        expect(problem.type).toBe('http-500');
        expect(problem.status).toBe(500);
        expect(typeof problem.title).toBe('string');
    });
});
