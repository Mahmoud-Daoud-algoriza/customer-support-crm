# Sla

> Source: `docs/data-model.md` §3 · `docs/architecture.md` §1

| Entity | Scope | Mutability | Introduced by |
|---|---|---|---|
| `Notification` | **Recipient-only** | `readAt` only | `sla-automation/sla-routing-escalation` (Story 09) |

**Owning stories:** 09. `Ticket` SLA fields are introduced by Story 05 and live on the `Tickets`
entity; the SLA arithmetic itself is `Domain/Modules/Sla/SlaClock.cs` (Story 05). In-app
notifications only, four types, no delivery or preference tables (A-13).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
