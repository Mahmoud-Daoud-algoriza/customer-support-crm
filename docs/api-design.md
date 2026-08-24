# API Design — Customer Support CRM

> **Source of truth:** [requirements.md](requirements.md) · [product-scope.md](product-scope.md) T1–T4, A-1…A-18 · [architecture.md](architecture.md) §2.1, §3, §4, §5, §6.3 · [data-model.md](data-model.md) (15 entities, DM-1…DM-7) · [story-backlog.md](story-backlog.md) and the 18 intakes
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
| Platform | 3 | 01 |
| Auth & identity | 4 | 02 |
| Users | 5 | 02 |
| Organization | 2 | 03 |
| Customers | 9 | 04 |
| Tickets (staff) | 17 | 05, 06, 07, 14 |
| Portal (customer) | 11 | 07, 13 |
| AI assists | 3 | 10, 11 |
| Knowledge base | 7 | 12 |
| Notifications | 3 | 09 |
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
| `GET` | `/config/bootstrap` | Anonymous | Branding, available languages, product name — needed before sign-in (T3-E, T2-J) | §12 |
| `GET` | `/config` | Authenticated | Read-only effective configuration: categories, priorities, quick replies, SLA targets, feedback scale. **Read-only by design** (T2-I) | §10.5 |

`GET /config` never writes. There is no configuration write endpoint anywhere in this API — T2-I
states that changing configuration is a redeploy.

### 5.2 Auth and identity — story 02

| Method | Path | Roles | Purpose |
|---|---|---|---|
| `POST` | `/auth/register` | Anonymous | Customer self-registration (A-15) |
| `POST` | `/auth/login` | Anonymous | Exchange credentials for a token |
| `GET` | `/auth/me` | Authenticated | Resolved identity: id, displayName, role, departmentId, branchId, customerId (AP-9) |
| `GET` | `/auth/me/permissions` | Authenticated | The caller's capability flags, so the front end can hide what it must not show — **a convenience, never an enforcement point** ([architecture.md](architecture.md) §2.2) |

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
| `GET` | `/customers/{id}/attachments` | Agent | |
| `POST` | `/customers/{id}/attachments` | Agent | `multipart/form-data` (AP-13) |

**`GET /customers/{id}/timeline`** is a **read projection** over that customer's tickets and ticket
activity ([data-model.md](data-model.md) §2.4) — it is not a stored entity, and it **excludes
internal notes** and any activity marked `Internal`. Customer notes are a separate collection and
do not appear in it.

`email` is **not** patchable: A-10 makes it the customer's identifier and there is no merge or
dedupe tooling (T2-A/§8). Creating a customer with a duplicate email → `409`.

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
`Urgent`), leaves status unchanged, writes an `Escalated` activity entry, notifies the department
manager. Returns `200` with the ticket. Not available to customers (A-16).

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
| `POST` | `/notifications/read-all` | Authenticated | `204` |

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

## 6. Core payload shapes

Illustrative field lists, matching [data-model.md](data-model.md) exactly. Not schemas.

**Ticket (staff)**

```json
{ "id": "...", "subject": "...", "description": "...",
  "customer": { "id": "...", "fullName": "...", "email": "..." },
  "departmentId": "...", "categoryCode": "billing", "priority": "High",
  "status": "Open", "isUrgent": true,
  "assignedUserId": "...", "createdByUserId": "...", "createdAt": "...",
  "firstResponseDueAt": "...", "resolutionDueAt": "...",
  "firstRespondedAt": null, "resolvedAt": null, "closedAt": null,
  "firstResponseBreached": false, "resolutionBreached": false }
```

**Ticket (portal)** — the same ticket, narrowed: `id`, `subject`, `description`, `categoryCode`,
`status`, `isUrgent`, `createdAt`, `resolvedAt`, `hasFeedback`. **No** assignee (AP-16), **no**
department, **no** SLA or breach fields, **no** internal anything.

**Message** — `{ id, ticketId, authorDisplayName, authorRole, direction, channel, body, postedAt }`.
`direction` and `channel` are **server-derived and read-only** (§7).

**Activity entry** — `{ id, occurredAt, activityType, actorKind, actorDisplayName?, oldValue?,
newValue?, visibility, messageId?, internalNoteId? }`.

