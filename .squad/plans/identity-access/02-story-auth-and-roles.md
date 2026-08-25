# Story 02 — Authentication, roles and permission enforcement

> **Source of truth:** `docs/requirements.md` §10.1–10.3 · `docs/product-scope.md` T1-D, A-4, A-9, A-16 · `docs/architecture.md` §4.1, §4.1.1, §4.2, §2.4, AD-7, AD-15 · `docs/data-model.md` §2.1, §2.14, §5 constraints 1–4 · `docs/api-design.md` §4.1, §4.2, §5.2, §5.3, §6.1, §6.9 · `docs/ui-design.md` §2, §4.1, §6
> **Intake:** `.squad/stories/identity-access/auth-and-roles/intake.md` · **Tier:** T1 — cannot be cut
> **Phase:** 1 — Identity and Organization.

## Prerequisites

- **Story 01 completed:** solution skeleton, `SupportCrmDbContext`, options binding, Problem
  Details handler, `/api/v1` prefix, Angular shells and interceptors.
- **Story 03 Backend Tasks 1–2 completed first.** `POST /users` requires `departmentId` for every
  staff role (data-model §2.1), so the `Department` and `Branch` entities and the **initial
  migration** must exist before this story's user administration is written. The two intakes say
  these stories are *"planned together or in immediate sequence"*; the implementation plan executes
  them as one adjacent pair, organization data layer first. See
  [03-story-departments-branches.md](../organization/03-story-departments-branches.md).

---

## Story Goal

Deliver the four-actor access model the whole system depends on.

1. Email + password sign-in issuing a JWT that **asserts identity only** (AD-7).
2. **Per-request resolution** of role, department and active status from the authoritative `User`
   record — never from a token claim (AD-15, architecture §4.1.1).
3. The four fixed hierarchical roles of A-4 as authorization policies, enforced **server-side**.
4. Administrator user management: create, read, list, patch, deactivate.
5. The **single audit-recording service** (architecture §2.4) and the `AuditEntry` entity, writing
   sign-in and user-administration entries from day one.

**Not in this story:** `POST /auth/register` (see the note in task 6), ticket scoping (Story 05
owns the scoping helper, because it is a Tickets-module concern — architecture §4.3 point 2), and
the audit **read** surface (Story 16 Part B).

---

## Context — Read These Files First

1. `docs/architecture.md` §4.1, and **§4.1.1 in full** — the staleness defect AD-15 exists to
   prevent, and the exact resolution step. Then §4.2 (three enforcement points) and §2.4 (the audit
   boundary: **one writer**, append-only by construction, Administrator-only read).
2. `docs/data-model.md` §2.1 `User` — the **conditional** `departmentId` / `customerId` columns;
   §2.14 `AuditEntry` — including `actorDescriptor` for a failed sign-in with no resolvable actor;
   §5 constraints 1–4; §6 indexes `User(email)` unique, `User(departmentId, isActive)`,
   `User(customerId)` unique non-null.
3. `docs/api-design.md` §4.1 (**a deactivated user gets `401`, not `403`**), §4.2, §5.2, §5.3,
   §6.1 (`AuthToken`, `Identity`, `User`, `UserSummary`), §6.11 (`POST /auth/login` body and the
   `invalid-credentials` rule), §7 (`User.role` / `User.departmentId` never self-set).
4. `docs/ui-design.md` §2 (route tree and the root role redirect), §4.1 (staff shell and avatar
   menu), §6 (User directory, User detail, Create user dialog — **the `Customer` role is absent
   from the role selector**).
5. `.squad/stories/identity-access/auth-and-roles/intake.md` — every acceptance criterion, and the
   Out of scope list (no SSO, OAuth, MFA, password policy, account recovery, role editor).
6. Grep `AppExceptions` in `backend/src/SupportCrm.Application/Abstractions/` — reuse the exception
   family Story 01 created; do not add a new error shape.

---

## Product rules (from story)

