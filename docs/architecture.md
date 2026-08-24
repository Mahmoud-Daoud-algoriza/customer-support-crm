# Architecture — Customer Support CRM

> **Source of truth:** [requirements.md](requirements.md) (all sections) · [product-scope.md](product-scope.md) T1–T4, A-1…A-18 · [sdd-workflow.md](sdd-workflow.md) stage 5 · [story-backlog.md](story-backlog.md) (18 intakes)
> **SDD stage:** 5 of 10. Gate 5 → 6 per [sdd-workflow.md](sdd-workflow.md) §4.
> **Status:** Design only. No code, no database schema, no API endpoints, no UI components.

**What this document decides:** the layering of the backend, the structure of the front end, where
authentication and authorization are enforced, where the three integration seams sit, how
configuration is read, and how the stack runs locally.

**What it deliberately leaves to later stages:** entities and their fields (stage 6), endpoint
contracts (stage 7), screens and components (stage 8), and file-level plans (stage 9).

**Design bias.** Three evenings, 9–12 hours ([product-scope.md](product-scope.md) §1.2). Every choice
below favours the option a reviewer can read in one pass over the option that scales. Where a
pattern would exist only to demonstrate that we know the pattern, it is excluded and the exclusion
is recorded in §8.

---

## 1. Architecture overview

One repository, three runtime processes, one database. The backend is a **layered modular
monolith**: a single deployable ASP.NET Core application, internally divided into feature modules,
with dependencies pointing in one direction only.

```
┌────────────────────────────────────────────────────────────────────────────┐
│  Browser (desktop or phone width)                                          │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  Angular + PrimeNG SPA                                               │  │
│  │  core · shared · layout · features{auth, workspace, portal, admin}   │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────┬────────────────────────────────────────┘
                                    │  HTTPS/JSON, bearer token
┌───────────────────────────────────▼────────────────────────────────────────┐
│  ASP.NET Core Web API  (single deployable)                                 │
│                                                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │  Presentation      controllers · auth middleware · problem details   │  │
│  │                    OpenAPI · SLA background monitor (hosted service) │  │
│  ├──────────────────────────────────────────────────────────────────────┤  │
│  │  Application       use-case services per module · DTOs · validation  │  │
│  │                    access scoping · audit + activity recording       │  │
│  │                    OWNS the outbound interfaces (seams)              │  │
│  ├──────────────────────────────────────────────────────────────────────┤  │
│  │  Domain            entities · enums · lifecycle state machine        │  │
│  │                    SLA calculation · invariants. No dependencies.    │  │
│  ├──────────────────────────────────────────────────────────────────────┤  │
│  │  Infrastructure    EF Core DbContext + mappings · seam implementations│ │
│  │                    (AI, channel adapters, ERP) · file storage        │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└───────────────────────────────────┬────────────────────────────────────────┘
                                    │  EF Core
┌───────────────────────────────────▼────────────────────────────────────────┐
│  SQL Server (single database, single schema)                               │
└────────────────────────────────────────────────────────────────────────────┘
```

**Dependency rule.** `Presentation → Application → Domain`, and `Infrastructure → Application,
Domain`. Domain depends on nothing. Application depends on no infrastructure technology — it
declares interfaces, Infrastructure implements them, and the composition root in Presentation
wires them together. This is the only structural rule in the codebase, and it is enforced by
project references rather than by convention (§7, AD-2).

**Modules.** Inside Application and Domain, code is organized into **ten backend feature modules**:

`Identity` · `Organization` · `Customers` · `Tickets` · `Sla` · `Knowledge` · `Ai` ·
`Reporting` · `Administration` · `Integrations`

These ten are **not** a one-to-one match for the **fourteen feature slugs** in
[story-backlog.md](story-backlog.md). Ten slugs have a corresponding backend module:

| Feature slug | Backend module |
|---|---|
| `identity-access` | `Identity` |
| `organization` | `Organization` |
| `customer-management` | `Customers` |
| `ticket-management` | `Tickets` |
| `sla-automation` | `Sla` |
| `knowledge-base` | `Knowledge` |
| `ai-assist` | `Ai` |
| `reporting` | `Reporting` |
| `administration` | `Administration` |
| `integration-seams` | `Integrations` |

The remaining four slugs are **front-end areas or cross-cutting platform concerns, and have no
backend module of their own**:

| Feature slug | What it is instead |
|---|---|
| `platform-foundation` | The host, composition root and runtime setup (§2.1, §6) |
| `agent-workspace` | An Angular area (§2.2) |
| `customer-portal` | An Angular area (§2.2). Its server-side behaviour, including customer feedback, lives in `Tickets` |
| `platform-experience` | i18n, RTL, responsive layout and branding (§2.3) |

