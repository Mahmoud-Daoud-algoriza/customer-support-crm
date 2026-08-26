import { ChangeDetectionStrategy, Component } from '@angular/core';
import { AppLayout } from '../component/app.layout';

/**
 * The Workspace and Admin shell (docs/ui-design.md §4.1). It reuses the Sakai layout chrome —
 * topbar, collapsible sidebar, off-canvas drawer below tablet — rather than re-implementing it.
 *
 * **The navigation region is deliberately empty in Story 01.** Story 02 fills the staff menu once
 * roles exist, because what an Agent, a Manager and an Administrator each see is a role decision
 * (see app/layout/component/app.menu.ts).
 */
@Component({
    selector: 'app-staff-shell',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [AppLayout],
    template: `<app-layout />`
})
export class StaffShellComponent {}
