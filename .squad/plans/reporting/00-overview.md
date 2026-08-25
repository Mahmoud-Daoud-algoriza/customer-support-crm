# reporting — plan overview

Entry point for the **reporting** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 15 | [15-story-management-dashboard.md](15-story-management-dashboard.md) | Management dashboard and operational reports | — | Stories 03, 05, 06, **09**, **13** |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/reporting/`](../../stories/reporting/).

## Dependency notes

**Phase 7.** Late by design — every metric depends on data other stories produce.

- ⛔ **PF-4 / S9-9 blocks `agentPerformance.assignedCount`.** `api-design.md` §9 required the
  semantics to be pinned **before this story was planned**; they were not. The decision is isolated
  to `AgentPerformanceQuery.AssignedCount`, which throws until it is recorded. The label stays
  exactly as T2-G words it, **with no clarifying tooltip**.
- ⚠ **OQ-1** affects what the satisfaction average *means*, not the query. The tile renders the
  configured scale beside the number and hardcodes no denominator.
- **Reporting aggregates are deliberately not narrowed by `TicketScope`** — AD-5 cites silent
  narrowing of aggregates as a reason global query filters were rejected.
- **"Empty is not zero"** is a tested rule, not a nicety.
- T2.
