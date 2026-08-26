import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectButtonModule } from 'primeng/selectbutton';
import { RuntimeConfigService } from '../../../core/config/runtime-config.service';
import { DirectionService } from '../../../core/i18n/direction.service';

/** Each language is labelled in its own script, so it is readable whichever one is active. */
const NATIVE_NAMES: Record<string, string> = {
    en: 'English',
    ar: 'العربية'
};

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
    imports: [FormsModule, SelectButtonModule],
    template: `
        <p-selectbutton
            [options]="options()"
            [ngModel]="direction.activeLanguage()"
            (ngModelChange)="switch($event)"
            optionLabel="label"
            optionValue="code"
            [allowEmpty]="false"
            ariaLabel="Language"
        />
    `
})
export class LanguageSwitcherComponent {
    protected readonly direction = inject(DirectionService);
    private readonly runtimeConfig = inject(RuntimeConfigService);

    protected readonly options = computed(() =>
        this.runtimeConfig.languages().map((code) => ({ code, label: NATIVE_NAMES[code] ?? code.toUpperCase() }))
    );

    protected switch(code: string): void {
        void this.direction.use(code);
    }
}