Where a story in one of those four needs server-side behaviour, it is served by the ten modules
above rather than by a module of its own. A feature slug is a unit of *planning*; a module is a
unit of *code organization*, and the two need not correspond.

Modules are folders with a public service surface, not separate assemblies or deployables. A
module calls another module through its Application service, never by reaching into its internals.
One database and one `DbContext` serve all modules; per-module contexts would buy isolation this
assessment has no use for.

---

## 2. Component responsibilities

### 2.1 Backend layers

**Presentation — `SupportCrm.Api`**

- Hosts HTTP. Controllers are thin: bind, delegate to one Application service, return the result.
- Authentication middleware; role-based endpoint gating (the coarse half of §4).
- Uniform error translation to Problem Details. No `try/catch` scattered through controllers.
- OpenAPI document generation — this *is* the API deliverable for requirements §11.1
  ([product-scope.md](product-scope.md) T2-L).
- Composition root: dependency injection registration, options binding and validation.
- **The SLA background monitor** runs here as a hosted service on a periodic timer, resolving a
  scoped Application service on each tick. It contains no business logic itself (§7, AD-6).
- Contains **no business rules**. If a controller makes a decision beyond HTTP concerns, it is
  in the wrong layer.

**Application — `SupportCrm.Application`**

- One service per use case group per module. This is where a story's behaviour lives.
- DTOs and input validation.
- **Access scoping** — the department and ownership rules of §4.3. This is the layer that decides
  which tickets a caller may see, because it is the layer that builds the queries.
- **Audit recording** (§2.4) and **ticket activity recording** (§2.5) are invoked from here, so
  that every path that changes state also records it.
- **Owns the outbound interfaces** — the AI service, the channel adapters, the ERP gateway, and
  file storage are all declared here as interfaces and implemented in Infrastructure. This is what
  makes them seams rather than couplings (§5).
- Orchestrates persistence through the `DbContext` abstraction; it does not know it is SQL Server.

**Domain — `SupportCrm.Domain`**

- Entities, value objects, and enumerations (categories, priorities, statuses, roles, channels).
- **The ticket lifecycle state machine** of [product-scope.md](product-scope.md) A-5 — which
  transitions are legal — expressed once, as domain logic, so no caller can bypass it.
- **SLA target calculation** — the 24/7 clock arithmetic of A-3.
- **Escalation semantics** — raise priority exactly one level, Urgent stays Urgent (A-5).
- Plain C# objects. No EF Core attributes, no framework references, no persistence concerns.
  Mapping lives in Infrastructure so the domain stays testable in isolation.

**Infrastructure — `SupportCrm.Infrastructure`**

- EF Core `DbContext`, entity type configurations, migrations, and the demo-data seeder.
- Implementations of every interface Application declares: AI provider and fake, channel adapters,
  ERP gateway, local-disk attachment storage.
- Nothing else depends on this project except the composition root.

**Tests — `SupportCrm.Tests`**

- Unit tests on the domain rules that carry the most risk: lifecycle transitions, SLA computation,
  escalation, and access scoping.
- A small number of integration tests that exercise scoping through the API as each role, since
  [product-scope.md](product-scope.md) T1-D requires permissions to be proven server-side rather
  than assumed from the UI.
- Coverage is targeted, not exhaustive ([product-scope.md](product-scope.md) §8).

### 2.2 Front-end structure

One Angular application, one deployable, three role-based areas inside it. Standalone components
with lazy-loaded routes.

```
frontend/src/app/
  core/        Singletons, provided once. Auth state and token handling, HTTP interceptors
               (auth header, error normalization), runtime config loader, i18n bootstrap,
               route guards, notification state, typed API client services.
  shared/      Reusable presentational pieces: form controls, PrimeNG re-exports, pipes,
               directives, empty/loading/error states. No feature knowledge, no HTTP.
  layout/      Application shells — header, navigation, language switcher. One shell per
               area. Direction-aware (§2.3).
  features/
    auth/      Sign-in and registration. Unauthenticated routes.
    workspace/ Agent and Manager area: ticket queue, ticket view, customers, reports.
    portal/    Customer area: submit, track, history, knowledge base, feedback.
    admin/     Administrator area: users, knowledge authoring, audit log.
```

**Role-based areas.** Each area is a lazily loaded route tree behind a guard that checks the
authenticated user's role. Areas exist so a customer never loads agent screens and an agent never
loads administration screens.

**Guards are a user-experience convenience, not a security control.** Everything a guard hides is
independently refused by the server (§4). Any statement of the form "the customer can't reach that
because the route is guarded" is not an acceptable answer in this codebase.

**API access** goes through typed client services in `core/`. Feature components do not call
`HttpClient` directly, so the eventual endpoint contracts from stage 7 are absorbed in one place.

### 2.3 Internationalization, direction and responsiveness (front end)

