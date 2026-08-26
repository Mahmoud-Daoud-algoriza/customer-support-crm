# Tickets

> Source: `docs/data-model.md` §3 · `docs/architecture.md` §1

| Entity | Scope | Mutability | Introduced by |
|---|---|---|---|
| `Ticket` | Department / customer | Mutable | `ticket-management/ticket-core` (Story 05) |
| `TicketActivity` | Follows ticket | **Append-only** | `ticket-management/ticket-lifecycle` (Story 06) |
| `TicketMessage` | Follows ticket | **Immutable** | `ticket-management/ticket-intake-messaging` (Story 07) |
| `TicketInternalNote` | **Staff-only** | **Immutable** | `agent-workspace/tasks-internal-notes` (Story 14) |
| `TicketTask` | Staff-only | Mutable | `agent-workspace/tasks-internal-notes` (Story 14) |
| `CustomerFeedback` | Customer / reports | Write-once | `customer-portal/portal-self-service` (Story 13) |

**Owning stories:** 05, 06, 07, 13, 14. The customer portal's server-side behaviour, including
customer feedback, lives here rather than in a portal module (architecture §1).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
