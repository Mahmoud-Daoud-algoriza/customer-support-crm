# agent-workspace — plan overview

Entry point for the **agent-workspace** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 08 | [08-story-agent-dashboard.md](08-story-agent-dashboard.md) | Agent dashboard queue, customer context and quick replies | — | Stories 04–07, 16 Part A |
| 14 | [14-story-tasks-internal-notes.md](14-story-tasks-internal-notes.md) | Tasks, reminders and internal notes | — | Stories 06, 08; verified with 13 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/agent-workspace/`](../../stories/agent-workspace/).

## Implementation progress

Story 08 is being delivered **in slices**, unlike stories 05–07, because two days remain and My
queue is the part the agent demo cannot do without.

| Slice | Plan tasks | Status |
|---|---|---|
| **08-1 — My queue** | **2** (queue ordering + `assigneeId=me` coverage; **no production backend change was needed** — Story 05 already implements both), **3** (`SlaIndicator`), **4** (`/workspace/queue`), **8** (staff landing and navigation) | ✅ **Done and verified 2026-08-31** |
| **08-2 — customer panel** | **6** — the side region / phone drawer, and the draft-survival rule (UI-4) | ⏳ Not started |
| **08-3 — quick replies** | **1** (`quickReplies` in `StaffConfig`), **7** (the composer control) | ⏳ Not started |

Task **5** is partly discharged: the queue reuses Story 05's existing `.app-table-view` /
`.app-card-view` pair rather than introducing a `shared/components/paged-table/`, so no shared table
component exists yet. The filter sheet at phone width, and the ticket-detail half of task 5, remain
with slice 08-2.

## Dependency notes

**Phases 4 and 7.** The two halves of the agent surface, deliberately far apart: 08 is the T1
primary demo surface, 14 is a T2 addition into the region slots 08 leaves.

- **08 does *not* depend on Story 09.** Due dates are required at creation, so SLA-urgency ordering
  is available from Story 05; Story 09 only *populates* the breach flags. No fallback ordering and
  no swap to record (finding **S9-8**).
- ⛔ **S9-1 blocks the dashboard task region in both stories.** `ui-design.md` §5.1, §13, Story 14's
  AC and `data-model.md` §6's index all assume a **cross-ticket** task list; `api-design.md`
  publishes none. 08 leaves the slot marked and empty; 14 cannot close its AC 2 until the decision
  is taken. **Do not invent an endpoint.**
- 08 is **T1 and cannot be cut**. 14 is T2: if cut in part, **keep internal notes and drop tasks** —
  notes carry the visibility rule the permission model demonstrates.
