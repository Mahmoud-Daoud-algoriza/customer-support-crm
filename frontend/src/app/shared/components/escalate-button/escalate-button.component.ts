import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TicketStatus } from '../../../core/api/tickets.client';
import { AuthStore } from '../../../core/auth/auth.store';
import { isTerminal, mayEscalate } from '../../lifecycle/transition-matrix';

/**
 * `Escalate` — docs/ui-design.md §5.3.
 *
 * <h3>A separate control, never inside the transition menu</h3>
 * Escalation is an **action, not a status change** (AP-7, A-5). Putting it in the transition menu
 * would contradict the model, and the two controls sit side by side for exactly that reason.
 *
 * <h3>The confirmation names the effect (UI-12), and now names the notification too</h3>
 * *Priority rises one level, status is unchanged* — and since **A-21** closed OQ-3 on 2026-08-31,
 * the dialog **may also say a manager will be notified**. It is worded as *"a manager"*, not *"the
 * department manager"*: A-21's cascade notifies the department's own manager, else every active
 * `Manager`, else every active `Administrator`, so **one unconditional sentence is true on every
 * rung**. The screen therefore needs no conditional and **no new payload field telling it which
 * rung fired** — and it is not given one (ui-design §11).
 *
 * <h3>Staff only</h3>
 * A-16's last row. The button is not rendered for a `Customer`, and the server refuses the call with
 * `403` regardless of what was rendered — **the guard hides, it does not protect**.
 */
@Component({
    selector: 'app-escalate-button',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, ConfirmDialogModule, TranslocoModule],
    providers: [ConfirmationService],
    template: `
        @if (visible()) {
            <p-confirmDialog />

            <p-button
                severity="warn"
                icon="pi pi-arrow-up"
                [label]="'tickets.escalate' | transloco"
                [loading]="busy()"
                [disabled]="busy()"
                (onClick)="confirm()" />
        }
    `
})
export class EscalateButtonComponent {
    private readonly auth = inject(AuthStore);
    private readonly confirmation = inject(ConfirmationService);
    private readonly transloco = inject(TranslocoService);

    readonly status = input.required<TicketStatus>();

    readonly busy = input(false);

    readonly escalate = output<void>();

    /**
     * Hidden for a customer (A-16) and on a terminal ticket — escalating a closed ticket raises the
     * priority of something nobody is working on, and §5.3 disables the lifecycle controls on
     * `Closed` and `Cancelled`.
     */
    protected readonly visible = computed(() =>
        mayEscalate(this.auth.role()) && !isTerminal(this.status())
    );

    /**
     * **Translated here, not passed as a key.** PrimeNG renders `header` and `message` verbatim, so
     * handing it a Transloco key would put the key on screen.
     */
    protected confirm(): void {
        this.confirmation.confirm({
            header: this.transloco.translate('tickets.escalate'),
            message: this.transloco.translate('tickets.escalateConfirm'),
            acceptLabel: this.transloco.translate('primeng.accept'),
            rejectLabel: this.transloco.translate('primeng.reject'),
            accept: () => this.escalate.emit()
        });
    }
}
