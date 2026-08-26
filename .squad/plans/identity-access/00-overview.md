# identity-access — plan overview

Entry point for the **identity-access** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 02 | [02-story-auth-and-roles.md](02-story-auth-and-roles.md) | Authentication, roles and permission enforcement | — | Story 01; **Story 03 data layer first** |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/identity-access/`](../../stories/identity-access/).

## Dependency notes

**Phase 1.** Executes as an **adjacent interleaved pair** with
[03-story-departments-branches.md](../organization/03-story-departments-branches.md): Story 03's
entities and the initial migration, then this story, then Story 03's endpoints and filters.
`POST /users` requires a `departmentId` for every staff role (finding **S9-12**).

- Introduces `AuditEntry` and the single `IAuditRecorder`, because this story's own acceptance
  criterion requires sign-in recording (finding **S9-5**). The audit **read** surface is
  [16 Part B](../administration/16-story-audit-configuration.md).
- **`POST /auth/register` is deferred to [Story 04](../customer-management/04-story-customer-records.md)** —
  A-15 needs a `Customer`, a `Branch` and a configured default branch (finding **S9-7**).
- Ticket scoping is [Story 05](../ticket-management/05-story-ticket-core.md)'s, because
  architecture §4.3 puts the helper in the Tickets module.
- **T1 — cannot be cut.**

## Delivery log

### 02 — Authentication, roles and permission enforcement — **complete** (2026-08-26)

| Task | Delivered |
|---|---|
| 1–2 Domain | `UserRole` (numeric order **is** the A-4 hierarchy; `RankAtLeast` is the only comparison), `User` with `CreateStaff` refusing the three shapes DM-1 forbids, `AuditEntry` + `AuditOutcome` + `AuditAction` constants. No EF attributes; `Domain` still has **zero** references |
| 3 Infrastructure | `UserConfiguration`, `AuditEntryConfiguration`, both `DbSet`s, and the single **`InitialSchema`** migration creating all four tables (`Branches`, `Departments`, `Users`, `AuditEntries`) exactly as both plans require |
| 4 AD-15 | `ICurrentUser` + `CurrentUserAccessor` + `CurrentUserMiddleware` — one indexed read per authenticated request, **no cache**, refusing a missing or deactivated user with `401` before authorization runs, and replacing the principal's role claim with the freshly read value |
| 5 Policies | Four hierarchical policies; no controller compares roles inline |
| 6 Auth | `AuthService` (login + `GetMeAsync`), `ITokenIssuer` → `JwtTokenIssuer`. `POST /auth/register` **deferred to Story 04** (S9-7) |
| 7 Audit | `IAuditRecorder` + `AuditRecorder` — the one writer, no update or delete method |
| 8 User admin | `UserAdminService` — the five endpoints, all validation server-side |
| 9 Api | `AuthController`, `UsersController`; `PagedResult<T>` envelope added (first paged endpoint) |
| 10 Seed | `IdentitySeeder` (`Order = 20`) — Administrator, Manager, two Agents in **different departments**; password from configuration, no credential in source |
| 11 Tests | `AuthorizationTests`, `UserAdminValidationTests`, `AuditRecordingTests` — **24 passing**, including the AD-15 deactivation regression |
| 12–14 Front end | Auth store/service, token interceptor, `401` handling with return URL, guards + role redirect, login, scaffolded register, staff-shell avatar menu and role-based nav, user directory/detail/create dialog |

**Executed early as a dependency:** Story 03 task 3 `OrganizationSeeder` (`Order = 10`). Story 02
task 10 cannot seed a staff user without a department. Story 03 is otherwise **not started** — no
query service, no controllers, no endpoints. Approved by the user before implementation.

**Two defects found by the story's own verification and fixed:**

1. **Enums serialized as integers**, which docs/api-design.md §2 forbids (`Enums ... never
   integers`). `JsonStringEnumConverter` added. It surfaced as `POST /users` rejecting
   `role: "Agent"` — the contract violation and a functional break were the same bug.
2. **A successful sign-in recorded `actorUserId = null`.** docs/data-model.md §2.14 permits null for
   exactly one reason — "no user could be resolved, a failed sign-in" — so a successful sign-in must
   be attributed. `IAuditRecorder` gained an explicit `actorUserId` override for the one case where
   the caller knows the actor but the request has no identity yet, because `POST /auth/login` is
   anonymous. A test now locks both directions of the rule.

**Verified:** build 0/0; 24 tests green; `InitialSchema` applied to real SQL Server with all four
tables; `Users.Email` collation confirmed `SQL_Latin1_General_CP1_CI_AS` and case-insensitive
sign-in exercised against it; the issued token carries **`sub`, `jti`, `iat`, `exp`, `iss`, `aud`
and nothing else**; live role gating on `/users` is 401/403/403/200 for
anonymous/Agent/Manager/Administrator; `/health` and `/config/bootstrap` still anonymous; all three
seeded staff roles sign in through the browser, land on `/workspace`, and see the Administration
section only as Administrator; an Agent deep-linking to `/admin/users` lands on `/403`.
