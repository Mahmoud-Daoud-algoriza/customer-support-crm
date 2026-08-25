# organization — plan overview

Entry point for the **organization** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 03 | [03-story-departments-branches.md](03-story-departments-branches.md) | Departments and branches | — | Story 01; paired with Story 02 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/organization/`](../../stories/organization/).

## Dependency notes

**Phase 1.** Split across the Story 02 pair: backend tasks 1–3 (entities, EF configuration,
seed data, the shared `InitialSchema` migration) run **before** Story 02; tasks 4–6 (the two
`Agent`-gated endpoints) run **after** it.

- The load-bearing outcome is a **negative** one: `Ticket` gets **no** `branchId`, now or ever.
  A ticket's branch is derived `Ticket → Customer → Branch` (data-model §2.3).
- `Department.managerUserId` is **optional** and **no fallback escalation recipient is invented** —
  **OQ-3** stays open. Seeded departments all have a manager; that is a demo convenience, not an
  answer.
- Departments are **T1 and cannot be cut**; branch may degrade to a display and filter field.
