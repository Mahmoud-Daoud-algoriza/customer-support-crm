import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { TicketStatus } from '../../../core/api/tickets.client';

/**
 * `StatusChip` — the shared component of docs/ui-design.md §8: *"One colour per A-5 status; **the
 * portal uses the same status vocabulary** — no separate customer wording was authorized."*
 *
 * So this component is shared by the queue, the lists, the ticket detail **and the portal**. There
 * is deliberately no customer-facing variant: inventing softer wording for the same six states
 * would be product vocabulary no approved document defines.
 *
 * The label is translated from the status code; the code itself is never rendered raw.
 */
@Component({
    selector: 'app-status-chip',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [TagModule, TranslocoModule],
    template: `<p-tag [severity]="severity()" [value]="'tickets.status.' + status() | transloco" />`
})
export class StatusChipComponent {
    readonly status = input.required<TicketStatus>();

    /**
     * One colour per status (§8). `Resolved` and `Closed` are deliberately different: closure is a
     * manual act after resolution (A-16), and collapsing them would hide that a ticket is still
     * open for a reopen.
     */
    protected readonly severity = computed<'info' | 'success' | 'warn' | 'secondary' | 'contrast'>(() => {
        switch (this.status()) {
            case 'New':
                return 'info';
            case 'Open':
                return 'contrast';
            case 'Pending':
                return 'warn';
            case 'Resolved':
                return 'success';
            default:
                // Closed and Cancelled — both terminal, both visually quiet.
                return 'secondary';
        }
    });
}
