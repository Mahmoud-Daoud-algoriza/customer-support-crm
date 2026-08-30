# API Design — Customer Support CRM

> **Source of truth:** [requirements.md](requirements.md) · [product-scope.md](product-scope.md) T1–T4, A-1…A-19 · [architecture.md](architecture.md) §2.1, §3, §4, §5, §6.3 · [data-model.md](data-model.md) (15 entities, DM-1…DM-7) · [story-backlog.md](story-backlog.md) and the 18 intakes
> **SDD stage:** 7 of 10. Gate 7 → 8 per [sdd-workflow.md](sdd-workflow.md) §4.
> **Status:** Contract design only. No application code, no controllers, no OpenAPI file, no UI.

**What this document decides:** the HTTP surface — resources, methods, request and response shapes,
validation, error semantics, status codes, pagination, filtering, sorting, and **the role gate on
every endpoint**.

**What it leaves to later stages:** screens and flows (stage 8), file-level plans (stage 9),
implementation (stage 10). The generated OpenAPI document is produced by the running application
(T2-L); this is its design, not its output.

**No business rule is invented here.** Every behaviour traces to a requirement, a scope item, an
assumption (A-*), or an approved decision (AD-*, DM-*, R-*). Where a *technical* choice was
required, it is recorded as an API decision (**AP-n**) in §3 with its rationale and rejected
alternative.

---

## 1. Coverage at a glance

| Module | Endpoints | Stories |
|---|---|---|
| Platform | 4 | 01 |
| Auth & identity | 3 | 02 |
| Users | 5 | 02 |
| Organization | 2 | 03 |
| Customers | 10 | 04 |
| Tickets (staff) | 17 | 05, 06, 07, 14 |
| Portal (customer) | 11 | 07, 13 |
| AI assists | 3 | 10, 11 |
| Knowledge base | 7 | 12 |
| Notifications | 2 | 09 |
| Reporting | 1 | 15 |
| Administration | 1 | 16 |
| **Total** | **66 distinct endpoints** | **all T1/T2 stories** |

The two portal knowledge-base endpoints are listed in both §5.7 and §5.9 for readability; they are
counted once here.

Stories 17 (i18n, responsive, branding) and 18 (channel/ERP seams) introduce **no endpoints** —
§10 explains why, and that is a design conclusion, not an omission.

---

## 2. Conventions

| Concern | Convention |
|---|---|
| **Base path** | `/api/v1` — a version prefix, and no versioning strategy beyond it (T2-L) |
| **Media type** | `application/json`; `multipart/form-data` for uploads only |
| **Auth** | `Authorization: Bearer <token>` (AD-7) |
| **Identifiers** | Opaque server-generated ids. Clients never construct one |
| **Timestamps** | RFC 3339, UTC, always `...Z`. The server never returns a local time |
| **Casing** | `camelCase` in JSON |
| **Errors** | Problem Details, RFC 9457 (AP-2) |
| **Collections** | Paged envelope (AP-3) |
| **Nulls** | Omitted rather than sent as `null`, except where null is meaningful (`assignedUserId`) |
| **Partial update** | `PATCH` with only the fields being changed |
| **Enums** | Sent as stable string codes (`New`, `Urgent`), never integers |

### 2.1 Pagination, filtering and sorting

Every collection endpoint accepts `page` (1-based, default 1) and `pageSize` (default 25, max 100)
and returns:

```json
{ "items": [ ... ], "page": 1, "pageSize": 25, "totalItems": 137, "totalPages": 6 }
```

Filters are query parameters named for the field they filter (`status`, `priority`,
`departmentId`). Repeating a parameter means OR within that field; different parameters AND
together. Sorting uses `sort=field:direction` (for example `sort=resolutionDueAt:asc`), restricted
to a per-endpoint whitelist (AP-15). An unknown filter or sort field is a `400`, never silently
ignored.

### 2.2 Status codes

| Code | Used for |
|---|---|
| `200` | Successful read, or a successful action returning the changed resource |
| `201` | Resource created; `Location` header set |
| `204` | Successful action with nothing to return |
| `400` | Malformed request, unknown filter/sort field, or a value outside a configured enumeration |
| `401` | Missing, invalid or expired token; or the resolved user is deactivated (§4.1) |
| `403` | Authenticated, role is known, and the role may not use this capability |
| `404` | Resource does not exist **or is outside the caller's scope** (AP-4) |
| `409` | State conflict: an illegal lifecycle transition, a duplicate email, a second feedback submission |
| `413` | Upload exceeds the configured size cap |
| `422` | Well-formed but semantically invalid (for example assigning to an out-of-department agent) |
| `503` | An integration seam is unavailable — used **only** by the AI endpoints (§8.1) |

---

## 3. API decisions

