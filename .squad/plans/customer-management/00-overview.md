# customer-management — plan overview

Entry point for the **customer-management** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 04 | [04-story-customer-records.md](04-story-customer-records.md) | Customer profiles, notes, attachments and interaction history | — | Stories 01–03, **16 Part A** |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/customer-management/`](../../stories/customer-management/).

## Dependency notes

**Phase 2.** Runs after [16 Part A](../administration/16-story-audit-configuration.md) supplies the
default branch (A-15) and the attachment size cap.

- Also delivers **`POST /auth/register`**, deferred from Story 02 (finding **S9-7**).
- The interaction timeline is built **against an empty ticket set** and completed by
  [Story 06](../ticket-management/06-story-ticket-lifecycle.md); the intake authorizes exactly this.
- The shared attachment service and the AP-19 download land here; the two **ticket**-scoped
  attachment endpoints are published by [Story 05](../ticket-management/05-story-ticket-core.md),
  so this story's attachment AC completes there (finding **S9-2**).
- ⚠ **OQ-5 must be answered before task 5** — whether changing `Customer.email` changes a linked
  portal login's sign-in email.
- Attachments (T2-A) are cut before profiles, notes and timeline (T1-A).
