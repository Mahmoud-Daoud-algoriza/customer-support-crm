import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { Department, OrganizationClient } from '../../../core/api/organization.client';
import { AuthStore } from '../../../core/auth/auth.store';

/**
 * The department filter — **the front-end half of A-2, in one place**.
 *
 * `docs/ui-design.md` §5.2: *"Scoping is visible, not hidden. An Agent sees only their department
 * and the department filter is fixed to their own department and disabled, with a hint explaining
 * why. A Manager sees the filter enabled across all departments."*
 *
 * That rule lives here so no screen re-implements it. `disabledForOwnDepartment` is what the ticket
 * list turns on; the user directory and the reports screen leave it off.
 *
 * **This component does not enforce anything.** The server narrows every ticket read and write to
 * the caller's department regardless of what any client sends (docs/architecture.md §4.3, and a
 * `departmentId` query parameter can never widen scope — docs/api-design.md §4.3). The disabled
 * control exists to make that enforcement *legible* rather than mysterious. Guards hide; they do
 * not protect.
 *
 * **There is no branch equivalent of this component, and there must not be one.** Branch is never a
 * ticket scope (A-2, T2-K): it is a filter on the customer directory and the reports screen, and it
 * appears in no authorization predicate anywhere.
 */
@Component({
    selector: 'app-department-filter',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, MessageModule, SelectModule, TranslocoModule],
    template: `
        <div class="app-department-filter">
            <p-select
                name="departmentId"
                [options]="departments()"
                [ngModel]="value()"
                (ngModelChange)="value.set($event)"
                optionLabel="name"
                optionValue="id"
                [showClear]="!locked()"
                [disabled]="locked()"
                [placeholder]="'organization.department.any' | transloco"
                [ariaLabel]="'organization.department.label' | transloco"
            />

            @if (locked()) {
                <p-message severity="secondary" [text]="'organization.department.ownDepartmentHint' | transloco" />
            }
        </div>
    `
})
export class DepartmentFilterComponent {
    private readonly api = inject(OrganizationClient);
    private readonly auth = inject(AuthStore);

    /**
     * When true, an **Agent** — and only an Agent — gets the filter pinned to their own department
     * and disabled. Manager and Administrator are unaffected: the A-4 hierarchy says the rule is
     * about the Agent role, not about seniority in general (docs/ui-design.md §5.2).
     */
    readonly disabledForOwnDepartment = input(false);

    /** The selected department id, or `null` for "any department". Two-way bindable. */
    readonly value = model<string | null>(null);

    protected readonly departments = signal<Department[]>([]);

    /**
     * Locked only for a caller who is an Agent and nothing more. `isAtLeast('Manager')` is the same
     * hierarchy check the server's policies use, so the two cannot disagree about who is senior.
     */
    protected readonly locked = computed(() => this.disabledForOwnDepartment() && !this.auth.isAtLeast('Manager'));

    constructor() {
        // Cached for the session by the client, so several filters on one screen cost one request.
        this.api.getDepartments().subscribe((departments) => this.departments.set(departments));

        // Pinning the value, not merely disabling the control: a disabled control that still sent
        // "any department" would show the agent a filter that does not describe what they are
        // seeing. The server would narrow the results either way, which is the point of the hint.
        effect(() => {
            if (!this.locked()) {
                return;
            }

            const own = this.auth.identity()?.departmentId ?? null;

            if (own !== null && this.value() !== own) {
                this.value.set(own);
            }
        });
    }
}
