# Story intake — Ticket status lifecycle, escalation and history

> **Source of truth:** `docs/requirements.md` §2.4–2.5 · `docs/product-scope.md` T1-B, A-5
> **Scope tier:** T1 (must genuinely work)

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `ticket-lifecycle`)
- **Work item type:** Story

---

## Title

```
Ticket status lifecycle, escalation and history
```

---

## Description

```
The second half of requirements §2: the state machine, escalation, and the audit trail of a
ticket. The lifecycle is fixed by product-scope A-5 and must be enforced, not merely displayed.

Status lifecycle (§2.4, A-5) — one fixed status set with enforced transitions:

    New → Open → Pending (waiting on customer) → Resolved → Closed
             ↑                              ↓
             └────────── reopen ────────────┘        (Resolved → Open)

    Any non-terminal status → Cancelled

- New       — created, not yet being worked. MAY already have an assignee: automatic assignment
              runs at creation, and assignment is not the start of work (A-18).
- Open      — an agent has started work. New -> Open IS the act of starting.
- Pending   — awaiting customer input. It does NOT pause the SLA clock (A-3).
- Resolved  — the agent believes it is done; triggers the feedback request (portal story).
- Closed    — terminal, reached from Resolved; no further replies.
- Cancelled — terminal, for tickets abandoned or created in error. Agents, Managers and
              Administrators may cancel any non-terminal ticket; a customer may cancel their
              own ticket ONLY while it is New (A-16, A-18).

An illegal transition must be refused by the server, not merely hidden in the UI.

Escalation (§2.4, A-5):
- Escalation is an ACTION, not a status. It raises priority one level, records an escalation
  entry in ticket history, and notifies the department manager. Status is unchanged.
- There are no escalation tiers and no L1/L2/L3 support levels.

Ticket history (§2.5):
- An append-only activity trail of every status, assignment, priority and category change,
  plus replies and internal notes.
- Every entry records actor, timestamp, and before/after values.
- History is never edited or deleted through the application.
```

---

## Acceptance criteria

```
- [ ] The six statuses exist exactly as listed; no others.
- [ ] Legal transitions succeed and every illegal transition is refused server-side with a clear
      error, verified by a test that bypasses the UI.
- [ ] Resolved can be reopened to Open by either an agent or the ticket's customer.
- [ ] Closed and Cancelled are terminal: no further replies or transitions are accepted.
- [ ] A ticket in New may carry an assignee; status and assignee are independent (A-18).
- [ ] The transition authority matrix of A-16 is enforced server-side: a customer cancelling an
      Open ticket is refused; an agent cancelling a non-terminal ticket succeeds.
- [ ] Escalating a ticket raises its priority exactly one level (Urgent stays Urgent), leaves
      status unchanged, writes a history entry, and notifies the department manager.
- [ ] Ticket history shows every status, assignment, priority and category change with actor,
      timestamp and before/after values.
- [ ] Replies and internal notes appear in the history in chronological order.
- [ ] History is append-only: no UI or API path edits or deletes an entry.
- [ ] The customer's interaction timeline (customer-management story) reflects these entries.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-core`.
- **Depends on code areas or other stories:** Notification delivery is defined by
  `sla-automation/sla-routing-escalation` (in-app only, A-13); if that story is not yet planned,
  raise the manager notification through the same abstraction it will own.
  Feeds `customer-management/customer-records` (timeline) and `reporting/management-dashboard`.

## Extra notes (optional)

- Cut order: T1 — cannot be cut.
- Ticket history (§2.5) is distinct from the system audit log (§10.4, `administration/
  audit-configuration`). Do not merge them: one is a per-ticket activity trail shown to agents,
  the other is a security/administration record. They may share an implementation approach only
  if both remain independently queryable.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`.
- Transition rules belong in one place on the server. Do not scatter them across UI components.
- **Do not invent** tables or endpoints here; Stage 6 (Data Model) and Stage 7 (API Design) fix those first.

## Out of scope

- SLA clocks and automatic breach escalation — `sla-automation/sla-routing-escalation`
  (this story delivers the manual escalation action and the history it writes).
- Escalation tiers, L1/L2/L3 support levels, configurable workflow engines (A-5, §8).
- Pausing the SLA clock on Pending (explicitly excluded by A-3).
