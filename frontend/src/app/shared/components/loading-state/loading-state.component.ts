import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { SkeletonModule } from 'primeng/skeleton';

/**
 * Skeletons that match the final layout, not a spinner over blank space (docs/ui-design.md §9).
 * Regions load independently, so one slow call never blanks a screen.
 */
@Component({
    selector: 'app-loading-state',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SkeletonModule, TranslocoModule],
    template: `
        <div class="app-state app-state--loading" role="status" [attr.aria-label]="label() ?? ('shared.loading' | transloco)">
            @for (row of rows(); track $index) {
                <p-skeleton height="1.75rem" styleClass="app-state__skeleton" />
            }
        </div>
    `
})
export class LoadingStateComponent {
    /** How many skeleton rows to draw — set it to the number of rows the real layout shows. */
    readonly rowCount = input(3);
    /**
     * Overrides the announced label. `null` uses the translated default — the label must never be an
     * English literal, in either language (Story 17 Part B task 1).
     */
    readonly label = input<string | null>(null);

    protected readonly rows = computed(() => Array.from({ length: this.rowCount() }));
}
