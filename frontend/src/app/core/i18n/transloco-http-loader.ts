import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable } from 'rxjs';

/** Dictionaries are static assets loaded at bootstrap (docs/architecture.md §2.3). */
@Injectable({ providedIn: 'root' })
export class TranslocoHttpLoader implements TranslocoLoader {
    private readonly http = inject(HttpClient);

    getTranslation(lang: string): Observable<Translation> {
        return this.http.get<Translation>(`assets/i18n/${lang}.json`);
    }
}
