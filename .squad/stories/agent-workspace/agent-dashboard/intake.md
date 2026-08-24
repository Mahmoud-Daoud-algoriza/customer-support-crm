# Story intake — Agent dashboard queue, customer context and quick replies

> **Source of truth:** `docs/requirements.md` §4.1, §4.2, §4.4 · `docs/product-scope.md` T1-C
> **Scope tier:** T1 (must genuinely work)

## Feature

- **Feature name (display):** Agent Workspace
- **Feature slug (folder under `plans/`):** `agent-workspace`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `agent-dashboard`)
- **Work item type:** Story

---

## Title

```
Agent dashboard queue, customer context and quick replies
```

---

## Description

```
The agent's daily surface — requirements §4.1, §4.2 and §4.4, at T1 depth.

Assigned tickets (§4.1):
- The logged-in agent's own queue, ordered by SLA urgency (soonest target first, breached
  tickets surfaced at the top).
- Ordering consumes the SLA data owned by `sla-automation/sla-routing-escalation`.

Customer information in context (§4.2):
- The customer panel is reachable from inside a ticket WITHOUT losing place — the agent does not
  navigate away and lose their draft or scroll position.
- The panel shows the customer's contact details and recent interaction history (§1.3).

Quick replies (§4.4):
- A small library of canned responses an agent can insert into a reply.
- Canned responses come from configuration (T2-I: no configuration UI in this assessment).
- Insertion places editable text into the reply draft; it never sends on its own.
```

---

## Acceptance criteria

```
- [ ] Signing in as an agent lands on a dashboard showing that agent's assigned tickets only.
- [ ] The queue is ordered by SLA urgency, with breached tickets visually distinct and first.
- [ ] Opening a ticket from the queue shows the thread, and the customer panel is reachable
      from within the ticket without navigating away or losing an in-progress reply draft.
- [ ] The customer panel shows contact details and that customer's recent interaction history.
- [ ] A quick-reply library is available in the reply composer; selecting one inserts editable
      text into the draft.
- [ ] Inserting a quick reply never sends the message by itself.
- [ ] An agent sees no ticket from another department anywhere on this surface.
- [ ] The dashboard renders a sensible empty state when the agent has no assigned tickets.
- [ ] The dashboard and ticket view are usable at phone width (T3-F, responsive web).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-core`,
  `ticket-management/ticket-lifecycle`, `ticket-management/ticket-intake-messaging`,
  `customer-management/customer-records`.
- **Depends on code areas or other stories:** SLA urgency ordering needs
  `sla-automation/sla-routing-escalation`. If that story is not yet complete, order by priority
  then age and swap in the SLA ordering when it lands — record the swap in the plan.
  AI assists (`ai-assist/ai-ticket-assists`) surface inside this ticket view.

## Extra notes (optional)

- Cut order: T1 — cannot be cut. This is the primary demo surface for the agent actor.
- Tasks, reminders and internal notes are the sibling T2 story `tasks-internal-notes`; the
  ticket view should leave a place for them rather than being restructured later.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG — use PrimeNG components
  rather than hand-rolled equivalents.
- **Do not invent** endpoints here; Stage 6 (API Design) and Stage 7 (UI Design) fix the queue
  and ticket-view contracts first.

## Out of scope

- Tasks, reminders, internal notes — sibling story `tasks-internal-notes` (T2-C).
- @mentions, presence, agent-to-agent chat, shared ticket ownership (T2-C, §8).
- Bulk actions on the queue, saved views, personal dashboards.
- Telephony, screen sharing, co-browsing (§8).
