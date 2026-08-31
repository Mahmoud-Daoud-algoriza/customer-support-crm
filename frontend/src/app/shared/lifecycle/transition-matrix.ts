import { UserRole } from '../../core/auth/identity.model';
import { TicketStatus } from '../../core/api/tickets.client';

/**
 * **A-5 legality and A-16 authority, duplicated on the client — finding F-1, open by design.**
 *
 * <h3>Why this file exists at all</h3>
 * **UI-3** requires the transition menu to offer only transitions that are legal from the current
 * status *and* permitted for the caller's role, computed **client-side**. The ticket payload does
 * **not** expose `allowedTransitions` (docs/api-design.md §6.4) — the contract publishes that set in
 * exactly one place, the `409 illegal-transition` problem detail — so the client has to reimplement
 * the two tables to render the menu at all.
 *
 * <h3>Why that is acceptable, and what makes it safe</h3>
 * **The server remains the authority.** A wrong offer here is refused with `403` or `409`; this file
 * can only make the UI show too much or too little, never let something illegal through. The
 * duplication is confined to **this one file** precisely so that if the API later returns
 * `allowedTransitions`, **exactly one file is deleted** and the menu reads the payload instead.
 *
 * <h3>F-1 is not closed by this story</h3>
 * Adding the field to the API would be new contract surface and a Stage 7 decision to be taken
 * explicitly — **not something a screen decides because it found the duplication inconvenient.**
 * Do not add it here.
 *
 * **Keep these two tables in sync with `TicketLifecycle` (Domain) and `TransitionAuthority`
 * (Application).** They are transcriptions of A-5 and A-16, not independent rules.
 */

/** A-5's graph — the same edges as `TicketLifecycle.Legal` on the server. */
const LEGAL: Readonly<Record<TicketStatus, readonly TicketStatus[]>> = {
    New: ['Open', 'Cancelled'],
    Open: ['Pending', 'Resolved', 'Cancelled'],
    Pending: ['Open', 'Resolved', 'Cancelled'],
    Resolved: ['Open', 'Closed', 'Cancelled'],
    Closed: [],
    Cancelled: []
};

/** `Closed` and `Cancelled` — no outgoing edge, read from the graph rather than listed twice. */
export function isTerminal(status: TicketStatus): boolean {
    return LEGAL[status].length === 0;
}

/** A-5 alone: is the edge in the graph, regardless of who is asking? */
export function isLegalTransition(from: TicketStatus, to: TicketStatus): boolean {
    return LEGAL[from].includes(to);
}

/**
 * A-16 alone: may this role invoke the edge?
 *
 * The customer column is exactly two cells — **cancel own while `New`** (the A-18 window) and
 * **reopen own `Resolved`**. Staff roles share one row: Agent, Manager and Administrator have
 * identical transition authority, and a Manager's cross-department reach is a *scope* rule the
 * server applies, not a menu rule.
 *
 * **Customers cannot close.** A-16 names that as a deliberate consequence.
 */
export function mayInvokeTransition(
    role: UserRole | undefined,
    from: TicketStatus,
    to: TicketStatus
): boolean {
    if (role === 'Customer') {
        return (to === 'Cancelled' && from === 'New') || (to === 'Open' && from === 'Resolved');
    }

    return role !== undefined;
}

/**
 * What the `Transition ▾` menu offers: **legal ∧ permitted** (UI-3).
 *
 * An empty result is the correct answer for a terminal ticket, and the menu renders a reason line
 * rather than an empty dropdown (docs/ui-design.md §5.3).
 */
export function offeredTransitions(
    role: UserRole | undefined,
    from: TicketStatus
): readonly TicketStatus[] {
    return LEGAL[from].filter((to) => mayInvokeTransition(role, from, to));
}

/** A-16's last row: escalation is staff-only. It is an action, not a transition (AP-7). */
export function mayEscalate(role: UserRole | undefined): boolean {
    return role !== undefined && role !== 'Customer';
}
