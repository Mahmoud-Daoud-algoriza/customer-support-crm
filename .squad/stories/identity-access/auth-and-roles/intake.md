# Story intake — Authentication, roles and permission enforcement

> **Source of truth:** `docs/requirements.md` §10.1–10.3 · `docs/product-scope.md` T1-D, A-4, A-9
> **Scope tier:** T1 (must genuinely work)

## Feature

- **Feature name (display):** Identity & Access
- **Feature slug (folder under `plans/`):** `identity-access`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `auth-and-roles`)
- **Work item type:** Story

---

## Title

```
Authentication, roles and permission enforcement
```

---

## Description

```
Deliver the four-actor access model the whole system depends on.

Users and roles (requirements §10.1–10.2):
- Administrators create and deactivate users and assign each user exactly one role and one
  department.
- Exactly four fixed, hierarchical roles (product-scope A-4), not a configurable RBAC engine:
    Customer      — submit tickets; view and reply to OWN tickets; read public KB; submit feedback
    Agent         — full work on tickets in OWN department; read/write customers; internal notes;
                    use AI assists
    Manager       — everything an Agent can, across ALL departments; reassign; view reports
    Administrator — everything a Manager can, plus user management, KB authoring, audit log access

Permissions (requirements §10.3):
- Enforcement is server-side, not merely hidden UI. Hiding a control in the front end is not
  an implementation of a permission.
- An Agent must not be able to read or act on another department's tickets.
- A Customer must not be able to read anything belonging to another customer.

Authentication (product-scope A-9):
- Email + password with session or token authentication, sufficient to distinguish the four roles.
- Customers self-register or are created by an agent.
- A customer is identified by an email address unique within the organization (A-10).
```

---

## Acceptance criteria

```
- [ ] A user can sign in and sign out; an unauthenticated request to a protected resource is refused.
- [ ] Each user has exactly one role and one department.
- [ ] An Administrator can create a user, assign role and department, and deactivate a user.
      A deactivated user can no longer sign in.
- [ ] Role capabilities match the A-4 table exactly, enforced on the server.
- [ ] An Agent attempting to read or modify a ticket in another department is refused by the
      server, verified by a test that bypasses the UI.
- [ ] A Customer attempting to read another customer's ticket is refused by the server.
- [ ] A Manager can act across all departments; an Administrator additionally reaches user
      management, KB authoring and the audit log.
- [ ] Email addresses are unique across users; duplicate registration is rejected.
- [ ] Sign-in, sign-out and user-administration actions are recorded for the audit log story.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `platform-foundation/solution-skeleton`.
- **Depends on code areas or other stories:** `organization/departments-branches` supplies the
  department a user is assigned to; these two are planned together or in immediate sequence.
  Every subsequent story depends on the actor model defined here.

## Extra notes (optional)

- Cut order: T1 — cannot be cut.
- The four roles are hardcoded and hierarchical. Do not build a role editor.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`.
- **Do not invent** endpoints or tables in this intake — Stage 6 (Data Model) and Stage 7
  (API Design) fix those first.

## Out of scope

- SSO, OAuth, MFA, password-policy engine, account recovery, anonymous ticket submission
  (product-scope A-9 and §8).
- Per-field permissions, custom role builders, delegation, branch-scoped restrictions (A-4).
- Multi-tenant isolation — the system is single-organization (A-2, T3-G).
