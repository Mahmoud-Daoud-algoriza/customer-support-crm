# customer-portal — plan overview

Entry point for the **customer-portal** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 13 | [13-story-portal-self-service.md](13-story-portal-self-service.md) | Customer portal self-service and feedback | — | Stories 02, 04–07, 12 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/customer-portal/`](../../stories/customer-portal/).

## Dependency notes

**Phase 6.** Builds the four portal screens around the endpoints
[Story 07](../ticket-management/07-story-ticket-intake-messaging.md) delivered, and completes the
eleven-endpoint portal path space.

- `CustomerFeedback` lives in the **`Tickets`** backend module (**DM-7**); `customer-portal` is a
  front-end area and **there is no eleventh backend module**.
- ⚠ **OQ-1 blocks the feedback control's shape.** The server side validates against
  `feedback.ratingScale` from configuration; the UI renders **from that range** and **must not
  hardcode a star widget** (ui-design §11).
- The portal shows **no assignee, department, priority or SLA field** (AP-16, UI-11) — asserted on
  raw JSON, not on a DTO type.
- T2, but **cut late**: it is the second actor surface and carries much of the permission
  demonstration.
