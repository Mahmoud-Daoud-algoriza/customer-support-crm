# Story intake — Departments and branches

> **Source of truth:** `docs/requirements.md` §12 (multi-department, multi-branch) · `docs/product-scope.md` T1-E, T2-K, A-2
> **Scope tier:** T1 for departments, T2 for branches

## Feature

- **Feature name (display):** Organization Structure
- **Feature slug (folder under `plans/`):** `organization`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `departments-branches`)
- **Work item type:** Story

---

## Title

```
Departments and branches
```

---

## Description

```
Establish the two organizational dimensions, with the deliberate asymmetry recorded in
product-scope A-2. The system serves ONE organization; there is no tenant concept.

Department (T1 — requirements §12 "Multi-department"):
- A first-class organizational dimension, e.g. Billing, Technical.
- It is the ROUTING AND PERMISSION BOUNDARY: it drives ticket assignment, SLA ownership,
  and what an agent is allowed to see.
- A ticket belongs to exactly one department. A user belongs to exactly one department.
- Each department has a manager who is the escalation recipient for the SLA story.

Branch (T2 — requirements §12 "Multi-branch"):
- A location attribute on customers and users.
- It is a REPORTING AND FILTERING ATTRIBUTE ONLY. It grants no isolation and is not a
  security boundary.
- A customer belongs to one branch.

Departments and branches are independent attributes, not a hierarchy.
```

---

## Acceptance criteria

```
- [ ] Departments exist as first-class records with a name and a designated manager.
- [ ] Branches exist as first-class records with a name.
- [ ] Every user is assigned exactly one department; every ticket carries exactly one department.
- [ ] Every customer carries exactly one branch.
- [ ] Ticket lists and reports can be filtered by department and by branch.
- [ ] Department scoping is enforced server-side for the Agent role (asserted jointly with
      `identity-access/auth-and-roles`).
- [ ] Branch is demonstrably NOT a permission boundary: an agent can see in-department tickets
      regardless of the customer's branch.
- [ ] Seed data contains at least two departments and two branches so scoping is demonstrable.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `platform-foundation/solution-skeleton`.
- **Depends on code areas or other stories:** Pairs with `identity-access/auth-and-roles`
  (users need a department). Consumed by ticket routing, SLA escalation, and reporting.

## Extra notes (optional)

- Cut order: departments are T1 and cannot be cut; branch may degrade to a plain display and
  filter field if time runs short.
- Departments and branches are seeded/configured, not managed through an admin UI in this
  assessment (product-scope T2-I: no configuration UI).

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`.
- **Do not invent** tables or endpoints here; Stage 6 (Data Model) and Stage 7 (API Design) fix those.

## Out of scope

- Department hierarchies, sub-departments, cross-department teams.
- Branch-scoped permissions or branch-level data isolation (explicitly A-2).
- Multi-tenant provisioning of any kind (product-scope §8, T3-G).
- Per-branch timezones, calendars, or SLA policies (A-3).
