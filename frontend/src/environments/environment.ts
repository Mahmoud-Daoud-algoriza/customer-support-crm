/**
 * The `web` container proxies `/api` to the `api` service (frontend/nginx.conf), and `ng serve`
 * proxies it to `http://localhost:5080` (proxy.conf.json), so the SPA and the API always share an
 * origin and one base URL serves both.
 */
export const environment = {
    apiBaseUrl: '/api/v1'
} as const;
