import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { toSignal } from '@angular/core/rxjs-interop';

/** The three shared status screens of docs/ui-design.md §2: `/403`, `/404`, `/error`. */
export type StatusKind = 'forbidden' | 'notFound' | 'error';

/**
 * `404` reads identically whether the record is missing or merely out of the caller's scope — AP-4
 * exists precisely so the UI cannot distinguish the two (docs/ui-design.md §9).
 */
@Component({
    selector: 'app-status-page',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ButtonModule, RouterLink, TranslocoModule],
    template: `
        <div class="app-centered-card-shell">
            <div class="app-centered-card app-state">
                <i class="pi app-state__icon" [class]="icon()" aria-hidden="true"></i>
                <h1 class="app-state__title">{{ 'status.' + kind() + '.title' | transloco }}</h1>
                <p class="app-state__message">{{ 'status.' + kind() + '.message' | transloco }}</p>
                <p-button [label]="'actions.backHome' | transloco" routerLink="/" severity="secondary" />
            </div>
        </div>
    `
})
export class StatusPageComponent {
    private readonly data = toSignal(inject(ActivatedRoute).data, { initialValue: {} as { kind?: StatusKind } });

    protected readonly kind = computed<StatusKind>(() => this.data().kind ?? 'error');
    protected readonly icon = computed(() => STATUS_ICONS[this.kind()]);
}

const STATUS_ICONS: Record<StatusKind, string> = {
    forbidden: 'pi-lock',
    notFound: 'pi-compass',
    error: 'pi-exclamation-triangle'
};
