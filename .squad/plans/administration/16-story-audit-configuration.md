# Story 16 — Audit log and system configuration

> **Source of truth:** `docs/requirements.md` §10.4, §10.5 · `docs/product-scope.md` **T2-H**, **T2-I**, A-3, A-6, A-14, A-15, A-4 · `docs/architecture.md` **§2.4**, **§6.3**, AD-10 · `docs/data-model.md` §2.14, §2.16, §5 constraint 15 · `docs/api-design.md` §5.1, §5.12, §6.9, **AP-17** · `docs/ui-design.md` §6
> **Intake:** `.squad/stories/administration/audit-configuration/intake.md` · **Tier:** T2
> **Phases: this story is SPLIT IN TIME.**

## ⚠ This story executes in two parts, at two different points in the sequence

`docs/story-backlog.md` records the split as an explicit exception: *"the configuration half is
consumed by stories 05, 09, 08 and 17. It is defined early; only the audit-log UI belongs at
position 16."* The intake says the same: *"Because configuration is consumed early by other
stories, the configuration half should be planned early even if the audit-log UI is planned late."*

| | **Part A — Configuration** | **Part B — Audit surface and configuration view** |
|---|---|---|
| **Executes in** | **Phase 2**, immediately after Story 03 and **before Story 04 and Story 05** | **Phase 7**, at its backlog position |
| **Delivers** | Tasks 1–5 below: the option types, startup validation, `GET /config`, `GET /config/staff` | Tasks 6–9 below: `GET /audit`, the audit screen, the read-only configuration screen |
| **Consumed by** | Stories 04 (default branch, attachment cap), 05 (categories, priorities, category→department map, SLA targets), 08 (quick replies), 09 (SLA targets), 13 (rating scale), 17 (branding) | Nothing. It is the last administrative surface |

**Do not execute Part B early and do not defer Part A.** Story 05 cannot create a ticket without
Part A, and Part B depends on audit rows that Stories 02–06 have been writing all along.

## Prerequisites

**Part A:** Story 01 (options binding, `ConfigController`), Story 03 (departments and branches
exist, so the category map and the default branch can reference real ids).

**Part B:** Story 02 (`AuditEntry`, `IAuditRecorder`, the Administrator role) and Story 06
(lifecycle actions, which are among the audited actions).

---

## Story Goal

The remaining two lines of requirements §10 — users, roles and permissions are Story 02's.

1. **System configuration (§10.5, T2-I)** — file- and environment-based configuration for
   categories, priorities, SLA targets and branding values, **validated at startup**, with
   **no configuration UI**. Changing configuration is a **redeploy**.
2. **Audit log (§10.4, T2-H)** — append-only entries written by a **single service**, read-only for
   Administrators, with basic filtering, and **distinct from ticket history** (AD-10).

---

## Context — Read These Files First

1. `docs/architecture.md` **§6.3 in full** — the eleven-row configuration table, layering
   (`appsettings.json` -> environment), **strongly typed options validated at startup**,
   **"No configuration UI. Changing configuration is a redeploy… A read-only view of effective
   configuration is permitted; a writable one is not."**, and the secrets discipline.
2. `docs/api-design.md` §5.1 — **AP-17, the three audience tiers**, the table of which value sits in
   which tier and **why**, and *"A Customer calling `/config/staff` gets **`403`** — a capability
   denial they can infer from their own role, so `403` is correct here and AP-4's `404` rule does
   not apply."* Then §6.9 for `BootstrapConfig`, `CustomerConfig` and `StaffConfig`.
3. `docs/architecture.md` **§2.4** — the audit boundary: **one writer**, called from Application
   services, **append-only by construction** (*"not merely no UI, but no service method"*),
   **Administrator-only read**, and what it records.
4. `docs/api-design.md` §5.12 — `GET /audit`, Administrator-only, filters, and *"No write, update or
   delete endpoint exists."*
5. `docs/data-model.md` §2.14 (including **why it is not merged with `TicketActivity`**) and
   **§2.16** — `Category`, `Priority`, `SlaPolicy`, `QuickReply`, `Branding` and `Setting` are
   **explicitly not entities**. They are configuration.
6. `.squad/stories/administration/audit-configuration/intake.md` — nine acceptance criteria.

---

## Product rules (from story)

- **Categories, priorities, SLA targets, quick replies, roles and branding are configuration, not
  tables** (data-model §2.16). **Do not create an entity for any of them.**
- **No screen writes configuration.** A read-only view is permitted; a writable one is not.
- **Invalid configuration fails fast at startup with a clear message** rather than degrading
  silently at runtime.
- **Every configured category must map to a department; an unmapped category is a configuration
  error and fails at startup validation** (A-14).
