import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';

/**
 * `SlaIndicator` — the shared component of docs/ui-design.md §8, used by My queue (§5.1).
 *
 * **Breached is visually distinct, not merely a different word** (§8). The red weight comes from
 * `.app-breach`, the same class the ticket list already uses, so "breached" looks identical wherever
 * it appears.
 *
 * <h3>Resolution-based, because that is what the row carries</h3>
 * `TicketListItem` carries `resolutionDueAt` but **not** `firstResponseDueAt` (docs/api-design.md
 * §6.4), so the queue's indicator is resolution-based. The ticket **detail** shows both clocks from
 * the full `Ticket` payload and does not use this component.
 *
 * <h3>A null due date renders "—", never "breached" and never "0"</h3>
 * **PF-5.** `resolutionDueAt` is required at creation (data-model §2.6) so the queue never sees a
 * null one, but the display rule ui-design §11 fixes belongs in the one component that renders SLA
 * time — not re-decided by each caller. PF-5 itself stays open for Story 09; this is the rendering
 * rule, not an answer to it.
 *
 * <h3>Latched flag first, clock second</h3>
 * `breached` is the server's **latching** flag (Story 09 populates it; it defaults to `false`). When
 * it is set the indicator says breached regardless of the clock, because a ticket that breached and
 * was then given a later deadline is still a breach — the flag is the fact, the clock is only a
 * derivation. A due date already in the past also reads as overdue, so the indicator stays truthful
 * before Story 09 starts latching anything.
 *
 * **No timer.** It renders once from the payload; nothing here polls or ticks (T3-B).
 */
@Component({
    selector: 'app-sla-indicator',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [TranslocoModule],
    template: `
        @if (state(); as sla) {
            <span class="app-sla" [class.app-breach]="sla.overdue">
                <!-- Numerals stay LTR-embedded inside RTL text (ui-design §10.2). -->
                <span class="app-ltr-numeric">
                    @for (part of sla.parts; track part.unit) {
                        {{ part.value }}{{ 'tickets.slaUnit.' + part.unit | transloco }}
                    }
                </span>
                <span>{{ sla.labelKey | transloco }}</span>
            </span>
        } @else {
            <span class="app-sla app-sla--unknown" [attr.aria-label]="'tickets.slaUnknown' | transloco">&mdash;</span>
        }
    `
})
export class SlaIndicatorComponent {
    /** The resolution deadline as the server computed it at creation (A-20 — it does not move). */
    readonly dueAt = input<string | null>(null);

    /** The server's latching breach flag. */
    readonly breached = input(false);

    /** Evaluated against the payload, not a live clock — see the no-timer note above. */
    readonly now = input<Date | null>(null);

    protected readonly state = computed(() => {
        const due = this.dueAt();

        if (!due) {
            return null;
        }

        const deadline = new Date(due).getTime();

        if (Number.isNaN(deadline)) {
            return null;
        }

        const from = (this.now() ?? new Date()).getTime();
        const overdue = this.breached() || deadline <= from;
        const distance = Math.abs(deadline - from);

        return {
            overdue,
            parts: splitDistance(distance),
            labelKey: overdue ? 'tickets.slaOverdue' : 'tickets.slaRemaining'
        };
    });
}

/** One rendered magnitude — a number and the translation key naming its unit. */
interface SlaPart {
    value: number;
    unit: 'day' | 'hour' | 'minute';
}

/**
 * Coarse by design: the queue is scanned, not read. The largest unit that is not zero, plus the next
 * one down when it adds information — `3d 4h`, `2h 15m`, `8m`.
 *
 * **It returns parts rather than a finished string**, so the template renders the unit words through
 * the `transloco` **pipe**. Building the string here with `TranslocoService.translate()` looked
 * simpler and was wrong twice over: the call returns the raw key when the scope has not loaded yet,
 * and a completed string does not re-translate when the language is switched. The pipe handles both.
 * Caught by this component's own spec.
 */
function splitDistance(milliseconds: number): SlaPart[] {
    const minutes = Math.floor(milliseconds / 60_000);
    const days = Math.floor(minutes / 1_440);
    const hours = Math.floor((minutes % 1_440) / 60);
    const remainingMinutes = minutes % 60;

    if (days > 0) {
        return hours > 0
            ? [{ value: days, unit: 'day' }, { value: hours, unit: 'hour' }]
            : [{ value: days, unit: 'day' }];
    }

    if (hours > 0) {
        return remainingMinutes > 0
            ? [{ value: hours, unit: 'hour' }, { value: remainingMinutes, unit: 'minute' }]
            : [{ value: hours, unit: 'hour' }];
    }

    return [{ value: remainingMinutes, unit: 'minute' }];
}