**Language (T2-J, A-11).** English and Arabic, switchable at runtime without losing application
state. That requirement rules out compile-time localization, which produces one bundle per locale
and needs a reload to switch, so translation is done by a **runtime translation library**
(Transloco or ngx-translate — equivalent for our purposes; the plan picks one). Translation
dictionaries are static assets loaded at bootstrap.

- **UI strings** are translated. **User-generated content** — tickets, notes, articles — is
  rendered exactly as authored and never machine-translated (A-11).
- The backend returns **codes, not user-facing prose**, wherever the front end can translate. This
  keeps translation in one place and is why the API is not the source of display text.
- Dates render in the **Gregorian calendar in both languages** (A-11).
- PrimeNG carries its own component strings; its locale settings are configured alongside the
  application dictionaries so both switch together.

**Direction (RTL).** Switching to Arabic sets `dir="rtl"` on the document root. Layout must mirror,
not merely re-align text, so:

- CSS uses **logical properties** (`margin-inline-start`, `padding-inline-end`, `inset-inline`)
  rather than left/right physical properties.
- Directional icons and any layout that hardcodes a side are handled in `shared/` once, not per
  feature.
- PrimeNG components are verified in Arabic rather than assumed correct.

**Responsive (T3-F, A-1).** "Web and mobile friendly" means responsive web — no native app, no
PWA. One layout system, breakpoint-driven, with the phone-width path explicitly designed for the
three surfaces that matter: the agent queue, the ticket view, and the portal. Wide content (ticket
tables, report tables) scrolls inside its own container so the page body never scrolls sideways.

### 2.4 Audit logging boundary

Requirements §10.4, [product-scope.md](product-scope.md) T2-H.

- **One writer.** A single audit-recording service in Application. Every audit entry in the system
  passes through it. Audit writes are never issued from controllers or from Infrastructure.
- **Called from the Application services** that perform auditable actions: sign-in, user
  administration, permission-relevant changes, and ticket lifecycle actions.
- **Append-only by construction.** The application exposes no update or delete path for audit
  entries — not merely no UI, but no service method.
- **Read by Administrators only**, enforced server-side like any other authorization rule (§4).
- **What it records:** actor, action, target, timestamp, outcome. The field-level shape is stage 6.

### 2.5 Ticket history / activity boundary

Requirements §2.5, [product-scope.md](product-scope.md) T1-B, A-5.

- **A separate concern from the audit log, deliberately.** Ticket history is a per-ticket activity
  trail shown to agents and, filtered, to customers. The audit log is a security and administration
  record shown to Administrators. They answer different questions for different actors.
- **They are independently queryable and neither is derived from the other.** This is stated in the
  `ticket-lifecycle` intake and holds at the architecture level: no "one table with a discriminator"
  shortcut that makes either view awkward.
- **One recorder inside the Tickets module** writes every activity entry: status changes,
  assignment changes, priority and category changes, escalations, messages, and internal notes.
  Application services call it on the same path that performs the change.
- **Append-only**, like the audit log.
- **Visibility is a property of the entry.** Internal notes are recorded in history and excluded
  from every customer-facing read — the portal thread, the customer interaction timeline, and
  notifications. That exclusion is applied in the Application layer, once, not in each UI
  ([product-scope.md](product-scope.md) T2-C).
- **The customer interaction timeline** (requirements §1.3) is a **read projection** over this
  history plus the customer's tickets. It is assembled on read, not maintained as a second store.

---

## 3. Request / data flow

The path every request takes, and the layer that owns each decision:

```
 1  Angular component
       └─ calls a typed API client service in core/
 2  HTTP interceptor attaches the bearer token
 3  ─────────── network ───────────
 4  ASP.NET Core: authentication middleware validates the token → user id only
       └─ per-request resolution loads the AUTHORITATIVE user record
          (role, department, active) and builds the current-user context   ← §4.1.1
          a missing or deactivated user is refused here
 5  Controller: role gate passes → binds the request → calls ONE Application service
 6  Application service:
       a. validates input
       b. applies ACCESS SCOPING using the current-user context   ← §4.3
       c. loads entities through the DbContext abstraction
       d. asks the DOMAIN to decide (legal transition? SLA target? escalation effect?)
       e. records ticket activity and/or an audit entry
       f. optionally calls a SEAM (AI suggestion, outbound channel, ERP)  ← §5
       g. saves
 7  Infrastructure: EF Core translates to SQL
 8  ─────────── SQL Server ───────────
 9  Result returns as a DTO — never a domain entity, never an EF-tracked graph
10  Controller shapes the HTTP response; errors become Problem Details
11  Angular client service returns typed data to the component
```

**Two flows do not start in the browser:**