- **Four fixed hierarchical roles** (A-4): `Customer`, `Agent`, `Manager`, `Administrator`. An
  endpoint marked `Agent` is also reachable by Manager and Administrator unless a narrower rule is
  stated (api-design §4.2). **No role table, no role editor, no per-field permission.**
- **The token carries the user id and standard issuance/expiry claims and nothing else.** Role,
  department and active status are **not** claims (AD-7, AD-15).
- **A user that no longer exists or is deactivated is refused with `401` on every request**, not
  only at sign-in, because the active flag is re-read per request.
- A staff role **requires** `departmentId` and **forbids** `customerId`; the `Customer` role is the
  reverse (DM-1). **The `Customer` role cannot be created through `POST /users` at all.**
- **A wrong password and a deactivated account return the same `401 invalid-credentials`** —
  distinguishing them would confirm which emails have accounts (api-design §6.11).
- **The front end is never an enforcement point.** Guards and hidden buttons are UX only.

---

## Backend Tasks

### 1 — Domain: `User` and the role enum

**Create file: `src/SupportCrm.Domain/Modules/Identity/UserRole.cs`**

```csharp
public enum UserRole { Customer = 0, Agent = 1, Manager = 2, Administrator = 3 }
```

The numeric order **is** the A-4 hierarchy; a `RankAtLeast` helper is the only comparison used.
Persisted as a **stable string code**, never an integer (api-design §2).

**Create file: `src/SupportCrm.Domain/Modules/Identity/User.cs`** — plain C#, **no EF attributes**
(AD-4). Fields exactly as data-model §2.1:

```csharp
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;          // unique, case-insensitive
    public string PasswordHash { get; private set; } = default!;   // never leaves the server
    public string DisplayName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public Guid? DepartmentId { get; private set; }                // required for staff, null for Customer
    public Guid? CustomerId { get; private set; }                  // required for Customer, null for staff
    public Guid? BranchId { get; private set; }                    // reporting attribute only (T2-K)
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
```

Add a private-constructor factory `CreateStaff(...)` that **throws** when `departmentId` is null or
`customerId` is set, and `Deactivate()`. `CreateCustomerUser(...)` is added by Story 04 — do not
write it here, because `Customer` does not exist yet.

### 2 — Domain: `AuditEntry`

**Create file: `src/SupportCrm.Domain/Modules/Administration/AuditEntry.cs`** with the fields of
data-model §2.14: `OccurredAt`, `ActorUserId?`, `ActorDescriptor?`, `Action`, `TargetType?`,
`TargetId?`, `Outcome` (`Success` | `Failure`).

> **Planning note — why the entity lands here and not in Story 16.** `data-model.md` §7 maps
> `AuditEntry` to the `audit-configuration` story, but **this story's own acceptance criterion**
> requires sign-in and user-administration actions to be recorded, and recording requires the
> table. Story 16 keeps the read surface (`GET /audit`), the filters and the admin screen. This is
> a placement decision about *which plan file contains the work*, not a change to any business
> rule; it is recorded in `00-implementation-plan.md` §"Audit findings" as **S9-5**.

### 3 — Infrastructure: EF configuration and migration

**Create files:**
`src/SupportCrm.Infrastructure/Persistence/Configurations/UserConfiguration.cs` and
`AuditEntryConfiguration.cs`.

- `User.Email` — unique index, case-insensitive collation
  (`SQL_Latin1_General_CP1_CI_AS`), data-model §6.
- `User(DepartmentId, IsActive)` composite index — round-robin candidates (T2-D) and department
  filtering.
- `User(CustomerId)` unique **filtered** index `WHERE CustomerId IS NOT NULL` — one login per
  profile.
- `AuditEntry(OccurredAt)` and `AuditEntry(ActorUserId)` indexes.
- `Role` and `Outcome` converted with `.HasConversion<string>()`.
- Foreign keys to `Department` and `Branch` are configured here; the `Customer` FK is added by
  Story 04's migration.

Add `DbSet<User>` and `DbSet<AuditEntry>` to `SupportCrmDbContext`.

