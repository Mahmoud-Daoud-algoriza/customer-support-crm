# Implementation Plan — Customer Support CRM

> **Source of truth:** `docs/requirements.md` · `docs/product-scope.md` T1–T4, A-1…A-19 · `docs/architecture.md` AD-1…AD-15 · `docs/data-model.md` DM-1…DM-7 (15 entities) · `docs/api-design.md` AP-1…AP-19 (66 endpoints) · `docs/ui-design.md` UI-1…UI-12 (24 screens) · `docs/story-backlog.md` and the 18 intakes
> **SDD stage:** 9 of 10. Gate 9 → 10 per `docs/sdd-workflow.md` §4.
> **Status:** Planning only. **No application code, no migration, no component exists.**
> **Date:** 2026-08-25

**What this document decides:** the workstreams, the phase order, which work is sequential and which
can run in parallel, the conventions every story plan shares, and the traceability from each plan
back to stories, endpoints, entities and screens.

**What it does not decide:** anything a design document already fixed, and **none of the open
questions**. §7 lists what remains unresolved, and every one of them stays that way.

**No business rule is invented here.** Where planning exposed a gap or a contradiction in the
approved documents, it is **reported in §7 as an `S9-n` finding**, not filled in.

---

## 1. The eighteen plans

One plan file per story, in `NN`-prefixed global execution sequence (`naming.globalSequence: true`),
matching the intended order in `docs/story-backlog.md`.

| NN | Plan | Feature slug | Tier | Phase |
|----|------|--------------|------|-------|
| 01 | [solution-skeleton](platform-foundation/01-story-solution-skeleton.md) | `platform-foundation` | T2 | 0 |
| 02 | [auth-and-roles](identity-access/02-story-auth-and-roles.md) | `identity-access` | **T1** | 1 |
| 03 | [departments-branches](organization/03-story-departments-branches.md) | `organization` | **T1**+T2 | 1 |
| 04 | [customer-records](customer-management/04-story-customer-records.md) | `customer-management` | **T1**+T2 | 2 |
| 05 | [ticket-core](ticket-management/05-story-ticket-core.md) | `ticket-management` | **T1** | 3 |
| 06 | [ticket-lifecycle](ticket-management/06-story-ticket-lifecycle.md) | `ticket-management` | **T1** | 3 |
| 07 | [ticket-intake-messaging](ticket-management/07-story-ticket-intake-messaging.md) | `ticket-management` | T2 | 4 |
| 08 | [agent-dashboard](agent-workspace/08-story-agent-dashboard.md) | `agent-workspace` | **T1** | 4 |
| 09 | [sla-routing-escalation](sla-automation/09-story-sla-routing-escalation.md) | `sla-automation` | T2 | 5 |
| 10 | [ai-service-seam](ai-assist/10-story-ai-service-seam.md) | `ai-assist` | **T1** | 5 (parallel) |
| 11 | [ai-ticket-assists](ai-assist/11-story-ai-ticket-assists.md) | `ai-assist` | **T1** | 5 |
| 12 | [kb-articles-search](knowledge-base/12-story-kb-articles-search.md) | `knowledge-base` | T2 | 6 (partly parallel) |
| 13 | [portal-self-service](customer-portal/13-story-portal-self-service.md) | `customer-portal` | T2 | 6 |
| 14 | [tasks-internal-notes](agent-workspace/14-story-tasks-internal-notes.md) | `agent-workspace` | T2 | 7 |
| 15 | [management-dashboard](reporting/15-story-management-dashboard.md) | `reporting` | T2 | 7 |
| 16 | [audit-configuration](administration/16-story-audit-configuration.md) | `administration` | T2 | **2 (Part A) + 7 (Part B)** |
| 17 | [i18n-responsive-branding](platform-experience/17-story-i18n-responsive-branding.md) | `platform-experience` | T2+T3 | **0 (Part A) + 8 (Part B)** |
| 18 | [channel-erp-adapters](integration-seams/18-story-channel-erp-adapters.md) | `integration-seams` | T3 | 8 |

**Two stories are split in time**, exactly as `docs/story-backlog.md` records:

- **Story 16** — *configuration* (Part A) is consumed by Stories 04, 05, 08, 09, 13 and 17, so it
  executes in **Phase 2**. Only the *audit surface* (Part B) sits at position 16.
- **Story 17** — the *i18n/RTL scaffolding* (Part A) is delivered inside Story 01, because
  retrofitting direction handling into every component later costs more. Only the *translation
  pass* (Part B) sits at position 17.

---

## 2. Workstreams

Fourteen workstreams cut **across** the phases. A workstream is a body of related code with one
owner-in-effect; a phase is a point in time. Use the workstream column to see what a change touches;
use the phase table (§3) to see when it happens.