| # | Decision | Rationale | Rejected |
|---|---|---|---|
| **AP-1** | Resource-oriented HTTP with a small number of action sub-resources (`/transition`, `/escalate`, `/read`) | Lifecycle changes are *actions* with authority rules, not field writes. Modelling `status` as a PATCH field would let a caller bypass the A-16 matrix by writing a field | Pure CRUD with `status` as a writable field — makes the transition matrix unenforceable at the contract level |
| **AP-2** | Problem Details (RFC 9457) for every error, with a stable `type` slug per error class | [architecture.md](architecture.md) §2.1 already mandates uniform error translation; a machine-readable `type` lets the front end localize messages, which T2-J requires (the API returns codes, not display prose) | Ad-hoc error objects per endpoint |
| **AP-3** | One paged envelope for all collections, even short ones | Uniformity beats micro-optimization; the agent queue and audit log will both need it, and a client that handles one handles all | Bare arrays for "small" collections — guarantees a breaking change later |
| **AP-4** | **Out-of-scope resources return `404`, not `403`** | An agent probing ticket ids must not learn which ids exist in other departments. `403` confirms existence; `404` does not. `403` is reserved for capability denial the caller can already infer from their own role (A-4) | `403` everywhere — leaks existence across the department boundary that §4.3 exists to protect |
| **AP-5** | **Separate `/portal` path space for the Customer role** | Different scoping, different DTOs (no internal notes, no assignee identity), different authority. Two path spaces make the authorization reasoning explicit in the contract and match [architecture.md](architecture.md) §2.2's separate areas | One path space with role-varying payloads — the riskiest possible shape for the internal-note visibility rule (T2-C) |
| **AP-6** | One `POST /tickets/{id}/transition` carrying the target status, not a verb per transition | A-5 and A-16 are a *matrix*. One endpoint keeps the matrix in one place, returns one `409` shape for every illegal transition, and cannot drift verb-by-verb | `/resolve`, `/close`, `/cancel`… — the matrix would be scattered across seven endpoints |
| **AP-7** | Escalation is its **own** endpoint, not a transition | A-5: escalation is an action, **not** a status change. Putting it in `/transition` would contradict the model | Folding it into the transition endpoint |
| **AP-8** | **No logout endpoint** | Tokens are short-lived with no refresh rotation ([architecture.md](architecture.md) §4.1). A server endpoint that cannot revoke anything would be a lie in the contract; the client discards the token | A cosmetic `POST /auth/logout` returning `204` |
| **AP-9** | `GET /auth/me` returns the **per-request resolved** identity | AD-15 makes the user record authoritative. An endpoint that echoed token claims would model the exact staleness AD-15 removed | Decoding the token client-side and trusting it |
| **AP-10** | Server-derived fields are **never accepted** in a request body (§7) | The client cannot be trusted with `direction`, `departmentId` on portal submissions, `status`, SLA fields, or actor fields. Accepting-and-ignoring invites clients to believe they work | Accepting and ignoring them |
| **AP-11** | **No inbound-channel endpoint exists** | T3-A adapters are internal; the fake writes through the Tickets ingestion service in-process ([architecture.md](architecture.md) §5.2). Publishing an HTTP ingestion endpoint would need a system actor that `Ticket.createdByUserId` cannot express — **this is how PF-2 is avoided rather than papered over** | A `/channels/inbound` webhook — would force an undecided actor rule into the contract |
| **AP-12** | AI endpoints are **synchronous** and return `503` with a typed problem when the seam is unavailable | T1-F requires graceful degradation; a `503` on one assist while every other endpoint keeps working *is* the degradation, visible in the contract | Async job + polling — infrastructure with no requirement behind it |
| **AP-13** | Attachment upload is `multipart/form-data`; download streams through an authorized endpoint | [architecture.md](architecture.md) §4.4 requires download to pass the owner's authorization path, so no direct or guessable file URL is exposed | Returning a static file path or a pre-signed URL |
| **AP-14** | Suggested articles are a **Knowledge** endpoint, not an AI one | T2-E fixes them as keyword retrieval (AD-13). Putting them under `/ai` would imply generation | `/ai/suggested-solutions` |
| **AP-15** | Sort and filter fields are per-endpoint whitelists; unknown values are `400` | Prevents clients depending on incidental orderings, and keeps the indexes of [data-model.md](data-model.md) §6 sufficient | Free-form sorting on any field |
| **AP-17** | **Configuration is split into three audience tiers** — public, customer-safe, staff-only (§5.1) | Quick replies are agent content and SLA targets are internal policy; no requirement gives a customer either. A single authenticated `/config` would hand both to the Customer role, which A-4 includes | One `/config` for every authenticated caller — leaks staff content across the role boundary |
| **AP-18** | **Surface that traces to no requirement is not published** — `/auth/me/permissions` and `/notifications/read-all` were removed | Roles are four, fixed and hierarchical (A-4), so a client derives capability from the role `/auth/me` already returns; A-13 asks for a list and an unread badge, not a bulk action. Unrequested surface is scope creep dressed as convenience | Keeping them as conveniences — they would become contract obligations no story asked for |
| **AP-19** | **One download endpoint, `GET /attachments/{attachmentId}/content`, serves every role** — the single deliberate exception to AP-5's portal path split | AP-13 promised an authorized download and the catalogue never defined one, while story 04 requires a file to be "downloaded again". A byte stream has no DTO to vary by audience, and the authorization question is identical for both — *may this caller reach the owning ticket or customer?* — so a second portal endpoint would duplicate a rule rather than separate one. Out-of-scope or missing → `404` (AP-4) | Three parent-scoped download endpoints; or exposing `storagePath` and letting the client fetch the file, which [architecture.md](architecture.md) §4.4 forbids |
| **AP-16** | The customer's ticket DTO **omits** assignee identity | No requirement gives a customer the name of their agent. Omitting is the smaller surface; adding later is additive | Returning the assignee to the portal |

---

## 4. Authentication and authorization

### 4.1 Authentication

`Authorization: Bearer <token>`, obtained from `POST /auth/login`. The token **asserts identity
only** (A-9, AD-7, [architecture.md](architecture.md) §4.1.1) — it carries the user id and standard
issuance and expiry claims and nothing an authorization decision depends on.

**On every authenticated request** the server resolves role, department and active status from the
**authoritative user record**. A user that no longer exists or has been deactivated gets `401`,
not `403` — they have no valid identity, regardless of what their token says.

Anonymous endpoints, and there are exactly four: `GET /health`, `GET /config/bootstrap`,
`POST /auth/register`, `POST /auth/login`.

### 4.2 Authorization — three layers in the contract

| Layer | Expressed as | Failure |
|---|---|---|
| Role gate | The **Roles** column on every endpoint below | `403` |
| Scope | Department (agents), ownership (customers) — applied to every read and every write path | `404` (AP-4) |
| Domain legality | A-5 transition legality, A-16 authority, terminal-status rules | `409` |

Roles are the four fixed hierarchical roles of A-4 and are **hierarchical**: an endpoint marked
`Agent` is also reachable by Manager and Administrator unless a narrower rule is stated.

### 4.3 Department scoping — how it appears in the contract

Enforced server-side per [architecture.md](architecture.md) §4.3, never by a client-supplied
parameter:

- **Agent** — every ticket read and write is narrowed to their own department. `GET /tickets`
  returns only in-department tickets even with no filter; `GET /tickets/{id}` for an
  out-of-department ticket returns **`404`**.
- **Manager, Administrator** — all departments.
- **Customer** — `/portal` only, narrowed to tickets whose customer is themselves.
- A `departmentId` **query filter** narrows within what the caller may already see. It can never
  widen scope, and supplying another department's id is not an error — it simply matches nothing.
- A `departmentId` in a **request body** is data to validate, never an identity to trust
  ([architecture.md](architecture.md) §4.3 point 1).

### 4.4 Branch

Branch is a **reporting and filtering attribute only** (A-2, T2-K). It appears in exactly two
places: as a field on customer and user payloads, and as a **filter** on
`GET /reports/dashboard` and `GET /customers`. **No endpoint scopes by branch, and `Ticket` has no
branch field** — a ticket's branch is derived `Ticket → Customer → Branch` ([data-model.md](data-model.md)
§2.3), so the reports endpoint resolves it through the customer.

---

## 5. Endpoint catalogue

Roles use A-4's hierarchy. **Story** is the [story-backlog.md](story-backlog.md) sequence number.

### 5.1 Platform — story 01

| Method | Path | Roles | Purpose | Requirement |
|---|---|---|---|---|
| `GET` | `/health` | Anonymous | Liveness plus database reachability | §11.1, T2-L |
| `GET` | `/config/bootstrap` | Anonymous | Branding, product name, available languages — needed before sign-in (T3-E, T2-J) | §12 |
| `GET` | `/config` | Authenticated — **all roles** | **Customer-safe configuration only**: the category list and the feedback rating scale | §10.5 |
| `GET` | `/config/staff` | **Agent** | **Staff-only configuration**: priorities, quick replies, SLA targets, category → department map | §10.5 |

**Configuration is split by audience (AP-17).** Every configured value sits in exactly one of the
three tiers, and the tier is decided by who legitimately needs it:

| Tier | Endpoint | Audience | Values | Why |
|---|---|---|---|---|
| Public | `/config/bootstrap` | Anonymous | Branding, product name, languages | Needed to render the sign-in screen (T3-E, T2-J) |
| Customer-safe | `/config` | Any authenticated user | Category list, feedback rating scale | A customer picks a **category** when submitting (A-14) and needs the **rating scale** to render the feedback control (§5.7) |
| Staff-only | `/config/staff` | Agent and above | Priorities, quick replies, SLA targets, category → department map | Quick replies are the agent canned-response library (T1-C); SLA targets and the routing map are internal policy. **A customer has no requirement that needs any of these** — they do not set priority (A-6), do not choose a department (A-14), and the portal ticket payload carries no priority or SLA field (§6) |

