import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { SelectModule } from 'primeng/select';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';
import { IdentityClient } from '../../../core/api/identity.client';
import { AuthStore } from '../../../core/auth/auth.store';
import { UserRow } from '../../../core/auth/identity.model';

/**
 * The `Assign ▾` control of docs/ui-design.md §5.3.
 *
 * <h3>Why this component is shaped the way it is — finding I-16</h3>
 *
 * **No approved endpoint lets an Agent list the staff in their department.** `GET /users` is
 * **Administrator-only** (docs/api-design.md §5.3), and §5 publishes no agent-readable directory of
 * assignable users. So the picker the plan sketches cannot be populated for the role that needs it
 * most, and inventing an endpoint is exactly what this story may not do.
 *
 * What is implemented instead, and why it is the smallest defensible reading:
 *
 * - **Every caller gets "Assign to me".** It needs no directory at all, and it is always valid: the
 *   ticket was loaded through the department scope, so an Agent reaching it is by construction in
 *   its department — which is the server's own rule for a legal assignee (§5 constraint 10).
 * - **An Administrator also gets a real picker**, filtered to active agents in the ticket's
 *   department — they are the one role `GET /users` admits. The directory is requested only when the
 *   caller may actually read it, because `errorInterceptor` routes any `403` to `/403` and a
 *   speculative call would bounce an Agent off the ticket. **The guard hides, it does not
 *   protect**: the server refuses an illegal assignee with `422` regardless of what was offered.
 *
 * **If the user reads task 13 as requiring a full picker for Agents**, the fix is a contract
 * addition — an agent-readable list of assignable users — and that is their call, not this story's.
 */
@Component({
    selector: 'app-ticket-assign',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, FormsModule, MessageModule, SelectModule, TranslocoModule],
    template: `
        <div class="app-assign">
            @if (candidates().length > 0) {
                <p-select [options]="candidates()" [(ngModel)]="selectedUserId" optionLabel="displayName" optionValue="id" [placeholder]="'tickets.assignPlaceholder' | transloco" [ariaLabel]="'tickets.assign' | transloco" />

                <p-button [label]="'tickets.assign' | transloco" [loading]="busy()" [disabled]="busy() || selectedUserId === null" (onClick)="assignSelected()" />
            }

            <!-- Always offered, and the only path that needs no directory. -->
            <p-button severity="secondary" [label]="'tickets.assignToMe' | transloco" [loading]="busy()" [disabled]="busy() || isAssignedToMe()" (onClick)="assignToMe()" />

            <!-- 422 renders inline, in context (docs/ui-design.md §9). -->
            @if (problem(); as failure) {
                <p-message severity="error" [text]="errorKey(failure) | transloco" />
            }
        </div>
    `
})
export class TicketAssignComponent implements OnInit {
    private readonly identity = inject(IdentityClient);
    private readonly auth = inject(AuthStore);

    /** The ticket's department — a candidate must belong to it, or the server answers `422`. */
    readonly departmentId = input.required<string>();

    /** The current assignee, so "Assign to me" can be disabled when it would be a no-op. */
    readonly assigneeId = input<string | null>(null);

    readonly busy = input(false);

    /** The failure to render inline — `422 assignee-out-of-department` above all. */
    readonly problem = input<ApiProblem | null>(null);

    readonly assign = output<string>();

    protected readonly candidates = signal<UserRow[]>([]);
    protected selectedUserId: string | null = null;

    protected errorKey = problemTranslationKey;

    protected readonly isAssignedToMe = computed(() => this.assigneeId() === this.auth.identity()?.id);

    /**
     * **`ngOnInit`, not the constructor** — `departmentId` is a *required* signal input and is not
     * set until the first change detection; reading one in a constructor throws `NG0950`. See
     * finding **I-18**.
     */
    ngOnInit(): void {
        // **Asked only when the caller may actually ask.** `GET /users` is Administrator-only, and
        // `errorInterceptor` routes ANY 403 to /403 — so speculatively calling it as an Agent would
        // bounce them off the ticket they were reading. The role check is therefore not an
        // optimisation, it is what keeps the screen usable.
        //
        // It is still only a guard: it decides what is SHOWN. The server refuses an illegal
        // assignee with 422 whatever this component offered, and refuses the directory itself with
        // 403 whatever this check concluded.
        if (!this.auth.isAtLeast('Administrator')) {
            return;
        }

        this.identity.listUsers({ role: 'Agent', departmentId: this.departmentId(), isActive: true, pageSize: 100 }).subscribe({ next: (result) => this.candidates.set(result.items) });
    }

    protected assignSelected(): void {
        if (this.selectedUserId !== null) {
            this.assign.emit(this.selectedUserId);
        }
    }

    protected assignToMe(): void {
        const me = this.auth.identity()?.id;

        if (me) {
            this.assign.emit(me);
        }
    }
}
