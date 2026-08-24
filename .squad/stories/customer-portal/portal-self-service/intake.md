# Story intake — Customer portal self-service and feedback

> **Source of truth:** `docs/requirements.md` §8 · `docs/product-scope.md` T2-F
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `portal-self-service`)
- **Work item type:** Story

---

## Title

```
Customer portal self-service and feedback
```

---

## Description

```
All five lines of requirements §8, as a separate, simpler surface for the Customer role over
the SAME backend (product-scope T2-F). It is not a second application with its own data.

Submit tickets (§8.1):
- The customer submits through the web form owned by `ticket-management/
  ticket-intake-messaging`; this story provides the portal surface around it.

Track requests (§8.2):
- A customer sees the status of their own open requests, with the status vocabulary of A-5.

View history (§8.3):
- A customer sees their own past tickets and the message thread on each.

Access FAQs (§8.4):
- A customer browses and searches PUBLIC knowledge-base articles only.

Submit feedback (§8.5):
- A one-question satisfaction rating with an optional comment, offered when a ticket reaches
  Resolved. This is the SOLE CSAT input in the system and feeds the satisfaction metric in
  `reporting/management-dashboard` (§9.4).
```

---

## Acceptance criteria

```
- [ ] A customer signs in and reaches a portal surface distinct from the agent workspace.
- [ ] A customer can submit a ticket and immediately see it in their list.
- [ ] A customer sees ONLY their own tickets, enforced server-side and verified by a test that
      bypasses the UI.
- [ ] Ticket status is shown using the A-5 vocabulary and updates as agents work the ticket.
- [ ] A customer can read the message thread and reply, and cannot see internal notes.
- [ ] A customer can browse and search public KB articles; internal articles never appear.
- [ ] When a ticket becomes Resolved, the customer is offered a one-question rating with an
      optional comment; submitting it records the rating against that ticket.
- [ ] A rating can be submitted once per ticket, and declining is a normal outcome, not an error.
- [ ] A customer can reopen a Resolved ticket (A-5), and reopening is reflected in ticket history.
- [ ] The portal is usable at phone width (T3-F, responsive web).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `identity-access/auth-and-roles`,
  `ticket-management/ticket-core`, `ticket-management/ticket-lifecycle`,
  `ticket-management/ticket-intake-messaging`, `knowledge-base/kb-articles-search`
  (public articles for §8.4).
- **Depends on code areas or other stories:** Feedback data feeds
  `reporting/management-dashboard` (§9.4). Internal-note exclusion is verified jointly with
  `agent-workspace/tasks-internal-notes`.

## Extra notes (optional)

- Cut order: T2. The portal is the second actor surface and carries much of the permission
  demonstration — cut it late among T2 items.
- The feedback question is deliberately singular. Product-scope §8 excludes any fuller CSAT/NPS
  survey system.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. One Angular application may host both surfaces; the
  separation is by route and role, not by a second deployable.
- **Do not invent** endpoints here; Stage 7 (API Design) and Stage 8 (UI Design) fix the portal
  contracts and screens first.

## Out of scope

- Anonymous ticket submission (A-9).
- A full CSAT/NPS survey system, follow-up surveys, survey scheduling (§8, product-scope §8).
- Live chat or chatbot on the portal (T3-B, T3-C).
- Customer self-service editing of their own profile beyond what `customer-management/
  customer-records` provides.
- Native mobile app or PWA/offline behaviour (T3-F, §8).