**Problem Details** — `{ type, title, status, detail, instance, errors? }`, where `type` is a
stable slug such as `illegal-transition`, `transition-not-permitted`, `assignee-out-of-department`,
`feedback-already-submitted`, `user-already-exists`, `ai-unavailable`, `attachment-too-large`.

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
| **PF-3** CSAT scale has no config key | `rating` is validated against `feedback.ratingScale` from `GET /config`. **Requires adding `Feedback:RatingScale` to [architecture.md](architecture.md) §6.3 — flagged below, not applied** |
| **PF-4** "tickets assigned" undefined | Field named and shaped; **semantics explicitly not decided here** (§5.11) |
| **PF-5** `firstRespondedAt` never set without a reply | Exposed as-is on the staff DTO, including `null`. No contract change; the behaviour question stays open for story 09 |
| **PF-6** registration when a `User` exists | Now stated as `409 user-already-exists` (§5.2) rather than left to inference |
| **PF-7** message `direction` | Server-derived, absent from every request model (§7) |

**Two follow-ups this document requires, neither applied here:**

1. **[architecture.md](architecture.md) §6.3 needs a `Feedback:RatingScale` configuration key**, so
   `GET /config` can publish it. This is a consequence of AP-9's design choice, and it touches an
   approved document — raised for approval rather than edited.
2. **PF-4's metric semantics** must be pinned before story 15 is planned.

**No new contradiction was found while writing this document.** Every rule needed was already
present in an approved source.

---

## 10. Traceability

### 10.1 Story → endpoints

| Story | Endpoints |
|---|---|
| 01 `solution-skeleton` | `/health`, `/config`, `/config/bootstrap` |
| 02 `auth-and-roles` | `/auth/*` (4), `/users/*` (5) |
| 03 `departments-branches` | `/departments`, `/branches` |
| 04 `customer-records` | `/customers/*` (9) |
| 05 `ticket-core` | `GET/POST /tickets`, `GET/PATCH /tickets/{id}`, `/assignment` |
| 06 `ticket-lifecycle` | `/transition`, `/escalate`, `/activity` |
| 07 `ticket-intake-messaging` | `/tickets/{id}/messages`, `/portal/tickets`, `/portal/tickets/{id}/messages` |
| 08 `agent-dashboard` | `GET /tickets` with `assigneeId=me` + SLA-urgency default sort; `/config` quick replies; `/customers/{id}` |
| 09 `sla-routing-escalation` | `/notifications/*` (3); SLA fields on the ticket payload |
| 10 `ai-service-seam` | — infrastructure behind §5.8 |
| 11 `ai-ticket-assists` | `/ai/*` (3) |
| 12 `kb-articles-search` | `/kb/*` (6), `/tickets/{id}/suggested-articles`, `/portal/kb/*` |
| 13 `portal-self-service` | `/portal/*` (9) |
| 14 `tasks-internal-notes` | `/tickets/{id}/tasks*`, `/tickets/{id}/internal-notes` |
| 15 `management-dashboard` | `/reports/dashboard` |
| 16 `audit-configuration` | `/audit`, `/config` |
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
| §4 Queue, customer context, quick replies, tasks, notes | `GET /tickets`, `/config`, `/tasks`, `/internal-notes` |
| §5 SLA targets, assignment, escalation, alerts | Ticket SLA fields, `/assignment`, `/escalate`, `/notifications` |
| §6 KB and search | `/kb/*` |
| §7.1–7.3 AI assists | `/ai/*` |
| §7.4 Suggested solutions | `/tickets/{id}/suggested-articles` |
| §7.5 Chatbot | None — not built (T3-C) |
| §8 Portal | `/portal/*` |
| §9 Reports | `/reports/dashboard` |
| §10 Users, roles, permissions, audit, configuration | `/users/*`, `/audit`, `/config` |
| §11.1 API | This document; OpenAPI is generated by the app (T2-L) |
| §11.2, §11.4 ERP, external systems | None — seam (§8.3) |
| §12 Language, branding | `/config/bootstrap` |
| §12 Multi-department, multi-branch | Scoping (§4.3) and filters (§4.4) |

### 10.3 Decision → contract

A-2 → §4.3, §4.4 · A-3 → SLA fields, §7 · A-4 → every **Roles** column · A-5 → §5.6 transition
table · A-6 → `categoryCode`, `priority` validation · A-8/AD-12 → §5.8 · A-9/AD-7/AD-15 → §4.1,
AP-9 · A-10 → email immutability, `409` · A-13 → §5.10 · A-14 → `POST /portal/tickets` ·
A-15 → §5.2 · A-16 → §5.6 authority table · A-17 → `isUrgent` · A-18 → assignment leaves status ·
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
