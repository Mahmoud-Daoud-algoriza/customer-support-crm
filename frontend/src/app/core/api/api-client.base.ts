import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/** Query values a caller may pass; arrays repeat the parameter, which means OR within that field. */
export type QueryValue = string | number | boolean | readonly (string | number | boolean)[] | null | undefined;

/**
 * The one place HTTP is spoken in this application. Feature components never inject `HttpClient`
 * directly (docs/architecture.md §2.2) — they inject a typed service in `core/api/` that extends
 * this base, so the api-design contracts are absorbed here.
 */
@Injectable()
export abstract class ApiClientBase {
    private readonly http = inject(HttpClient);

    protected get<T>(path: string, query?: Record<string, QueryValue>): Observable<T> {
        return this.http.get<T>(this.url(path), { params: toParams(query) });
    }

    protected post<T>(path: string, body?: unknown): Observable<T> {
        return this.http.post<T>(this.url(path), body ?? {});
    }

    protected patch<T>(path: string, body: unknown): Observable<T> {
        return this.http.patch<T>(this.url(path), body);
    }

    protected put<T>(path: string, body: unknown): Observable<T> {
        return this.http.put<T>(this.url(path), body);
    }

    protected delete<T>(path: string): Observable<T> {
        return this.http.delete<T>(this.url(path));
    }

    /**
     * A binary response — the one shape `GET /attachments/{id}/content` returns (AP-19): the file
     * stream, with no JSON body at all (docs/api-design.md §6.7).
     *
     * It goes through `HttpClient` like every other call **because that is the only way the bearer
     * token is sent**: `authInterceptor` sets `Authorization` on an `HttpRequest`, and the token
     * lives in `localStorage`, not in a cookie. A plain `<a href>` to the same path would carry no
     * header and be answered `401`. See finding I-13.
     */
    protected getBlob(path: string): Observable<Blob> {
        return this.http.get(this.url(path), { responseType: 'blob' });
    }

    private url(path: string): string {
        return `${environment.apiBaseUrl}/${path.replace(/^\//, '')}`;
    }
}

function toParams(query?: Record<string, QueryValue>): HttpParams {
    let params = new HttpParams();
    if (!query) {
        return params;
    }

    for (const [key, value] of Object.entries(query)) {
        if (value === null || value === undefined) {
            continue;
        }
        if (Array.isArray(value)) {
            for (const entry of value) {
                params = params.append(key, String(entry));
            }
        } else {
            params = params.set(key, String(value));
        }
    }

    return params;
}