- **SLA breach detection.** The hosted service in Presentation ticks on a periodic timer, resolves
  a scoped SLA Application service, and runs the same escalation path a manual escalation uses —
  flag breached, raise priority one level, notify the department manager, record activity. No
  duplicate escalation logic ([product-scope.md](product-scope.md) T2-D; `sla-routing-escalation`
  intake).
- **Inbound messages from a channel adapter.** A normalized inbound message enters the Tickets
  module's ingestion service — the same service the web form uses — so channel origin is data, not
  a separate code path (§5.2).

**Transactions.** One unit of work per request, owned by the Application service, committed once.
No ambient or nested transactions.

---

## 4. Security boundary

### 4.1 Authentication

- Email and password, per [product-scope.md](product-scope.md) A-9. No SSO, OAuth, MFA, password
  policy engine, or account recovery.
- **Token-based (JWT bearer).** The front end is a separate origin from the API under Docker
  Compose, and a bearer token is the least-friction fit for that shape.
- **The token asserts identity only.** It carries the user id plus standard issuance and expiry
  claims — and nothing an authorization decision depends on. Role, department and active status
  are deliberately **not** authoritative claims (§4.1.1).
- The signing key is a **secret from environment configuration** (§6.3) and is never committed.
- Short-lived tokens; expiry means signing in again. No refresh-token rotation
  ([product-scope.md](product-scope.md) §8).
- Passwords are stored using ASP.NET Core's standard password hashing. Deactivated users are
  refused at sign-in **and on every subsequent request**, because the active flag is re-read as
  part of §4.1.1 rather than trusted from a token minted earlier.

### 4.1.1 Identity comes from the token; authorization data is resolved per request

**Why authorization data does not live in the token.** An Administrator can move an agent between
departments, change their role, or deactivate them at any moment. A claim minted at sign-in keeps
its value until the token expires. An agent moved from Billing to Technical would go on reading
Billing's tickets — and be refused Technical's — for the remaining life of their token, while the
server believed it was enforcing §4.3 correctly. The same staleness applies to a role change (a
demoted Manager keeps cross-department reach) and to deactivation (a disabled account keeps
working until expiry). That is a confidentiality defect rather than a latency inconvenience, and
it fails silently.

**The resolution.** After the token is validated, a **per-request resolution step** loads the
**authoritative user record** — role, department, active status — for the user id the token
asserts, and builds from it the request-scoped current-user context that §4.3 depends on:

- A user that no longer exists, or is deactivated, is refused at this point. Authorization never
  runs on a stale principal.
- The freshly read role also populates the principal used by the coarse endpoint gates of §4.2,
  so the check at the edge and the row scoping in the Application layer read the same
  authoritative value rather than two different vintages of it.
- **Role, department and active status are resolved together**, because the §4.3 rule is a
  function of all three. Refreshing department while still trusting a stale role would leave the
  same defect reachable through a different claim.

**The cost, and why it is accepted.** One indexed read per authenticated request, against the same
database the request is about to query anyway, in a single-process monolith at assessment scale.
No caching layer is introduced to avoid it: [product-scope.md](product-scope.md) §8 excludes
Redis, and an in-process cache would reintroduce the staleness this correction removes, with
invalidation logic on top. Token revocation lists, or very short expiry with refresh rotation,
would also close the gap and are more machinery than a single lookup (§7, AD-15).

### 4.2 Authorization — three enforcement points

| # | Where | Decides | Example |
|---|---|---|---|
| 1 | Presentation, endpoint attribute | **Coarse role gate** — may this role touch this capability at all? | Only Administrator reaches user administration or the audit log |
| 2 | Application, access scoping | **Which rows** may this caller see or act on? | An agent's ticket query is narrowed to their department |
| 3 | Domain | **Is this operation legal at all**, for anyone? | An illegal status transition is refused regardless of role |

The role model is the four fixed hierarchical roles of A-4 — Customer, Agent, Manager,
Administrator — expressed as policies. There is no role editor, no per-field permission, and no
custom RBAC engine.

**The front end is never an enforcement point.** Route guards and hidden buttons are UX.

### 4.3 Department-level ticket access (requirement 7, in detail)

This is the rule most likely to be got wrong, so it gets one implementation and one test suite.

**The rule** ([product-scope.md](product-scope.md) A-2, A-4):

| Role | Ticket visibility |
|---|---|
| Customer | Only tickets whose customer is themselves |
| Agent | Only tickets in their own department |
| Manager | All departments |
| Administrator | All departments |

Branch is **not** part of this rule. Branch is a reporting and filtering attribute only (A-2,
T2-K); an agent sees in-department tickets regardless of the customer's branch.

**How it is enforced:**

1. **One request-scoped current-user context**, built at the start of the request from the
   authoritative user record (§4.1.1), is injected into Application services. Services never read
   claims directly and never accept a caller-supplied user id, department id, or role — identity
   comes from that resolved context alone. A department id arriving in a request body is data to
   validate, never an identity to trust — and neither is a department carried in a token.
