import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';

/**
 * The satisfaction rating control — docs/ui-design.md §7.3, §11.
 *
 * <h3>⚠ OQ-1 is open, and this component is shaped by that, not around it</h3>
 * **The scale is undecided.** [product-scope.md] T2-F specifies *"a one-question satisfaction rating
 * with an optional comment"* and fixes **no scale**; [data-model.md] §2.15 stores an ordinal and
 * *"encodes no range … and none may be inferred … into a validation rule, a check constraint, or a
 * UI control"*.
 *
 * So this component **takes `{ min, max }` and renders whatever they say**. The values come from
 * `feedback.ratingScale` in `GET /config` — the approved `Feedback rating scale` key
 * (architecture §6.3), whose boundary values are deliberately undecided.
 *
 * **There is no star widget here, no `1..5` array, and no thumbs pair.** ui-design §11 is explicit:
 * *"the plan must not hardcode a star widget until OQ-1 is answered."* If this file ever contains a
 * literal number that could be a scale boundary, OQ-1 has been answered by accident.
 *
 * <h3>The seam for a binary answer</h3>
 * §11 records the two shapes the answer could take: *"an ordinal range renders as a rating scale; a
 * binary scale renders as two buttons."* This renders the **ordinal** shape — one selectable value
 * per step from `min` to `max`, which is the general case and already degenerates correctly to two
 * controls when the configured range spans two values.
 *
 * **Should OQ-1 decide a genuine binary (thumbs up / down) rather than a two-step ordinal**, the
 * change is confined here: `binaryLabels()` below is the seam — give the two positions their own
 * labels and icons, and no caller changes, because the emitted value is still the ordinal the
 * contract carries. **Do not add that branch speculatively.**
 *
 * <h3>Declining is normal</h3>
 * §7.3: *"declining is normal: the UI never nags or blocks."* This component starts with **nothing
 * selected**, has **no default**, and emits only when the customer picks a value. It renders no
 * reminder, no asterisk and no "please rate us".
 */
@Component({
    selector: 'app-rating-input',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [TranslocoModule],
    template: `
        <fieldset class="app-rating" [disabled]="disabled()">
            <legend class="app-rating__legend">{{ 'portal.feedback.question' | transloco }}</legend>

            <div class="app-rating__scale" role="radiogroup" [attr.aria-label]="'portal.feedback.question' | transloco">
                @for (step of steps(); track step) {
                    <button
                        type="button"
                        class="app-rating__step app-ltr-numeric"
                        role="radio"
                        [class.app-rating__step--selected]="step === value()"
                        [attr.aria-checked]="step === value()"
                        [disabled]="disabled()"
                        (click)="select(step)">
                        {{ step }}
                    </button>
                }
            </div>

            <!-- The ends of whatever range was configured, named rather than numbered, so the scale
                 reads as an opinion instead of an arithmetic puzzle. The wording is generic on
                 purpose: it is true of every candidate OQ-1 might choose. -->
            <p class="app-rating__ends">
                <span>{{ 'portal.feedback.lowest' | transloco }}</span>
                <span>{{ 'portal.feedback.highest' | transloco }}</span>
            </p>
        </fieldset>
    `
})
export class RatingInputComponent {
    /**
     * The scale's lower bound, **from configuration** (`feedback.ratingScale.min`). It is a required
     * input with no default: a default here would be a scale, and OQ-1 has not chosen one.
     */
    readonly min = input.required<number>();

    /** The scale's upper bound, from `feedback.ratingScale.max`. Required, for the same reason. */
    readonly max = input.required<number>();

    /** The current selection, or `null` while nothing has been chosen — the starting state. */
    readonly value = input<number | null>(null);

    readonly disabled = input(false);

    readonly valueChange = output<number>();

    /**
     * Every selectable value from `min` to `max` inclusive, **computed, never listed**.
     *
     * A configured range that is inverted or absurd yields an empty scale rather than an exception:
     * startup validation already checks `Min < Max` (`ConfigurationValidator`), so a bad pair is a
     * configuration fault the server refuses to start on — this component simply does not invent a
     * scale to cover for one.
     */
    protected readonly steps = computed<number[]>(() => {
        const min = this.min();
        const max = this.max();

        if (!Number.isFinite(min) || !Number.isFinite(max) || max < min) {
            return [];
        }

        return Array.from({ length: max - min + 1 }, (_, index) => min + index);
    });

    /**
     * **The seam ui-design §11 asks for.** Should OQ-1 answer *binary* rather than *ordinal*, the two
     * positions get their own labels and icons here and the template branches on this — the emitted
     * value stays the ordinal the contract carries, so `PortalClient.submitFeedback` and the server
     * are untouched.
     *
     * It returns `null` today because **no such decision has been taken**, and a component that
     * quietly behaved like a thumbs pair at two steps would be answering OQ-1 in code.
     */
    protected binaryLabels(): readonly [string, string] | null {
        return null;
    }

    protected select(step: number): void {
        if (this.disabled()) {
            return;
        }

        this.valueChange.emit(step);
    }
}
