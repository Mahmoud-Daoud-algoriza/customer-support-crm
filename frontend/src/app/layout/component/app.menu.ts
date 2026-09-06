import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { AuthStore } from '../../core/auth/auth.store';
import { DirectionService } from '../../core/i18n/direction.service';
import { AppMenuitem } from './app.menuitem';

/**
 * The staff shell's navigation (docs/ui-design.md §4.1).
 *
 * **Role-based visibility: *Reports* for Manager+, the *Admin* section for Administrator only.**
 * Hiding is a convenience — the routes are guarded and the endpoints independently return `403`
 * (docs/architecture.md §2.2, §4.2). Nothing here is a permission.
 *
 * Entries whose screens arrive later point at routes that do not resolve yet, so they are added by
 * the story that builds the screen rather than left as dead links.
 *
 * **This menu is staff-only, and Story 13 is why.** Story 12 put the portal's two destinations here
 * because the portal shell was still the shared staff chrome. It no longer is: ui-design §4.2 gives
 * the portal a header with two links and **no sidebar**, and `PortalShellComponent` now renders it.
 * A Customer never mounts this component, so a portal section here would be unreachable code —
 * and, if it were ever reached, a customer inside agent chrome (AD-14). **Do not add one back.**
 */
@Component({
    selector: 'app-menu',
    standalone: true,
    imports: [CommonModule, AppMenuitem, RouterModule],
    template: `<ul class="layout-menu">
        @for (item of model(); track item.label; let i = $index) {
            @if (item.separator) {
                <li class="menu-separator"></li>
            } @else {
                <li app-menuitem [item]="item" [index]="i" [root]="true"></li>
            }
        }
    </ul> `
})
export class AppMenu {
    private readonly store = inject(AuthStore);
    private readonly transloco = inject(TranslocoService);
    private readonly direction = inject(DirectionService);

    readonly model = computed<MenuItem[]>(() => {
        // `computed()` only re-runs when a *signal* read inside it changes. `TranslocoService`
        // exposes the active language as a plain getter, not a signal, so reading
        // `getActiveLang()` here established no dependency and the menu only ever picked up a
        // language switch on the next full reload. `DirectionService.activeLanguage` is the
        // signal that already drives `dir`/`lang` on the document (T2-J) — reading it here makes
        // the menu re-translate live, the same way.
        this.direction.activeLanguage();

        const t = (key: string) => this.transloco.translate(key);
        const sections: MenuItem[] = [];

        if (this.store.isAtLeast('Agent')) {
            const workspace: MenuItem[] = [];

            // Each is added by the story that builds the screen, so no entry is ever a dead link.

            // **First, because it is the landing screen** (docs/ui-design.md §4.1, §5.1, UI-2).
            workspace.push({ label: t('nav.queue'), icon: 'pi pi-fw pi-inbox', routerLink: ['/workspace/queue'] });
            workspace.push({ label: t('nav.tickets'), icon: 'pi pi-fw pi-ticket', routerLink: ['/workspace/tickets'] });
            workspace.push({ label: t('nav.customers'), icon: 'pi pi-fw pi-users', routerLink: ['/workspace/customers'] });
            workspace.push({ label: t('nav.knowledge'), icon: 'pi pi-fw pi-book', routerLink: ['/workspace/knowledge'] });
            workspace.push({ label: t('nav.notifications'), icon: 'pi pi-fw pi-bell', routerLink: ['/workspace/notifications'] });

            if (this.store.isAtLeast('Manager')) {
                // Story 15 — Manager+ only (docs/ui-design.md §4.1, §5.7). Hiding the entry is
                // convenience; GET /reports/dashboard refuses an Agent with 403 regardless.
                workspace.push({ label: t('nav.reports'), icon: 'pi pi-fw pi-chart-bar', routerLink: ['/workspace/reports'] });
            }

            if (workspace.length > 0) {
                sections.push({ label: t('nav.workspace'), items: workspace });
            }
        }

        if (this.store.isAtLeast('Administrator')) {
            sections.push({
                label: t('nav.admin'),
                items: [
                    { label: t('nav.users'), icon: 'pi pi-fw pi-users', routerLink: ['/admin/users'] },
                    { label: t('nav.audit'), icon: 'pi pi-fw pi-history', routerLink: ['/admin/audit'] },
                    { label: t('nav.configuration'), icon: 'pi pi-fw pi-cog', routerLink: ['/admin/configuration'] },
                    { label: t('nav.knowledgeAuthoring'), icon: 'pi pi-fw pi-book', routerLink: ['/admin/knowledge'] },
                ],
            });
        }

        // **No Customer section, by design** (docs/ui-design.md §4.2). The portal has its own shell
        // with two header links and no sidebar, so a Customer never renders this menu at all — see
        // the class remarks.

        return sections;
    });
}
