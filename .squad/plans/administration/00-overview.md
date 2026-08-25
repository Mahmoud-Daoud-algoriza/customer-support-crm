# administration — plan overview

Entry point for the **administration** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 16 | [16-story-audit-configuration.md](16-story-audit-configuration.md) | Audit log and system configuration | — | **Part A:** Stories 01, 03 · **Part B:** Stories 02, 06 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/administration/`](../../stories/administration/).

## Dependency notes

**SPLIT IN TIME — Phase 2 and Phase 7.** `docs/story-backlog.md` records the exception.

| Part | Phase | Delivers |
|---|---|---|
| **A — Configuration** | **2**, before Stories 04 and 05 | Option types, **startup validation**, `GET /config`, `GET /config/staff` |
| **B — Audit surface** | **7** | `GET /audit`, the audit screen, the read-only configuration screen |

- **Do not execute Part B early and do not defer Part A.** Story 05 cannot create a ticket without
  Part A's category list, category→department map (A-14) and SLA targets (A-3).
- The `AuditEntry` entity and the single `IAuditRecorder` land in
  [Story 02](../identity-access/02-story-auth-and-roles.md), whose AC requires sign-in recording
  (finding **S9-5**). Part B adds only the read surface.
- **AP-17's three audience tiers must not be merged back into one** — that was blocking defect B-2.
- The rating-scale key holds a **placeholder commented with OQ-1**; no `min`/`max` constant may
  appear anywhere else in the codebase.
- T2. If cut in part, keep the audit write path and drop the filtering UI.