- **Configuration is split into three audience tiers** (AP-17). A customer has no requirement that
  needs priorities, quick replies, SLA targets or the routing map.
- **Audit entries are append-only.** No application path updates or deletes one.
- **Only an Administrator can read the audit log**; other roles are **refused server-side**.
- **The audit log and ticket history remain independently queryable and neither is derived from the
  other** (AD-10).

---

# Part A — Configuration *(executes in Phase 2)*

## Backend Tasks

### 1 — The option types

Create each in `src/SupportCrm.Application/Configuration/`, following the Story 01 pattern
(`SectionName` constant, `[Required]` annotations, bound and `ValidateOnStart`).

| File | Section | Contents | Source |
|---|---|---|---|
| `CategoryOptions.cs` | `SupportCrm:Categories` | A **flat list** of `{ Code, Name, DepartmentId }` — the category list **and** the category→department map in one place, because A-14 makes them inseparable | A-6, **A-14** |
| `PriorityOptions.cs` | `SupportCrm:Priorities` | The four levels. Validated to equal the `TicketPriority` enum **exactly** — configuration may not add a fifth | A-6 |
| `SlaTargetOptions.cs` | `SupportCrm:Sla:Targets` | Per priority: `FirstResponseHours`, `ResolutionHours` | **A-3** |
| `QuickReplyOptions.cs` | `SupportCrm:QuickReplies` | `[{ Id, Title, Body }]` | T1-C |
| `RegistrationOptions.cs` | `SupportCrm:Registration` | `DefaultBranchId` | **A-15** |
| `AttachmentOptions.cs` | `SupportCrm:Attachments` | `MaxSizeBytes` | T2-A, api-design `attachment-too-large` |
| `FeedbackOptions.cs` | `SupportCrm:Feedback:RatingScale` | `Min`, `Max` | architecture §6.3 — **values deliberately undecided, OQ-1** |

`BrandingOptions` and `LocalizationOptions` already exist (Story 01); `AiOptions` is Story 10's.
**No new configuration key beyond architecture §6.3's table and the attachment cap is introduced.**

> **`FeedbackOptions` — OQ-1.** The **key** is approved (architecture §6.3, closing PF-3/B-1); its
> **values are not decided**. Ship `appsettings.json` with a value **and a comment naming OQ-1** so
> a reader sees it is a placeholder, not a decision. **No `Min`/`Max` constant may appear anywhere
> else in the codebase** — the schema, the domain, the service and the UI all read it from here
> (Story 13).

### 2 — Startup validation

**Create file: `src/SupportCrm.Application/Configuration/ConfigurationValidator.cs`** — an
`IValidateOptions<T>` set plus a startup check that runs **before the first request**:

1. **Every configured category maps to a department that exists.** An unmapped or dangling
   `DepartmentId` is a **startup failure** with the offending category code in the message (A-14).
2. **Every priority has an SLA target**, and both hour values are `> 0` (A-3).
3. **`Registration:DefaultBranchId` references an existing branch** (A-15).
4. **`Priorities` equals the `TicketPriority` enum**, in order.
5. **`Feedback:RatingScale.Min < Max`** — a structural check only. **It asserts nothing about which
   values are correct** (OQ-1).
6. **`Attachments:MaxSizeBytes > 0`.**

Checks 1 and 3 read the database, so they run in the `DatabaseInitializer` **after** migrations and
seeding, not during option binding. Failure **stops the host** with a single clear message —
*"invalid configuration fails fast rather than degrading silently at runtime"* (intake AC).

### 3 — `GET /config` — the customer-safe tier

**File: `src/SupportCrm.Api/Controllers/ConfigController.cs`** — add, `[Authorize]` (**all
authenticated roles**):

```json
{ "categories": [ { "code": "billing", "name": "Billing" } ],
  "feedback": { "ratingScale": { "min": 1, "max": 5 } } }
```

**The category list here carries `code` and `name` only — never `departmentId`.** A customer picks
a category (A-14) and never learns the routing map. The `ratingScale` values shown are whatever
configuration holds; **the contract fixes no scale** (OQ-1).

### 4 — `GET /config/staff` — the staff-only tier

Same controller, policy **`RequireAgent`**:

```json
{ "priorities": ["Low", "Medium", "High", "Urgent"],
  "quickReplies": [ { "id": "...", "title": "...", "body": "..." } ],
  "slaTargets": [ { "priority": "High", "firstResponseHours": 4, "resolutionHours": 24 } ],
  "categoryDepartmentMap": [ { "categoryCode": "billing", "departmentId": "..." } ] }
```

