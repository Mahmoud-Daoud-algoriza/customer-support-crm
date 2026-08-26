import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { ApiProblem, problemTranslationKey } from '../../../core/api/api-problem';

/**
 * The message comes from the Problem Details `type` slug mapped to a **translated** string. The
 * server's `detail` is never rendered raw, because the API returns codes and the front end owns
 * display text (docs/ui-design.md §9, T2-J, AP-2).
 *
 * Every later screen reuses this component; none re-invents it.
 */
@Component({
    selector: 'app-error-state',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, MessageModule, TranslocoModule],
    template: `
        <div class="app-state app-state--error">
            <p-message severity="error" [text]="messageKey() | transloco" />
            @if (retryable()) {
                <p-button [label]="'actions.retry' | transloco" severity="secondary" (onClick)="retry.emit()" />
            }
        </div>
    `
})
export class ErrorStateComponent {
    readonly problem = input.required<ApiProblem>();
    readonly retryable = input(true);

    readonly retry = output<void>();

    /** Falls back to a generic key so an unmapped slug still reads as a sentence, never as a code. */
    readonly messageKey = computed(() => problemTranslationKey(this.problem()));
}