| # | Workstream | Lives in | Stories |
|---|---|---|---|
| **W1** | **Platform and runtime** — solution, Compose, OpenAPI, Problem Details, health | `backend/src/SupportCrm.Api`, `docker-compose.yml` | 01 |
| **W2** | **Data and EF Core** — entities, configurations, migrations, seeders | `Domain/Modules/*`, `Infrastructure/Persistence` | 02–07, 09, 12–14 |
| **W3** | **Identity and access** — auth, per-request identity, policies, scoping | `Modules/Identity`, `Api/Auth`, `Modules/Tickets/TicketScope.cs` | 02, 03, 05 |
| **W4** | **Ticket domain** — lifecycle, escalation, activity, SLA arithmetic | `Domain/Modules/Tickets`, `Domain/Modules/Sla` | 05, 06, 09 |
| **W5** | **Channels and messaging** — the message model and its one ingestion path | `Modules/Tickets` | 07, 18 |
| **W6** | **Automation and background** — SLA sweep, round-robin, notifications | `Modules/Sla`, `Api/BackgroundServices` | 06 (port), 09 |
| **W7** | **AI seam** — the abstraction, the fake, the provider adapter, the assists | `Modules/Ai`, `Infrastructure/Seams/Ai` | 10, 11 |
| **W8** | **Knowledge** — articles, keyword search, suggested solutions | `Modules/Knowledge` | 12 |
| **W9** | **Reporting and administration** — aggregates, audit read, configuration | `Modules/Reporting`, `Modules/Administration`, `Application/Configuration` | 02 (write path), 15, 16 |
| **W10** | **Front end — workspace** — queue, ticket detail, customers | `frontend/src/app/features/workspace` | 05–09, 11, 12, 14 |
| **W11** | **Front end — portal and admin** | `features/portal`, `features/admin` | 02, 04, 12, 13, 15, 16 |
| **W12** | **Platform experience** — i18n, RTL, responsive, branding | `core/i18n`, `shared/`, `layout/` | 01 (Part A), 17 (Part B) |
| **W13** | **Integration seams** — channel adapter, ERP gateway, seam documentation | `Modules/Integrations`, `Infrastructure/Seams` | 18 |
| **W14** | **Testing and verification** | `backend/tests/SupportCrm.Tests` | every story |

**W2 is the serialization constraint.** One `DbContext` and one migration chain (AD-1, A-12) mean
migrations are **strictly ordered**; two stories cannot generate migrations concurrently without a
merge conflict in the model snapshot. §5 says how to handle it.

---

## 3. Phases and execution order

```
Phase 0  FOUNDATION                    01  (+ 17 Part A)
              │
Phase 1  IDENTITY & ORGANIZATION       03·data ──▶ 02 ──▶ 03·api+ui
              │
Phase 2  CONFIGURATION & CUSTOMERS     16 Part A ──▶ 04
              │
Phase 3  TICKET CORE LOOP              05 ──▶ 06
              │
Phase 4  CHANNELS & WORKSPACE          07 ──▶ 08
              │
Phase 5  AUTOMATION & AI               09 ──▶ 11        (10 runs in parallel from Phase 1)
              │
Phase 6  KNOWLEDGE & PORTAL            12 ──▶ 13        (12 backend partly parallel from Phase 2)
              │
Phase 7  COLLABORATION, REPORTING,     14 · 15 · 16 Part B
         ADMINISTRATION
              │
Phase 8  EXPERIENCE & SEAMS            17 Part B ──▶ 18
```

| Phase | Contents | Exit condition |
|---|---|---|
| **0 · Foundation** | 01, 17 Part A | One command starts the stack; `/health` reports the database reachable; OpenAPI renders; the SPA loads and switches language with `dir="rtl"` |
| **1 · Identity & Organization** | 03 data layer → 02 → 03 endpoints and filters | Four roles enforced **server-side**; a user deactivated after their token was issued is refused on the next request (AD-15); two departments and two branches seeded |
| **2 · Configuration & Customers** | 16 Part A → 04 | Invalid configuration **fails at startup**; `/config` and `/config/staff` split by audience; customers, notes and attachments work; registration implements exactly the three A-15 outcomes |
| **3 · Ticket core loop** | 05 → 06 | **The T1 heart.** Department scoping proven by tests as each role; the full 6×6 transition matrix enforced; escalation raises one level and leaves status unchanged; history append-only |
| **4 · Channels & workspace** | 07 → 08 | A customer reply on a `Pending` ticket reopens it in the same transaction, attributed to the customer; the agent queue lands on SLA-urgency order; the customer panel does not lose a draft |
| **5 · Automation & AI** | 09 → 11, with 10 already done in parallel | The in-process sweep flags breaches and escalates; round-robin assigns without changing status; all three AI assists work **with no credentials**, and an AI outage blocks nothing |
| **6 · Knowledge & portal** | 12 → 13 | Internal articles never reach the portal (`404`, not `403`); a customer sees only their own tickets; feedback once per ticket |
| **7 · Collaboration, reporting, administration** | 14, 15, 16 Part B | An internal note is unreachable by a customer through **any** path; the dashboard refuses Agents server-side and shows "empty is not zero"; the audit log is read-only and independently queryable |
| **8 · Experience & seams** | 17 Part B → 18 | Every T1/T2 screen translated in both languages with no missing key; no horizontal body scroll at 390 px; branding changes without a front-end rebuild; the seams run with no credentials |

### Why this order, where it differs from the backlog sequence

The backlog sequence is preserved for **numbering and traceability**. Three refinements were
required by dependencies the approved documents already imply:

1. **Story 03's data layer runs before Story 02.** `POST /users` requires a `departmentId` for every
   staff role (data-model §2.1). Both intakes say these stories are *"planned together or in
   immediate sequence"*; Phase 1 makes that sequence explicit. **(S9-12)**
