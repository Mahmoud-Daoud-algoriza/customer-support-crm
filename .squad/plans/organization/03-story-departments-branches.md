# Story 03 — Departments and branches

> **Source of truth:** `docs/requirements.md` §12 (multi-department, multi-branch) · `docs/product-scope.md` T1-E, T2-K, A-2 · `docs/architecture.md` §4.3, AD-5 · `docs/data-model.md` §2.2, §2.3, §5 constraints 5–6, §6 · `docs/api-design.md` §4.3, §4.4, §5.4, §6.2 · `docs/ui-design.md` §5.2, §5.4, §5.7
> **Intake:** `.squad/stories/organization/departments-branches/intake.md` · **Tier:** T1 (departments) + T2 (branches)
> **Phase:** 1 — Identity and Organization.

## Prerequisites

- **Story 01 completed:** solution skeleton, `SupportCrmDbContext`, seeder pipeline, options binding.
- **Executed as an adjacent pair with Story 02.** Backend tasks 1–3 below (entities, EF
  configuration, seed data) run **before** Story 02's user administration, because
  `POST /users` requires a `departmentId` that must already exist. Tasks 4–6 (endpoints and
  front-end filters) run **after** Story 02, because both endpoints are `Agent`-gated. Both intakes
  say these stories are *"planned together or in immediate sequence"*; this is that sequence made
  explicit.

---

## Story Goal

Establish the two organizational dimensions with the deliberate **asymmetry** of A-2, and make that
asymmetry structural rather than documented.

1. **`Department`** — the routing and permission boundary. A ticket belongs to exactly one; a staff
   user belongs to exactly one. Each department has an optional manager who is the escalation
   recipient for Story 09.
2. **`Branch`** — a location attribute on customers and staff users. **A reporting and filtering
   attribute only.** It grants no isolation and appears in **no authorization predicate anywhere**.
3. Two read endpoints so later screens can populate filters and assignment pickers.
4. Seed data with at least two departments and two branches, so department scoping is demonstrable
   in Story 05 and branch's *non*-scoping is demonstrable at the same time.

**The single most important outcome of this story is a negative one:** `Ticket` gets **no**
`branchId` column, now or ever. A ticket's branch is derived `Ticket -> Customer -> Branch`
(data-model §2.3).

---

## Context — Read These Files First

1. `docs/data-model.md` **§2.3 in full**, including *"How a ticket's branch is obtained — derived,
   by intent"* and the source audit table. It records why no source requires a ticket-level branch
   relationship, and states that a branch-level rule would be a **scope change, not a model tweak**.
2. `docs/data-model.md` §2.2 `Department` — the fields, and the **OQ-3** block: a department may
   have **no** manager, and *no fallback recipient is invented*. Read the whole box; it constrains
   Stories 06 and 09.
3. `docs/product-scope.md` A-2 — department is the routing and permission boundary; branch grants
   no isolation.
4. `docs/architecture.md` §4.3 — the visibility table and the five enforcement points, and the
   line *"Branch is **not** part of this rule."* AD-5 records why an explicit helper beats an EF
   global query filter.
5. `docs/api-design.md` §5.4 (**no write endpoints** — departments and branches are seeded and
   configured, T2-I), §4.4 (branch appears in exactly two places), §6.2 payload shapes.
6. `.squad/stories/organization/departments-branches/intake.md` — the acceptance criteria,
   especially *"Branch is demonstrably NOT a permission boundary"*.

---

## Product rules (from story)

- **Department and branch are independent attributes, not a hierarchy** (A-2). No sub-departments,
  no cross-department teams, no branch-scoped permissions, no tenants.
- **`Department.managerUserId` is optional.** A department can exist before anyone is appointed to
  it. **Do not add a fallback recipient, do not route to all Managers, do not route to
  Administrators, and do not silently drop a notification** — each is a product decision and OQ-3
  is open (data-model §2.2).
- `managerUserId`, when set, must reference an **active** user of role `Manager` or
  `Administrator` — an **Application-layer** rule, not a foreign-key constraint (data-model §2.2).
