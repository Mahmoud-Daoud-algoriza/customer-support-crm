import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { MessageModule } from 'primeng/message';
import { TableModule } from 'primeng/table';
import { forkJoin } from 'rxjs';
import { ApiProblem } from '../../../core/api/api-problem';
import { Department, OrganizationClient } from '../../../core/api/organization.client';
import { CustomerConfig, PlatformApiService, StaffConfig } from '../../../core/api/platform-api.service';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * Configuration — `/admin/configuration` (docs/ui-design.md §6, docs/architecture.md §6.3).
 * Administrator only.
 *
 * **Read-only with no save control anywhere.** No input, no toggle, no editable field — not a
 * disabled one, none at all (architecture §6.3: *"A read-only view of effective configuration is
 * permitted; a writable one is not"*). Every value below is rendered as plain text.
 *
 * **Reads `GET /config` and `GET /config/staff`** (docs/ui-design.md §6) through the existing
 * `PlatformApiService` — the same client the ticket list and quick-reply control already use, so
 * this screen introduces no new endpoint. **Branding is not fetched again**: it is already loaded
 * once at application start by `RuntimeConfigService` (`GET /config/bootstrap`, architecture §6.3),
 * and this screen reads that instead of issuing a second call for the same values.
 *
 * The feedback rating-scale row shows the configured `min`–`max` and nothing more — **it does not
 * annotate what the scale should be**, because OQ-1 is still open (docs/api-design.md §6.9).
 */
@Component({
    selector: 'app-configuration',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ErrorStateComponent, LoadingStateComponent, MessageModule, TableModule, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'admin.configuration.title' | transloco }}</h1>
            </header>

            <p-message severity="info" [text]="'admin.configuration.redeployBanner' | transloco" />

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
            @if (view(); as data) {
                <section>
                    <h2>{{ 'admin.configuration.branding' | transloco }}</h2>
                    <p-table [value]="[data.branding]">
                        <ng-template pTemplate="header">
                            <tr>
                                <th>{{ 'admin.configuration.productName' | transloco }}</th>
                                <th>{{ 'admin.configuration.primaryColor' | transloco }}</th>
                            </tr>
                        </ng-template>
                        <ng-template pTemplate="body" let-row>
                            <tr>
                                <td>{{ row.productName }}</td>
                                <td>{{ row.primaryColor }}</td>
                            </tr>
                        </ng-template>
                    </p-table>
                </section>

                <section>
                    <h2>{{ 'admin.configuration.categories' | transloco }}</h2>
                    <p-table [value]="data.categories">
                        <ng-template pTemplate="header">
                            <tr>
                                <th>{{ 'admin.configuration.code' | transloco }}</th>
                                <th>{{ 'admin.configuration.name' | transloco }}</th>
                                <th>{{ 'admin.configuration.department' | transloco }}</th>
                            </tr>
                        </ng-template>
                        <ng-template pTemplate="body" let-row>
                            <tr>
                                <td>{{ row.code }}</td>
                                <td>{{ row.name }}</td>
                                <td>{{ row.departmentName }}</td>
                            </tr>
                        </ng-template>
                    </p-table>
                </section>

                <section>
                    <h2>{{ 'admin.configuration.priorities' | transloco }}</h2>
                    <p>{{ data.priorities.join(' · ') }}</p>
                </section>

                <section>
                    <h2>{{ 'admin.configuration.slaTargets' | transloco }}</h2>
                    <p-table [value]="data.slaTargets">
                        <ng-template pTemplate="header">
                            <tr>
                                <th>{{ 'admin.configuration.priorityLabel' | transloco }}</th>
                                <th>{{ 'admin.configuration.firstResponseHours' | transloco }}</th>
                                <th>{{ 'admin.configuration.resolutionHours' | transloco }}</th>
                            </tr>
                        </ng-template>
                        <ng-template pTemplate="body" let-row>
                            <tr>
                                <td>{{ row.priority }}</td>
                                <td class="app-ltr-numeric">{{ row.firstResponseHours }}</td>
                                <td class="app-ltr-numeric">{{ row.resolutionHours }}</td>
                            </tr>
                        </ng-template>
                    </p-table>
                </section>

                <section>
                    <h2>{{ 'admin.configuration.quickReplies' | transloco }}</h2>
                    <p-table [value]="data.quickReplies">
                        <ng-template pTemplate="header">
                            <tr>
                                <th>{{ 'admin.configuration.quickReplyTitle' | transloco }}</th>
                                <th>{{ 'admin.configuration.quickReplyBody' | transloco }}</th>
                            </tr>
                        </ng-template>
                        <ng-template pTemplate="body" let-row>
                            <tr>
                                <td>{{ row.title }}</td>
                                <td>{{ row.body }}</td>
                            </tr>
                        </ng-template>
                    </p-table>
                </section>

                <section>
                    <h2>{{ 'admin.configuration.feedbackRatingScale' | transloco }}</h2>
                    <!-- The values shown are whatever configuration holds. OQ-1 is open — this row
                         states the configured bounds and claims nothing about what they should be. -->
                    <p class="app-ltr-numeric">{{ data.ratingScale.min }} – {{ data.ratingScale.max }}</p>
                </section>
            } @else {
                <app-loading-state [rowCount]="6" />
            }
            }
        </section>
    `
})
export class ConfigurationComponent {
    private readonly platform = inject(PlatformApiService);
    private readonly organization = inject(OrganizationClient);
    private readonly runtimeConfig = inject(RuntimeConfigService);

    protected readonly view = signal<ConfigurationView | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    constructor() {
        this.load();
    }

    protected load(): void {
        this.view.set(null);
        this.problem.set(null);

        forkJoin({
            customer: this.platform.getCustomerConfig(),
            staff: this.platform.getStaffConfig(),
            departments: this.organization.getDepartments()
        }).subscribe({
            next: ({ customer, staff, departments }) =>
                this.view.set(toView(customer, staff, departments, this.runtimeConfig)),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}

interface ConfigurationView {
    branding: { productName: string; primaryColor: string };
    categories: { code: string; name: string; departmentName: string }[];
    priorities: string[];
    slaTargets: StaffConfig['slaTargets'];
    quickReplies: StaffConfig['quickReplies'];
    ratingScale: { min: number; max: number };
}

function toView(
    customer: CustomerConfig,
    staff: StaffConfig,
    departments: Department[],
    runtimeConfig: RuntimeConfigService
): ConfigurationView {
    const departmentNameById = new Map(departments.map((d) => [d.id, d.name]));
    const departmentIdByCategory = new Map(
        staff.categoryDepartmentMap.map((m) => [m.categoryCode, m.departmentId])
    );

    return {
        branding: {
            productName: runtimeConfig.productName(),
            primaryColor: runtimeConfig.primaryColor()
        },
        categories: customer.categories.map((c) => ({
            code: c.code,
            name: c.name,
            departmentName: departmentNameById.get(departmentIdByCategory.get(c.code) ?? '') ?? '—'
        })),
        priorities: staff.priorities,
        slaTargets: staff.slaTargets,
        quickReplies: staff.quickReplies,
        ratingScale: customer.feedback.ratingScale
    };
}
