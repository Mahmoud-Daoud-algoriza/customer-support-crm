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