A Customer calling `/config/staff` gets **`403`** — a capability denial they can infer from their
own role, so `403` is correct here and AP-4's `404` rule does not apply.

**No endpoint in this API writes configuration** — T2-I states that changing it is a redeploy.

### 5.2 Auth and identity — story 02

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `POST` | `/auth/register` | Anonymous | Customer self-registration (A-15) |
| `POST` | `/auth/login` | Anonymous | Exchange credentials for a token |
| `GET` | `/auth/me` | Authenticated | Resolved identity: id, displayName, role, departmentId, branchId, customerId (AP-9) |

**`POST /auth/register`** — request `{ email, password, fullName, phone? }`.

Per **A-15**, exactly three outcomes:

| Situation | Result |
|---|---|
| No `Customer` and no `User` for the email | `201` — creates `Customer` (with the **configured default branch**) and its `User` |
| A `Customer` profile exists (agent-created), no `User` | `201` — creates the `User` and **links it to the existing profile**. No duplicate customer |
| A `User` already exists for the email | `409` `type: user-already-exists` — **PF-6**, derived from the unique-email constraint, stated rather than left to inference |

The request **cannot** specify a branch, a role, or a customer id. Role is always `Customer`;
branch is always the configured default (A-15).

### 5.3 Users — story 02

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/users` | Administrator | Paged. Filters: `role`, `departmentId`, `isActive`, `q` |
| `POST` | `/users` | Administrator | Create a staff user: `{ email, password, displayName, role, departmentId, branchId? }` |
| `GET` | `/users/{id}` | Administrator | |
| `PATCH` | `/users/{id}` | Administrator | `displayName`, `role`, `departmentId`, `branchId` |
| `POST` | `/users/{id}/deactivate` | Administrator | `204`. The user's next request gets `401` (§4.1) |

**Validation.** A staff role requires `departmentId` and forbids `customerId`; the `Customer` role
cannot be created here at all (customers arrive through registration or an agent creating a
profile) — DM-1. Duplicate email → `409`.

### 5.4 Organization — story 03

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/departments` | Agent | List — for filters and assignment. Each item carries `id`, `name`, `managerUserId?` |
| `GET` | `/branches` | Agent | List — for filters and customer forms |

**No write endpoints.** Departments and branches are seeded and configured (T2-I,
`departments-branches` intake). **Customers never call these** — under A-14 a customer chooses a
category, never a department.

### 5.5 Customers — story 04

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/customers` | Agent | Paged. Filters: `q` (name/email), `branchId`. Sort: `fullName`, `createdAt` |
| `POST` | `/customers` | Agent | `{ fullName, email, phone?, branchId }` |
| `GET` | `/customers/{id}` | Agent | |
| `PATCH` | `/customers/{id}` | Agent | `fullName`, `phone`, `branchId` |
| `GET` | `/customers/{id}/timeline` | Agent | The §1.3 interaction history |
| `GET` | `/customers/{id}/notes` | Agent | Paged |
| `POST` | `/customers/{id}/notes` | Agent | `{ body }` — author and timestamp are server-set |
| `GET` | `/customers/{id}/attachments` | Agent | Metadata list (§6.7) |
| `POST` | `/customers/{id}/attachments` | Agent | `multipart/form-data` (AP-13) |
| `GET` | `/attachments/{attachmentId}/content` | Authenticated | **Download.** Streams the bytes; authorization resolves through the owning ticket or customer (AP-19) |

**`GET /customers/{id}/timeline`** is a **read projection** over that customer's tickets and ticket
activity ([data-model.md](data-model.md) §2.4) — it is not a stored entity, and it **excludes
internal notes** and any activity marked `Internal`. Customer notes are a separate collection and
do not appear in it.

**`email` is patchable.** No approved source makes it immutable — A-10 makes it the customer's
*identifier*, which is not the same thing, and an unfixable typo in a customer's email would be a
rule this project never agreed to. The validation that **is** supported by the model applies on
both create and update: email is required, must be a valid address, and is **unique across
customers, case-insensitively** ([data-model.md](data-model.md) §5 constraint 1) — a duplicate
is `409` `type: customer-email-in-use`. There is no merge or dedupe tooling (T2-A/§8), so a
duplicate is rejected rather than reconciled.

> **A customer's email and their portal sign-in are one address — A-19, closing OQ-5**
> *(decided 2026-08-27; this box previously recorded the open question).*
>
> A customer profile may have a linked portal login whose `User.email` is also unique and is the
> sign-in identifier (DM-1, A-15). **When `PATCH /customers/{id}` changes `Customer.email`, the
> linked `User.email` is updated to the same value, in the same unit of work** ([architecture.md](architecture.md)
> §3). There is no response in which one has changed and the other has not.
>
> | Situation | Result |
> |---|---|
> | The customer has **no** linked login | `200` — the profile changes. Nothing to propagate (DM-1) |
> | The customer **has** a linked login, and the new address is free | `200` — profile **and** login change together. The customer signs in with the new address; the old one stops working |
> | The new address already belongs to **another customer** | `409` `type: customer-email-in-use` — as before, unchanged |
> | The new address already belongs to **another user**, staff included | `409` `type: user-already-exists` — **PF-6's existing slug**, because it is PF-6's existing rule: `User.email` is unique case-insensitively across all users ([data-model.md](data-model.md) §5 constraint 1). **Neither row is written** |
>
> The two `409`s are distinct because the collisions are distinct, and a client already handles both
> slugs — `customer-email-in-use` from this endpoint, `user-already-exists` from `POST /auth/register`
> (§5.2). **No new problem type is introduced by this decision.**
>
> **The propagation is audited, and the audit is invisible in the contract.** Row 2 above — the only
> row that changes a login — also writes one `AuditEntry` with `action = UserEmailChanged` against
> the linked user, actor = the authenticated agent, in the same unit of work
> ([data-model.md](data-model.md) §2.14, §5 constraint 1b). **No response body changes, no field is
> added to `Customer` (§6.3), and no endpoint is added**: audit entries are read through
> `GET /audit` (§5.12, Administrator-only), which already filters by `action`. The other rows write
> no audit entry, because no login changed — and a rejected request writes nothing at all.
>
> **This changes no other endpoint.** `User.email` remains unpatchable through `PATCH /users/{id}`
> (§5.3) — the propagation is a server-side consequence of the customer patch, not a new writable
> field, and no request model gains an `email` property (AP-10). **The caller's session is
> unaffected**: the token asserts identity only and carries `sub`, never an email (AD-7), so a
> signed-in customer whose address changes mid-session is not signed out — they simply sign in with
> the new address next time.

### 5.6 Tickets (staff) — stories 05, 06, 07, 14

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/tickets` | Agent | The queue. Paged, department-scoped |
| `POST` | `/tickets` | Agent | Create on behalf of a customer |
| `GET` | `/tickets/{id}` | Agent | |
| `PATCH` | `/tickets/{id}` | Agent | `categoryCode`, `priority` only |
| `POST` | `/tickets/{id}/transition` | Agent | Lifecycle change (AP-6) |
| `POST` | `/tickets/{id}/escalate` | Agent | Escalation action (AP-7) |
| `POST` | `/tickets/{id}/assignment` | Agent | `{ assignedUserId }` — assign or reassign |
| `GET` | `/tickets/{id}/activity` | Agent | Full history, internal entries included |
| `GET` | `/tickets/{id}/messages` | Agent | Customer-visible thread |
| `POST` | `/tickets/{id}/messages` | Agent | `{ body }` — an outbound reply |
| `GET` | `/tickets/{id}/internal-notes` | Agent | **Staff only, by path** (AP-5) |
| `POST` | `/tickets/{id}/internal-notes` | Agent | `{ body }` |
| `GET`/`POST` | `/tickets/{id}/tasks` | Agent | `{ title, dueAt, assignedUserId }` |
| `PATCH` | `/tickets/{id}/tasks/{taskId}` | Agent | `{ isDone }` — `completedAt` is server-set |
| `GET`/`POST` | `/tickets/{id}/attachments` | Agent | AP-13 |

