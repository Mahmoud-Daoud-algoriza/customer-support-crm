import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';

/**
 * An empty state is never an error (docs/ui-design.md §9): no tickets assigned, no ratings yet, no
 * search results and no notifications are all normal. It says what would fill the region and, where
 * one exists, offers the action that would.
 *
 * Every later list and region reuses this component; none re-invents it.
 */
@Component({
    selector: 'app-empty-state',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule],
    template: `
        <div class="app-state">
            <i class="pi app-state__icon" [class]="icon()" aria-hidden="true"></i>
            <p class="app-state__title">{{ title() }}</p>
            @if (message()) {
                <p class="app-state__message">{{ message() }}</p>
            }
            @if (actionLabel()) {
                <p-button [label]="actionLabel()!" severity="secondary" (onClick)="action.emit()" />
            }
        </div>
    `
})
export class EmptyStateComponent {
    readonly title = input.required<string>();
    readonly message = input<string | null>(null);
    readonly icon = input('pi-inbox');
    readonly actionLabel = input<string | null>(null);

    readonly action = output<void>();
}
