import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClientBase } from './api-client.base';

/** `GET /health` — docs/api-design.md §5.1. */
export interface HealthStatus {
    status: 'ok' | 'degraded';
    database: 'reachable' | 'unreachable';
    utcNow: string;
}

/** `BootstrapConfig` — docs/api-design.md §6.9. Anonymous, needed before sign-in. */
export interface BootstrapConfig {
    productName: string;
    logoUrl: string;
    primaryColor: string;
    languages: string[];
    defaultLanguage: string;
}

/**
 * The two platform endpoints Story 01 delivers. `GET /config` and `GET /config/staff` arrive with
 * Stories 02 and 16 — both require authentication.
 */
@Injectable({ providedIn: 'root' })
export class PlatformApiService extends ApiClientBase {
    getHealth(): Observable<HealthStatus> {
        return this.get<HealthStatus>('health');
    }

    getBootstrapConfig(): Observable<BootstrapConfig> {
        return this.get<BootstrapConfig>('config/bootstrap');
    }
}