**Filters** on `GET /tickets`: `status`, `priority`, `categoryCode`, `assigneeId`, `departmentId`,
`breached`, `q`. **Sort whitelist**: `resolutionDueAt`, `firstResponseDueAt`, `createdAt`,
`priority`. **Default sort is SLA urgency** — `resolutionDueAt:asc` with breached tickets
first — which is what T1-C's queue requires; `assigneeId=me` produces the agent's own queue.

**`POST /tickets`** — request:

```json
{ "customerId": "...", "subject": "...", "description": "...",
  "categoryCode": "billing", "priority": "High", "departmentId": "..." }
```

`departmentId` is **optional**: omitted, it is derived from the category mapping (A-14); supplied,
it overrides, because A-14 makes the mapping a default and not a cage for agents. `isUrgent` is
**not accepted here** — it is customer input only (A-17), and an agent sets `priority` directly.

**`POST /tickets/{id}/transition`** — request `{ "targetStatus": "Resolved" }`. Returns `200` with
the updated ticket. Enforcement, in order: role gate (`403`) → scope (`404`) → **A-16 authority**
(`403`, `type: transition-not-permitted`) → **A-5 legality** (`409`,
`type: illegal-transition`, with `allowedTransitions` in the problem detail).

Legal transitions (A-5), and who may invoke each (A-16):

| From → To | Customer | Agent / Manager / Administrator |
|---|---|---|
| `New → Open` | ✖ directly — but their **reply** triggers it automatically from `Pending` only (§5.7) | ✔ |
| `Open → Pending` | ✖ | ✔ |
| `Pending → Open` | ✖ directly; **automatic on customer reply** (R-13) | ✔ |
| `Pending → Resolved`, `Open → Resolved` | ✖ | ✔ |
| `Resolved → Closed` | ✖ | ✔ (manual only — no timer, A-16) |
| `Resolved → Open` (reopen) | ✔ own | ✔ |
| any non-terminal `→ Cancelled` | ✔ own, **only while `New`** | ✔ |

`Closed` and `Cancelled` are terminal: any transition, message or note → `409`.

**`POST /tickets/{id}/escalate`** — no body. Raises priority exactly one level (`Urgent` stays
`Urgent`), leaves status unchanged, writes an `Escalated` activity entry, and notifies the
department manager — **or, when the department has none, the next authority level up** (**A-21**:
every active `Manager`, else every active `Administrator`, else nobody). Returns `200` with the
ticket **in every case** — the absence of a recipient never blocks the escalation and is never an
error. **The response is identical on every rung** and names no recipient; who was notified is
observable only through `GET /notifications`, which is recipient-scoped. Not available to customers
(A-16).

**`POST /tickets/{id}/assignment`** — assigning a user who is not an **active staff member of the
ticket's department** → `422` `type: assignee-out-of-department`. **Assignment does not change
status** (A-18): a `New` ticket stays `New`.

### 5.7 Portal (customer) — stories 07, 13

Every endpoint is Customer-only and scoped to the caller's own tickets. An id belonging to another
customer returns `404` (AP-4).

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/portal/tickets` | Own tickets. Filters: `status`. Sort: `createdAt` |
| `POST` | `/portal/tickets` | Submit — the web form (§3.5) |
| `GET` | `/portal/tickets/{id}` | Own ticket detail |
| `POST` | `/portal/tickets/{id}/transition` | `Cancelled` (while `New`) or `Open` (reopen a `Resolved`) |
| `GET` | `/portal/tickets/{id}/messages` | Own thread — **internal notes are unreachable by path** |
| `POST` | `/portal/tickets/{id}/messages` | Reply. **May transition the ticket — see below** |
| `GET`/`POST` | `/portal/tickets/{id}/attachments` | |
| `POST` | `/portal/tickets/{id}/feedback` | CSAT (§8.5) |
| `GET` | `/portal/kb/articles`, `/portal/kb/articles/{id}` | Public, published articles only (§8.4) |

**`POST /portal/tickets`** — request:

```json
{ "subject": "...", "description": "...", "categoryCode": "billing", "isUrgent": true }
```

- `customerId` is **not accepted** — it is the caller's own profile.
- `departmentId` is **not accepted** — derived from `categoryCode` (A-14).
- `priority` is **not accepted** — customers do not set priority (A-6). `isUrgent` is the boolean
  they may send (A-17); it does not set priority and is visible to agents and the AI suggestion.
- An unmapped or unknown `categoryCode` → `400`.

**`POST /portal/tickets/{id}/messages` — the one status side effect in this API.** Per **R-13**: if
the ticket is `Pending`, posting the reply transitions it to `Open` **in the same transaction**,
writing both a `MessagePosted` and a `StatusChanged` activity entry, the latter **attributed to the
replying customer** with `actorKind = User` (**R-14**). The rule fires from `Pending` **only** — a
reply on `New` leaves it `New`, and a reply on `Resolved` does **not** reopen it (reopening is the
explicit transition above). The response returns the created message **and the ticket's current
status**, so the client never has to guess whether the transition happened.

**`POST /portal/tickets/{id}/feedback`** — request `{ rating, comment? }`.
Preconditions: the ticket has reached `Resolved`; no feedback exists yet (a second submission →
`409`, `type: feedback-already-submitted`). Declining is simply never calling it — the absence of a
row is meaningful (T2-F), so there is no "declined" state to record.

> **`rating` and OQ-1.** The permitted values are **not fixed by this contract**. `GET /config`
> publishes `feedback.ratingScale` (`min`, `max`) and the server validates against it, returning
> `400` outside the range. This keeps the contract scale-agnostic while OQ-1 is open — but it
> **requires a `Feedback:RatingScale` key that [architecture.md](architecture.md) §6.3 does not yet
> have**. See §9, item 1 — flagged for approval, not silently added.

### 5.8 AI assists — stories 10, 11

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `POST` | `/tickets/{id}/ai/summary` | Agent | Summarize the thread (§7.1) |
| `POST` | `/tickets/{id}/ai/suggested-reply` | Agent | Draft a reply (§7.2) |
| `POST` | `/ai/classification-suggestion` | Agent | Suggest `categoryCode` and `priority` (§7.3) |

All three are `POST` because they perform work, not because they mutate: **none of them changes a
ticket.** The seam can only return suggestions (AD-12), and that is visible here — no AI endpoint
sends a message, changes a status, or assigns.

- Every response carries `"generatedBy": "ai"` so the UI can label it (A-8).
- `/ai/classification-suggestion` takes `{ subject, description, isUrgent? }` and is callable
  **before** a ticket exists, which is what "suggest at creation" (§7.3) requires.
- When the seam is unavailable: `503`, `type: ai-unavailable` (AP-12). Every other endpoint
  continues to work — that is T1-F's degradation requirement expressed as a contract.
- **Acceptance or override is recorded as ticket history** when the agent saves the ticket, not by
  these endpoints (DM-5). There is no AI persistence endpoint because there is no AI entity.

### 5.9 Knowledge base — story 12

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/kb/articles` | Agent | Search and browse **all** articles. Filters: `q`, `type`, `visibility`, `isPublished` |
| `GET` | `/kb/articles/{id}` | Agent | |
| `POST` | `/kb/articles` | Administrator | Authoring is Administrator-only (A-4) |
| `PATCH` | `/kb/articles/{id}` | Administrator | |
| `POST` | `/kb/articles/{id}/publish` | Administrator | |
| `POST` | `/kb/articles/{id}/unpublish` | Administrator | |
| `GET` | `/tickets/{id}/suggested-articles` | Agent | Keyword retrieval for this ticket (§7.4) |
| `GET` | `/portal/kb/articles`, `/portal/kb/articles/{id}` | Customer | **Public and published only** |

