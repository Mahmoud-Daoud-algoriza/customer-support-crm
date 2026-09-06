import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';
import { SelectButtonModule } from 'primeng/selectbutton';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { DirectionService } from '../../../core/i18n/direction.service';

/** Each language is labelled in its own script, so it is readable whichever one is active. */
const NATIVE_NAMES: Record<string, string> = {
    en: 'English',
    ar: 'العربية'
};

/**
 * The order the two buttons sit in, left to right, **regardless of the active direction**. The
 * switcher is the one control that must not mirror: it is how a reader who cannot read the current
 * language gets back out, so its buttons stay where they were last seen. Anything the server offers
 * that is not listed here follows, in the order the server gave.
 */
const FIXED_ORDER = ['en', 'ar'];

/**
 * The switcher lives in all three shells (docs/ui-design.md §10.1). Switching happens at runtime:
 * no reload, no loss of application state (T2-J).
 *
 * The offered languages come from `/config/bootstrap`, so the server decides what is available and
 * this component hardcodes nothing.
 */
@Component({
    selector: 'app-language-switcher',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [FormsModule, SelectButtonModule, TranslocoModule],
    template: `
        <p-selectbutton
            [options]="options()"
            [ngModel]="direction.activeLanguage()"
            (ngModelChange)="switch($event)"
            optionLabel="label"
            optionValue="code"
            [allowEmpty]="false"
            [ariaLabel]="'shared.language' | transloco"
        />
    `,
    // `direction: ltr` pins the row to a physical left-to-right layout, so English stays on the left
    // and العربية on the right in Arabic too; `unicode-bidi: isolate` keeps that island from
    // disturbing the surrounding RTL topbar. This is the deliberate exception to the mirror-by-
    // default rule in docs/ui-design.md §10.2 — see FIXED_ORDER above.
    styles: [
        `
            :host {
                direction: ltr;
                unicode-bidi: isolate;
            }
        `
    ]
})
export class LanguageSwitcherComponent {
    protected readonly direction = inject(DirectionService);
    private readonly runtimeConfig = inject(RuntimeConfigService);

    protected readonly options = computed(() =>
        [...this.runtimeConfig.languages()]
            .sort((a, b) => rank(a) - rank(b))
            .map((code) => ({ code, label: NATIVE_NAMES[code] ?? code.toUpperCase() }))
    );

    protected switch(code: string): void {
        void this.direction.use(code);
    }
}

/** Listed languages sort by their fixed position; anything else sorts after them, order preserved. */
function rank(code: string): number {
    const index = FIXED_ORDER.indexOf(code);

    return index === -1 ? FIXED_ORDER.length : index;
}