2. **One scoping helper in the Tickets module** turns that context into a query restriction. Every
   ticket query — list, detail, dashboard, report, portal, AI assist, export — composes it. It is
   the single place the table above is expressed in code.
3. **Write paths re-check on load.** Reading a ticket for modification goes through the same
   scoped path, so an agent cannot act on an out-of-department ticket by guessing its identifier.
   Fetch-then-authorize, never authorize-then-fetch-by-id.
4. **Assignment is validated against the ticket's department**, so a ticket cannot be assigned to
   an agent who could not then see it.
5. **Tests assert refusal at the API, as each role, bypassing the UI** — required explicitly by the
   `auth-and-roles`, `ticket-core`, `tasks-internal-notes` and `portal-self-service` intakes.

**Rejected alternative:** EF Core global query filters. They look attractive and are a poor fit
here — the filter is role-dependent, Managers and Administrators must bypass it, reporting
aggregates must not be silently narrowed, and a filter that is silently absent fails open. An
explicit helper that a reader can find and a test can target is safer than an invisible one
(§7, AD-5).

### 4.4 Other security-relevant boundaries

- **Internal notes** are excluded from customer-facing reads in the Application layer, once
  (§2.5). A customer-visible thread is assembled by a service that cannot return an internal entry.
- **Attachments** are stored on local disk with a size cap ([product-scope.md](product-scope.md)
  T2-A); download passes through the same authorization path as the ticket or customer that owns
  the file, so files are not reachable by guessing a path. No virus scanning is in scope.
- **Baseline only.** No rate limiting, WAF, threat model, penetration testing, or
  encryption-at-rest work ([product-scope.md](product-scope.md) §8).

---

## 5. Integration seams

Three seams, one pattern: **Application declares the interface, Infrastructure implements it, the
composition root selects the implementation from configuration.** Each ships with a fake that runs
with no external account, because [product-scope.md](product-scope.md) §10 (item 5) requires the
whole system to start and demo without credentials.

### 5.1 AI service seam (requirements §7 · T1-F, A-7, A-8 · `ai-service-seam` intake)

- **One interface** in Application covers every AI capability: ticket summary, suggested reply,
  and suggested category/priority. The future chatbot (T3-C) is documented as a further consumer
  of this same interface and is not built.
- **Two implementations**, both in Infrastructure:
  - a **real provider adapter**, used when a provider and credentials are configured;
  - a **deterministic offline fake**, which returns the same output for the same input on every
    run, making demos and tests repeatable.
- **Selection is configuration-driven**, with the fake as the default for local runs (§6.3).
- **The interface can only return suggestions.** It has no method that sends a message, changes a
  status, or reassigns a ticket. A-8's "advisory and human-approved" rule is therefore a property
  of the shape of the seam, not a discipline someone has to remember.
- **Failure is contained.** A provider error or timeout surfaces as "AI unavailable" for that one
  feature; ticket creation, replies and status changes continue. An AI outage never blocks support
  work.
- **Which provider** is deliberately unanswered here — [product-scope.md](product-scope.md) §9
  question 1 keeps it open, including whether data-residency limits apply to sending customer
  content. The seam is what lets that stay open.
- **Suggested solutions** (requirements §7.4) do **not** use this seam. They are keyword retrieval
  over the knowledge base ([product-scope.md](product-scope.md) T2-E) and belong to the Knowledge
  module.

### 5.2 External communication channels (requirements §3.1, §3.2, §3.4, §11.3 · T3-A · `channel-erp-adapters` intake)

- **One normalized message model** owned by the Tickets module, carrying the channel a message
  arrived on. The web form and portal messaging (T2-B) — the only *real* channels in this
  assessment — write into it from day one.
- **One outbound adapter interface**, with a **console/log adapter** as the shipped
  implementation. Sending produces a log entry and a recorded ticket activity, and attempts no
  external call.
- **Inbound** arrives as a normalized message into the same ingestion service the web form uses.
- Consequently, adding email, WhatsApp or SMS later means writing an adapter — not a second
  message concept, a second thread model, or a second ingestion path. That is the whole claim this
  seam makes, and it is the claim a reviewer should test.
- **In-app notifications are a different thing** and must not be confused with this seam.
  Notifications (A-13) are in-app only — a list and an unread badge — served by their own
  Application service. When email or SMS delivery is added later, it becomes a *consumer* of the
  channel adapter.
- **Real-time live chat (T3-B) is not built.** Portal messaging is ordinary request/response. No
  WebSocket transport, no presence, no queueing — and nothing in the design describes polling as
  real-time chat.

### 5.3 ERP and external systems (requirements §11.2, §11.4 · T3-D)