`q` searches title and body using the database's own text matching (AD-13). An `Internal` or
unpublished article is **`404`** on the portal paths — never `403`, which would confirm it exists.
`/tickets/{id}/suggested-articles` returns ordinary articles with a match score; it is deliberately
**not** under `/ai` (AP-14) because it retrieves rather than generates.

### 5.10 Notifications — story 09

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/notifications` | Authenticated | The caller's own. Filter `unreadOnly`; response includes `unreadCount` for the badge |
| `POST` | `/notifications/{id}/read` | Authenticated | `204` |

Recipient-scoped: a notification belonging to another user is `404`. Types are exactly the four of
A-13. **There is no create endpoint** — notifications are raised by the server (assignment, SLA
breach, escalation, customer reply), never by a client. **In-app only**; no delivery-channel or
preference endpoints exist (A-13, T3-A).

### 5.11 Reporting — story 15

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/reports/dashboard` | Manager | All four metric groups in one response. Filters: `departmentId`, `branchId` |

One endpoint, because T2-G is one dashboard with a fixed metric set — no report builder, no
scheduling, no export. Agents and Customers get `403`. Response groups: `ticketCounts` (by status,
priority, category, department), `slaAttainment` (met/breached percentage per priority),
`agentPerformance` (per agent), `satisfaction` (average rating, response count).

`branchId` filters through the customer (§4.4). Each group returns an explicit empty shape rather
than a misleading zero when there is no data — "no ratings submitted" is not "0.0 average".

> **`agentPerformance.assignedCount` and PF-4.** T2-G says "tickets assigned" without saying whether
> that means *currently assigned* or *ever assigned*. Both are computable and the response shape is
> identical. **This contract does not decide it** — the field is defined as "tickets assigned,
> per T2-G" and the semantics must be pinned before story 15 is implemented (§9, item 2).

### 5.12 Administration — story 16

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `GET` | `/audit` | Administrator | Paged. Filters: `actorUserId`, `action`, `from`, `to` |

Read-only, Administrator-only, `403` for everyone else. **No write, update or delete endpoint
exists** — the log is append-only by construction (T2-H), and the only writer is the server itself.
Distinct from `/tickets/{id}/activity`, which is the per-ticket trail for agents (AD-10).

---

## 6. Payload shapes

Every field below exists in [data-model.md](data-model.md). Nothing is invented; where a response
carries a **display name** alongside an id, that is a projection of `User.displayName` or
`Customer.fullName` so a screen can render a person without a second call — not a new stored field.

