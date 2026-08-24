# Story intake — SLA targets, auto-assignment, escalation and notifications

> **Source of truth:** `docs/requirements.md` §5 · `docs/product-scope.md` T2-D, A-3, A-13
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `sla-routing-escalation`)
- **Work item type:** Story

---

## Title

```
SLA targets, auto-assignment, escalation and notifications
```

---

## Description

```
All four lines of requirements §5, at the simplest defensible depth fixed by product-scope
A-3 and T2-D. The simplifications below are deliberate decisions, not omissions.

Response and resolution targets (§5.1, A-3):
- Two clocks per ticket: FIRST RESPONSE and RESOLUTION.
- Targets are defined PER PRIORITY, in configuration, in hours.
- The clock is 24/7 wall-clock, starting at ticket creation.
- It does NOT pause while waiting on the customer (status Pending does not stop the clock),
  and it ignores business hours, holidays and per-branch timezones.

Automatic assignment (§5.2):
- Round-robin across active agents in the ticket's department.
- No skills, no load balancing, no capacity rules.
- Manual assignment (ticket-core) always overrides.

Escalation rules (§5.3):
- Exactly ONE rule: on target breach → flag the ticket breached → raise priority one level →
  notify the department manager.
- Rules live in code-level configuration. There is no rule editor.

Alerts and notifications (§5.4, A-13):
- IN-APP ONLY: a notification list and an unread badge. No email, SMS or push delivery —
  that is T3-A.
- Notifications are generated for: assignment, SLA breach, escalation, and a customer reply on
  an assigned ticket.
- No per-user notification preferences.

Breach evaluation runs as a simple periodic in-process check at coarse granularity (minutes,
not seconds). Timing precision is explicitly not a goal. No job queue, no message broker.
```

---

## Acceptance criteria

```
- [ ] First-response and resolution targets are read from configuration per priority.
- [ ] Each ticket exposes its target times and its remaining/overdue state, computed on a 24/7
      clock from creation.
- [ ] Moving a ticket to Pending demonstrably does NOT pause the clock (A-3).
- [ ] A new unassigned ticket is auto-assigned round-robin to an active agent in its department;
      successive tickets go to different agents.
- [ ] Auto-assignment never selects an agent outside the ticket's department or a deactivated user.
- [ ] A manual reassignment overrides the automatic one and is recorded in ticket history.
- [ ] On breach, the ticket is flagged breached, its priority rises exactly one level (Urgent
      stays Urgent), and the department manager receives an in-app notification.
- [ ] The breach event is written to ticket history.
- [ ] Notifications appear in an in-app list with an unread badge for the four listed events.
- [ ] Breach detection runs periodically in-process — no queue, broker or external scheduler.
- [ ] Agent dashboard queue ordering (T1-C) consumes this SLA data.
- [ ] SLA attainment data is queryable for `reporting/management-dashboard`.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-core`,
  `ticket-management/ticket-lifecycle`, `organization/departments-branches` (department manager
  is the escalation recipient).
- **Depends on code areas or other stories:** The manual escalation action lives in
  `ticket-lifecycle`; this story adds the automatic trigger and must reuse that same escalation
  path rather than duplicating it. Supplies ordering data to `agent-workspace/agent-dashboard`
  and metrics to `reporting/management-dashboard`.

## Extra notes (optional)

- Cut order: T2. If cut, `agent-workspace/agent-dashboard` falls back to priority-then-age
  ordering and `reporting/management-dashboard` loses its SLA tile — note both in the plan.
- The 24/7 no-pause clock is a known simplification of real support practice. Product-scope §9
  question 5 records the real policy as an open question. Do not "fix" it here.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`.
- SLA computation belongs on the server in one place, consumed by dashboard, portal and reports.
- **Do not invent** tables or endpoints here; Stage 6 (Data Model) and Stage 7 (API Design) fix those first.

## Out of scope

- Business hours, holiday calendars, per-branch timezones, pause-on-customer-reply (A-3).
- Skill-based or load-balanced routing, agent capacity, availability status (T2-D).
- A user-editable rules or workflow engine, per-customer SLA agreements (§8).
- Email/SMS/push notification delivery (T3-A) and per-user notification preferences (A-13).
- Message brokers, job queues, external schedulers (§8, technical exclusions).
