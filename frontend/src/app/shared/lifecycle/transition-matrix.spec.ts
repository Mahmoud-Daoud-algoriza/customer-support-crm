import { TICKET_STATUSES, TicketStatus } from '../../core/api/tickets.client';
import { UserRole } from '../../core/auth/identity.model';
import { isLegalTransition, isTerminal, mayEscalate, mayInvokeTransition, offeredTransitions } from './transition-matrix';

/**
 * **The client copy of A-5 and A-16 — finding F-1's duplication, pinned.**
 *
 * This file is the reason the spec matters. The ticket payload does not expose
 * `allowedTransitions`, so `transition-matrix.ts` reimplements two server rules; a silent drift
 * between the two would show the wrong menu until someone hit a `403` or `409` in production. These
 * expectations are transcribed from **docs/product-scope.md A-5 and A-16 directly**, not from the
 * implementation, so they fail if either copy moves.
 *
 * The server-side twins are `TicketLifecycleTests` (the full 6×6 matrix) and
 * `TransitionAuthorityTests` (the A-16 columns). **When F-1 is closed and this file is deleted, this
 * spec goes with it.**
 */
describe('transition-matrix (F-1)', () => {
    /** A-5, transcribed. */
    const LEGAL_EDGES: ReadonlyArray<[TicketStatus, TicketStatus]> = [
        ['New', 'Open'],
        ['New', 'Cancelled'],
        ['Open', 'Pending'],
        ['Open', 'Resolved'],
        ['Open', 'Cancelled'],
        ['Pending', 'Open'],
        ['Pending', 'Resolved'],
        ['Pending', 'Cancelled'],
        ['Resolved', 'Open'],
        ['Resolved', 'Closed'],
        ['Resolved', 'Cancelled']
    ];

    const STAFF_ROLES: readonly UserRole[] = ['Agent', 'Manager', 'Administrator'];

    it('matches A-5 over the full 6x6 matrix, complement included', () => {
        for (const from of TICKET_STATUSES) {
            for (const to of TICKET_STATUSES) {
                const expected = LEGAL_EDGES.some(([f, t]) => f === from && t === to);

                expect(isLegalTransition(from, to)).toBe(expected);
            }
        }
    });

    it('treats Closed and Cancelled as terminal and nothing else', () => {
        for (const status of TICKET_STATUSES) {
            expect(isTerminal(status)).toBe(status === 'Closed' || status === 'Cancelled');
        }
    });

    it('offers a terminal ticket nothing at all', () => {
        expect(offeredTransitions('Agent', 'Closed')).toEqual([]);
        expect(offeredTransitions('Agent', 'Cancelled')).toEqual([]);
    });

    it('gives the customer exactly A-16 two cells and no others', () => {
        const permitted: string[] = [];

        for (const from of TICKET_STATUSES) {
            for (const to of TICKET_STATUSES) {
                if (mayInvokeTransition('Customer', from, to)) {
                    permitted.push(`${from}->${to}`);
                }
            }
        }

        // Cancel own while New (the A-18 window), and reopen own Resolved. Customers cannot close.
        expect(permitted).toEqual(['New->Cancelled', 'Resolved->Open']);
    });

    it('never offers a customer a target that is not legal as well as permitted', () => {
        for (const from of TICKET_STATUSES) {
            for (const target of offeredTransitions('Customer', from)) {
                expect(isLegalTransition(from, target)).toBeTrue();
                expect(mayInvokeTransition('Customer', from, target)).toBeTrue();
            }
        }
    });

    it('gives all three staff roles identical transition authority', () => {
        for (const from of TICKET_STATUSES) {
            const offered = STAFF_ROLES.map((role) => offeredTransitions(role, from).join(','));

            expect(new Set(offered).size).toBe(1);
        }
    });

    it('offers staff every legal edge from a non-terminal status', () => {
        expect(offeredTransitions('Agent', 'New')).toEqual(['Open', 'Cancelled']);
        expect(offeredTransitions('Agent', 'Resolved')).toEqual(['Open', 'Closed', 'Cancelled']);
    });

    it('offers nothing to a signed-out caller', () => {
        expect(offeredTransitions(undefined, 'New')).toEqual([]);
        expect(mayEscalate(undefined)).toBeFalse();
    });

    it('keeps escalation staff-only (A-16 last row)', () => {
        expect(mayEscalate('Customer')).toBeFalse();

        for (const role of STAFF_ROLES) {
            expect(mayEscalate(role)).toBeTrue();
        }
    });
});