**A Customer calling this gets `403`** — and that is correct here, not `404`, because it is a
capability denial the caller can infer from their own role (api-design §5.1).

> **B-2 is why this split exists.** The first version of the contract returned quick replies and SLA
> targets to **every** authenticated caller, including Customers. **Do not merge these two endpoints
> back into one.**

### 5 — Part A tests

**Create file: `tests/SupportCrm.Tests/Administration/ConfigurationTierTests.cs`**

1. `GET /config` as `Customer` -> `200`, and the payload contains **`categories`** and
   **`feedback.ratingScale`** and **nothing else** — assert on the raw JSON keys.
2. **No category object in `GET /config` contains a `departmentId` key.**
3. `GET /config/staff` as `Customer` -> **`403`**; as `Agent` -> `200` with all four groups.
4. `GET /config/bootstrap` is still **anonymous** (Story 01 regression).

**Create file: `tests/SupportCrm.Tests/Administration/ConfigurationValidationTests.cs`** — each
starts a host with deliberately broken configuration and asserts a **startup failure naming the
problem**:

5. A category mapped to a non-existent department.
6. A priority with no SLA target.
7. A `DefaultBranchId` that does not exist.
8. A five-value `Priorities` list.

**Create file: `tests/SupportCrm.Tests/Administration/NoConfigurationEntityTests.cs`**

9. **No `DbSet` exists for a category, priority, SLA policy, quick reply, branding or setting** —
   assert by reflecting over `SupportCrmDbContext` (data-model §2.16). This is the test that keeps
   T2-I true as later stories add tables.

**Part A stops here. Story 04 and Story 05 execute next.**

---

# Part B — Audit surface and configuration view *(executes in Phase 7)*

## Backend Tasks

### 6 — Confirm the write path is complete

The entity, the recorder and the write call sites landed in **Story 02**. Before building the read
surface, **audit the coverage** against the intake's list and add any missing call site:

| Action group | Written by | Actions |
|---|---|---|
| Sign-in | Story 02 | `SignInSucceeded`, `SignInFailed` |
| User administration | Story 02 | `UserCreated`, `UserDeactivated`, `UserRoleChanged`, `UserDepartmentChanged` |
| User administration | **Story 04** | **`UserEmailChanged`** — written when a customer's email change propagates to their linked portal login (**A-19**, data-model §2.14 and §5 constraint 1b). It is user administration by consequence rather than by endpoint: the caller patched a customer, and a sign-in identifier moved |
| Ticket lifecycle | Story 06 | `TicketStatusChanged`, `TicketEscalated` |