- **Departments and branches are seeded and configured, not managed through an admin UI** (T2-I).
  There is no `POST`, `PATCH` or `DELETE` for either.

---

## Backend Tasks

*(Tasks 1–3 run before Story 02's user administration; tasks 4–6 run after it.)*

### 1 — Domain entities

**Create file: `src/SupportCrm.Domain/Modules/Organization/Department.cs`**

```csharp
public sealed class Department
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;   // unique
    public Guid? ManagerUserId { get; private set; }        // optional — see OQ-3
}
```

**Create file: `src/SupportCrm.Domain/Modules/Organization/Branch.cs`**

```csharp
public sealed class Branch
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;   // unique
}
```

Plain C#, **no EF attributes** (AD-4). Neither type carries a collection navigation to `Ticket` —
nothing in the reads below needs one, and a `Department.Tickets` property would invite loading a
department's whole ticket set.

**Add nothing else.** In particular, **do not** add a `Branch.Departments`, a
`Department.ParentId`, or any tenant field (A-2, T3-G).

### 2 — Infrastructure: EF configuration and the initial migration

**Create files:** `Persistence/Configurations/DepartmentConfiguration.cs` and
`BranchConfiguration.cs`.

- `Department.Name` and `Branch.Name` — unique indexes.
- `Department.ManagerUserId` — **no foreign key to `User`.** The referential rule (active
  Manager or Administrator) is a cross-row Application rule, and an FK here would create a
  create-order cycle with `User.DepartmentId`. Document the choice in the configuration file.
- `User.DepartmentId` and `User.BranchId` foreign keys are configured in Story 02's
  `UserConfiguration`; both are `DeleteBehavior.Restrict`.

Add `DbSet<Department>` and `DbSet<Branch>` to `SupportCrmDbContext`.

**The initial migration is generated once, after Story 02 task 3 has added `User` and
`AuditEntry`,** so a single `InitialSchema` migration creates all four tables:

```bash
dotnet ef migrations add InitialSchema -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Seed data

**Create file: `src/SupportCrm.Infrastructure/Persistence/Seeders/OrganizationSeeder.cs`
(`Order = 10`)** — the first seeder to run.

- **At least two departments**: `Billing` and `Technical`.
- **At least two branches**: `Head Office` and `North Branch`.
- Manager assignment is a **second pass** run by `IdentitySeeder` (Story 02, `Order = 20`) once
  Manager users exist, because `managerUserId` must reference an active Manager.

> **Every seeded department is given a manager.** `data-model.md` §2.2 states this explicitly as a
> **seeding convenience that keeps the demo path well defined — it is not an answer to OQ-3.** Do
> not let seed data become the reason nobody notices the question is open.

Seeded ids are **deterministic constants** in the seeder file, so later seeders (Stories 04, 05)
can reference a department or branch without a lookup.

### 4 — Application: read services

**Create file: `src/SupportCrm.Application/Modules/Organization/OrganizationQueryService.cs`**

- `GetDepartmentsAsync()` -> `IReadOnlyList<DepartmentDto>` = `{ id, name, managerUserId? }`
  (api-design §6.2). **`managerUserId` is omitted when absent** — a department need not have one.
- `GetBranchesAsync()` -> `IReadOnlyList<BranchDto>` = `{ id, name }`.

Both are small unpaged lists used to populate filters and pickers; they still return the standard
paged envelope for uniformity (AP-3).

**Create file: `src/SupportCrm.Application/Modules/Organization/DepartmentValidator.cs`** — a single
`EnsureManagerIsEligibleAsync(Guid managerUserId)` used by the seeder second pass: the referenced
user must exist, be `IsActive`, and have role `Manager` or `Administrator` (data-model §2.2).

### 5 — Api: controllers

**Create file: `src/SupportCrm.Api/Controllers/DepartmentsController.cs`** —
`GET /api/v1/departments`, policy `RequireAgent`.

**Create file: `src/SupportCrm.Api/Controllers/BranchesController.cs`** —
`GET /api/v1/branches`, policy `RequireAgent`.

**No write endpoints exist on either controller** (api-design §5.4). **Customers never call these**
— under A-14 a customer chooses a *category*, never a department, so no `/portal` variant is
published.

### 6 — Tests

**Create file: `tests/SupportCrm.Tests/Organization/OrganizationEndpointTests.cs`**

1. `GET /departments` as `Agent` -> `200` with at least two rows.
2. `GET /departments` as `Customer` -> `403`.
3. A department seeded without a manager serializes **without** a `managerUserId` key, and does
   not serialize `null` (api-design §2, "nulls omitted").

**Create file: `tests/SupportCrm.Tests/Organization/BranchIsNotABoundaryTests.cs`** — the acceptance
criterion that matters most. It cannot be fully exercised until tickets exist, so:

- **Now:** a source-level assertion that `Ticket` has no branch member —
  `typeof(Ticket).GetProperties()` contains nothing matching `Branch` — written as a **skipped
  test with an explicit reason** referencing Story 05, and enabled there.
- **Story 05:** the behavioural test — *an agent sees an in-department ticket whose customer is in
  a different branch* — is added to this same file by
  [05-story-ticket-core.md](../ticket-management/05-story-ticket-core.md) task 10.

Cross-reference both directions in a comment so the pair is not lost.

---

## Frontend Tasks

### 7 — Typed clients and filter wiring

- `core/api/organization.client.ts` — `getDepartments()`, `getBranches()`, both cached for the
  session (these lists change only by redeploy, T2-I).
- Extend `features/admin/users/` (Story 02) so the department selector is populated from
  `getDepartments()` rather than a hardcoded list.

### 8 — Where each filter may and may not appear

This is the front-end half of A-2 and it must be got right once, in `shared/`:

| Screen | Department filter | Branch filter |
|---|---|---|
| `/workspace/tickets` (ui-design §5.2) | **Yes** — *fixed to the agent's own department and disabled* for `Agent`, with a hint explaining why; **enabled across all departments** for `Manager+` | **No.** Branch is never a ticket scope |
| `/workspace/customers` (ui-design §5.4) | No | **Yes** — its legitimate reporting use (T2-K) |
| `/workspace/reports` (ui-design §5.7) | Yes | Yes |
| `/admin/users` | Yes | No |

Build `shared/components/department-filter/` with a `disabledForOwnDepartment` input so the Story 05
ticket list cannot re-implement the rule. **This makes architecture §4.3 legible rather than
mysterious — the server enforces it either way.**

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Migration applies:** `docker compose up --build`; `Departments` and `Branches` tables exist and
   contain the seeded rows.
3. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Organization`.
4. **Endpoints:** `GET /api/v1/departments` as an Agent token -> `200` with `Billing` and
   `Technical`; as a Customer token -> `403`.
5. **Negative check on the model:** `grep -ri "branchid" backend/src/SupportCrm.Domain/Modules/Tickets/`
   returns **nothing** — and will still return nothing after Story 05.
6. **Frontend:** the ticket-list department filter is disabled for a seeded Agent and enabled for
   the seeded Manager.

---

## Done Criteria

- [ ] Departments exist as first-class records with a name and an **optional** designated manager.
- [ ] Branches exist as first-class records with a name.
- [ ] Every staff user is assigned exactly one department (enforced in Story 02).
- [ ] Ticket lists and reports can be filtered by department and by branch (wired here, exercised in
      Stories 05 and 15).
- [ ] **`Ticket` has no branch column**, and the branch filter resolves through the customer.
- [ ] Branch appears in **no** authorization predicate anywhere in the codebase.
- [ ] Seed data contains at least two departments and two branches.
- [ ] No write endpoint for departments or branches exists.
- [ ] **OQ-3 is not answered here.** No fallback escalation recipient is invented; the seeded
      manager is a demo convenience and is commented as such.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 04.**
