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
- `Department.managerUserId` is **optional**, and ✅ **OQ-3 is closed (2026-08-31) by A-21** — when
  a department has no usable manager, escalation notifies every active `Manager`, else every active
  `Administrator`, else nobody, and **never blocks the escalation itself**. The field stays optional
  and **this story's model was not changed**. Seeded departments all have a manager; that remains a
  demo convenience rather than the answer.
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

**OQ-3 was untouched by this story**, correctly: no fallback escalation recipient existed anywhere
in the code, and the `Department.ManagerUserId` doc comment said so at the point a future reader
would look. ✅ **OQ-3 was closed on 2026-08-31 by A-21**, and the answer landed **outside this
story**, in `Application/Modules/Sla/EscalationRecipientPolicy.cs`.

### 03 — Departments and branches — **complete** (2026-08-27)

The remaining tasks, taken after Story 02 exactly as the interleave of S9-12 requires.

| Task | Delivered |
|---|---|
| 1–3 (already logged above) | Entities, EF configuration, the shared `InitialSchema` migration and `OrganizationSeeder` — delivered earlier as Story 02's approved prerequisite. **Not re-done here** |
| 4 Application | `OrganizationQueryService` (`GetDepartmentsAsync`, `GetBranchesAsync`) with `DepartmentDto` / `BranchDto`, and `DepartmentValidator.EnsureManagerIsEligibleAsync` — the single home of the "active Manager or Administrator" rule that `DepartmentConfiguration` deliberately declines to express as a foreign key |
| 5 Api | `DepartmentsController`, `BranchesController` — one `GET` each, policy `RequireAgent`. **No write action on either**, and no `/portal` variant: under A-14 a customer chooses a category, never a department |
| 6 Tests | `OrganizationEndpointTests` (8 passing) and `BranchIsNotABoundaryTests` (1 **skipped by design**) |
| 7 Front end | `core/api/organization.client.ts` — `getDepartments()`, `getBranches()`, both `shareReplay`-cached for the session (T2-I). The Story 02 free-text **department-id inputs are gone**: the create dialog and the user detail form now bind a selector populated from `GET /departments` |
| 8 Front end | `shared/components/department-filter/` with `disabledForOwnDepartment`, wired into `/admin/users` with the flag **off**. Stories 04, 05 and 15 consume it for their own screens |

**`IdentitySeeder`'s manager second pass now calls `DepartmentValidator`** instead of re-expressing
the eligibility rule inline, which is what Story 03 task 4 always intended and what the seeder's own
comment forward-referenced. The throw is caught so an ineligible demo manager leaves the department
**without** one — a legal state — rather than taking the API down or silently appointing someone.

**One defect found and fixed.** `CreateUserDialogComponent.departmentMissing` was a `computed` over a
**plain** field, so it cached against `touched` alone: after one failed submit the warning and the
disabled submit button could never clear, whichever department the administrator then picked. The
field is now a signal. The bug predates this story and was reached by replacing the input it guarded.

**Verified.** Build 0 warnings / 0 errors; 32 backend tests pass and 1 is skipped by design; 4
front-end specs pass (the repo's first — the karma target and its dependencies were already
configured by Story 01, so no test infrastructure was added). Against real SQL Server on a **wiped
volume**: `InitialSchema` applies, `OrganizationSeeder` writes 4 rows, the second pass assigns 2
managers, and `GET /api/v1/departments` returns `Billing` and `Technical`. Live role matrix on both
endpoints — anonymous `401`, Customer `403`, Agent/Manager/Administrator `200`; `POST`, `PATCH`,
`PUT` and `DELETE` all `405`; an unknown sort field `400`. A department with a null `ManagerUserId`
serializes as `{"id","name"}` with **no `managerUserId` key at all**. `grep -ri "branchid"` over
`Domain/Modules/Tickets/` returns nothing, and every `Branch` reference in the backend is a payload
field, an FK-target check, a seeder or the read endpoint — **none is in an authorization predicate**.
Stories 01 and 02 re-checked live: `/health` and `/config/bootstrap` still anonymous, `/users` still
`401`/`403`/`403`/`200`, the SPA still serves.

**Verification step 6 could not be run as written**, and that is an ordering fact rather than a gap:
it names the `/workspace/tickets` department filter, and the ticket list is **Story 05**. The rule it
checks was still verified — `department-filter.component.spec.ts` asserts that an `Agent` is pinned
and disabled with a hint while a `Manager` and an `Administrator` are enabled across all departments.
**Story 05 must run the step against the real screen**, and remove the `Skip` on
`BranchIsNotABoundaryTests.Ticket_has_no_branch_member` (task 10).

**OQ-3 was open and unanswered when this story shipped**, and the shape it left was the right one:
`DepartmentValidator` constrains only a manager that *is* set and says nothing about a department
without one, and the seeded managers remain a demo convenience, commented as such at both ends.
✅ **OQ-3 is now closed (2026-08-31) by A-21**, and the answer landed **outside this story** — in
`Application/Modules/Sla/EscalationRecipientPolicy.cs`. **Nothing in this story changed:**
`DepartmentValidator` is untouched and still says nothing about the null case, which is correct,
because eligibility of a manager that *is* set and choice of recipient when there is none are two
different rules.

**Implementation choices not fixed by the approved documents**, both following the `/users`
precedent: the sort whitelist for these two endpoints is `name` only (§2.1 requires *a* whitelist and
names none), and the front-end client requests `pageSize=100`, the contract's documented maximum —
so an organization with more than 100 departments would be truncated, which is a question for
api-design and ui-design rather than a client-side loop. Both are recorded at the code.