2. **Story 16 Part A runs in Phase 2.** Story 05 cannot create a ticket without the category list,
   the category→department map (A-14) and the per-priority SLA targets (A-3). Already recorded as a
   split-in-time exception in `docs/story-backlog.md`. **(S9-13)**
3. **`POST /auth/register` moves from Story 02 to Story 04.** A-15 requires a `Customer`, a
   `Branch` and a configured default branch, none of which exist at Story 02. **(S9-7)**

**Nothing else moves.** In particular the cut order of `docs/story-backlog.md` still holds:
cut **18** first; then 14, 15, 16 Part B, 17 Part B; then 12, 09, 13, 07; **never** 01–06, 08, 10, 11.

---

## 4. Dependency graph

```
                                   01 ─────────────────────────────────┐
                                    │                                  │
                    ┌───────────────┼───────────────┐                  │
                    ▼               ▼               ▼                  ▼
                 03·data           10 ⟂          17·A (in 01)      16·A ──┐
                    │               │                                     │
                    ▼               │                                     │
                   02 ──────────────┼──────────────┬──────────────────────┤
                    │               │              │                      │
        ┌───────────┼───────────┐   │              ▼                      ▼
        ▼           ▼           ▼   │             12 ⟂                   04
    03·api        16·B         ...  │              │                      │
                                    │              │                      ▼
                                    │              │                     05
                                    │              │                      │
                                    │              │                      ▼
                                    │              │                     06
                                    │              │              ┌───────┴───────┐
                                    │              │              ▼               ▼
                                    │              │             07              09
                                    │              │              │               │
                                    │              │              ▼               │
                                    │              │             08 ◀─────────────┘
                                    │              │              │
                                    └──────────────┼──────────────┤
                                                   │              ▼
                                                   ├────────────▶ 11
                                                   │
                                                   ├────────────▶ 13 ──▶ 15
                                                   │              │
                                                   └──────────────┴────▶ 14
                                                                        │
                                                   17·B ◀───────────────┘
                                                     │
                                                     ▼
                                                    18

⟂ = can run in parallel with the main line
```

| Story | Hard prerequisites | Why |
|---|---|---|
| 01 | — | Everything depends on it |
| 02 | 01, **03 data layer** | Staff users require a department (data-model §2.1) |
| 03 | 01; endpoints need 02 | `/departments` and `/branches` are `Agent`-gated |
| 04 | 01, 02, 03, **16 A** | `Customer.branchId` required; default branch and attachment cap configured |
| 05 | 01–04, **16 A** | Category list, category→department map, SLA targets |
| 06 | 05 | Operates on `Ticket` and `TicketActivity` |
| 07 | 05, 06, 04, 16 A | Reply triggers the lifecycle rule Story 06 owns |
| 08 | 04, 05, 06, 07, 16 A | Assembles the screens those stories deliver. **Not 09 — see S9-8** |
| 09 | 03, 05, 06, 16 A | Reuses Story 06's escalation path; needs `Department.managerUserId` |
| 10 | 01 only | **Parallelizable from Phase 1** |
| 11 | 10, 05, 06, 07, 08 | Consumes the seam; renders in the ticket view |
| 12 | 02; task 4 and the ticket region need 05 | Authoring is Administrator-only; suggestions read a ticket |
| 13 | 02, 04, 05, 06, 07, 12 | Builds the screens around Story 07's endpoints |
| 14 | 06, 08; verified jointly with 13 | The visibility rule is proven against the portal |
| 15 | 03, 05, 06, **09**, **13** | The SLA tile needs breach flags; the satisfaction tile needs feedback |
| 16 A | 01, 03 | Category map and default branch reference real ids |
| 16 B | 02, 06 | Audits the actions those stories write |
| 17 A | 01 | Delivered inside 01 |
| 17 B | **every T1/T2 screen** (02–16) | A translation pass over screens that do not exist would be repeated |
| 18 | 07, 09, 04 | Must match the existing message model |

---

## 5. What can run in parallel, and what cannot

### Safe to parallelize

| Parallel work | Why it is safe |
|---|---|
| **Story 10 (AI seam) alongside Phases 1–4** | Depends only on Story 01. Touches `Modules/Ai` and `Infrastructure/Seams/Ai` — **no file any other story edits**, and it creates **no entity and no migration** (DM-5) |
| **Story 12 backend tasks 1–3 and admin/staff screens alongside Phases 3–5** | Depends only on Story 02. `KnowledgeArticle` has **no relationship to any other entity** (data-model §2.13), so its migration is independent. Only task 4 (suggested articles) and the ticket-detail region need Story 05 |
| **Backend and front end within one story** | Every DTO is fixed by `api-design.md` §6 before any plan runs. Two workers can build against the contract and meet at integration |
| **Story 15's four aggregate queries** | Independent of one another; only their prerequisites differ |
| **Story 17 Part B's translation pass, split by area** | `workspace`, `portal`, `admin` dictionaries can be swept in parallel, provided `shared/` and `layout/` are swept **first and once** — architecture §2.3: *"mirroring is handled once in `shared/` and `layout/`, never per feature"* |
| **Test authoring alongside implementation** | Every test file in every plan names its story; none is shared |

### Must be sequential