- **One outbound gateway interface** in Application isolating any external-system call, with a
  **no-op implementation** selected by configuration.
- Customer records carry an **optional external reference** (its field shape is stage 6), unused by
  default.
- No real connection, field mapping, sync strategy, or conflict resolution.
- Requirements §11.4 "External systems" stays unbounded on purpose — [product-scope.md](product-scope.md)
  §9 question 3 records that it cannot be scoped until a system is named. The gateway is the place
  it would attach.

### 5.4 What each seam must document

For each of the three, the implementing story records what a real implementation would have to
add — accounts and onboarding, webhooks, retries, delivery receipts, opt-outs, field mapping — so
that "designed for, not delivered" is a verifiable claim rather than an assertion.

---

## 6. Deployment / runtime architecture

### 6.1 Repository layout

```
customer-support-crm/
  backend/                 ASP.NET Core solution: Api · Application · Domain · Infrastructure · Tests
  frontend/                Angular + PrimeNG application
  docs/                    Requirements, scope, SDD-stage documents (this file)
  .squad/                  Story intakes and, later, implementation plans
  docker-compose.yml       The three services below
```

Single repository, matching the stated constraint and [.squad/config.yaml](../.squad/config.yaml)
`projectRoots: [backend, frontend]`.

### 6.2 Docker Compose

Three services, one network, one volume. Nothing else.

| Service | Contents | Notes |
|---|---|---|
| `db` | SQL Server | Named volume for data. Health check so the API waits for readiness. |
| `api` | ASP.NET Core Web API | Depends on `db`. Reads configuration from environment. Serves OpenAPI. |
| `web` | Built Angular app behind a static web server | Depends on `api`. Serves the SPA and routes unknown paths to `index.html`. |

- **One documented command** starts everything, with no external accounts or credentials —
  [product-scope.md](product-scope.md) §10, item 5.
- **Migrations and demo seeding run at API startup.** For an assessment that must come up clean on
  a fresh machine, this is the right trade; it is explicitly not a production practice, and the
  decision is recorded as such (§7, AD-8).
- No reverse proxy, no TLS termination, no orchestrator, no additional containers for caching,
  search, or queues.

### 6.3 Configuration strategy

Layered, standard ASP.NET Core: `appsettings.json` → `appsettings.{Environment}.json` →
**environment variables** (what Compose supplies) . Bound to strongly typed options objects and
**validated at startup**, so invalid configuration fails fast with a clear message instead of
degrading at runtime — required by the `audit-configuration` intake.

| Concern | Where it lives | Notes |
|---|---|---|
| **Database connection** | Environment variable from Compose (`.env`, git-ignored) | Never in a committed file |
| **AI provider** | Configuration selects `fake` or a real provider; credentials from environment | Default is `fake`; the system must run with none |
| **Categories** | Configuration — a flat list | Fixed enumeration, not a user-managed taxonomy (A-6) |
| **Category → department map** | Configuration — one department per category | Sets a ticket's department at creation, before assignment (A-14). Validated at startup: every category must map |
| **Default branch** | Configuration — one branch id | Assigned to self-registering customers (A-15) |
| **Priorities** | Configuration — Low, Medium, High, Urgent | Four levels, fixed (A-6) |
| **SLA targets** | Configuration — first-response and resolution hours **per priority** | 24/7 clock (A-3) |
| **Quick replies** | Configuration — a small canned-response library | T1-C |
| **Branding** | Configuration — product name, logo, primary colour | T3-E; a seam, not a theming engine |
| **Feedback rating scale** | Configuration — the rating-scale boundaries (`min`, `max`) | Published to clients and validated server-side by the feedback endpoint (api-design §5.7). **The boundary values are deliberately not decided here — OQ-1 is open** (product-scope §9, data-model §8). The key exists so the contract has a home for the answer; inventing the answer is out of scope |
| **JWT signing key** | Environment variable | Secret |

**No configuration UI.** Changing configuration is a redeploy — [product-scope.md](product-scope.md)
T2-I, stated as a deliberate simplification. A read-only view of effective configuration is
permitted; a writable one is not.

**Reaching the front end.** Branding and the active language set are needed by the SPA at startup,
so the Angular application loads a small runtime configuration document during bootstrap before
the first screen renders. Whether that document is served by the API or as a static asset is a
stage 7 decision; the architectural point is that branding is **read at runtime**, never compiled
into a component or stylesheet.

**Secrets discipline.** `.squad/secrets.yaml` is already git-ignored by squad-kit; application
secrets follow the same rule through the environment. No credential is committed, and the default
configuration requires none.

---

## 7. Key architectural decisions

