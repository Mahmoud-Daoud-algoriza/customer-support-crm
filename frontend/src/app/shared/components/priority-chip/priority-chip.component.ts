import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { TicketPriority } from '../../../core/api/tickets.client';

/**
 * `PriorityChip` — the shared component of docs/ui-design.md §8. Four levels (A-6).
 *
 * **Staff only. It must never be imported by a portal component** (§8, and Story 05 task 12 says so
 * in as many words). The portal ticket payload carries no priority at all (AP-16, §6.4), so a
 * portal screen importing this would have nothing to pass it — and a customer indicates urgency
 * with `isUrgent`, which is a different thing that does not set priority (A-17).
 */
@Component({
    selector: 'app-priority-chip',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [TagModule, TranslocoModule],
    template: `<p-tag [severity]="severity()" [value]="'tickets.priority.' + priority() | transloco" />`
})
export class PriorityChipComponent {
    readonly priority = input.required<TicketPriority>();

    protected readonly severity = computed<'secondary' | 'info' | 'warn' | 'danger'>(() => {
        switch (this.priority()) {
            case 'Low':
                return 'secondary';
            case 'Medium':
                return 'info';
            case 'High':
                return 'warn';
            default:
                return 'danger';
        }
    });
}