| Constraint | Reason |
|---|---|
| **EF Core migrations** | One `DbContext`, one migration chain (AD-1, A-12). Two concurrent `dotnet ef migrations add` runs conflict in the model snapshot. **Only one worker generates a migration at a time**, in the phase order of §3 |
| **Phase 1 → 2 → 3 → 4 on the main line** | Each phase's entities and services are the next one's prerequisites |
| **03 data → 02 → 03 endpoints** | The interleave of S9-12 |
| **16 Part A → 04 and 05** | Startup validation must pass before a ticket can exist |
| **05 → 06** | Story 06 adds the guarded `TransitionTo` that Story 05 deliberately withholds, so no caller can bypass a state machine that does not yet exist |
| **06 → 07** | Story 07 calls `ApplyAutomaticCustomerReplyTransitionAsync` |
| **06 → 09** | Story 09 **reuses** `EscalateAsync`; the intake forbids duplicating it |
| **09 → 15** | The SLA tile has no data without breach flags |
| **13 → 15** | The satisfaction tile has no data without feedback |
| **All screens → 17 Part B** | A translation pass over screens that do not exist would be repeated |
| **07 → 18** | The adapters must match the existing message model, not define one |
| **Seeder order** | `Order`: 10 organization · 20 identity · 30 customers · 40 tickets · 50 knowledge. Later seeders reference deterministic ids from earlier ones |

### A workable two-worker split

| | Worker A (main line) | Worker B |
|---|---|---|
| Phase 0–1 | 01, 03 data, 02, 03 api | **10** (AI seam, backend only) |
| Phase 2–3 | 16 A, 04, 05, 06 | **12** backend tasks 1–3, admin authoring screens |
| Phase 4–5 | 07, 08, 09 | **11** (once 08 lands), 12 task 4 |
| Phase 6–7 | 13, 15 | 14, 16 B |
| Phase 8 | 17 B | 18 |

**Worker B never generates a migration while Worker A is mid-phase.** Story 12's
`KnowledgeArticles` migration is taken at a phase boundary.

---

## 6. Shared conventions every plan assumes

Fixed once here so eighteen plans do not each invent them.

### Backend

```
backend/
  SupportCrm.sln
  Directory.Build.props                    net10.0 · nullable · TreatWarningsAsErrors
  src/SupportCrm.Domain/          Modules/{Identity,Organization,Customers,Tickets,Sla,
                                           Knowledge,Ai,Reporting,Administration,Integrations}/
  src/SupportCrm.Application/     Modules/… · Abstractions/ · Configuration/
  src/SupportCrm.Infrastructure/  Persistence/{Configurations,Seeders} · Seams/{Ai,Channels,Erp} · Storage/ · Security/
  src/SupportCrm.Api/             Controllers/ · Auth/ · Errors/ · BackgroundServices/
  tests/SupportCrm.Tests/         Domain/ · Identity/ · Tickets/ · Portal/ · Sla/ · Ai/ · Knowledge/ ·
                                  Reporting/ · Administration/ · Integrations/ · Customers/ · Organization/
```

- **Ten module folders** in `Domain` and `Application` (architecture §1). `Infrastructure` is
  organized by **concern**, not by module.
- **The dependency rule is compiler-enforced** (AD-2). `SupportCrm.Domain` ends the project with
  **zero** package and project references (AD-4).
- **No repository, no unit-of-work, no mediator, no domain-event bus** (AD-3, architecture §8).
- **Route prefix `/api/v1` applied once**, via a shared `ApiControllerBase`.
- **Migration names** are the story's subject in PascalCase: `InitialSchema`, `Customers`,
  `Tickets`, `TicketMessages`, `Notifications`, `KnowledgeArticles`, `CustomerFeedback`,
  `TasksAndInternalNotes`.
- **Seeder order** as listed in §5.
- **String column lengths come from `data-model.md` §6.1** — five tiers (`Code` 64 · `Name` 200 ·
  `Email` 256 · `Line` 512 · `Text` max), with a tier assigned to every string field in the model.
  **No story picks a length.** A column in an index key must be `Code`, `Name` or `Email`;
  `Text` is never indexed. Added by amendment 2026-08-26, after Story 03 hit the gap.
- **One unit of work per request, committed once** (architecture §3).
- Toolchain verified present: **.NET SDK 10.0.202**, **Node 22.21**, **Angular CLI 20.3.7**,
  **Docker 28.5.1**. `dotnet-ef` **10.0.11** is pinned as a local tool in
  `backend/dotnet-tools.json` (added during Story 03; `dotnet ef` had nothing to run before).

### Front end

```
frontend/src/app/
  core/     auth/ api/ config/ i18n/ interceptors/ guards/ notifications/
  shared/   components/ pipes/ directives/ styles/ lifecycle/
  layout/   staff-shell/ portal-shell/ auth-shell/
  features/ auth/ workspace/ portal/ admin/
```

- **Angular 20 standalone + PrimeNG 20 (Aura preset) + Transloco.** Compile-time localization is
  **rejected** by AD-9.
- **Feature components never call `HttpClient` directly** (architecture §2.2).
- **Filters live in the URL** (UI-9) and their names **mirror the API exactly**.
- **Logical CSS properties only**, enforced by a stylelint rule from Story 01.
- **Guards hide; they do not protect** — every guard mirrors an independently enforced server rule.
- **Error text comes from the Problem Details `type` slug**, translated; the server's `detail` is
  never rendered raw.
