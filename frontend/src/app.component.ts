import { Component, computed, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { DirectionService } from './app/core/i18n/direction.service';

/**
 * The root shell.
 *
 * `<p-toast>` sits here rather than in each of the three area shells (auth, staff, portal) for one
 * reason: it must survive the navigation that some errors cause. A `401` redirects to sign-in and a
 * `403` routes to `/403`, both of which tear down the shell the user was in — a toast mounted there
 * would be destroyed before it was read. It also covers the status pages, which sit outside every
 * shell.
 *
 * **It is the transport-level surface only** — the `5xx` and unreachable-server rows of
 * `errorInterceptor`. It is not the in-app notification centre: that is `Notification` (A-13, T2-D),
 * a stored per-recipient entity with an unread badge, and Story 09 delivers it.
 */
@Component({
    selector: 'app-root',
    standalone: true,
    imports: [RouterModule, ToastModule],
    template: `
        <router-outlet></router-outlet>
        <p-toast [position]="toastPosition()" />
    `
})
export class AppComponent {
    private readonly direction = inject(DirectionService);

    /**
     * **The toast rises from the trailing bottom corner, and that corner mirrors under RTL**
     * (docs/ui-design.md §10.2). PrimeNG's `position` takes physical values only, so the logical
     * choice is made here from the active direction — the same way the ticket detail chooses its
     * drawer's edge — rather than being left as a hardcoded `bottom-right` that would sit over the
     * wrong corner in Arabic.
     */
    protected readonly toastPosition = computed<'bottom-left' | 'bottom-right'>(() =>
        this.direction.isRtl() ? 'bottom-left' : 'bottom-right'
    );
}
