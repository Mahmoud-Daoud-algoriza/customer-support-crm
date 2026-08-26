# organization — plan overview

Entry point for the **organization** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 03 | [03-story-departments-branches.md](03-story-departments-branches.md) | Departments and branches | — | Story 01; paired with Story 02 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/organization/`](../../stories/organization/).

## Dependency notes

**Phase 1.** Split across the Story 02 pair: backend tasks 1–3 (entities, EF configuration,
seed data, the shared `InitialSchema` migration) run **before** Story 02; tasks 4–6 (the two
`Agent`-gated endpoints) run **after** it.

- The load-bearing outcome is a **negative** one: `Ticket` gets **no** `branchId`, now or ever.
  A ticket's branch is derived `Ticket → Customer → Branch` (data-model §2.3).
- `Department.managerUserId` is **optional** and **no fallback escalation recipient is invented** —
  **OQ-3** stays open. Seeded departments all have a manager; that is a demo convenience, not an
  answer.
- Departments are **T1 and cannot be cut**; branch may degrade to a display and filter field.

## Delivery log

### 03 — Departments and branches — **data layer partially delivered** (2026-08-26)

Scope taken: the part S9-12 identifies as Story 02's prerequisite. **Tasks 1 and 2 (minus the
migration) only.** No endpoint, no front-end work, no Story 02 code.

| Task | State | Notes |
|---|---|---|
| 1 — Domain entities | ✅ Done | `Department` (`Id`, `Name`, `ManagerUserId?`) and `Branch` (`Id`, `Name`) under `Domain/Modules/Organization/`. Plain C#, no EF attributes (AD-4), no collection navigations, no `ParentId`, no tenant field. `Domain` still has **zero** project and package references |
| 2 — EF configuration | ✅ Done | `DepartmentConfiguration`, `BranchConfiguration`; `DbSet<Department>`, `DbSet<Branch>`. Unique index on each `Name`; **no FK on `ManagerUserId`** |
| 2 — `InitialSchema` migration | ⏸ **Deferred by the plan** | Task 2 states it is generated *"after Story 02 task 3 has added `User` and `AuditEntry`"*, so one migration creates all four tables. Story 02 task 3 says the same thing from its side, and `00-implementation-plan.md` §6 lists no separate Organization or Identity migration |
| 3 — `OrganizationSeeder` | ⏸ **Blocked on the migration** | The seeder queries `Departments`. With no migration the table does not exist, so registering it would crash the API at startup and regress Story 01. It follows the migration, not this step |
| 4–6 — Services, controllers, tests | ⏸ Not started | Post-Story-02 by design; both endpoints are `Agent`-gated |
| 7–8 — Front end | ⏸ Not started | Depends on Story 02's admin screens |

**Verified:** `dotnet build` 0 warnings / 0 errors; the 2 Story 01 tests still pass; the intended
schema was confirmed by generating a throwaway migration, reading its `Up()` — `Departments` and
`Branches`, `nvarchar(200)` unique names, nullable `ManagerUserId` **with no foreign key** — and
then reverting it, leaving no migration behind. `grep -ri "branchid"` over
`Domain/Modules/Tickets/` returns nothing, and the `Branch` type appears in no Api or Application
code.

**Implementation choice not fixed by the approved documents:** `Name` is `nvarchar(200)`. No
document states a string length anywhere, and SQL Server cannot build a unique index over
`nvarchar(max)`, so a bound was required. Recorded in the configuration files.

**Tooling added:** `backend/dotnet-tools.json` pinning `dotnet-ef` 10.0.11 as a local tool — the
plans' own `dotnet ef migrations add` command had nothing to run.

**OQ-3 is untouched.** No fallback escalation recipient exists anywhere in the code, and the
`Department.ManagerUserId` doc comment says so at the point a future reader would look.