Migration (run after Story 03's entities exist so both land in one initial migration):

```bash
dotnet ef migrations add InitialSchema -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 4 — Application: the current-user context (AD-15)

This is the highest-risk piece in the story. **One** implementation, in one file.

**Create file: `src/SupportCrm.Application/Abstractions/ICurrentUser.cs`**

```csharp
public interface ICurrentUser
{
    Guid Id { get; }
    UserRole Role { get; }
    Guid? DepartmentId { get; }
    Guid? CustomerId { get; }
    string DisplayName { get; }
    bool IsInRoleAtLeast(UserRole minimum);
}
```

**Create file: `src/SupportCrm.Api/Auth/CurrentUserMiddleware.cs`** — runs **after** authentication
and **before** authorization:

1. Read the `sub` claim (the user id) — **the only claim any decision reads**.
2. Load the `User` row by id, `AsNoTracking`.
3. **Refuse with `401` when the user does not exist or `IsActive == false`.** Not `403` — they have
   no valid identity (api-design §4.1).
4. Populate the request-scoped `ICurrentUser` **and** replace the `ClaimsPrincipal`'s role claim
   with the freshly read role, so the coarse endpoint gates of §4.2 and the row scoping of §4.3
   read the same authoritative value rather than two vintages of it (architecture §4.1.1).

Add a comment at the lookup naming the accepted cost: *one indexed read per authenticated request;
no cache, because a cache would reintroduce the staleness AD-15 removes and product-scope §8
excludes a caching layer.*

**Application services never read claims and never accept a caller-supplied user id, department id
or role** (architecture §4.3 point 1). Enforce it by making `ICurrentUser` the only source.

### 5 — Application: authorization policies

**Create file: `src/SupportCrm.Api/Auth/AuthorizationPolicies.cs`** — four policies named
`RequireCustomer`, `RequireAgent`, `RequireManager`, `RequireAdministrator`, each satisfied by that
role **or higher** (A-4 hierarchy). Controllers use `[Authorize(Policy = ...)]`; no controller
compares roles inline.

### 6 — Application: authentication service

**Create file: `src/SupportCrm.Application/Modules/Identity/AuthService.cs`**

- `LoginAsync(email, password)` — look up by normalized email; verify with
  `PasswordHasher<User>` (ASP.NET Core standard hashing, architecture §4.1). On success issue the
  token and write an audit entry `SignInSucceeded / Success`. On failure — **including a
  deactivated account** — throw `UnauthorizedException("invalid-credentials")` and write
  `SignInFailed / Failure` with `ActorDescriptor` set to the submitted email and `ActorUserId` null
  (data-model §2.14).
- `GetMeAsync()` — projects `ICurrentUser` into the `Identity` payload of api-design §6.1.
  **This is the per-request resolved identity, not a decoded token** (AP-9).

**Create file: `src/SupportCrm.Infrastructure/Security/JwtTokenIssuer.cs`** implementing an
Application-declared `ITokenIssuer`. Claims: `sub` (user id), `jti`, `iat`, `exp`.
**No `role`, no `department`, no `email` claim is emitted** — AD-15 is enforced by the shape of
this method, not by a reviewer noticing. Signing key from `SupportCrm:Jwt:SigningKey`
(environment only). Short-lived token; **no refresh rotation and no logout endpoint** (AP-8) — the
client discards the token.

> **`POST /auth/register` is deferred to Story 04.** Registration creates a `Customer` with the
> **configured default branch** and links or creates its `User` (A-15). Neither `Customer` nor the
> default-branch configuration key exists until Stories 04 and 16 Part A, and inventing a
> placeholder branch would contradict A-15. The endpoint, its three A-15 outcomes and the
> `409 user-already-exists` rule are implemented in
> [04-story-customer-records.md](../customer-management/04-story-customer-records.md) task 7.
> This deviation from api-design §10.1's story mapping is recorded as **S9-7** in
> `00-implementation-plan.md`.

### 7 — Application: the single audit recorder

**Create file: `src/SupportCrm.Application/Modules/Administration/IAuditRecorder.cs`**

```csharp
public interface IAuditRecorder
{
    Task RecordAsync(string action, string outcome, string? targetType = null,
                     Guid? targetId = null, string? actorDescriptor = null, CancellationToken ct = default);
}
```

**Create file: `src/SupportCrm.Application/Modules/Administration/AuditRecorder.cs`** — the **one
writer** (architecture §2.4). It resolves the actor from `ICurrentUser` when one exists and falls
back to `actorDescriptor`. **It exposes no update and no delete method — not merely no UI, but no
service method.** Audit writes are issued from Application services only, never from a controller
and never from Infrastructure.

Actions written by this story: `SignInSucceeded`, `SignInFailed`, `UserCreated`,
`UserDeactivated`, `UserRoleChanged`, `UserDepartmentChanged` (data-model §2.14).

### 8 — Application: user administration

**Create file: `src/SupportCrm.Application/Modules/Identity/UserAdminService.cs`** implementing the
five endpoints of api-design §5.3. Validation rules, all server-side:

- A staff role **requires** `departmentId` and **forbids** `customerId`.
- **`role == Customer` is rejected outright** — customers arrive through registration or an agent
  creating a profile (DM-1).
- `departmentId` must reference an existing department.
- Duplicate email -> `ConflictException("user-already-exists")`.
- `PATCH` accepts `displayName`, `role`, `departmentId`, `branchId` **only**. Email and password are
  not patchable here; `role`/`departmentId` are Administrator-set and never self-set (api-design §7).
- `POST /users/{id}/deactivate` -> `204`; the user's **next** request gets `401` via task 4.

### 9 — Api: controllers

**Create files:**

| File | Endpoints | Policy |
|---|---|---|
| `Controllers/AuthController.cs` | `POST /auth/login` (Anonymous), `GET /auth/me` (Authenticated) | — |
| `Controllers/UsersController.cs` | `GET /users`, `POST /users`, `GET /users/{id}`, `PATCH /users/{id}`, `POST /users/{id}/deactivate` | `RequireAdministrator` |

`GET /users` filters: `role`, `departmentId`, `isActive`, `q`; paged envelope; unknown filter or
sort field -> `400` (AP-15). Response shapes exactly api-design §6.1 — **`passwordHash` appears in
no response, ever**.

### 10 — Seed data

**Create file: `src/SupportCrm.Infrastructure/Persistence/Seeders/IdentitySeeder.cs`**
(`Order = 20`, after Story 03's organization seeder at `Order = 10`). Seed one Administrator, one
Manager and at least **two Agents in different departments**, so the department-scoping tests of
Story 05 have material. Passwords come from configuration with a documented development default;
**no credential is hardcoded in source**.

### 11 — Tests

**Create file: `tests/SupportCrm.Tests/Identity/AuthorizationTests.cs`** — integration tests
through the API **as each role, bypassing the UI**, which product-scope T1-D requires:

1. An unauthenticated request to `GET /users` -> `401`.
2. An `Agent` token on `GET /users` -> `403`.
3. An `Administrator` token on `GET /users` -> `200`.
4. **A user deactivated after their token was issued gets `401` on the very next request** — the
   AD-15 regression test. Assert it explicitly; it is the defect this design exists to prevent.
5. A wrong password and a deactivated account return the **same** `401 invalid-credentials` body.
6. `GET /auth/me` returns the current role after an Administrator changes it, **without** a new
   token being issued.

**Create file: `tests/SupportCrm.Tests/Identity/UserAdminValidationTests.cs`** — staff role without
a department is rejected; `role: "Customer"` on `POST /users` is rejected; duplicate email -> `409`.

---

## Frontend Tasks

### 12 — Auth state and the token interceptor

- `core/auth/auth.store.ts` — signal-based store holding the token and the `Identity` payload;
  persists the token in browser storage and rehydrates on load.
- `core/auth/auth.service.ts` — `login()`, `logout()`, `loadMe()`. **`logout()` clears the token
  client-side; there is no logout endpoint** (AP-8) — do not call one.
- `core/interceptors/auth.interceptor.ts` — attaches `Authorization: Bearer <token>`.
- Extend `core/interceptors/error.interceptor.ts`: a `401` clears the store and redirects to
  `/auth/login` **preserving the return URL** (ui-design §9).

### 13 — Guards and the role redirect

- `core/guards/authenticated.guard.ts`, and `roleAtLeast(role)` guard factory.
- `app.routes.ts` — `/workspace` behind `Agent+`, `/admin` behind `Administrator`, `/portal`
  behind `Customer`; `/` redirects **by role**: Customer -> `/portal`, staff -> `/workspace`
  (ui-design §2). This replaces Story 01's temporary `HealthCheckComponent` route.
- Add a comment in the guard file: *guards hide, they do not protect; every route here mirrors a
  server rule that is independently enforced* (architecture §2.2).

### 14 — Screens

- `features/auth/login/` — the centred card in the auth shell. `400`/`401` render the **translated**
  string chosen by the Problem Details `type`; the server `detail` is never shown raw.
- `features/auth/register/` — **route and component scaffolded, submit disabled with a
  "coming with Story 04" note**, because the endpoint is delivered there. Do not call a
  non-existent endpoint.
- `layout/staff-shell/` — fill in the navigation of ui-design §4.1: brand block from
  `/config/bootstrap`, language switcher, avatar menu showing display name, role and department
  from `GET /auth/me`, sign-out clearing the token. *Reports* appears for Manager+, the *Admin*
  section for Administrator only. The notification bell is added by Story 09; leave its slot.
- `features/admin/users/` — user directory (`PagedTable`, filters `role`, `departmentId`,
  `isActive`, `q`), user detail with `PATCH` and a `ConfirmDialog` on deactivate (UI-12), and the
  create-user dialog. **The `Customer` role is absent from the role selector** (ui-design §6). The
  form enforces "a staff role requires a department" client-side **and the server re-validates**.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:** `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Identity`
   — all authorization and validation tests green, including the AD-15 deactivation test.
3. **Migration applies:** `docker compose up --build` then
   `curl http://localhost:5080/api/v1/health` -> `"database":"reachable"`; the `Users`,
   `AuditEntries`, `Departments` and `Branches` tables exist.
4. **Sign-in works:** `POST /api/v1/auth/login` with a seeded Administrator returns `accessToken`,
   `expiresAt` and an embedded `user`. Decode the token and confirm it carries **`sub`, `jti`,
   `iat`, `exp` and nothing else**.
5. **Regression:** `GET /api/v1/health` and `GET /api/v1/config/bootstrap` still answer anonymously.
6. **Frontend:** `cd frontend && npm run build`, then sign in as each seeded role and confirm the
   root redirect, the visible navigation and the avatar menu match A-4.

---

## Done Criteria

- [ ] A user can sign in and sign out; an unauthenticated request to a protected resource is refused.
- [ ] Each user has exactly one role, and each staff user exactly one department.
- [ ] An Administrator can create a user, assign role and department, and deactivate a user;
      **a deactivated user can no longer sign in and is refused on their next request**.
- [ ] Role capabilities match the A-4 table exactly, enforced on the server.
- [ ] Email addresses are unique across users; a duplicate is rejected with `409`.
- [ ] Sign-in and user-administration actions are recorded through the **single** audit recorder.
- [ ] The issued token carries **no** role, department or active-status claim (AD-7, AD-15).
- [ ] `passwordHash` appears in no response.
- [ ] The `Customer` role cannot be created through `POST /users`.
- [ ] Role gating is proven by tests that call the API as each role, **bypassing the UI**.
- [ ] `00-overview.md` updated with this story.

**Deferred, by design, and verified in the story named:**
`POST /auth/register` (Story 04) · agent department scoping on tickets (Story 05) · customer
isolation on tickets (Story 05 and Story 13) · the audit **read** surface (Story 16 Part B).

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 03.**
