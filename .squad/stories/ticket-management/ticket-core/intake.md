# Story intake — Ticket creation, listing and assignment

> **Source of truth:** `docs/requirements.md` §2.1–2.3 · `docs/product-scope.md` T1-B, A-6
> **Scope tier:** T1 (must genuinely work — the core loop)

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `ticket-core`)
- **Work item type:** Story

---

## Title

```
Ticket creation, listing and assignment
```

---

## Description

```
The first half of requirements §2 — creating, finding and owning tickets. The lifecycle,
escalation and history half is the sibling story `ticket-lifecycle`.

Creation (§2.1):
- An agent creates a ticket on behalf of a customer.
- A customer creates a ticket through the portal (the portal UI itself is `customer-portal`).
- A ticket is linked to exactly one customer and exactly one department (A-2).

Listing and tracking (§2.1):
- List tickets with filtering by status, priority, category, assignee and department.
- Listing is department-scoped for Agents, unrestricted for Managers and Administrators,
  and restricted to own tickets for Customers.

Categories and priorities (§2.2, A-6):
- Both are fixed configuration enumerations, not user-managed taxonomies.
- Priority is a four-level scale: Low, Medium, High, Urgent.
- Categories are a flat list — no sub-categories.
- Priority is set by an agent or by the AI suggestion. A customer may indicate urgency at
  submission but does not set priority directly.

Assignment (§2.3):
- An agent can be assigned to a ticket, and a ticket can be reassigned.
- Assignment does not change status: an assigned ticket stays New until an agent starts work
  (A-18).
- Managers may reassign across departments; agents may not.
```

---

## Acceptance criteria

```
- [ ] An agent can create a ticket for an existing customer, setting subject, description,
      category, priority and department.
- [ ] A ticket always references exactly one customer and one department.
- [ ] The ticket list supports filtering by status, priority, category, assignee and department,
      and the filters combine.
- [ ] An Agent's list shows only their own department's tickets; a Manager's shows all;
      a Customer's shows only their own.
- [ ] Category and priority values come from configuration, not from a database-managed
      taxonomy, and an unknown value is rejected.
- [ ] Priority offers exactly Low, Medium, High, Urgent.
- [ ] An agent can assign and reassign a ticket within their department; an attempt to assign a
      ticket to an agent from another department is refused server-side.
- [ ] Every assignment change is recorded for the ticket history story.
- [ ] Seed data provides enough tickets across departments and priorities to demonstrate filtering.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `platform-foundation/solution-skeleton`,
  `identity-access/auth-and-roles`, `organization/departments-branches`,
  `customer-management/customer-records`.
- **Depends on code areas or other stories:** Immediately precedes
  `ticket-management/ticket-lifecycle`. Consumed by the agent dashboard, SLA automation,
  AI assists, the portal and reporting.

## Extra notes (optional)

- Cut order: T1 — cannot be cut. If anything in the assessment must work, it is this.
- Automatic assignment (round-robin) belongs to `sla-automation/sla-routing-escalation`;
  this story delivers manual assignment only.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG.
- **Do not invent** tables or endpoints here; Stage 6 (Data Model) and Stage 7 (API Design)
  fix the ticket shape before any plan is generated.

## Out of scope

- Status transitions, escalation, ticket history — sibling story `ticket-lifecycle`.
- Automatic assignment and SLA clocks — `sla-automation/sla-routing-escalation`.
- Ticket merging, splitting, linking, parent/child and recurring tickets (product-scope §8).
- User-editable categories or priorities (A-6).