- **`404` reads identically for missing and out-of-scope** (AP-4).

### Server-side rules that appear in more than one plan

| Rule | One implementation, in |
|---|---|
| Per-request identity resolution (AD-15) | `Api/Auth/CurrentUserMiddleware.cs` — Story 02 |
| Department/ownership scoping (AD-5, §4.3) | `Modules/Tickets/TicketScope.cs` — Story 05 |
| Transition **legality** (A-5) | `Domain/Modules/Tickets/TicketLifecycle.cs` — Story 06 |
| Transition **authority** (A-16) | `Application/Modules/Tickets/TransitionAuthority.cs` — Story 06 |
| Ticket activity writing (§2.5) | `TicketActivityRecorder` — Story 05 |
| Audit writing (§2.4) | `AuditRecorder` — Story 02 |
| Notification raising (A-13) | `INotificationPublisher` — Story 06 port, Story 09 implementation |
| SLA arithmetic (A-3) | `Domain/Modules/Sla/SlaClock.cs` — Story 05 |
| Attachment storage and authorized download (§4.4, AP-19) | `AttachmentService` + `LocalDiskAttachmentStorage` — Story 04 |
| Keyword search (AD-13) | `Modules/Knowledge/ArticleSearch.cs` — Story 12 |
| Portal article visibility (constraint 19) | `PortalArticleService.PortalVisible` — Story 12 |

---

## 7. Audit findings — what planning exposed

A full traceability and consistency audit was run across `product-scope.md`, `architecture.md`,
`data-model.md`, `api-design.md`, `ui-design.md`, `story-backlog.md`, `sdd-workflow.md` and all 18
intakes. **Findings are reported, not resolved.** Four block a named acceptance criterion.

### 7.1 Blocking — a story's acceptance criterion cannot be met until a decision is taken

