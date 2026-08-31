import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { TagModule } from 'primeng/tag';
import { EmptyStateComponent } from '../empty-state/empty-state.component';

/**
 * One row of a thread, in the shape **both** path spaces can satisfy.
 *
 * `channel` and `authorRole` are **optional** because the portal payload omits them
 * (docs/api-design.md §6.4) — and the component renders them only when the `showChannel` input says
 * to, so a portal caller that accidentally passed them would still not display them. Two gates, not
 * one.
 */
export interface MessageThreadItem {
    id: string;
    author: { id: string; displayName: string };
    direction: 'Inbound' | 'Outbound';
    body: string;
    postedAt: string;
    channel?: string | null;
    authorRole?: string | null;
}

/**
 * `MessageThread` — **two configurations of one component** (docs/ui-design.md §8).
 *
 * <h3>One component, two audiences</h3>
 * The staff configuration shows `channel` and `authorRole`; the portal configuration does not
 * (§6.4, UI-11). That is a single input, `showChannel` — **not a second component and not a second
 * template**, because the thread's job is identical on both sides and duplicating it is how two
 * renderings drift.
 *
 * <h3>Inbound and outbound render on opposite sides, and mirror under RTL</h3>
 * Sides are set with **logical properties only** (`margin-inline-start`, `border-inline-start`), so
 * the Arabic layout mirrors without a second stylesheet — `npm run lint:styles` fails on a physical
 * property, which is what keeps this true (docs/ui-design.md §10.2).
 *
 * <h3>This is not chat, and nothing here polls</h3>
 * **T3-B.** The component takes a list and renders it. It owns no timer, no interval, no
 * subscription and no "new messages" affordance, and nothing in the UI or in this file describes it
 * as real-time. Refreshing is the parent's business, and the parent does it in response to an
 * action the user took.
 *
 * <h3>It never renders an internal note</h3>
 * Not by filtering — internal notes come from a **different endpoint** that neither caller requests
 * here (T2-C, AP-5, UI-5). Story 14 renders them in their own region, in a different colour block.
 */
@Component({
    selector: 'app-message-thread',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DatePipe, EmptyStateComponent, TagModule, TranslocoModule],
    template: `
        @if (messages().length === 0) {
            <app-empty-state [title]="'tickets.threadEmptyTitle' | transloco" [message]="'tickets.threadEmptyMessage' | transloco" />
        } @else {
            <ol class="app-thread">
                @for (message of messages(); track message.id) {
                    <li
                        class="app-thread__item"
                        [class.app-thread__item--inbound]="message.direction === 'Inbound'"
                        [class.app-thread__item--outbound]="message.direction === 'Outbound'">
                        <p class="app-thread__meta app-ltr-numeric">
                            <span class="app-thread__author">{{ message.author.displayName }}</span>

                            @if (showChannel() && message.authorRole) {
                                <span class="app-thread__role">{{ 'roles.' + message.authorRole | transloco }}</span>
                            }

                            · {{ message.postedAt | date: 'short' }}

                            <!-- The channel is the seam made visible: a staff reader can see which
                                 channel a message arrived on without any channel-specific code
                                 existing anywhere (docs/architecture.md §5.2). -->
                            @if (showChannel() && message.channel) {
                                <p-tag severity="secondary" [value]="'tickets.channel.' + message.channel | transloco" />
                            }
                        </p>

                        <p class="app-thread__body">{{ message.body }}</p>
                    </li>
                }
            </ol>
        }
    `
})
export class MessageThreadComponent {
    readonly messages = input.required<readonly MessageThreadItem[]>();

    /**
     * The staff configuration. **Defaults to `false`**, which is the safe direction: a caller that
     * forgets to set it gets the *narrower* rendering, never the one that shows staff vocabulary to
     * a customer.
     */
    readonly showChannel = input(false);
}
