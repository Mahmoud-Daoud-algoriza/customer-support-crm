# Story intake — Tasks, reminders and internal notes

> **Source of truth:** `docs/requirements.md` §4.3, §4.5 · `docs/product-scope.md` T2-C
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** Agent Workspace
- **Feature slug (folder under `plans/`):** `agent-workspace`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `tasks-internal-notes`)
- **Work item type:** Story

---

## Title

```
Tasks, reminders and internal notes
```

---

## Description

```
Requirements §4.3 and §4.5, deliberately reduced to the minimum defensible implementation
(product-scope T2-C).

Tasks and reminders (§4.3):
- A due-dated to-do attached to a ticket, assigned to an agent, markable done.
- Overdue tasks are visible to the assigned agent on the agent dashboard.
- No calendar, no recurrence, no push reminders.

Team collaboration (§4.5) — internal notes ONLY:
- A note on a ticket, visible to Agents, Managers and Administrators, and NEVER to the customer.
- Product-scope T2-C states this explicitly: internal notes are the whole of collaboration for
  this assessment. No @mentions, no presence, no chat, no shared ownership.
- Internal notes appear in ticket history (§2.5) but must be excluded from every
  customer-visible surface: the portal thread, the customer interaction timeline, and any
  customer notification.
```

---

## Acceptance criteria

```
- [ ] An agent can add a task to a ticket with a due date and an assignee, and mark it done.
- [ ] The assigned agent sees their open and overdue tasks on the agent dashboard.
- [ ] An agent can add an internal note to a ticket; it shows author and timestamp.
- [ ] Internal notes are visible to Agent, Manager and Administrator roles.
- [ ] A Customer cannot see internal notes anywhere: not in the portal thread, not in the
      interaction timeline, not through the API. Verified by a server-side test performed as a
      customer, not by checking the UI.
- [ ] Internal notes appear in ticket history for internal roles.
- [ ] Tasks and notes are recorded with actor and timestamp and are not silently editable by
      other users.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-lifecycle`,
  `agent-workspace/agent-dashboard`.
- **Depends on code areas or other stories:** Customer-visibility exclusion must be verified
  jointly with `customer-portal/portal-self-service` and
  `customer-management/customer-records` (timeline).

## Extra notes (optional)

- Cut order: T2 — cut before any T1 story. If cut in part, keep internal notes and drop tasks:
  notes carry the visibility rule that the permission model demonstrates, tasks do not.
- The internal/external visibility split is the highest-risk detail in this story. Treat it as a
  server-side rule, not a UI filter.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG.
- **Do not invent** tables or endpoints here; Stage 5 and Stage 6 fix those first.

## Out of scope

- @mentions, notifications on mention, presence indicators, agent-to-agent chat, shared or
  co-owned tickets (T2-C, product-scope §8).
- Calendar integration, recurring tasks, push or email reminders (T2-C, T3-A).
- Tasks not attached to a ticket (standalone personal to-dos).