| ID | Finding | Blocks | Nature |
|---|---|---|---|
| **S9-1** | **The dashboard task region has no endpoint.** `ui-design.md` §5.1 and §13, this story's own AC, and `data-model.md` §6's index `TicketTask(assignedUserId, isDone, dueAt)` *"Open and overdue tasks on the dashboard"* all assume a **cross-ticket** task list. `api-design.md` §5.6 publishes only `GET`/`POST /tickets/{id}/tasks` | Story 14 AC 2; Story 08 region | **Contradiction.** Either publish `GET /tasks?assignedUserId=me&isDone=false` (a Stage 7 change, 66 → 67) **or** drop the region (a Stage 8 change, recorded per product-scope §10) |
| **S9-4** | **No contract path records AI suggestion acceptance or override.** Story 11's AC requires it, `data-model.md` §2.7 provides `AiSuggestionOffered`/`AiSuggestionResolved`, and `api-design.md` §5.8 says it happens *"when the agent saves the ticket"* — but `POST /tickets` accepts **no field** that could carry it, §6.11 adds none, and §7 lists none | Story 11 AC 4 | **Contradiction.** Either add a request field to `POST /tickets` or add a recording endpoint. Both are Stage 7 changes |
| **S9-9** | **PF-4 was required to be pinned *before* Story 15 was planned** (`api-design.md` §9 item 2) and was not. *"Tickets assigned"* is currently-assigned or ever-assigned; the response shape is identical either way | Story 15 `agentPerformance.assignedCount` | **Process gap.** Isolated to `AgentPerformanceQuery.AssignedCount`, which throws until decided |
| **S9-10** | **PF-2 comes due in Story 18.** The inbound fake adapter has **no actor**, while `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required and `actorKind = System` is reserved for the SLA monitor (R-14) | Story 18 AC 3 | **Carried finding, now scheduled.** Three options are set out in that plan; none is chosen |

### 7.2 Non-blocking — resolved from the approved documents, recorded for visibility

| ID | Finding | Resolution taken |
|---|---|---|
| **S9-2** | **`GET`/`POST /tickets/{id}/attachments` is assigned to no story** in `api-design.md` §10.1, though §5.6 lists both and Story 04's AC requires *"attached to a ticket… and downloaded again"* | The shared service and the AP-19 download land in Story 04; the two ticket-scoped endpoints are published in Story 05, where `Ticket` and its scoping helper exist. Read from the intakes, not invented |
| **S9-3** | **OQ-2 and OQ-3 reach further than `PROJECT-PROGRESS.md` §6.1 records.** OQ-2 is listed against Story 09, but the **first** code path that changes a priority is Story 05's `PATCH /tickets/{id}`. OQ-3 is listed against Story 09, but Story 06's **manual** escalation has the same undefined recipient | Both stories carry the block. OQ-2 is isolated to `SlaClock.OnPriorityChanged`; OQ-3 leaves the flag and priority raise unaffected and publishes no notification when a department has no manager. **Neither is answered** |
| **S9-5** | **Three entity placements deviate from `data-model.md` §7's story→entity map**, because the AC that first *writes* the data sits in an earlier story: `AuditEntry` (§7 says 16; Story 02's AC requires sign-in recording), `TicketActivity` (§7 says 06; Story 05's AC requires assignment recording), SLA due-date computation (§7 says 09; §2.6 makes both timestamps **required at creation**) | Placement rule adopted and applied consistently: **the entity lands with the story that first writes it; the read surface lands with the story that owns it.** This changes no business rule and no schema |
| **S9-6** | **`GET /tickets` has no `customerId` filter**, yet `ui-design.md` §5.3's customer panel shows *"recent tickets"* | Derived client-side from distinct `ticketId` values in `GET /customers/{id}/timeline`, which already carries `ticketId` and `ticketSubject`. **No filter invented** |
| **S9-7** | **`POST /auth/register` cannot be implemented in Story 02.** A-15 requires a `Customer`, a `Branch` and a configured default branch | Moved to Story 04 task 7; `api-design.md` §10.1's story mapping is unchanged as a document |
| **S9-8** | **Story 08 does not hard-depend on Story 09**, contrary to `PROJECT-PROGRESS.md` §3 and the intake's fallback offer. Due dates are **required at creation** (`data-model.md` §2.6), so SLA-urgency ordering is available from Story 05 | The queue is built once, correctly. Story 09 adds only the *population* of the latching breach flags. **No fallback ordering, and no swap to record** |
| **S9-11** | **`ui-design.md`'s header is stale**: it cites *"65 endpoints, AP-1…AP-18"* while `api-design.md` now has **66** endpoints and **AP-19** (`GET /attachments/{attachmentId}/content`, added when F-2 was closed) | **Documentation staleness only.** Every screen in §5–§7 maps to an endpoint that exists; no screen consumes a removed one. A one-line header correction, deferred to the user |
| **S9-12** | **Story 02 depends on Story 03's entities** — `POST /users` requires a `departmentId` for staff | Phase 1 executes them as an **adjacent interleaved pair**: Story 03's data layer, then Story 02, then Story 03's endpoints and filters. Both intakes already say *"planned together or in immediate sequence"* |
| **S9-13** | **`api-design.md` §10.1 assigns `/config` and `/config/staff` to Story 01**, whose AC says *"No business entity, screen, or endpoint beyond health exists in this story"* — and both endpoints require authentication, which Story 01 does not have | Story 01 delivers `/health` and the **anonymous** `/config/bootstrap` plus the options mechanism; the two authenticated tiers land in **Story 16 Part A**, which §10.1 also lists against Story 16. Consistent with the split-in-time exception already in `docs/story-backlog.md` |

### 7.3 Open questions — carried unchanged, resolved by nobody in this stage

| ID | Question | Must be answered before |
|---|---|---|
| **OQ-1** | The CSAT rating scale | Story 13 task 9 (the control's shape) and Story 15's satisfaction tile |
| **OQ-2** | On a priority change, do SLA due dates recompute or stay frozen? | Story 05's `PATCH` priority branch **and** Story 09 task 4 |
| ~~**OQ-3**~~ | ~~Who is notified on breach when a department has no manager?~~ | **Closed 2026-08-31 by A-21 — the notification climbs to the next authority level** (every active `Manager`, else every active `Administrator`, else nobody), and escalation is never blocked. Implemented once as `IEscalationRecipientPolicy`, which **Story 06's escalate and Story 09's sweep both call** rather than re-express |
| ~~**OQ-5**~~ | ~~Does changing `Customer.email` change a linked portal login's sign-in email?~~ | **Closed 2026-08-27 by A-19 — yes, atomically.** Story 04 **task 3** implements it (the "task 5" this row named was always the wrong task; task 5 is the timeline) |
| **F-1** | Should the ticket payload expose `allowedTransitions`? | **Nothing.** `ui-design.md` **UI-3** is approved and says compute client-side; the server remains the authority and a wrong offer gets `403`/`409`. The `sdd-workflow.md` gate 9 → 10 requires no decision here, so **F-1 stays open**. Story 06 confines the duplicated matrix to **one file**, `shared/lifecycle/transition-matrix.ts`, so closing F-1 later deletes exactly one file |
| **PF-2** | System actor for inbound channel tickets | Story 18 — see **S9-10** |
| **PF-4** | *"Tickets assigned"* — currently or ever? | Story 15 — see **S9-9** |
| **PF-5** | `firstRespondedAt` is null on a ticket resolved without a reply, so it is permanently first-response breached | **Nothing is blocked.** Story 09 implements A-3 as written and **reports** the consequence; Story 08 renders `null` as **"—"**, never "breached" and never "0" |
| **N-5** | `data-model.md` types `rating` as *"an ordinal value"* while OQ-1's candidates include a binary thumbs up/down | **Recorded, not fixed** — resolving it would pre-empt OQ-1 |
| **product-scope §9 · 1, 2, 3, 4, 5, 6, 7** | AI provider and data residency · ERP product · unbounded external systems · tenancy model · real SLA policy · chatbot handoff · anonymous submission | **None.** Each sits behind a seam or a configuration point precisely so it can stay open (architecture §9) |

---

## 8. Traceability

### 8.1 Story → endpoints → entities → screens

| NN | Endpoints delivered | Entities introduced | Screens |
|---|---|---|---|
| 01 | `/health`, `/config/bootstrap` | — | Shells, `/403`, `/404`, `/error` |
| 02 | `/auth/login`, `/auth/me`, `/users` ×5 | `User`, `AuditEntry` *(S9-5)* | Sign in, register (scaffold), avatar menu, guards, `/admin/users` ×2 |
| 03 | `/departments`, `/branches` | `Department`, `Branch` | Department and branch filters |
| 04 | `/customers` ×9, `/attachments/{id}/content`, **`/auth/register`** *(S9-7)* | `Customer`, `CustomerNote`, `Attachment` | Customer directory, customer detail, register |
| 05 | `GET`/`POST /tickets`, `GET`/`PATCH /tickets/{id}`, `/assignment`, **`/tickets/{id}/attachments` ×2** *(S9-2)* | `Ticket`, `TicketActivity` *(S9-5)* | Ticket list, ticket detail header, customer panel |
| 06 | `/transition`, `/escalate`, `/activity` | — | Transition menu, escalate, activity region |
| 07 | `/tickets/{id}/messages` ×2, `POST /portal/tickets`, `/portal/tickets/{id}/messages` ×2 | `TicketMessage` | Staff thread, composer, portal stubs |
| 08 | **none** | — | **My queue**, customer panel, quick replies |
| 09 | `/notifications` ×2 | `Notification` | Bell, notification screen |
| 10 | **none** *(a conclusion, api-design §10.1)* | **none** (DM-5) | **none** |
| 11 | `/tickets/{id}/ai/summary`, `/ai/suggested-reply`, `/ai/classification-suggestion` | — | AI assist panel, categorization at creation |
| 12 | `/kb/articles` ×6, `/tickets/{id}/suggested-articles`, `/portal/kb/articles` ×2 | `KnowledgeArticle` | Staff knowledge ×2, admin authoring ×2, portal help ×2, suggested-articles region |
| 13 | `GET /portal/tickets`, `GET /portal/tickets/{id}`, `/portal/…/transition`, `/attachments` ×2, `/feedback` | `CustomerFeedback` | All four portal screens |
| 14 | `/internal-notes` ×2, `/tasks` ×2, `PATCH /tasks/{taskId}` | `TicketInternalNote`, `TicketTask` | Internal-notes region, tasks region, *(queue task region — **S9-1**)* |
| 15 | `/reports/dashboard` | **none** (aggregates) | Reports |
| 16 | `/config`, `/config/staff` *(Part A)*, `/audit` *(Part B)* | — | `/admin/audit`, `/admin/configuration` |
| 17 | **none** *(a conclusion)* | **none** | Cross-cutting across all 24 |
| 18 | **none** *(AP-11, §8.3)* | **none** (DM-6) | **none** |

**Endpoint total: 66**, matching `api-design.md` §1. **Entity total: 15**, matching
`data-model.md` §3. **Screen total: 24**, matching `ui-design.md` §13.

### 8.2 Product-scope item → plan

| Scope item | Plan(s) |
|---|---|
| T1-A Customer management | 04 |
| T1-B Ticket management | 05, 06 |
| T1-C Agent dashboard | 08 |
| T1-D Users, roles, permissions | 02 (+ scoping in 05) |
| T1-E Multi-department routing | 03 (+ enforcement in 05) |
| T1-F Ticket-facing AI | 10, 11 |
| T2-A Attachments | 04 (+ ticket endpoints in 05) |
| T2-B Web form + portal messaging | 07 |
| T2-C Tasks & internal notes | 14 |
| T2-D SLA & automation | 09 |
| T2-E Knowledge base | 12 |
| T2-F Customer portal | 13 |
| T2-G Reports & dashboards | 15 |
| T2-H Audit logs | 02 (write path), 16 B (read surface) |
| T2-I System configuration | 16 A |
| T2-J Arabic & English | 17 A + 17 B |
| T2-K Multi-branch | 03, 04, 15 |
| T2-L Public API | 01 |
| T3-A External channels | 18 |
| T3-B Live chat | 18 (documented, not built) |
| T3-C AI chatbot | 10 (extension point documented), 18 (documented) |
| T3-D ERP & external systems | 18 |
| T3-E Custom branding | 01 (loader), 17 B (proof) |
| T3-F Mobile / responsive | 08, 13, 17 B |
| T3-G Multi-tenancy | 03 (boundary noted, not built) |
| **T4** | **Absent by decision.** Every plan restates the relevant exclusions in its Done Criteria |

### 8.3 Decision → where a plan enforces it

| Decision | Enforced in |
|---|---|
| **A-2** department is the boundary, branch is reporting | 05 `TicketScope` · 03 test that `Ticket` has no branch member · 15 branch filter through the customer |
| **A-3** 24/7 clock, per-priority targets, no pause | 05 `SlaClock.ComputeAtCreation` · 09 sweep + the explicit `Pending`-does-not-pause test |
| **A-4** four fixed hierarchical roles | 02 policies · every controller's policy attribute |
| **A-5** six statuses, escalation is an action | 06 `TicketLifecycle` (6×6 matrix test) · 06 `Escalation.RaiseOneLevel` |
| **A-6** fixed categories and priorities | 16 A options + startup validation · 05 validation |
| **A-8 / AD-12** AI advisory only | 10 reflection test that no action method exists · 11 UI-6 labelling |
| **A-13** in-app notifications only, four types | 06 `NotificationType` enum · 09 |
| **A-14** category determines department | 16 A startup validation · 05 `CreateAsync` step 2 · 07 portal submit |
| **A-15** default branch, link existing profile | 04 task 7, three outcomes each tested |
| **A-16** transition authority | 06 `TransitionAuthority` · 13 portal tests |
| **A-17** `isUrgent` is customer input only | 05 (`400` on staff create) · 07 (accepted, does not set priority) |
| **A-18** assignment is not the start of work | 05 assign test asserts `status = New` · 09 auto-assign test · 13 cancel-while-assigned test |
| **AD-5** explicit scoping helper | 05 `TicketScope`, and 15's comment on why reports do **not** compose it |
| **AD-8** migrate and seed at startup | 01 `DatabaseInitializer`, commented as a non-production choice |
| **AD-9** runtime i18n | 01 task 12 (Transloco) · 17 B state-survival test |
| **AD-10** history separate from audit | 16 B `AuditAndHistoryAreSeparateTests` |
| **AD-13** database keyword search | 12 `ArticleSearch` · grep check for excluded search infrastructure |
| **AD-15** authorization data resolved per request | 02 `CurrentUserMiddleware` + the deactivation regression test |
| **AP-4** out of scope returns `404` | 05, 12, 13 tests |
| **AP-5** separate portal path space | 07, 13, 14 (`/portal/.../internal-notes` not routable) |
| **AP-11** no inbound endpoint | 07 and 18 grep checks |
| **AP-17** three configuration tiers | 16 A tier tests, asserted on raw JSON keys |
| **AP-19** one authorized download endpoint | 04 `AttachmentService.OpenForDownloadAsync` |
| **DM-6** seams persist one field | 18 test that `externalReference` is settable through no endpoint |
| **DM-7** `CustomerFeedback` under `Tickets` | 13 task 1 file location |
| **R-13 / R-14** automatic `Pending → Open`, attributed to the customer | 06 method · 07 end-to-end test asserting the actor |
| **T2-C** internal-note visibility | 14 canary test across three customer-facing reads |
| **T2-I** no configuration UI | 16 A `NoConfigurationEntityTests` · 16 B DOM check for zero inputs |

---

## 9. Verification strategy

Coverage is **targeted, not exhaustive** (product-scope §8, architecture §2.1). Tests concentrate on
the rules that carry the most risk.

| Layer | What is tested | Where |
|---|---|---|
| **Domain unit tests, no database** | Transition legality (full 6×6), escalation one-level with `Urgent` fixed, SLA arithmetic across a weekend and midnight | 05, 06 |
| **Integration tests through the API, as each role, bypassing the UI** — required explicitly by the `auth-and-roles`, `ticket-core`, `tasks-internal-notes` and `portal-self-service` intakes | Department scoping, customer isolation, transition authority, internal-note invisibility, portal isolation, report access, audit access | 02, 05, 06, 13, 14, 15, 16 B |
| **Structural tests (reflection / grep)** | No AI action method (AD-12) · no repository type (AD-3) · no configuration entity (T2-I) · append-only recorders · `Ticket` has no branch member · exactly one channel adapter · no excluded infrastructure package | 03, 05, 06, 10, 14, 16, 18 |
| **Raw-JSON assertions** | No `passwordHash`, no `storagePath`, no staff field on a portal payload, no internal-note text in any customer-facing read | 02, 04, 13, 14 |
| **Manual demo checks** | One-command start · draft survives the customer panel and the language switch · branding changes without a front-end rebuild · no horizontal body scroll at 390 px | 01, 08, 17 B |

**Commands, uniform across plans:** `dotnet build backend/SupportCrm.sln` ·
`dotnet test backend/SupportCrm.sln [--filter …]` · `cd frontend && npm ci && npm run build` ·
`npx stylelint "src/**/*.scss"` · `docker compose up --build`.

---

## 10. Gate 9 → 10

`docs/sdd-workflow.md` §4: *"A story's plan exists, cites concrete paths and verification commands,
and its prerequisites are already implemented."*

| Condition | Status |
|---|---|
| A plan exists for every story | ✅ 18 of 18 |
| Plans cite concrete paths, type names and signatures | ✅ Every plan names files to create and the symbols in them |
| Plans cite runnable verification commands | ✅ 6–9 steps per plan |
| Prerequisites stated per story | ✅ §4, and a `## Prerequisites` block in each plan |
| **Prerequisites already implemented** | ⛔ **Not yet — and it cannot be.** This is a per-story gate, satisfied one story at a time during Stage 10. It is met for **Story 01 now** (no prerequisites) and for each later story as its predecessor completes |

**Stage 9 is complete. Gate 9 → 10 is met for Story 01, which is the only story it can be met for
before implementation begins.**

**Before Stage 10 starts, four decisions are outstanding** — S9-1, S9-4, S9-9/PF-4 and
S9-10/PF-2 — plus the open questions OQ-1, OQ-2 and OQ-3. **None blocks Story 01, and none blocks
Phase 0 or Phase 1.** The earliest is **OQ-2, which blocks Story 05** in Phase 3.

> **Since written:** **OQ-2 was closed 2026-08-30 by A-20** and implemented by Story 05; **OQ-3 was
> closed 2026-08-31 by A-21**, with its shared `IEscalationRecipientPolicy` built before Story 06
> began. **OQ-1 is the only open question left** of the three, and it reaches Stories 13 and 15.

**OQ-5 was closed on 2026-08-27 by A-19** — a customer's email and their portal sign-in are one
address — which unblocks Story 04 task 3. It was the only open question that gated Phase 2.
