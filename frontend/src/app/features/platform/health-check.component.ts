import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../core/api/api-problem';
import { HealthStatus, PlatformApiService } from '../../core/api/platform-api.service';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../shared/components/loading-state/loading-state.component';

/**
 * Temporary landing screen. It exists only to prove the SPA reaches the API and the API reaches the
 * database — **Story 02 replaces it with the role redirect** (docs/ui-design.md §2).
 *
 * It uses the shared loading and error states rather than its own, so the pattern every later
 * screen follows is established here.
 */
@Component({
    selector: 'app-health-check',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ErrorStateComponent, LoadingStateComponent, TagModule, TranslocoModule],
    template: `
        <section class="app-health">
            <h1 class="app-health__title">{{ 'health.title' | transloco }}</h1>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (health(); as result) {
                    <dl class="app-health__facts">
                        <dt>{{ 'health.api' | transloco }}</dt>
                        <dd>
                            <p-tag [severity]="result.status === 'ok' ? 'success' : 'warn'" [value]="'health.status.' + result.status | transloco" />
                        </dd>
                        <dt>{{ 'health.databaseLabel' | transloco }}</dt>
                        <dd>
                            <p-tag [severity]="result.database === 'reachable' ? 'success' : 'danger'" [value]="'health.db.' + result.database | transloco" />
                        </dd>
                        <dt>{{ 'health.serverTime' | transloco }}</dt>
                        <dd class="app-ltr-numeric">{{ result.utcNow }}</dd>
                    </dl>
                } @else {
                    <app-loading-state [rowCount]="3" />
                }
            }
        </section>
    `
})
export class HealthCheckComponent {
    private readonly api = inject(PlatformApiService);

    protected readonly health = signal<HealthStatus | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    constructor() {
        this.load();
    }

    protected load(): void {
        this.health.set(null);
        this.problem.set(null);

        this.api.getHealth().subscribe({
            next: (result) => this.health.set(result),
            // A degraded API answers 503 with the same body, so a failure is still informative.
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
