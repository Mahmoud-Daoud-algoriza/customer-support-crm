import { Injectable, computed, inject, signal } from '@angular/core';
import { NotificationRow, NotificationsClient } from '../api/notifications.client';
import { AuthStore } from '../auth/auth.store';

/**
 * The unread badge's state, and the recent rows the bell panel shows — Story 09 task 11.
 *
 * <h3>Refreshed on demand, and nothing polls</h3>
 * **T3-B.** The store is refreshed when the shell asks it to — on navigation and after a mark-read —
 * and at no other time. There is **no interval and no socket**: the SLA sweep runs on a coarse timer
 * server-side, so a badge that lags a navigation is exactly as fresh as A-3 requires, and nothing
 * here is described as real-time.
 *
 * <h3>Staff only</h3>
 * A-13's four events are staff-facing and no requirement gives a customer an in-app feed
 * (docs/ui-design.md §4.2), so {@link refresh} **does nothing for a Customer** — the portal shell has
 * no bell, and a customer's session must not fire a request for a list that would be empty by design.
 *
 * <h3>A failure is silent</h3>
 * The badge is an ambient convenience on every staff screen. A failed refresh leaves the last known
 * count and raises no error state, because a red banner across the shell is a worse outcome than a
 * stale number — the notification **screen** has its own error state, with a retry.
 */
@Injectable({ providedIn: 'root' })
export class NotificationStore {
    private readonly api = inject(NotificationsClient);
    private readonly auth = inject(AuthStore);

    private readonly _unreadCount = signal(0);
    private readonly _recent = signal<NotificationRow[]>([]);
    private readonly _loading = signal(false);

    /** What the bell's badge renders — the caller's total unread, not a page's worth. */
    readonly unreadCount = this._unreadCount.asReadonly();

    /** The newest rows, for the bell panel. The full list is `/workspace/notifications`. */
    readonly recent = this._recent.asReadonly();

    readonly loading = this._loading.asReadonly();

    readonly hasUnread = computed(() => this._unreadCount() > 0);

    /**
     * Re-reads the badge and the panel's rows. Safe to call on every navigation: one small request,
     * and it is skipped entirely for a signed-out or Customer session.
     */
    refresh(): void {
        if (!this.auth.isSignedIn() || !this.auth.isAtLeast('Agent')) {
            return;
        }

        this._loading.set(true);

        // A short page: this feeds the bell panel, not the screen. `unreadCount` is the whole list's
        // total regardless, so a small page does not understate the badge.
        this.api.list(false, { pageSize: 10 }).subscribe({
            next: (page) => {
                this._unreadCount.set(page.unreadCount);
                this._recent.set(page.items);
                this._loading.set(false);
            },
            error: () => this._loading.set(false)
        });
    }

    /**
     * Marks one notification read and refreshes. **No optimistic decrement** (UI-8): the count moves
     * after the server confirms, because the badge is the one number a user would notice being wrong.
     */
    markRead(id: string): void {
        this.api.markRead(id).subscribe({
            next: () => this.refresh(),
            error: () => this.refresh()
        });
    }

    /** Clears the badge on sign-out, so the next user never sees the previous one's count. */
    clear(): void {
        this._unreadCount.set(0);
        this._recent.set([]);
    }
}
