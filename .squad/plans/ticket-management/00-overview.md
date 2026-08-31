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

## Implementation progress

Story 05 was delivered **whole, not in slices** — the plan's thirteen numbered tasks were the unit
of work, so this feature has no slice map of the kind
[customer-management](../customer-management/00-overview.md) needed.

| Story | Plan tasks | Status |
|---|---|---|
| **05 — ticket core** | **1–13** — the entities, `SlaClock`, the migration and seeder, `TicketScope`, `TicketService`, the activity recorder, ticket attachments, the controller, the tests, and the list / detail / assign screens | ✅ **Done and verified 2026-08-31** |
| **06 — ticket lifecycle** | **1–11** — the A-5 state machine and the guarded `TransitionTo`, the A-16 authority matrix, escalation as an action, the notification seam, the activity read, the completed customer timeline, the three endpoints, the tests, and the transition / escalate / activity front end | ✅ **Done and verified 2026-08-31** |
| **07 — ticket intake and messaging** | **1–10** — the `TicketMessage` model with its channel and direction enums, the EF configuration and the `TicketMessages` migration, the one ingestion service and the portal submission, the staff and portal endpoints, the seeded thread and `Pending` ticket, the tests, the typed clients, the shared thread and composer, the staff thread region, and the portal route stubs | ✅ **Done and verified 2026-08-31** |

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
- ✅ **OQ-2 is closed (2026-08-30) by A-20** — the SLA due timestamps **freeze**; a priority change
  does not move them. It reached **Story 05's `PATCH` priority branch**, not only Story 09 (finding
  **S9-3**), which is why the answer was needed here. `SlaClock.OnPriorityChanged` is a deliberate
  no-op, implemented by Story 05 task 1.
- ✅ **OQ-3 is closed (2026-08-31) by A-21** — escalation notifies the department's manager, else
  every active `Manager`, else every active `Administrator`, else nobody, and **is never blocked by
  the absence of one**. It reached **Story 06's manual escalation**, not only Story 09 (finding
  **S9-3**), which is why the answer was needed here. Story 06 resolves recipients through
  **`IEscalationRecipientPolicy`**, which already exists; the cascade is not re-expressed in the
  story.
- 05 and 06 are **T1 and cannot be cut**. 07 is T2 and is **cut last among T2 items**.