| # | Decision | Why | Rejected alternative |
|---|---|---|---|
| AD-1 | **Layered modular monolith**, single deployable | Requirements span 12 domains that share one customer and one ticket concept. One process, one database, one transaction boundary is the simplest thing that satisfies them, and it is deployable in one Compose service | Microservices — no independent scaling or deployment need exists, and they would consume the entire time budget |
| AD-2 | **Four projects** (Api, Application, Domain, Infrastructure) rather than folders in one project | The dependency rule becomes compiler-enforced at near-zero cost; a reviewer can verify the layering by reading project references | Folders in one project — cheaper to create, but the layering becomes a convention that erodes under time pressure |
| AD-3 | **No repository or unit-of-work layer over EF Core** | `DbContext` already is both. A hand-written repository over it adds indirection and tests nothing extra | Generic repository pattern — pattern for its own sake, explicitly against the brief |
| AD-4 | **Domain kept free of EF attributes**; mapping configured in Infrastructure | Lifecycle and SLA rules — the highest-risk logic — stay unit-testable with no database | Annotating entities directly — faster to write, couples the rules to persistence |
| AD-5 | **Access scoping as an explicit Application-layer helper** | Role-dependent, must be bypassable by Manager and Administrator, and must not silently narrow reports. Visible and testable beats invisible | EF global query filters — fail open when accidentally absent, awkward for admin bypass and aggregates (§4.3) |
| AD-6 | **SLA breach detection as a periodic hosted service** in-process | A-3 sets coarse granularity (minutes) and explicitly disclaims timing precision. A timer in the API process meets it exactly | Hangfire/Quartz or a message broker — infrastructure with no requirement behind it, and excluded by the brief |
| AD-7 | **JWT bearer authentication, token asserting identity only** | Separate origins under Compose make a bearer token the simplest fit. Authorization data is deliberately kept out of the token so it cannot go stale (AD-15) | Cookie sessions — workable, but adds cross-origin and CSRF handling for no gain here |
| AD-8 | **Migrations and seed data applied at API startup** | A fresh checkout must come up demo-ready with one command (product-scope §10, item 5) | A separate migration step — more correct for production, more friction for a three-evening demo. Recorded as a knowingly non-production choice |
| AD-9 | **Runtime i18n library rather than compile-time localization** | T2-J requires switching language without losing application state; compile-time localization needs one bundle per locale and a reload | `@angular/localize` — better for production bundle size, wrong for a live switcher |
| AD-10 | **Ticket history and audit log kept separate** | Different actors, different questions, different visibility rules; the intakes require both to stay independently queryable | One event table with a discriminator — superficially tidy, makes both reads awkward and blurs the visibility rule |
| AD-11 | **Seam interfaces owned by Application, implemented in Infrastructure** | The dependency rule is what makes AI, channels and ERP swappable without touching business logic | Calling provider SDKs from services or controllers — the seam would exist on a diagram only |
| AD-12 | **AI seam can only return suggestions** | A-8's advisory/human-approved rule becomes structural rather than a convention someone must remember | A general "AI action" interface — would make autonomous behaviour a one-line mistake away |
| AD-13 | **Keyword search in SQL Server for the knowledge base** | T2-E fixes search as database text matching; suggested solutions are retrieval, not generation | A search engine or vector database — excluded by the brief and unjustified at this data volume |
| AD-15 | **Role, department and active status resolved per request from the user record**, never from token claims | An Administrator can change an agent's department, role, or active status at any time. A claim minted at sign-in holds the old value until expiry, so a moved agent would keep reading their former department's tickets while the server believed §4.3 was being enforced. The §4.3 rule is a function of role, department and user id, so all three must be current (§4.1.1) | Trusting claims — stale until expiry, fails silently, and is the defect this decision exists to prevent. Revocation lists or very short expiry with refresh rotation — close the gap, cost more than one indexed read. Caching the user record — reintroduces the staleness and adds invalidation; a cache layer is excluded by §8 |
| AD-14 | **One Angular application with role-based lazy areas** | Agents, customers and administrators need different surfaces, not different deployables; one build keeps i18n, branding and the API client shared | Separate SPAs per role — triples build and i18n work for no benefit |

---

## 8. Explicit exclusions

Restated here, per gate 5 → 6, so the constraint survives into planning. Not built, not stubbed,
not designed:

**Architecture and infrastructure**

- **Microservices** — one backend application (AD-1)
- **CQRS** — one model, read and written by the same services
- **Event sourcing** — current state is stored; history is an append-only trail, not an event store
- **Message brokers / queues** (Kafka, RabbitMQ, SQS) — a periodic in-process check instead (AD-6)
- **Kubernetes**, service mesh, serverless — local Docker Compose only
- **Redis or any caching layer** — no measured need at this scale
- **Dedicated search engine** (Elasticsearch, OpenSearch) — SQL Server text matching (AD-13)
- **Vector database / embeddings infrastructure** — suggested solutions are keyword retrieval
- **Native mobile applications** — responsive web only (A-1, T3-F); no PWA, offline mode, or push
- Read replicas, sharding, multi-region deployment, HA, disaster recovery
- Reverse proxy, API gateway, service discovery, distributed tracing stack
- Repository/unit-of-work abstraction over EF Core (AD-3); mediator pipelines; a domain-event bus

