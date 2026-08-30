# sla-automation — plan overview

Entry point for the **sla-automation** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 09 | [09-story-sla-routing-escalation.md](09-story-sla-routing-escalation.md) | SLA targets, auto-assignment, escalation and notifications | — | Stories 03, 05, 06, 16 Part A |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/sla-automation/`](../../stories/sla-automation/).

## Dependency notes

**Phase 5.**

- **Reuses [Story 06](../ticket-management/06-story-ticket-lifecycle.md)'s `EscalateAsync` rather
  than duplicating it**, as the intake requires, and **replaces** the temporary
  `LoggingNotificationPublisher` with the persistent one — the swap Story 06's plan promised to
  record.
- Also replaces Story 05's no-op `IAutoAssignmentPolicy` with round-robin. **Auto-assignment does
  not change status** (A-18).
- ⚠ **OQ-2 must be answered before task 4.** Its answer lives in exactly one method,
  `SlaClock.OnPriorityChanged`, which throws until then.
- ✅ **OQ-3 is closed (2026-08-31) by A-21** — when a department has no usable manager the
  notification climbs to every active `Manager`, else every active `Administrator`, else nobody; the
  flag and the priority raise still occur in every case. Task 4 resolves recipients through
  **`IEscalationRecipientPolicy`**, the same policy Story 06's manual escalate uses; the cascade is
  **not** re-expressed here.
- ⚠ **PF-5 stays open** — a ticket resolved without a reply is permanently first-response breached.
  Implemented as A-3 words it and **reported**, not silently changed.
- T2. If cut, the queue keeps its ordering (S9-8) and
  [Story 15](../reporting/15-story-management-dashboard.md) loses its SLA tile.
