# Story intake — Audit log and system configuration

> **Source of truth:** `docs/requirements.md` §10.4, §10.5 · `docs/product-scope.md` T2-H, T2-I
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** Administration
- **Feature slug (folder under `plans/`):** `administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `audit-configuration`)
- **Work item type:** Story

---

## Title

```
Audit log and system configuration
```

---

## Description

```
The remaining two lines of requirements §10 (users, roles and permissions are delivered by
`identity-access/auth-and-roles`).

Audit log (§10.4, T2-H):
- Append-only audit entries written by a SINGLE service, so there is one write path and one
  place to reason about coverage.
- Recorded actions: sign-in, user administration (create, deactivate, role or department
  change), permission-relevant changes, and ticket lifecycle actions.
- Each entry records actor, action, target, timestamp and outcome.
- Read-only list view for Administrators, with basic filtering.
- Distinct from ticket history (§2.5): ticket history is a per-ticket activity trail shown to
  agents; the audit log is a security and administration record shown to Administrators.

System configuration (§10.5, T2-I):
- File- and environment-based configuration for categories, priorities, SLA targets, roles and
  branding values.
- NO configuration UI. Changing configuration is a redeploy. This is a deliberate simplification.
- A read-only view of effective configuration is acceptable and useful for the demo, provided it
  cannot write.
```

---

## Acceptance criteria

```
- [ ] Audit entries are written for sign-in, user administration, permission-relevant changes
      and ticket lifecycle actions.
- [ ] Each entry records actor, action, target, timestamp and outcome.
- [ ] Entries are append-only: no application path updates or deletes one.
- [ ] Only an Administrator can read the audit log; other roles are refused server-side.
- [ ] The audit list supports basic filtering (by actor, action type and date range).
- [ ] The audit log and ticket history remain independently queryable and neither is derived
      from the other in a way that loses detail.
- [ ] Categories, priorities, SLA targets, roles and branding values are all read from
      file/environment configuration at startup.
- [ ] No screen writes configuration; any configuration view is read-only.
- [ ] Invalid configuration fails fast at startup with a clear message rather than degrading
      silently at runtime.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `identity-access/auth-and-roles` (the actions being audited and
  the Administrator role that reads the log), `ticket-management/ticket-lifecycle`
  (lifecycle actions).
- **Depends on code areas or other stories:** The configuration half is consumed by
  `ticket-management/ticket-core` (categories, priorities),
  `sla-automation/sla-routing-escalation` (targets), `agent-workspace/agent-dashboard`
  (quick replies) and `platform-experience/i18n-responsive-branding` (branding values).
  Those stories read configuration; this story owns how configuration is defined and validated.

## Extra notes (optional)

- Cut order: T2. If cut in part, keep the audit write path (it is cheap and it is a §10
  requirement) and drop the filtering UI.
- Because configuration is consumed early by other stories, the configuration half should be
  planned early even if the audit-log UI is planned late.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`.
- One audit-write service, called from the places that need it. Do not scatter audit writes
  across controllers.
- **Do not invent** tables or endpoints here; Stage 5 and Stage 6 fix those first.

## Out of scope

- Tamper-proofing, cryptographic signing, immutable external audit storage (T2-H).
- Retention policies, archival, audit export (T2-H, §8).
- A configuration UI, runtime configuration reload, feature-flag management (T2-I).
- Compliance programs that would consume the audit log — GDPR/PDPL tooling, right to erasure,
  consent management (§8).
- User and role management screens — `identity-access/auth-and-roles` owns those (§10.1–10.2).
