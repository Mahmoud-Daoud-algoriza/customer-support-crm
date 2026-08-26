import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AppLayout } from '../component/app.layout';

/**
 * The Customer portal shell (docs/ui-design.md §4.2).
 *
 * **The navigation region is deliberately empty in Story 01.** Story 13 fills the portal
 * navigation. It is a separate shell from the staff one so a customer never loads agent chrome
 * (AD-14).
 */
@Component({
    selector: 'app-portal-shell',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [AppLayout],
    template: `<app-layout />`
})
export class PortalShellComponent {}
