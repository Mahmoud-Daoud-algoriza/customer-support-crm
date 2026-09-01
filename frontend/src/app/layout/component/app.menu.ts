import { CommonModule } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslocoService } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { AuthStore } from '../../core/auth/auth.store';
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
 * **The portal section is Story 13's to complete.** Story 12 adds the two destinations that exist —
 * submit a request, and Help — because `/portal/help` has to be reachable from the shell; the
 * requests list joins them with Story 13.
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

    readonly model = computed<MenuItem[]>(() => {
        // Reading the active language makes the menu re-translate on a language switch without a
        // reload (T2-J).
        this.transloco.getActiveLang();

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
                // TODO Story 15: Reports — Manager+ only (docs/ui-design.md §4.1).
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

        // The Customer portal (docs/ui-design.md §4.2). A Customer is not "at least an Agent", so
        // none of the staff sections above apply to them — and no staff entry may be added here.
        // Story 13 adds the requests list and makes it the landing entry.
        if (this.store.role() === 'Customer') {
            sections.push({
                label: t('nav.portal'),
                items: [
                    { label: t('nav.submitRequest'), icon: 'pi pi-fw pi-plus', routerLink: ['/portal/requests/new'] },
                    { label: t('nav.help'), icon: 'pi pi-fw pi-book', routerLink: ['/portal/help'] },
                ],
            });
        }

        return sections;
    });
}
