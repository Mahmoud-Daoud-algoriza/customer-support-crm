# ticket-management — plan overview

Entry point for the **ticket-management** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 05 | [05-story-ticket-core.md](05-story-ticket-core.md) | Ticket creation, listing and assignment | — | Stories 01–04, 16 Part A |
| 06 | [06-story-ticket-lifecycle.md](06-story-ticket-lifecycle.md) | Ticket status lifecycle, escalation and history | — | Story 05 |
| 07 | [07-story-ticket-intake-messaging.md](07-story-ticket-intake-messaging.md) | Web form intake and in-portal messaging | — | Stories 05, 06, 04, 16 Part A |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/ticket-management/`](../../stories/ticket-management/).

## Dependency notes

**Phases 3–4. The core loop the assessment stands or falls on.** Strictly sequential:
05 → 06 → 07.

- **05** introduces `Ticket`, `TicketActivity` and the **one department-scoping helper**
  (`TicketScope`) that every later ticket query composes — the single most important file in the
  codebase. It also computes both SLA due timestamps, which `data-model.md` §2.6 makes **required at
  creation** (finding **S9-5**), and publishes the ticket attachment endpoints (finding **S9-2**).
- **06** adds the guarded `TransitionTo` that 05 deliberately withholds, the A-5 legality machine,
  the A-16 authority matrix, escalation, and the `/activity` read. It also declares
  `INotificationPublisher`, which [Story 09](../sla-automation/09-story-sla-routing-escalation.md)
  implements persistently.
- **07** supplies the **trigger** for the automatic `Pending → Open` rule 06 implements (R-13,
  R-14), and the channel-agnostic message model
  [Story 18](../integration-seams/18-story-channel-erp-adapters.md) must match.
- ⚠ **OQ-2 blocks Story 05's `PATCH` priority branch**, not only Story 09 (finding **S9-3**).
- ⚠ **OQ-3 reaches Story 06's manual escalation**, not only Story 09 (finding **S9-3**).
- 05 and 06 are **T1 and cannot be cut**. 07 is T2 and is **cut last among T2 items**.
