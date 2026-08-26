import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AppMenuitem } from './app.menuitem';

/**
 * The shell's navigation region.
 *
 * **Deliberately empty in Story 01.** What a Customer, an Agent, a Manager and an Administrator
 * each see is a role decision, and roles do not exist until Story 02:
 *
 *   TODO Story 02: the staff menu — queue, tickets, customers, notifications (docs/ui-design.md §4.1)
 *                  plus the Administrator entries, each behind the role that may see it.
 *   TODO Story 13: the portal menu — my requests, submit, help (docs/ui-design.md §4.2).
 *
 * A menu entry is a convenience, never a control: everything it hides is independently refused by
 * the server (docs/architecture.md §2.2).
 */
@Component({
    selector: 'app-menu',
    standalone: true,
    imports: [CommonModule, AppMenuitem, RouterModule],
    template: `<ul class="layout-menu">
        <ng-container *ngFor="let item of model; let i = index">
            <li app-menuitem *ngIf="!item.separator" [item]="item" [index]="i" [root]="true"></li>
            <li *ngIf="item.separator" class="menu-separator"></li>
        </ng-container>
    </ul> `
})
export class AppMenu {
    model: MenuItem[] = [];
}
