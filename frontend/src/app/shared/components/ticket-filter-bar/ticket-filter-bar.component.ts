import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { CustomerConfig, PlatformApiService } from '../../../core/api/platform-api.service';
import { TICKET_PRIORITIES, TICKET_STATUSES, TicketListFilter } from '../../../core/api/tickets.client';
import { DepartmentFilterComponent } from '../department-filter/department-filter.component';

/**
 * `TicketFilterBar` — the shared component of docs/ui-design.md §8, used by the queue and the
 * ticket list. **Bound to URL query parameters** (UI-9): it emits a filter, and the screen puts it
 * in the URL. It holds no state of its own that the URL does not.
 *
 * **The filter names mirror the API exactly** (§5.2): `status`, `priority`, `categoryCode`,
 * `assigneeId`, `departmentId`, `breached`, `q`. No translation step anywhere.
 *
 * **The department filter is the shared one from Story 03**, with `disabledForOwnDepartment` **on**:
 * an Agent sees it fixed to their own department and disabled, with a hint; a Manager sees it
 * enabled across all departments (§5.2). That rule lives in one place and is not re-implemented
 * here — this component only decides to switch it on.
 *
 * **There is no branch filter, and there must never be one.** Branch is a reporting attribute
 * (A-2, T2-K); it appears on the customer directory and the reports screen, never as a ticket scope.
 */
@Component({
    selector: 'app-ticket-filter-bar',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, DepartmentFilterComponent, FormsModule, InputTextModule, SelectModule, TranslocoModule],
    template: `
        <div class="app-filters">
            <input pInputText [placeholder]="'tickets.search' | transloco" [(ngModel)]="q" (keyup.enter)="emit()" />

            <p-select [options]="statusOptions" [(ngModel)]="status" [showClear]="true" [placeholder]="'tickets.anyStatus' | transloco" [ariaLabel]="'tickets.statusLabel' | transloco" (onChange)="emit()" />

            <p-select [options]="priorityOptions" [(ngModel)]="priority" [showClear]="true" [placeholder]="'tickets.anyPriority' | transloco" [ariaLabel]="'tickets.priorityLabel' | transloco" (onChange)="emit()" />

            <p-select
                [options]="categories()"
                [(ngModel)]="categoryCode"
                optionLabel="name"
                optionValue="code"
                [showClear]="true"
                [placeholder]="'tickets.anyCategory' | transloco"
                [ariaLabel]="'tickets.categoryLabel' | transloco"
                (onChange)="emit()"
            />

            <!-- Story 03's shared rule, switched on. An Agent is pinned and told why. -->
            <app-department-filter [disabledForOwnDepartment]="true" [value]="departmentId()" (valueChange)="applyDepartment($event)" />

            <p-select
                [options]="assigneeOptions"
                [(ngModel)]="assigneeId"
                optionLabel="label"
                optionValue="value"
                [showClear]="true"
                [placeholder]="'tickets.anyAssignee' | transloco"
                [ariaLabel]="'tickets.assigneeLabel' | transloco"
                (onChange)="emit()"
            />

            <p-select [options]="breachedOptions" [(ngModel)]="breached" optionLabel="label" optionValue="value" [showClear]="true" [placeholder]="'tickets.anyBreach' | transloco" [ariaLabel]="'tickets.breachLabel' | transloco" (onChange)="emit()" />

            <p-button [label]="'actions.apply' | transloco" severity="secondary" (onClick)="emit()" />
        </div>
    `
})
export class TicketFilterBarComponent {
    private readonly platform = inject(PlatformApiService);

    /** The current filter, read from the URL by the screen. */
    readonly value = input.required<TicketListFilter>();

    readonly filterChange = output<TicketListFilter>();

    protected readonly statusOptions = [...TICKET_STATUSES];
    protected readonly priorityOptions = [...TICKET_PRIORITIES];

    /**
     * `assigneeId` accepts the literal `me` (docs/api-design.md §5.6), which is what produces the
     * agent's own queue. Offering only "mine" and "unassigned"-free choices keeps the control to
     * what the contract can serve: there is no endpoint an Agent may call to list other agents.
     * See finding I-16.
     */
    protected readonly assigneeOptions = [{ label: 'tickets.assignedToMe', value: 'me' }];

    protected readonly breachedOptions = [
        { label: 'tickets.breachedOnly', value: true },
        { label: 'tickets.notBreached', value: false }
    ];

    /** Categories come from `GET /config` — customer-safe configuration, so any role may read it. */
    protected readonly categories = signal<CustomerConfig['categories']>([]);

    protected readonly departmentId = computed(() => this.value().departmentId ?? null);

    protected q = '';
    protected status: string | null = null;
    protected priority: string | null = null;
    protected categoryCode: string | null = null;
    protected assigneeId: string | null = null;
    protected breached: boolean | null = null;

    constructor() {
        this.platform.getCustomerConfig().subscribe((config) => this.categories.set(config.categories));

        // **The URL fills the controls, not the other way round.**
        //
        // Without this the bar starts blank while the URL carries filters, and the FIRST emit — which
        // `app-department-filter` fires by itself, because it pins an Agent to their own department
        // on init — rebuilds the query string from those blank fields and silently drops every other
        // filter. A deep link with `?status=New&priority=High` lost both on load. Found by reloading
        // a filtered list in a real browser; recorded as finding **I-19**.
        effect(() => {
            const current = this.value();

            this.q = current.q ?? '';
            this.status = current.status ?? null;
            this.priority = current.priority ?? null;
            this.categoryCode = current.categoryCode ?? null;
            this.assigneeId = current.assigneeId ?? null;
            this.breached = current.breached ?? null;
        });
    }

    protected applyDepartment(departmentId: string | null): void {
        this.emit({ departmentId });
    }

    /**
     * Emits the whole filter, not a delta: the screen writes it to the URL in one navigation, and a
     * partial emit would make two controls race to describe the same list.
     */
    protected emit(overrides: Partial<TicketListFilter> = {}): void {
        this.filterChange.emit({
            q: this.q.trim() === '' ? null : this.q.trim(),
            status: (this.status as TicketListFilter['status']) ?? null,
            priority: (this.priority as TicketListFilter['priority']) ?? null,
            categoryCode: this.categoryCode,
            assigneeId: this.assigneeId,
            departmentId: this.departmentId(),
            breached: this.breached,
            ...overrides
        });
    }
}
