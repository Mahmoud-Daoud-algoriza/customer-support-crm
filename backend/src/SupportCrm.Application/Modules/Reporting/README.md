# Reporting

> Source: `docs/data-model.md` §3, §7 · `docs/architecture.md` §1

**No entity.** Reports are aggregates read over `Ticket`, `TicketActivity`, `CustomerFeedback`
and `User`. No pre-aggregated tables, no warehouse.

**Owning stories:** `reporting/management-dashboard` (Story 15); `agent-workspace/agent-dashboard`
(Story 08) reads the same projections for a single agent.

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
