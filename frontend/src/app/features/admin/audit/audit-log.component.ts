import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ApiProblem } from '../../../core/api/api-problem';
import { AuditClient, AuditEntry, AuditListFilter } from '../../../core/api/audit.client';
import { Paged } from '../../../core/api/paged';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * Audit log — `/admin/audit` (docs/ui-design.md §6). Administrator only; the server refuses every
 * other role with `403`, independently of this guard (docs/architecture.md §4.2).
 *
 * **Read-only. No row action of any kind** — no edit, no delete, no export, no bulk selection. The
 * log is append-only (T2-H, docs/architecture.md §2.4). This is deliberately **not**
 * `app-ticket-filter-bar` reused: that component is the ticket list's own filter set
 * (`status`/`priority`/`categoryCode`/…), and reusing it here would either grow it with audit-only
 * fields or silently expose ticket filters this screen has no use for.
 *
 * **Filters live in the URL** (UI-9), under the API's own names (§5.12), so a filtered view is
 * shareable and survives a reload — the same pattern `TicketListComponent` uses.
 *
 * **A different screen from the ticket activity region, on purpose** (AD-10): different actors,
 * different questions. Nothing here links to or reads `/tickets/{id}/activity`.
 */
@Component({
    selector: 'app-audit-log',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DatePickerModule, DatePipe, EmptyStateComponent, ErrorStateComponent, FormsModule, InputTextModule, LoadingStateComponent, PaginatorModule, TableModule, TagModule, TranslocoModule],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'admin.audit.title' | transloco }}</h1>
            </header>

            <div class="app-filters">
                <input pInputText [placeholder]="'admin.audit.actorPlaceholder' | transloco" [(ngModel)]="actorUserId" (keyup.enter)="applyFilters()" />
                <input pInputText [placeholder]="'admin.audit.actionPlaceholder' | transloco" [(ngModel)]="action" (keyup.enter)="applyFilters()" />
                <p-datepicker [(ngModel)]="from" [placeholder]="'admin.audit.fromLabel' | transloco" [showIcon]="true" [showClear]="true" dateFormat="yy-mm-dd" />
                <p-datepicker [(ngModel)]="to" [placeholder]="'admin.audit.toLabel' | transloco" [showIcon]="true" [showClear]="true" dateFormat="yy-mm-dd" />
                <p-button [label]="'actions.apply' | transloco" severity="secondary" (onClick)="applyFilters()" />
            </div>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="reload()" />
            } @else {
                @if (page(); as result) {
                    @if (result.totalItems === 0) {
                        <app-empty-state [title]="'admin.audit.emptyTitle' | transloco" [message]="'admin.audit.emptyMessage' | transloco" icon="pi-history" />
                    } @else {
                        <div class="app-scroll-x">
                            <p-table [value]="result.items" [tableStyle]="{ 'min-width': '56rem' }">
                                <ng-template pTemplate="header">
                                    <tr>
                                        <th>{{ 'admin.audit.occurredAt' | transloco }}</th>
                                        <th>{{ 'admin.audit.actor' | transloco }}</th>
                                        <th>{{ 'admin.audit.action' | transloco }}</th>
                                        <th>{{ 'admin.audit.target' | transloco }}</th>
                                        <th>{{ 'admin.audit.outcome' | transloco }}</th>
                                    </tr>
                                </ng-template>
                                <ng-template pTemplate="body" let-entry>
                                    <tr>
                                        <td class="app-ltr-numeric">{{ entry.occurredAt | date: 'short' }}</td>
                                        <td>{{ entry.actor?.displayName ?? entry.actorDescriptor ?? '—' }}</td>
                                        <td>{{ entry.action }}</td>
                                        <td>{{ entry.targetType ? entry.targetType + ' · ' + entry.targetId : '—' }}</td>
                                        <td>
                                            <p-tag
                                                [severity]="entry.outcome === 'Success' ? 'success' : 'danger'"
                                                [value]="entry.outcome"
                                            />
                                        </td>
                                    </tr>
                                </ng-template>
                            </p-table>
                        </div>

                        <p-paginator [first]="(result.page - 1) * result.pageSize" [rows]="result.pageSize" [totalRecords]="result.totalItems" (onPageChange)="goToPage($event.page)" />
                    }
                } @else {
                    <app-loading-state [rowCount]="6" />
                }
            }
        </section>
    `
})
export class AuditLogComponent {
    private readonly api = inject(AuditClient);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);

    protected readonly page = signal<Paged<AuditEntry> | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);

    protected actorUserId: string | null = null;
    protected action: string | null = null;
    protected from: Date | null = null;
    protected to: Date | null = null;

    constructor() {
        // UI-9: the URL drives the screen, exactly as the ticket list does it.
        this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
            this.readFilterInto(params);
            this.load(params);
        });
    }

    protected applyFilters(): void {
        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: {
                actorUserId: this.actorUserId || null,
                action: this.action || null,
                from: this.from ? startOfDay(this.from).toISOString() : null,
                to: this.to ? endOfDay(this.to).toISOString() : null,
                page: null
            }
        });
    }

    /** The paginator reports a **0-based** index; the API's `page` is 1-based (§2.1). */
    protected goToPage(zeroBasedPage: number | undefined): void {
        const page = (zeroBasedPage ?? 0) + 1;

        void this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { page: page > 1 ? page : null },
            queryParamsHandling: 'merge'
        });
    }

    protected reload(): void {
        this.load(this.route.snapshot.queryParamMap);
    }

    private readFilterInto(params: ParamMap): void {
        this.actorUserId = params.get('actorUserId');
        this.action = params.get('action');
        this.from = params.get('from') ? new Date(params.get('from')!) : null;
        this.to = params.get('to') ? new Date(params.get('to')!) : null;
    }

    private load(params: ParamMap): void {
        this.page.set(null);
        this.problem.set(null);

        const page = Number(params.get('page'));

        const filter: AuditListFilter = {
            actorUserId: params.get('actorUserId'),
            action: params.get('action'),
            from: params.get('from'),
            to: params.get('to'),
            page: Number.isFinite(page) && page > 1 ? page : undefined
        };

        this.api.list(filter).subscribe({
            next: (result) => this.page.set(result),
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}

/**
 * A date-only picker gives midnight; a "from" bound naturally means the start of that day in the
 * viewer's own time zone.
 */
function startOfDay(date: Date): Date {
    const start = new Date(date);
    start.setHours(0, 0, 0, 0);

    return start;
}

/**
 * **Implementation choice, not a documented rule**: no approved document defines the endpoint of a
 * date-only "to" bound, and the audit intake names no precedent for one. A date picker gives
 * midnight, and a viewer picking "today" as the end of a range expects today's entries included, so
 * "to" is treated as inclusive of the whole selected day.
 */
function endOfDay(date: Date): Date {
    const end = new Date(date);
    end.setHours(23, 59, 59, 999);

    return end;
}
