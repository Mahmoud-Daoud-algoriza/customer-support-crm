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

## Implementation progress

Story 15 was delivered **whole** — all six plan tasks in one vertical slice, backend and front end
together.

| Story | Plan tasks | Status |
|---|---|---|
| **15 — Management dashboard** | **1–6** — the four aggregate queries, `GET /reports/dashboard`, the seed-data second rating, the twenty-one tests, the typed client and `/workspace/reports`, and the four regions with their empty states | ⚠ **Implemented 2026-09-06; verification PARTIAL** — backend, data layer and live stack verified; **front-end screen checks outstanding** (no browser-driving capability). **No blocked criterion** |

✅ **PF-4 / S9-9 was answered before implementation began (R-18): "tickets assigned" means
*currently* assigned.** The plan's blocked-decision box is therefore discharged, not carried —
`AgentPerformanceQuery` encodes the reading once and no method throws. **The plan's claim that the
blocker was "isolated to one method" was too narrow**: the field is composed into the single §6.8
response, so a throwing method would have made every dashboard call a `500`.

**OQ-1 needed no answer here**, exactly as the plan's box says — the tile renders the configured
scale beside the average and hardcodes no denominator.

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
