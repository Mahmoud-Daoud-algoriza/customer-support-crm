import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { PaginatorModule } from 'primeng/paginator';
import { ApiProblem } from '../../../core/api/api-problem';
import { NotificationPage, NotificationRow, NotificationsClient } from '../../../core/api/notifications.client';
import { NotificationStore } from '../../../core/notifications/notification.store';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingStateComponent } from '../../../shared/components/loading-state/loading-state.component';

/**
 * Notifications — `/workspace/notifications` (docs/ui-design.md §5.8). Agent+.
 *
 * <h3>Four types, each row linking to its ticket</h3>
 * The four of A-13 and no others. The link is the point of the screen: a notification exists to get
 * someone to a ticket, so the row is the route to it rather than a message to be read and dismissed.
 *
 * <h3>There is no mark-all-read control, here or in the bell panel</h3>
 * **AP-18.** `POST /notifications/read-all` was removed from the contract as unrequested surface, so
 * there is no endpoint to call and deliberately no button that would want one. A row is marked read
 * as the user opens it, one at a time.
 *
 * <h3>Reads go through the store, so the badge and this screen never disagree</h3>
 * Marking read here updates the shell's badge, because both read the same `NotificationStore`. A
 * screen that marked rows read against the client directly would leave the bell showing a count this
 * list had already cleared.
 *
 * **Nothing polls** (T3-B): the list loads on entry and after a mark-read.
 */
@Component({
    selector: 'app-notification-list',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        ButtonModule, DatePipe, EmptyStateComponent, ErrorStateComponent, LoadingStateComponent,
        PaginatorModule, RouterLink, TranslocoModule
    ],
    template: `
        <section class="app-page">
            <header class="app-page__header">
                <h1 class="app-page__title">{{ 'notifications.title' | transloco }}</h1>

                <!-- The only filter §5.8 implies. There is no mark-all-read beside it (AP-18). -->
                <p-button
                    severity="secondary"
                    [outlined]="true"
                    [label]="(unreadOnly() ? 'notifications.showAll' : 'notifications.showUnread') | transloco"
                    (onClick)="toggleFilter()"
                />
            </header>

            @if (problem(); as failure) {
                <app-error-state [problem]="failure" (retry)="load()" />
            } @else {
                @if (page(); as result) {
                    @if (result.items.length === 0) {
                        <!-- An empty feed is normal, not an error (§9). -->
                        <app-empty-state
                            [title]="'notifications.emptyTitle' | transloco"
                            [message]="'notifications.emptyMessage' | transloco"
                            icon="pi-bell"
                        />
                    } @else {
                        <ul class="app-notifications">
                            @for (row of result.items; track row.id) {
                                <li class="app-notifications__row" [class.app-notifications__row--unread]="!row.readAt">
                                    <div class="app-notifications__main">
                                        <a
                                            class="app-notifications__type"
                                            [routerLink]="['/workspace/tickets', row.ticketId]"
                                            (click)="open(row)"
                                        >
                                            {{ 'notifications.type.' + row.type | transloco }}
                                        </a>

                                        <span class="app-notifications__subject">{{ row.ticketSubject }}</span>
                                    </div>

                                    <span class="app-notifications__when app-ltr-numeric">
                                        {{ row.createdAt | date: 'short' }}
                                    </span>
                                </li>
                            }
                        </ul>

                        <p-paginator
                            [first]="(result.page - 1) * result.pageSize"
                            [rows]="result.pageSize"
                            [totalRecords]="result.totalItems"
                            (onPageChange)="goToPage($event.page)"
                        />
                    }
                } @else {
                    <app-loading-state [rowCount]="6" [label]="'notifications.title' | transloco" />
                }
            }
        </section>
    `
})
export class NotificationListComponent {
    private readonly api = inject(NotificationsClient);
    private readonly store = inject(NotificationStore);

    protected readonly page = signal<NotificationPage | null>(null);
    protected readonly problem = signal<ApiProblem | null>(null);
    protected readonly unreadOnly = signal(false);

    private pageNumber = 1;

    constructor() {
        this.load();
    }

    protected toggleFilter(): void {
        this.unreadOnly.set(!this.unreadOnly());
        this.pageNumber = 1;
        this.load();
    }

    /** The paginator reports a **0-based** index; the API's `page` is 1-based (§2.1). */
    protected goToPage(zeroBasedPage: number | undefined): void {
        this.pageNumber = (zeroBasedPage ?? 0) + 1;
        this.load();
    }

    /**
     * Marks the row read on the way to its ticket — through the **store**, so the shell's badge drops
     * at the same moment this row stops being highlighted.
     */
    protected open(row: NotificationRow): void {
        if (!row.readAt) {
            this.store.markRead(row.id);
        }
    }

    protected load(): void {
        this.page.set(null);
        this.problem.set(null);

        this.api.list(this.unreadOnly(), { page: this.pageNumber }).subscribe({
            next: (result) => {
                this.page.set(result);

                // The screen and the badge read the same total, so entering this screen corrects a
                // badge that a background sweep has moved since the last navigation.
                this.store.refresh();
            },
            error: (failure: ApiProblem) => this.problem.set(failure)
        });
    }
}
