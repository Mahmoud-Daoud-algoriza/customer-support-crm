import { Directive } from '@angular/core';

/**
 * Keeps numerals, timestamps and SLA countdowns reading left-to-right **inside** RTL text
 * (`docs/ui-design.md` §10.2), so `2026-09-06 14:30` does not come apart when the interface is
 * Arabic.
 *
 * ```html
 * <span appLtrEmbed>{{ ticket.createdAt | appDate }}</span>
 * ```
 *
 * <h3>It reuses the one rule, it does not add a second</h3>
 * The declaration lives in `shared/styles/_app.scss` as `.app-ltr-numeric` — `direction: ltr` plus
 * `unicode-bidi: isolate` — and has since Story 01. This directive applies **that same class**, so
 * there is exactly one place the behaviour is defined and the markup that already carries the class
 * literally stays correct. Mirroring rules live in `shared/` and `layout/`, never per feature
 * (`docs/architecture.md` §2.3).
 *
 * `isolate` rather than `embed` is deliberate: it stops the LTR island from re-ordering the RTL text
 * on either side of it, which is exactly the failure a bare `direction: ltr` produces.
 */
@Directive({
    selector: '[appLtrEmbed]',
    standalone: true,
    host: { class: 'app-ltr-numeric' }
})
export class LtrEmbedDirective {}