**Product-level exclusions carried from [product-scope.md](product-scope.md) §8** — real
email/WhatsApp/SMS delivery, real-time chat, the AI chatbot, live ERP connections, multi-tenancy,
configurable rule engines, report builders and exports, and the rest of that list. This document
adds no capability beyond what the scope tiers allow.

**Quality attributes not pursued** — performance and load targets, security hardening beyond
baseline, compliance tooling (GDPR/PDPL), WCAG certification, cross-browser matrix testing, and a
production observability stack. Logging is ordinary application logging to the console.

---

## 9. Traceability to product-scope sections

**Architecture section → scope item**

| This document | Serves |
|---|---|
| §1 Layered modular monolith | [product-scope.md](product-scope.md) §8 technical exclusions; §1.1 "coherent, honest architecture with clean seams" |
| §2.1 Backend layers | T1-B, T1-D, T2-D (business rules with one home) |
| §2.2 Front-end structure, role areas | T1-C, T2-F, A-4 |
| §2.3 i18n, RTL, responsive | T2-J, T3-E, T3-F, A-1, A-11 |
| §2.4 Audit boundary | T2-H (requirements §10.4) |
| §2.5 Ticket history boundary | T1-B, T2-C, A-5 (requirements §2.5, §1.3) |
| §3 Request/data flow | T1-B, T2-D |
| §4.1 Authentication | T1-D, A-9 |
| §4.1.1 Per-request identity resolution | T1-D, T1-E, A-2, A-4 — keeps the §4.3 rule current |
| §4.2 Authorization points | T1-D, A-4 |
| §4.3 Department access enforcement | T1-D, T1-E, A-2, A-4 |
| §4.4 Internal notes, attachments | T2-C, T2-A |
| §5.1 AI seam | T1-F, T3-C, A-7, A-8 |
| §5.2 Channel seam | T2-B, T3-A, T3-B, A-13 |
| §5.3 ERP seam | T3-D |
| §6.1 Repository layout | A-12, single-repo constraint |
| §6.2 Docker Compose | A-12, §10 item 5 |
| §6.3 Configuration | T2-I, T3-E, A-3, A-6 |
| §7 Decisions | §1.1 (what is assessed) |
| §8 Exclusions | §8 (out of scope), T4 |

**Story intake → architecture section** — every story has a home:

| Story | Primary architecture section |
|---|---|
| `solution-skeleton` | §6.1, §6.2, §6.3 |
| `auth-and-roles` | §4.1, §4.2 |
| `departments-branches` | §4.3 |
| `customer-records` | §2.1, §2.5 (timeline projection), §4.4 |
| `ticket-core` | §2.1, §3, §4.3 |
| `ticket-lifecycle` | §2.1 (Domain state machine), §2.5 |
| `ticket-intake-messaging` | §5.2 (message model) |
| `agent-dashboard` | §2.2, §2.3 |
| `sla-routing-escalation` | §2.1 (Domain SLA), §3 (hosted service), §6.3 |
| `ai-service-seam` | §5.1 |
| `ai-ticket-assists` | §5.1, §2.1 |
| `kb-articles-search` | §5.1 (what it is *not*), AD-13 |
| `portal-self-service` | §2.2, §4.3, §4.4 |
| `tasks-internal-notes` | §2.5, §4.4 |
| `management-dashboard` | §2.1, §4.2 |
| `audit-configuration` | §2.4, §6.3 |
| `i18n-responsive-branding` | §2.3, §6.3 |
| `channel-erp-adapters` | §5.2, §5.3 |

**Open questions unaffected by this document.** [product-scope.md](product-scope.md) §9 holds seven
open questions. This architecture resolves none of them — it places each behind a seam or a
configuration point so they can stay open: the AI provider (§5.1), the ERP product and unbounded
external systems (§5.3), real SLA policy (§6.3 targets are configuration), chatbot handoff (§5.1),
and multi-tenancy, which A-2 fixes as single-organization while §1 keeps one database and one
application — the boundary a tenant would later need is noted, not built.

---

**Stage 5 gate check.** This document states the backend layering (§1, §2.1), where the three T3
seams sit (§5), how department scoping is enforced server-side (§4.3), where configuration is read
(§6.3), and an explicit restatement of the technical exclusions (§8). Gate 5 → 6 is met.

**Next stage:** 6 — Data Model (`docs/data-model.md`). Not started.