**Every write goes through `IAuditRecorder`.** `grep` for direct `AuditEntries.Add(` outside
`AuditRecorder.cs` — there must be none (architecture §2.4: *"Audit writes are never issued from
controllers or from Infrastructure."*).

**The `action` filter is a free-text match on a string column, so this table needs no code.** No
enum, no lookup table and no per-action UI list exists — `AuditAction`'s constants are a
typo guard, not a closed set (data-model §2.14 gives the actions as examples). A story that adds
an action adds a constant and a call site; **this table is the inventory that keeps it visible**,
and `UserEmailChanged` is the first action a story other than 02 or 06 contributes.

### 7 — Application: the audit read service

**Create file: `src/SupportCrm.Application/Modules/Administration/AuditQueryService.cs`** —
`ListAsync(actorUserId?, action?, from?, to?, page, pageSize)`, ordered newest first, using the
`AuditEntry(OccurredAt)` and `AuditEntry(ActorUserId)` indexes.

Response exactly api-design §6.9 `AuditEntry`: **`actor` is null when no user could be resolved — a
failed sign-in — and `actorDescriptor` then carries the submitted identifier.**

**This service exposes exactly one method.** There is no create, update or delete — the log is
append-only by construction (T2-H).

### 8 — Api: the audit endpoint

**Create file: `src/SupportCrm.Api/Controllers/AuditController.cs`** —
`GET /api/v1/audit?actorUserId=&action=&from=&to=`, policy **`RequireAdministrator`**.
`403` for everyone else.

**No write, update or delete endpoint exists**, and none may be added.

### 9 — Part B tests

**Create file: `tests/SupportCrm.Tests/Administration/AuditReadTests.cs`**

10. `GET /audit` as `Administrator` -> `200`; as `Manager` -> **`403`**; as `Agent` -> `403`;
    as `Customer` -> `403`.
11. Filters by `actorUserId`, by `action` and by a `from`/`to` date range, and they combine.
12. A **failed** sign-in appears with `actor: null`, a populated `actorDescriptor`, and
    `outcome: "Failure"`.
13. **No route accepts a write to `/audit`** — `POST`, `PATCH`, `PUT` and `DELETE` all return
    `404`/`405`.
14. **Append-only by reflection:** no service in `Modules/Administration` exposes a method whose
    name contains `Update` or `Delete`, and no code path removes an `AuditEntry`.

**Create file: `tests/SupportCrm.Tests/Administration/AuditAndHistoryAreSeparateTests.cs`** — the
AD-10 assertion the intake requires:

15. A **`UserRoleChanged`** audit entry exists with **no** corresponding `TicketActivity` row (it
    has no ticket).
16. A **`MessagePosted`** activity row exists with **no** corresponding `AuditEntry`.
17. Both are queryable **independently**, and neither read joins the other's table.

---

## Frontend Tasks

### 10 — Audit log — `/admin/audit` (ui-design §6)

- `PagedTable` with occurred-at, actor (or `actorDescriptor`), action, target, outcome.
- Filters: actor, action, date range — bound to URL query parameters (UI-9). Dates use the PrimeNG
  calendar in the **Gregorian calendar in both languages** (A-11).
- **Read-only. No row action of any kind** — no edit, no delete, no export, no bulk selection. The
  log is append-only (T2-H).
- **This is a different screen from the ticket activity region on purpose** (AD-10): different
  actors, different questions. Do not link them or merge them.

### 11 — Configuration view — `/admin/configuration` (ui-design §6)

- Reads `GET /config` **and** `GET /config/staff` and renders the effective values grouped by
  concern: categories (with their department mapping), priorities, SLA targets, quick replies,
  branding, feedback rating scale.
- **A banner states that changing configuration is a redeploy** (T2-I).
- **Read-only with no save control anywhere.** No input, no toggle, no editable field — not a
  disabled one, none at all. *"A read-only view of effective configuration is permitted; a writable
  one is not"* (architecture §6.3).
- The feedback rating-scale row shows the configured `min`–`max`. **Do not annotate it with a
  claim about what the scale should be** — OQ-1 is open.

---

## Verification Steps

**Part A**

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Config tests pass:**
   `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~ConfigurationTier|FullyQualifiedName~ConfigurationValidation|FullyQualifiedName~NoConfigurationEntity"`.
3. **Fail-fast by hand:** point a category at a random GUID and start the API — it **exits with a
   message naming that category**, rather than starting and failing on the first ticket.
4. **Tier check:** `curl` `/api/v1/config` with a customer token — the body has exactly
   `categories` and `feedback`, and no `departmentId` anywhere.

**Part B**

5. **Audit tests pass:**
   `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~Audit"` — all eight green.
6. **Coverage by hand:** sign in with a wrong password, create a user, deactivate a user, and
   transition a ticket; all four appear in `GET /api/v1/audit`.
7. **No write path:** `curl -X POST /api/v1/audit` -> `404`/`405`.
8. **Regression:** every prior suite still passes; `/config` and `/config/staff` are unchanged by
   Part B.
9. **Frontend:** the audit screen has **no row action**; the configuration screen has **no input
   element** — verify with a DOM query for `input, select, textarea, button[type=submit]` returning
   only the filter controls on the audit screen and **nothing** on the configuration screen.

---

## Done Criteria

**Part A**

- [ ] Categories, priorities, SLA targets, quick replies, the default branch, the attachment cap,
      the feedback rating scale and branding values are all read from **file/environment
      configuration at startup**.
- [ ] **Every configured category maps to an existing department**; an unmapped category is a
      **startup failure** (A-14).
- [ ] **Invalid configuration fails fast at startup with a clear message.**
- [ ] `GET /config` returns **only** the customer-safe tier; `GET /config/staff` returns the
      staff-only tier and is **`403`** for a Customer (AP-17, B-2).
- [ ] **No entity exists for any configured concept** (data-model §2.16), asserted by test.
- [ ] **OQ-1 is not answered.** The rating-scale key holds a placeholder, commented as such, and no
      `min`/`max` constant appears anywhere else in the codebase.

**Part B**

- [ ] Audit entries are written for sign-in, user administration, permission-relevant changes and
      ticket lifecycle actions, **all through the single recorder**.
- [ ] Each entry records actor, action, target, timestamp and outcome.
- [ ] Entries are **append-only**: no application path updates or deletes one, asserted by test.
- [ ] **Only an Administrator can read the audit log**; every other role is refused server-side.
- [ ] The audit list supports filtering by actor, action type and date range.
- [ ] **The audit log and ticket history remain independently queryable and neither is derived from
      the other** (AD-10).
- [ ] **No screen writes configuration**; the configuration view is read-only with no save control.
- [ ] `00-overview.md` updated with this story, **noting both execution points**.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 17 Part B.**
