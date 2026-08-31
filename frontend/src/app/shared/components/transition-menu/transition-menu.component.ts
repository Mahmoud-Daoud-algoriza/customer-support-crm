import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { MessageModule } from 'primeng/message';
import { TicketStatus } from '../../../core/api/tickets.client';
import { AuthStore } from '../../../core/auth/auth.store';
import { isTerminal, offeredTransitions } from '../../lifecycle/transition-matrix';

/**
 * The `Transition ▾` menu of docs/ui-design.md §5.3.
 *
 * <h3>It offers legal ∧ permitted, computed client-side (UI-3)</h3>
 * Only transitions **legal from the current status** *and* **permitted for the caller's role** —
 * both tables live in `shared/lifecycle/transition-matrix.ts`, the single file finding **F-1**
 * confines the duplication to. **The server remains the authority**: a wrong offer here is refused
 * with `403` or `409`, so this component can only show too much or too little.
 *
 * <h3>Escalate is not in this menu</h3>
 * Escalation is an **action, not a status change** (AP-7, A-5), so it is a separate control —
 * `app-escalate-button`. Folding it in would contradict the model.
 *
 * <h3>Terminal statuses say why, rather than going quietly inert</h3>
 * `Closed` and `Cancelled` offer nothing, and the control renders a **reason line** instead of a
 * disabled-looking button with no explanation (§5.3).
 *
 * <h3>No optimistic UI (UI-8)</h3>
 * This component emits and waits. It never changes what it displays — the parent replaces the ticket
 * from the server's response, because a transition can be refused.
 */
@Component({
    selector: 'app-transition-menu',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, MenuModule, MessageModule, TranslocoModule],
    template: `
        @if (terminal()) {
            <!-- A reason, not an inert control (docs/ui-design.md §5.3). -->
            <p class="app-page__meta">
                {{ 'tickets.terminalReason' | transloco: { status: statusLabel() } }}
            </p>
        } @else if (items().length > 0) {
            <p-menu #menu [model]="items()" [popup]="true" />

            <p-button
                [label]="'tickets.transition' | transloco"
                icon="pi pi-angle-down"
                iconPos="right"
                severity="secondary"
                [loading]="busy()"
                [disabled]="busy()"
                (onClick)="menu.toggle($event)" />
        }
    `
})
export class TransitionMenuComponent {
    private readonly auth = inject(AuthStore);
    private readonly transloco = inject(TranslocoService);

    readonly status = input.required<TicketStatus>();

    readonly busy = input(false);

    readonly transition = output<TicketStatus>();

    protected readonly terminal = computed(() => isTerminal(this.status()));

    protected readonly statusLabel = computed(() =>
        this.transloco.translate(`tickets.status.${this.status()}`)
    );

    /**
     * The offered targets, as PrimeNG menu items.
     *
     * **Labels come from the i18n dictionary keyed by the status code**, never from the server and
     * never assembled from prose — T2-J puts display text on the client and the API returns codes.
     */
    protected readonly items = computed<MenuItem[]>(() =>
        offeredTransitions(this.auth.role(), this.status()).map((target) => ({
            // The imperative read is deliberate: PrimeNG's MenuItem takes a plain string, so the
            // pipe cannot be used inside the model. The key is the STATUS CODE — the same
            // dictionary the chip uses, so a target and a chip can never disagree.
            label: this.transloco.translate(`tickets.status.${target}`),
            command: () => this.transition.emit(target)
        }))
    );
}