**Three fields never appear in any response, ever:** `passwordHash`, `storagePath`
([architecture.md](architecture.md) §4.4 — downloads go through §6.7's endpoint, never a path), and
any raw actor id where only a display name is needed.

### 6.1 Identity and access

**AuthToken** — `POST /auth/login`, `POST /auth/register` response

```json
{ "accessToken": "...", "expiresAt": "2026-08-25T18:00:00Z", "user": { … Identity … } }
```

**Identity** — `GET /auth/me`, and embedded above. The **per-request resolved** values (AP-9), not
token claims.

```json
{ "id": "...", "displayName": "...", "email": "...", "role": "Agent",
  "departmentId": "...", "branchId": "...", "customerId": null, "isActive": true }
```

`departmentId` is present for staff roles and null for `Customer`; `customerId` is the reverse
(DM-1). `isActive` is always `true` in a successful response — an inactive user gets `401` (§4.1).

**User** — `GET /users/{id}`, and the row shape of `GET /users`

```json
{ "id": "...", "email": "...", "displayName": "...", "role": "Agent",
  "departmentId": "...", "branchId": "...", "isActive": true, "createdAt": "..." }
```

**UserSummary** — embedded wherever a person is referenced: `{ "id": "...", "displayName": "..." }`.

### 6.2 Organization

**Department** — `{ "id": "...", "name": "...", "managerUserId": "..." }`. `managerUserId` may be
absent — a department need not have a manager ([data-model.md](data-model.md) §2.2). **When it is
absent, escalation notifies the next authority level** ([product-scope.md](product-scope.md)
**A-21**, closing OQ-3 on 2026-08-31); **the payload is unchanged by that decision** and names no
recipient.

**Branch** — `{ "id": "...", "name": "..." }`.

### 6.3 Customers

**Customer** — `GET /customers/{id}`

```json
{ "id": "...", "fullName": "...", "email": "...", "phone": "...",
  "branch": { "id": "...", "name": "..." },
  "externalReference": null, "createdAt": "..." }
```

`externalReference` is the ERP seam field (DM-6) — read-only, unused by default, never settable.

**CustomerListItem** — the row shape of `GET /customers`

```json
{ "id": "...", "fullName": "...", "email": "...", "phone": "...",
  "branch": { "id": "...", "name": "..." }, "openTicketCount": 3 }
```

> `openTicketCount` is an aggregate over that customer's non-terminal tickets. It is not a stored
> field; it is here because [ui-design.md](ui-design.md) §5.4 specifies it on the customer
> directory, and computing it in the client would mean one query per row.

**CustomerNote** — `{ "id": "...", "author": { UserSummary }, "body": "...", "createdAt": "..." }`.
Immutable once written ([data-model.md](data-model.md) §2.5), so no `updatedAt` exists.

**TimelineEntry** — `GET /customers/{id}/timeline`. A **read projection** over the customer's
tickets and ticket activity (§1.3, [data-model.md](data-model.md) §2.4), never a stored row:

```json
{ "occurredAt": "...", "ticketId": "...", "ticketSubject": "...",
  "activityType": "StatusChanged", "actorKind": "User",
  "actor": { UserSummary }, "oldValue": "New", "newValue": "Open" }
```

**Internal entries are excluded from this projection** — the endpoint never returns an entry whose
visibility is `Internal`, and customer notes are a separate collection that does not appear here.

### 6.4 Tickets

**Ticket (staff)** — `GET /tickets/{id}`

```json
{ "id": "...", "subject": "...", "description": "...",
  "customer": { "id": "...", "fullName": "...", "email": "..." },
  "departmentId": "...", "categoryCode": "billing", "priority": "High",
  "status": "Open", "isUrgent": true,
  "assignee": { UserSummary } | null, "createdBy": { UserSummary }, "createdAt": "...",
  "firstResponseDueAt": "...", "resolutionDueAt": "...",
  "firstRespondedAt": null, "resolvedAt": null, "closedAt": null,
  "firstResponseBreached": false, "resolutionBreached": false }
```

`assignee` is null until assignment — **and may be non-null while `status` is `New`**, because
assignment is not the start of work (A-18). `firstRespondedAt` stays null until the first outbound
message and may remain null on a resolved ticket (**PF-5**).

**TicketListItem** — the row shape of `GET /tickets`. Everything the queue renders
([ui-design.md](ui-design.md) §5.1) and nothing more:

```json
{ "id": "...", "subject": "...", "customer": { "id": "...", "fullName": "..." },
  "status": "Open", "priority": "High", "categoryCode": "billing",
  "departmentId": "...", "assignee": { UserSummary } | null, "createdAt": "...",
  "resolutionDueAt": "...", "firstResponseBreached": false, "resolutionBreached": false }
```

**Ticket (portal)** — `GET /portal/tickets/{id}` and the row shape of `GET /portal/tickets`

```json
{ "id": "...", "subject": "...", "description": "...", "categoryCode": "billing",
  "status": "Pending", "isUrgent": true, "createdAt": "...", "resolvedAt": null,
  "hasFeedback": false }
```

**No** assignee (AP-16), **no** department, **no** priority, **no** SLA or breach fields, **no**
internal anything. `hasFeedback` is computed from the existence of a feedback row (§7).

**Message** — `{ "id", "ticketId", "author": { UserSummary }, "authorRole", "direction",
"channel", "body", "postedAt" }`. The portal variant omits `channel` and `authorRole`, keeping
`direction` so the thread can distinguish the two sides.

**`POST /portal/tickets/{id}/messages` response** — the created message **plus the ticket's current
status**, because posting it may have transitioned the ticket (R-13):

```json
{ "message": { … Message … }, "ticketStatus": "Open", "statusChanged": true }
```

`statusChanged` is true only when the automatic `Pending → Open` fired. The client never has to
guess, and never has to re-fetch to find out.

**InternalNote** — `{ "id", "ticketId", "author": { UserSummary }, "body", "createdAt" }`.
Returned **only** by `/tickets/{id}/internal-notes`, which no portal path reaches (T2-C, AP-5).

**Activity entry** — `GET /tickets/{id}/activity`

```json
{ "id": "...", "occurredAt": "...", "activityType": "StatusChanged",
  "actorKind": "User", "actor": { UserSummary } | null,
  "oldValue": "Pending", "newValue": "Open", "visibility": "CustomerVisible",
  "messageId": null, "internalNoteId": null }
```

`actor` is null exactly when `actorKind` is `System` — which is the SLA monitor only. The automatic
`Pending → Open` carries `actorKind: "User"` and the **replying customer** as `actor` (**R-14**).

**Task** — `{ "id", "ticketId", "title", "dueAt", "assignee": { UserSummary }, "isDone",
"completedAt", "createdBy": { UserSummary }, "createdAt" }`.

**Feedback** — `{ "id", "ticketId", "rating", "comment", "submittedAt" }`. `rating` is an ordinal
whose permitted range comes from configuration; **this contract fixes no range** (**OQ-1**).

### 6.5 Knowledge base

**Article** — `GET /kb/articles/{id}`

```json
{ "id": "...", "title": "...", "body": "...", "type": "Faq",
  "visibility": "Public", "isPublished": true,
  "author": { UserSummary }, "createdAt": "...", "updatedAt": "..." }
```

**ArticleListItem** — `{ "id", "title", "type", "visibility", "isPublished", "updatedAt" }`. No
body, so a list does not ship every article's full text.

**Portal article** — `{ "id", "title", "body", "type", "updatedAt" }`. No `visibility`, no
`isPublished`, no author: the portal only ever receives public, published articles, so returning
those fields would state the obvious and leak the taxonomy.

**SuggestedArticle** — `GET /tickets/{id}/suggested-articles` —
`{ "id", "title", "type", "matchScore" }`. `matchScore` is the database's own text-match ranking
(AD-13), exposed so a screen can order results; it is a query artefact, not a stored field.

### 6.6 Notifications

**Notification** — `{ "id", "type", "ticketId", "ticketSubject", "createdAt", "readAt" }`.
`type` is one of the four of A-13. `ticketSubject` is projected so a list row is readable without a
call per notification.

**`GET /notifications` envelope** — the standard paged envelope plus `"unreadCount": 3` at the top
level, which is what the shell's badge renders.

### 6.7 Attachments

**AttachmentMetadata** — returned by every attachment list and by a successful upload

```json
{ "id": "...", "fileName": "...", "contentType": "application/pdf",
  "sizeBytes": 184320, "uploadedBy": { UserSummary }, "uploadedAt": "..." }
```

**`storagePath` is never returned.** The bytes come from `GET /attachments/{attachmentId}/content`
(AP-19), which authorizes through the owning ticket or customer and returns `404` when the caller
cannot reach it. The response is the file stream with `Content-Type` and a `Content-Disposition`
filename; there is no JSON body.

### 6.8 Reporting

**DashboardReport** — `GET /reports/dashboard`. Four groups, matching T2-G exactly:

```json
{ "ticketCounts": {
    "byStatus":     [ { "key": "Open",    "count": 42 } ],
    "byPriority":   [ { "key": "High",    "count": 11 } ],
    "byCategory":   [ { "key": "billing", "count": 30 } ],
    "byDepartment": [ { "key": "<departmentId>", "name": "Billing", "count": 30 } ] },
  "slaAttainment": [ { "priority": "High", "met": 18, "breached": 3, "attainmentPercent": 85.7 } ],
  "agentPerformance": [ { "agent": { UserSummary }, "assignedCount": 12,
                          "resolvedCount": 9, "averageResolutionHours": 6.4 } ],
  "satisfaction": { "averageRating": 4.2, "responseCount": 17 } }
```

**Empty is not zero.** When no ratings exist, `satisfaction` returns
`{ "averageRating": null, "responseCount": 0 }` — never `0.0`, which would read as universal
dissatisfaction. The same rule applies to `averageResolutionHours` for an agent who has resolved
nothing.

> `assignedCount` is deliberately unqualified — whether "assigned" means currently or ever assigned
> is **PF-4**, still undecided. The field name and shape are stable either way.

### 6.9 Administration and configuration

**AuditEntry** — `GET /audit`

```json
{ "id": "...", "occurredAt": "...", "actor": { UserSummary } | null,
  "actorDescriptor": "someone@example.com", "action": "SignInFailed",
  "targetType": "User", "targetId": "...", "outcome": "Failure" }
```

`actor` is null when no user could be resolved — a failed sign-in — and `actorDescriptor` then
carries the submitted identifier ([data-model.md](data-model.md) §2.14).

**BootstrapConfig** — `GET /config/bootstrap`, anonymous

```json
{ "productName": "...", "logoUrl": "...", "primaryColor": "#0B5FFF",
  "languages": ["en", "ar"], "defaultLanguage": "en" }
```

**CustomerConfig** — `GET /config`, every authenticated role

```json
{ "categories": [ { "code": "billing", "name": "Billing" } ],
  "feedback": { "ratingScale": { "min": 1, "max": 5 } } }
```

> The `ratingScale` **values shown are illustrative placeholders, not a decision.** They come from
> the `Feedback rating scale` configuration key ([architecture.md](architecture.md) §6.3) and
> **OQ-1 is still open** — no scale is fixed by this contract.

**StaffConfig** — `GET /config/staff`, Agent and above

```json
{ "priorities": ["Low", "Medium", "High", "Urgent"],
  "quickReplies": [ { "id": "...", "title": "...", "body": "..." } ],
  "slaTargets": [ { "priority": "High", "firstResponseHours": 4, "resolutionHours": 24 } ],
  "categoryDepartmentMap": [ { "categoryCode": "billing", "departmentId": "..." } ] }
```

The SLA hour values are illustrative; the real ones are configuration (A-3).

### 6.10 AI assists

All three return a result plus the label A-8 requires, and none of them mutates a ticket (AD-12):

- **Summary** — `{ "summary": "...", "generatedBy": "ai", "generatedAt": "..." }`
- **Suggested reply** — `{ "draft": "...", "generatedBy": "ai", "generatedAt": "..." }`
- **Classification suggestion** — `{ "categoryCode": "billing", "priority": "High",
  "generatedBy": "ai", "generatedAt": "..." }`

A suggested `categoryCode` outside the configured list is rejected server-side before it is
returned, so a client never receives an unusable suggestion.

### 6.11 Request bodies not stated elsewhere

- **`POST /auth/login`** — `{ "email": "...", "password": "..." }` → **AuthToken**. A wrong
  credential is `401` with `type: invalid-credentials`; a deactivated user gets the same response,
  because distinguishing them would confirm which emails have accounts.
- **`POST /kb/articles`** — `{ "title", "body", "type", "visibility", "isPublished"? }`.
  `isPublished` defaults to false, so an article is drafted before it is visible. `author` is the
  authenticated Administrator, never supplied.
- **`PATCH /kb/articles/{id}`** — any of `{ "title", "body", "type", "visibility" }`.
  **`isPublished` is not patchable here** — publishing is the dedicated `/publish` and `/unpublish`
  action pair (AP-1), so publication state changes through one path only.

### 6.12 Problem Details

`{ "type", "title", "status", "detail", "instance", "errors"? }`, where `type` is a stable slug:
`illegal-transition`, `transition-not-permitted`, `assignee-out-of-department`,
`feedback-already-submitted`, `user-already-exists`, `customer-email-in-use`,
`invalid-credentials`, `ai-unavailable`, `attachment-too-large`. `illegal-transition` additionally
carries `allowedTransitions`.

The front end renders a **translated** string chosen by `type`; the server's `detail` is never shown
raw (T2-J, [ui-design.md](ui-design.md) §9).

---

## 7. Server-derived fields — never accepted from a client

Requested explicitly, and each one is enforced by **omission from the request model**, not by
accepting and ignoring (AP-10). A request containing one is `400`, so a client is never misled into
thinking it worked.

| Field | Derived from |
|---|---|
| `TicketMessage.direction` | The author's role — customer → `Inbound`, agent → `Outbound` (**PF-7**) |
| `TicketMessage.channel` | The endpoint used: `WebForm` for portal creation, `Portal` for portal replies (T2-B) |
| `Ticket.departmentId` (portal) | The category mapping (A-14) |
| `Ticket.status` | Transition endpoints only, never a PATCH field (AP-1) |
| `Ticket.isUrgent` (staff create) | Not accepted — customer input only (A-17) |
| `Ticket.customerId` (portal) | The authenticated caller's profile |
| SLA due dates and breach flags | Server computation from `createdAt` and priority (A-3) |
| `firstRespondedAt`, `resolvedAt`, `closedAt` | Lifecycle side effects |
| `createdByUserId`, `authorUserId`, `uploadedByUserId`, activity actors | The authenticated caller |
| `Customer.branchId` (registration) | The configured default branch (A-15) |
| `User.role`, `User.departmentId` (self) | Administrator-only, never self-set |
| Any activity or audit entry | Written by the server; no create endpoint exists |
| `hasFeedback` (portal ticket) | Computed from the existence of a `CustomerFeedback` row — a response projection, not a stored field |

---

## 8. Integration seams in the contract

### 8.1 AI (T1-F, T3-C)

Three endpoints (§5.8), all behind the single Application-owned abstraction
([architecture.md](architecture.md) §5.1). The contract never names a provider, exposes a model, or
accepts provider parameters — so the provider stays an open question (§9 of the scope) without the
API caring. Unavailability is `503`; the chatbot (T3-C) has **no endpoint** because it is not built.

### 8.2 Communication channels (T2-B real, T3-A future)

The only real channels are the web form and portal messaging, and both are ordinary endpoints
(§5.7). **`channel` is a field on every message from day one** — that is the seam. There is **no
inbound endpoint and no adapter-management endpoint** (AP-11): adapters are internal, and
publishing an ingestion endpoint would force the undecided system-actor question of **PF-2** into
the contract. Adding email or WhatsApp later adds an adapter, not an endpoint.

### 8.3 ERP (T3-D)

**No endpoints.** The gateway is an internal outbound interface with a no-op implementation. The
only trace in this API is `externalReference` on the customer payload — read-only, unused by
default, and not settable through any endpoint.

### 8.4 Live chat (T3-B)

**No endpoints, no WebSocket, no long-poll.** Portal messaging is request/response. Nothing in this
contract is described as real-time.

---

## 9. Findings carried into this document

The pre-flight's non-blocking findings, each handled explicitly rather than becoming a silent
assumption:

| Finding | How this contract handles it |
|---|---|
| **PF-2** system actor for inbound tickets | **Avoided by design** — AP-11 publishes no ingestion endpoint, so no contract needs an actor the model cannot express. The gap remains real for story 18 and is unchanged by this document |
| **PF-3** CSAT scale has no config key | ✅ **Closed 2026-08-25.** `rating` validates against `feedback.ratingScale` from `GET /config`, and the `Feedback rating scale` key is now an approved entry in [architecture.md](architecture.md) §6.3 — **values still undecided (OQ-1)** |
| **PF-4** "tickets assigned" undefined | Field named and shaped; **semantics explicitly not decided here** (§5.11) |
| **PF-5** `firstRespondedAt` never set without a reply | Exposed as-is on the staff DTO, including `null`. No contract change; the behaviour question stays open for story 09 |
| **PF-6** registration when a `User` exists | Now stated as `409 user-already-exists` (§5.2) rather than left to inference |
| **PF-7** message `direction` | Server-derived, absent from every request model (§7) |

**Post-flight corrections applied 2026-08-25.** The Stage 7 post-flight review found two blocking
defects in this document's first version; both are closed:

- **B-1** — the `Feedback:RatingScale` key now exists in [architecture.md](architecture.md) §6.3,
  approved with its **values left undecided** because OQ-1 is still open.
- **B-2** — `GET /config` returned quick replies and SLA targets to every authenticated caller,
  including customers. Configuration is now split into three audience tiers (§5.1, AP-17).

Also corrected: the unsupported claim that a customer's email is immutable (§5.5, N-2), and two
endpoints that traced to no requirement were removed (AP-18).

**Follow-ups still open, none blocking Stage 8:**

1. ~~**Response shapes**~~ — **closed 2026-08-25.** §6 is now a full payload catalogue: every
   resource type the API returns has a shape, and the three missing request bodies are stated.
   Closing it surfaced a real contradiction — AP-13 promised an authorized attachment download
   that the catalogue never defined, while story 04 requires one. `GET
   /attachments/{attachmentId}/content` was added (AP-19), taking the count 65 → 66.
2. **PF-4's metric semantics** must be pinned before story 15 is planned.
3. ~~**OQ-5**~~ — **closed 2026-08-27 by A-19.** Changing `Customer.email` **does** change a linked
   portal login's sign-in email, atomically, with `User.email`'s existing uniqueness rule applied to
   the new value (§5.5). Closing it also removed a reachable gap in §5.2: had the two been allowed
   to diverge, a profile could hold an address whose registration outcome none of A-15's three rows
   covered — a profile matching the submitted email while already holding a login under a different
   one, which §5 constraint 3 forbids linking a second login to. Divergence is now impossible, so
   the three outcomes remain exhaustive.

**No new contradiction was found while writing this document.** Every rule needed was already
present in an approved source.

---

## 10. Traceability

### 10.1 Story → endpoints

| Story | Endpoints |
|---|---|
| 01 `solution-skeleton` | `/health`, `/config/bootstrap`, `/config`, `/config/staff` |
| 02 `auth-and-roles` | `/auth/*` (3), `/users/*` (5) |
| 03 `departments-branches` | `/departments`, `/branches` |
| 04 `customer-records` | `/customers/*` (9), `/attachments/{id}/content` |
| 05 `ticket-core` | `GET/POST /tickets`, `GET/PATCH /tickets/{id}`, `/assignment` |
| 06 `ticket-lifecycle` | `/transition`, `/escalate`, `/activity` |
| 07 `ticket-intake-messaging` | `/tickets/{id}/messages`, `/portal/tickets`, `/portal/tickets/{id}/messages` |
| 08 `agent-dashboard` | `GET /tickets` with `assigneeId=me` + SLA-urgency default sort; `/config/staff` quick replies; `/customers/{id}` |
| 09 `sla-routing-escalation` | `/notifications/*` (2); SLA fields on the ticket payload |
| 10 `ai-service-seam` | — infrastructure behind §5.8 |
| 11 `ai-ticket-assists` | `/ai/*` (3) |
| 12 `kb-articles-search` | `/kb/*` (6), `/tickets/{id}/suggested-articles`, `/portal/kb/*` |
| 13 `portal-self-service` | `/portal/*` (9) |
| 14 `tasks-internal-notes` | `/tickets/{id}/tasks*`, `/tickets/{id}/internal-notes` |
| 15 `management-dashboard` | `/reports/dashboard` |
| 16 `audit-configuration` | `/audit`, `/config`, `/config/staff` |
| 17 `i18n-responsive-branding` | **None** — front-end concern; branding and languages ride on `/config/bootstrap` |
| 18 `channel-erp-adapters` | **None** — internal seams by AP-11 and §8.3 |

Stories 10, 17 and 18 introducing no endpoints is a **conclusion**, stated here so a reader can see
it was decided rather than forgotten.

### 10.2 Requirement → endpoints

| Req | Endpoints |
|---|---|
| §1 Customers, notes, attachments, history | `/customers/*` |
| §2 Tickets, categories, assignment, lifecycle, history | `/tickets/*` |
| §3.3, §3.5 Portal messaging, web form | `/portal/tickets*`, `/tickets/{id}/messages` |
| §3.1, §3.2, §3.4 Email, WhatsApp, SMS | None — seam (§8.2) |
| §4 Queue, customer context, quick replies, tasks, notes | `GET /tickets`, `/config/staff` (quick replies), `/tasks`, `/internal-notes` |
| §5 SLA targets, assignment, escalation, alerts | Ticket SLA fields, `/assignment`, `/escalate`, `/notifications` |
| §6 KB and search | `/kb/*` |
| §7.1–7.3 AI assists | `/ai/*` |
| §7.4 Suggested solutions | `/tickets/{id}/suggested-articles` |
| §7.5 Chatbot | None — not built (T3-C) |
| §8 Portal | `/portal/*` |
| §9 Reports | `/reports/dashboard` |
| §10 Users, roles, permissions, audit, configuration | `/users/*`, `/audit`, `/config`, `/config/staff` |
| §11.1 API | This document; OpenAPI is generated by the app (T2-L) |
| §11.2, §11.4 ERP, external systems | None — seam (§8.3) |
| §12 Language, branding | `/config/bootstrap` |
| §12 Multi-department, multi-branch | Scoping (§4.3) and filters (§4.4) |

### 10.3 Decision → contract

A-2 → §4.3, §4.4 · A-3 → SLA fields, §7 · A-4 → every **Roles** column · A-5 → §5.6 transition
table · A-6 → `categoryCode`, `priority` validation · A-8/AD-12 → §5.8 · A-9/AD-7/AD-15 → §4.1,
AP-9 · A-10 → email **uniqueness**, `409` (the immutability this line once claimed was removed by
N-2 on 2026-08-25; §5.5 has said `email` is patchable ever since) · A-13 → §5.10 ·
A-14 → `POST /portal/tickets` · A-15 → §5.2 · A-16 → §5.6 authority table · A-17 → `isUrgent` ·
A-18 → assignment leaves status · A-19 → §5.5 customer email propagates to the linked login ·
AD-13 → §5.9 · DM-1 → §5.2, §5.3 · DM-5 → §5.8 · DM-7 → feedback under Tickets · R-13/R-14 → §5.7

---

## 11. Stage 7 gate check

Gate 7 → 8 from [sdd-workflow.md](sdd-workflow.md) §4: *"`docs/api-design.md` gives a contract for
each capability the stories need, with role-based access stated per endpoint (A-4), and matches the
data model exactly."*

| Condition | Status |
|---|---|
| A contract for each capability the stories need | ✅ 66 distinct endpoints; all 16 endpoint-bearing stories covered; the three that introduce none are justified in §10.1 |
| Role-based access stated per endpoint (A-4) | ✅ Every row in §5 carries a **Roles** column; §4.2 defines the three layers and their failure codes |
| Matches the data model exactly | ✅ Every payload field in §6 exists in [data-model.md](data-model.md); no field invented, no entity implied that the model lacks |
| Department scoping enforced server-side | ✅ §4.3, with `404` (AP-4) so the boundary does not leak existence |
| Branch derivation respected | ✅ §4.4 — filter only, resolved through the customer; no ticket branch field |
| Lifecycle and transition authority respected | ✅ §5.6 reproduces A-5 legality and A-16 authority, with distinct `409`/`403` semantics |
| Automatic `Pending → Open` on customer reply | ✅ §5.7, including the R-14 actor attribution and the from-`Pending`-only scope |
| Request/response models, validation, errors, pagination, filtering, sorting, status codes | ✅ §2, §6, and per-endpoint notes |
| Server-derived fields not client-settable | ✅ §7, thirteen field classes, enforced by omission |
| Seams respected | ✅ §8 — no provider, adapter, or ERP surface exposed |
| PF-2…PF-7 handled without silent assumptions | ✅ §9 |
| No business rule invented | ✅ §3 records 16 **technical** decisions; every behavioural rule cites a source |

**Gate 7 → 8 is met**, with two follow-ups recorded in §9 that need approval before they are
applied to an approved document.

**Next stage:** 8 — UI Design (`docs/ui-design.md`). Not started.
