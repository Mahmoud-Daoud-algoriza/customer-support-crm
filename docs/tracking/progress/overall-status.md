[← Back to PROJECT-PROGRESS.md](../PROJECT-PROGRESS.md)

## 1. Overall Status

### Snapshot (as of 2026-09-06)

| | |
|---|---|
| **SDD stage** | Stage 10 (Implementation) in progress — 9 of 10 pipeline stages complete (90% of the pipeline) |
| **Phase** | Phase 7, in progress (details in [Phase-by-Phase Progress](#phase-by-phase-progress) below) |
| **Status** | 🟢 On track — 2 of 4 originally-blocked acceptance criteria remain (see [Blockers & Decisions Needed](#blockers--decisions-needed)) |
| **Progress** | **71.1%** — method in §1.1 ([progress-calculation.md](progress-calculation.md)); likely an undercount — see the bookkeeping-gap blocker below |
| **Stories implemented & verified in full** | 10 of 18 — stories 01, 02, 03, 04, 05, 06, 07, 12, 13 and 16 (16 counts as one story across both parts) |
| **Stories implemented, verification partial** | 14 and 15 — backend/data-layer/live-stack checks passed 2026-09-06, front-end screen checks outstanding for both, so neither counts toward the 71.1% yet |
| **Published API surface** | 66 operations — with story 15, this is the complete api-design contract |
| **Last updated** | 2026-09-06 |

Stories 01–07, 12, 13 and 16 are implemented **and** verified in full. See §3, §8 and §10 for the
detail behind every number above.

**Why 71.1%, and what would move it.** §1.1 now counts stories 12 and 13; §10 continues to narrate
stories 08–11 as complete, which this task's own measurements again corroborate but did not itself
verify against §8's standard, so they are still not added here (see the bookkeeping-gap blocker
below — **if §10 is right, the figure is 14 of 18 → 14 ÷ 18 × 65 + 35 = 85.6%**, and the previously
recorded 85.9% did not follow from the stated method at any story count and is superseded by this
arithmetic). **Story 14 does not move this figure**, and deliberately so: a story counts only when
it is implemented **and** verified in full, and story 14's front-end screen checks are outstanding.
Covering those checks moves the delivery track from 10 to 11 — and the total to **74.7%** — and not
before.

### Current Focus — Story 15 (`management-dashboard`)

**Status:** implemented — all six plan tasks, delivered whole. Backend, data-layer and live-stack
verification passed 2026-09-06. **Not verified in full**: plan verification step 8 (all four regions
rendering, filters combining and surviving a reload, tables scrolling internally at phone width)
could not be run — no browser-driving capability was available, the same limitation story 14 hit.
The UI evidence is outstanding and named in §8, so story 15 does not count toward §1.1's delivery
track either.

**Decision closed before a line was written — ✅ PF-4 / S9-9 (R-18).** The approved documents
genuinely did not decide *"tickets assigned"*: requirements §9.3 and T2-G word it unqualified,
api-design §5.11 says *"this contract does not decide it"*, §6.8 calls it *"deliberately
unqualified"*, and §9 item 2's requirement to pin it was never met. The product owner chose
***currently assigned*** on 2026-09-06, so `AgentPerformanceQuery.AssignedCount` was implemented
rather than left throwing. This mattered more than the plan assumed: the field is composed into the
single §6.8 response, so a throwing method would have made *every* dashboard call a `500` — PF-4
blocked the whole endpoint, not one field, which is why the decision was taken up front instead of
the story being part-delivered.

**What landed:**
- Four aggregate queries — counts by status/priority/category/department, SLA attainment per
  priority, agent performance, and satisfaction — composed by `DashboardReportService` behind
  **one** `RequireManager` endpoint.
- §9 introduces no entity (data-model §7), so there is no migration.
- `TicketScope` is deliberately not composed, and a test asserts a Manager's unfiltered dashboard
  spans both departments (AD-5).
- Branch resolves **through the customer**, proven live: the API's per-branch counts equal the SQL
  count joined via `Customers` (6 and 5) and **differ** from the count joined via the assigned agent
  (4 and 3).
- *"Empty is not zero"* is implemented three times over, each asserted: no ratings →
  `averageRating: null`; no resolutions → `averageResolutionHours: null`; a priority with no
  tickets → `attainmentPercent: null`, **never `100`**. Proven live on a scratch database with
  every rating deleted — the raw JSON reads `"averageRating":null` exactly as §6.8 writes it.

**Verification:**
- Backend suite: **451 passing, 0 skipped** (was 430/0) — 21 of them this story's own.
- Front end: **66 specs**, unchanged.
- `openapi/v1.json` now publishes **66 operations — the complete api-design contract.**
- ⚠ Front-end screen checks: **outstanding** (see above).

**Findings:** three, all informational — **I-38, I-39, I-40** (§6.8): the resolution average is
computed over a two-column projection rather than in SQL, because no date-arithmetic expression
translates on both providers; AP-15's unknown-parameter refusal is applied to this endpoint only,
with the wider gap reported rather than fixed; and three meaningful nulls opt back into serialization
under §2's own exception. **I-1, I-2, I-12, I-16, I-25, I-35, I-36 and I-37 remain the user's call**
from earlier stories.

**Awaiting explicit approval before the next unit of work** — this task does not start story 16, 17
or 18. The bookkeeping gap for stories 08–11 is untouched and still open, as is story 14's
outstanding UI evidence. Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.

### What's Been Implemented

**Foundation — stories 01–03, story 16 (Parts A & B), story 04, plus the story 05–13 headline
counts:** Stories 01, 02, 03; story 16 in full (Part A's five tasks and Part B's six); story 04 in
full (all fourteen plan tasks); story 05 in full (all thirteen); story 06 in full (all eleven);
story 07 in full (all ten); story 12 in full (all eleven); story 13 in full (all twelve).

- Authentication, the four-role model, per-request identity resolution, user administration, the
  audit recorder, the two organization read endpoints and all three configuration tiers run.
- `backend/` holds the five-project solution; `frontend/` the Angular + PrimeNG app (built on
  PrimeNG Sakai `20.0.0`); `docker-compose.yml` runs three services and **two volumes**.
- The `Customers` module is published and reachable from a screen: profiles with the **A-19 email
  propagation**, immutable notes, the timeline read projection and attachments on local disk —
  behind the **ten endpoints of api-design §5.5**, plus **`POST /auth/register`**.

**Story 05 — `ticket-core`:**
- Adds the ticket core — `Tickets` and `TicketActivities` mapped and migrated.
- `TicketScope` — the one department-scoping helper every later ticket query composes (AD-5).
- `SlaClock` computes both due timestamps at creation and **freezes them on a priority change
  (A-20)**.
- Workspace gains a filtered ticket list, a ticket detail header, an assign control and a customer
  panel.

**Story 06 — `ticket-lifecycle`:**
- `TicketLifecycle` expresses A-5's graph once in the Domain; `Ticket.TransitionTo` is the only
  writer of `Status`.
- `TransitionAuthority` holds A-16 separately, so authority fails `403` and legality fails `409`
  with `allowedTransitions`.
- `Escalation.RaiseOneLevel` plus **A-21**'s recipient policy drive `POST /escalate`.
- `TicketActivityQueryService` publishes the append-only history, and `CustomerTimelineService` —
  written by story 04 against an empty ticket set — now projects real rows.

**Story 07 — `ticket-intake-messaging`:**
- `TicketMessage` — the single normalized message model, carrying `direction` and `channel`, both
  **server-derived** (PF-7, AP-10).
- `TicketMessageService.PostAsync` is the **one ingestion method** the staff endpoint, the portal
  endpoint and — later, in-process — story 18's adapter all call, so channel origin is data rather
  than a code path (architecture §5.2).
- `PortalTicketService` publishes the authenticated web form, deriving customer from the caller and
  department from the category (A-9, A-14).
- A customer reply on a `Pending` ticket returns it to `Open` in the same transaction, attributed to
  the replying customer.
- `openapi/v1.json`: 28 paths as of story 07 (25 after story 06, 22 after story 05, 18 after story
  04, 11 before it); **re-measured live in this task, it publishes 34**, including the one path Part
  B adds, `/api/v1/audit` — confirmed the only new one this slice contributes. The other five
  plausibly belong to stories 09 and 11 (notifications and the AI assists, per §10's narrative) but
  were not individually enumerated or attributed in this task — see the bookkeeping gap §1.1
  discloses.

**Story 16 Part B — administration:**
- `AuditQueryService` is the one read method over `AuditEntry` — newest first, filterable by
  `actorUserId`, `action` and a `from`/`to` range — behind `AuditController`'s single `GET /audit`,
  `RequireAdministrator`-gated, with no write verb of any kind.
- Front end adds `AuditClient`, the `/admin/audit` screen (URL-bound filters, no row action) and the
  `/admin/configuration` screen, which reads `GET /config` and `GET /config/staff` through the
  **existing** `PlatformApiService` and reads branding from the **already-loaded**
  `RuntimeConfigService` rather than a third call.
- Demo tickets across two departments and four priorities are seeded at startup, **two of them now
  carrying a thread and one left in `Pending`** so R-13 is one click away.

**Story 13 — `portal-self-service`:**
- `CustomerFeedback` — the sole CSAT input, owned by the **`Tickets`** module (**DM-7**) —
  write-once by construction, no mutator and no update or delete method anywhere.
- `CustomerFeedbackService` checks four preconditions in a fixed order (scope `404` → reached
  `Resolved` `409` → one-per-ticket `409` → **the configured range** `400`), with **no rating
  constant anywhere in the Domain, the schema, the service, the tests or the UI** (**OQ-1**).
- `PortalTicketService` gained the caller's own list, their own detail and the transition — the
  last of which **delegates to story 06's `TicketLifecycleService` rather than restating A-16** and
  re-reads the ticket as the portal DTO so no staff field can be returned.
- `PortalTicketsController` publishes the remaining **six** endpoints, taking the portal to
  **eleven** and matching api-design §5.7 exactly.
- Front end replaces the Story 07 stubs: a portal shell with **two destinations, no sidebar and no
  notification bell** (§4.2), the **card** list of §7.1 with its *"response needed from you"* cue,
  the four-input form of §7.2, and the five-region detail of §7.3 — cancel offered **only while
  `New`**, reopen **only on `Resolved`**, no manual reopen on `Pending`, and a `RatingInput` that
  renders **from `feedback.ratingScale`** and hardcodes no scale.

**Story 14 — `tasks-internal-notes`:**
- `TicketInternalNote` — immutable by construction, no mutator, and **a separate table no
  customer-facing query names**, which is how T2-C's visibility rule is structural rather than a
  filter (data-model §2.9).
- `TicketTask`, whose single mutator `SetDone` is the only writer of `completedAt` (§5 constraint
  23).
- `TicketActivity.InternalNotePosted` — a new factory that fixes both the type and **`Internal`
  visibility**, so §2.7's two invariants cannot be spelled wrongly at a call site.
- `TicketActivityConfiguration` completes `InternalNoteId` as a real foreign key, the placeholder it
  had carried since story 05.
- `TasksAndInternalNotes` migration creates both tables with the two §6 indexes.
- `TicketsController` gains **five** endpoints — `GET`/`POST /internal-notes`, `GET`/`POST /tasks`,
  `PATCH /tasks/{taskId}` — all `RequireAgent`, and **no `/portal` counterpart exists or may be
  added** (AP-5).
- `openapi/v1.json` measured live in this task publishes **48 paths and 65 operations**, which is
  api-design's 66 endpoints minus story 15's `GET /reports/dashboard`.
- Front end adds the **internal-notes region** with its persistent *"Not visible to the customer"*
  marker (UI-5) and the **tasks region** with overdue rows visually distinct, both wired into ticket
  detail where ui-design §5.3's layout puts them.
- ⛔ **The dashboard task region is not built (S9-1).**

**Story 15 — `management-dashboard`:**
- New `Application/Modules/Reporting/` folder — the tenth module of architecture §1 to be
  populated — holding `DashboardReportService` and the four aggregate queries of §9.1–§9.4.
- §9 introduces no entity (data-model §7), so story 15 adds **no migration and no DbSet**: every
  number is an aggregate over data other stories own.
- `DashboardReportService.Filtered` applies the two filters **once**, resolving `branchId`
  **through the customer** because `Ticket` has no branch column (§4.4) — and it **deliberately does
  not compose `TicketScope.ForCaller`**, because Manager and Administrator are unrestricted across
  departments and composing it would let a future role change silently narrow an aggregate, the
  exact failure AD-5 cites.
- `AgentPerformanceQuery` is the **single home of the PF-4 reading** (R-18).
- `ReportsController` publishes **one** `RequireManager` endpoint and enforces AP-15's per-endpoint
  whitelist, so `?format=csv` is a `400` rather than a JSON dashboard.
- Front end adds `ReportsClient`, the `/workspace/reports` route behind a `Manager+` guard, the
  Reports nav entry for Manager and above, and the four regions of ui-design §5.7 — rendered with
  **PrimeNG table and tag components only, with no charting dependency added**.
- `openapi/v1.json`, measured live in this task, now publishes **49 paths and 66 operations — the
  complete api-design contract**, story 15 having closed the one endpoint that was missing.

### Blockers & Decisions Needed

🟢 On track overall — **four acceptance criteria were originally blocked, none of them in the
delivered core**, and two remain:

- ⛔ **S9-1 — now due, and blocking.** Blocks story 14's AC 2 (the dashboard task region) — the
  first of the four to actually stop work. Task 8 was left unbuilt rather than resolved by
  invention. **The decision is the user's**: Option A (publish a cross-ticket task endpoint) or
  Option B (drop the region).
- 🔴 **S9-4 — still open.** One of the two genuine contradictions the stage-9 audit found (alongside
  S9-1). Neither blocks stories 01–07 or 16.
- 🟠 **PF-2 — carried finding, blocks story 18.**
- ✅ **PF-4 / S9-9 — CLOSED 2026-09-06 (R-18).** The product owner chose *currently assigned*, which
  unblocked story 15 in full.
- ⚠ **Bookkeeping gap (flagged in §1.1 and §10).** §10's own narrative already records stories
  08–11 as complete and verified (2026-08-31, with findings I-28–I-34), and this task's own fresh
  measurements corroborate it — but that has never reached this headline, §1.1's count, §3's table
  or §8's evidence rows. This is why 71.1% is likely an undercount: if §10 is right, the true figure
  is 14 of 18 → 14 ÷ 18 × 65 + 35 = **85.6%**.

### Next Immediate Step

**Awaiting the user's choice — not asserted here.** Story 15 is implemented and this task stops at
it. Stories **16 (done)**, **17 Part B** and **18** are what remain of the approved scope, with 17
Part B next in phase order. Four things are outstanding and are the honest candidates for "next":

1. **Front-end screen checks for stories 14 AND 15** — without these, neither is verified in full
   nor counts in §1.1.
2. **The S9-1 decision** — Option A (publish `GET /tasks?assignedUserId=me&isDone=false`) or Option
   B (drop the region) — the only thing standing between story 14 and its AC 2.
3. **The bookkeeping reconciliation** of stories 08–11.
4. **Story 17 Part B** — the translation and RTL pass, which the plan requires to run *after* every
   T1/T2 screen exists — and with story 15 delivered, they now do.

**Caveat:** §10's own item-0 entries still narrate stories 08 through 11 as complete while §3's
table shows them `Not Started`. That reconciliation should happen before "next" is fully
trustworthy, and it remained outside story 14's scope.

### Phase-by-Phase Progress

| Phase | Status | What it covers |
|---|---|---|
| 0 & 1 | ✅ Delivered | Design and planning; project skeleton |
| 2 | ✅ Complete | Story 16 Part A (configuration) and story 04 across all six slices |
| 3 | ✅ Complete | Story 05 (`ticket-core`) and story 06 (`ticket-lifecycle`), both delivered whole, not sliced — **the core loop the assessment rests on is closed** |
| 4 | 🟡 Has begun | Story 07 (`ticket-intake-messaging`), delivered whole |
| 6 | ✅ Complete | Story 12 (knowledge base) and story 13 (customer portal — the second actor surface, carrying most of the permission demonstration) |
| 7 | 🟡 Continues | Story 16 Part B (complete, out of order per the plan's split-in-time exception) → story 14 (implemented, S9-1 open) → story 15 (implemented, verification partial) |

*(Phases 5 and part of 7's ordering are referenced above only as "out of order relative to phases
5–6" in the source material — this document doesn't further detail Phase 5's contents.)*

**Phase 3 in detail:** `Ticket`, `TicketActivity`, `TicketScope`, `SlaClock` and the seven ticket
operations, then the **A-5 state machine, the A-16 authority matrix, escalation as an action
(AP-7), the append-only history read and the completed customer timeline**, with the workspace
transition menu, escalate control and activity region.

**Phase 4 in detail:** the one normalized `TicketMessage` model that carries its channel of origin,
the web-form intake at `POST /portal/tickets`, the two-sided thread, and **the one status side
effect in this API** (R-13/R-14).

**Phase 7 in detail:**
- Story 16 Part B (the audit log and the read-only configuration screen) closed this story out of
  order relative to phases 5–6, exactly as the plan's split-in-time exception describes.
- Story 14 (`tasks-internal-notes`, implemented 2026-09-06) — `TicketInternalNote` and
  `TicketTask`, the five endpoints of api-design §5.6, the ticket-detail internal-notes and tasks
  regions, and the seeded note and task. Its dashboard task region (plan task 8) is **not built**
  and is blocked on S9-1, and its front-end screen checks are outstanding, so the story is not yet
  verified in full.
- Story 15 (`management-dashboard`, followed on 2026-09-06) — the four aggregate queries,
  `GET /reports/dashboard`, the `/workspace/reports` screen and its four regions. PF-4 was answered
  by the product owner before any of it was written (R-18), so nothing was invented and no method
  throws.
- **Phase 7's reporting half is delivered, and the API now publishes every endpoint api-design
  defines.**

---

## Historical — Previous Focus Snapshots

**These are frozen point-in-time snapshots of what "Current Focus" said at an earlier date.** They
are kept for the record, not for current state — see [Current Focus](#current-focus--story-15-management-dashboard)
above for what's true now. Newest first.

#### Story 14 (previous focus, as of 2026-09-06)

**Status:** implemented — plan tasks 1–7, 9 and 10, delivered whole. Backend, data-layer and
live-stack verification passed 2026-09-06. **Not verified in full**: the front-end screen checks of
plan verification step 7 and the browser pass of ui-design §10 could not be run — no
browser-driving capability was available (no puppeteer, no playwright; karma's ChromeHeadless runs
specs, not screens), and installing one was outside the slice. The UI evidence is outstanding and
named in §8, and until it is covered, story 14 does not count toward §1.1's delivery track.

⛔ **Plan task 8 — the dashboard task region — is NOT built, and that is S9-1 coming due.** Four
approved documents assume a cross-ticket task list and api-design §5.6 publishes none; the plan
says *"do not invent an endpoint"*, so `TicketTaskService.ListForUserAsync` was written, marked
`// S9-1: no endpoint publishes this`, and left referenced by no controller, and
`agent-queue.component.ts:175` keeps its empty marked slot. Story 14's AC 2 stays open pending the
user's Option A / Option B decision.

**What was proven:**
- T2-C's visibility rule holds structurally, not by a filter: a canary note
  (`INTERNAL-CANARY-9137`) posted live against real SQL Server appears in the staff notes read and
  the staff activity read, and `grep -c` returns **0** on the portal thread, the portal detail, the
  portal list and the customer timeline — against a thread of **3** messages.
- A customer on the staff note route is **403** on both verbs; `/portal/.../internal-notes` and
  `/portal/.../tasks` are **404**, not routable.
- **The guard was proven load-bearing**: reverting `Visibility = Internal` to `CustomerVisible`
  turned exactly **2** tests red — the leak check and the visibility assertion — with 428
  unaffected; the line was restored and re-proved green (`git diff --stat` showed 68 insertions,
  **0 deletions**).
- `completedAt` round-trips both ways (§5 constraint 23), a body carrying it is **400** with the
  row re-read unchanged (AP-10), and an out-of-department assignee is **422** on the reused
  `assignee-out-of-department` slug.
- The migration applied from scratch on a throwaway database, and the seeder wrote **1 internal
  note and 1 task** onto the ticket that already has 3 customer messages — the one-screen contrast
  task 6 asks for — with the dev volume never wiped.
- `DateTimeOffset` ordering, which SQLite cannot prove, was verified across three UTC offsets.

**Verification:** Backend suite **430 passing, 0 skipped** (was 417/1), of which **13 are this
slice's own**; the long-skipped `InternalNotesAreUnreachableTests` is now implemented, not deleted
or hollowed out, which is why the skip count falls to zero. Front end **66 specs**, unchanged and
unedited.

**Findings:** one new, **I-37** (informational), plus two deliberate deviations recorded in §6.8 —
the `TicketAssigneePolicy` extraction and the re-expressed `CustomerNotesAndTimelineTests` guard.
**I-1, I-2, I-12, I-16, I-25, I-35 and I-36 remain the user's call** from earlier stories.

**Note:** awaiting explicit approval before the next unit of work — this task does not start story
15 or any other story. The bookkeeping gap this file has carried since 2026-09-01 is untouched and
still open: §3's table still shows stories 08–11 `Not Started` while §10 narrates them complete, and
reconciling that is outside story 14's scope. Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.

#### Story 13 (previous focus, as of 2026-09-01)

**Status:** complete — implemented and verified 2026-09-01, delivered whole rather than in slices,
like stories 05, 06, 07 and 12. Phase 6 closes, and with it the second actor surface.

**What landed:**
- A customer signs in to a shell that is not the agent workspace — **two destinations, no sidebar,
  no notification bell** (§4.2) — submits through the four-input form of §7.2, tracks their own
  requests as **cards** with a *"response needed from you"* cue on `Pending` (§7.1), reads the
  thread and replies, and **cannot reach an internal note by any path** (T2-C, AP-5).
- **A-16's customer column is now reachable, which closes finding I-23**: cancel is offered **only
  while `New`** — a window A-18 keeps genuinely open, proven live by cancelling an auto-assigned
  `New` request — and disappears with a line once work has started; reopen is offered **only on
  `Resolved`**; no manual reopen is offered on `Pending`, because R-13 does it automatically and the
  reply's own envelope carries `ticketStatus` and `statusChanged` so the chip moves **in place,
  without a re-fetch** (proven in a real browser: chip `Pending` → `Open` with the *"reopened"*
  cue).
- `CustomerFeedback` lives in the `Tickets` module (DM-7), is unique per ticket and write-once, and
  **declining is a normal outcome** — the absence of a row is the recorded answer, so there is no
  decline endpoint and no "declined" state.

⚠ **OQ-1 is NOT answered here, and nothing in this story answers it by accident:** the schema
carries no check constraint (confirmed live — the database accepts a rating of 999), the service
reads the range from the `Feedback rating scale` key on every call, the tests derive both
out-of-range values from configuration, and `RatingInput` renders `min..max` with no star widget,
no 1–5 array and no thumbs pair.

**Verification:** Backend suite **418 passing, 1 skipped by design** (was 401/1), of which **16 are
this slice's own** and are exactly the sixteen the plan names (`PortalIsolationTests` 6,
`PortalLifecycleTests` 5, `FeedbackTests` 5). Front end **66 specs** (was 62), **4 of them this
slice's**.

**Findings:** two raised — **I-35 and I-36** — and both need a user decision: §7.1 asks each card to
show a *last update* that no payload or entity carries, and the feedback endpoint's second refusal
needed a slug (`feedback-not-available`) that api-design §6.12's enumeration does not list. **I-1,
I-2, I-12, I-16 and I-25 remain the user's call** from earlier stories.

**Note:** awaiting explicit approval before the next unit of work — this task does not start story
14 or any other story. The bookkeeping gap this file has carried since 2026-09-01 is untouched and
still open: §3's table still shows stories 08–11 `Not Started` while §10 narrates them complete, and
reconciling that is outside this story's scope. Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.

#### Story 12 (previous focus, as of 2026-09-01)

**Status:** complete — implemented and verified 2026-09-01, delivered whole rather than in slices,
like stories 05, 06 and 07. Phase 6 opens with the knowledge base.

**What landed:**
- One `KnowledgeArticle` entity with a `type` field — FAQs, help articles and solution guides as one
  concept, not three subsystems (T2-E) — with `visibility` and `isPublished` as two independent
  facts.
- `ArticleSearch` is the one implementation of AD-13, `LIKE` over title and body with a title match
  weighted above a body match, composed by staff search, portal search and §7.4's retrieval alike.
- `PortalArticleService.PortalVisible` is the one implementation of data-model §5 constraint 19, so
  an `Internal` or unpublished article is **`404`, never `403`** (AP-4) — proven live, with a
  byte-identical Problem Details body for missing, internal and unpublished.
- **Suggested solutions are a Knowledge endpoint, not an AI one** (AP-14): `GET
  /tickets/{id}/suggested-articles` scopes the ticket first, extracts keywords from its subject and
  description, and returns existing articles with the database's own `matchScore`.
  `SuggestedArticleService` references no `IAiAssistService`, and a test asserts by route-table
  inspection that no `/ai` path serves suggested solutions.
- Front end: the staff search and reader at `/workspace/knowledge`, the suggested-articles region
  below the AI panel — a different component with different wording, deliberately, because it sits
  next to one labelled AI-generated — the admin authoring list and a plain markdown editor with a
  preview, explicit Publish/Unpublish buttons separate from Save, and no delete or version control
  anywhere, and portal help at `/portal/help`.

**Verification:** Backend suite **401 passing, 1 skipped by design** (was 385/1), of which **16 are
this slice's own** (`KnowledgeVisibilityTests` 8, `SearchAndSuggestionTests` 8). Front end **62
specs** (was 51), **11 of them this slice's**.

**Findings:** none raised.

**Note:** awaiting explicit approval before the next unit of work — this task does not start story
13 or any other story. The bookkeeping gap this file has carried since 2026-09-01 is untouched and
still open: §3's table still shows stories 08–11 `Not Started` while §10 narrates them complete, and
reconciling that is outside this story's scope. Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.

#### Story 16 Part B (previous focus, as of 2026-09-01)

**Status:** complete — implemented and verified 2026-09-01: `AuditQueryService`, `AuditController`
(`GET /audit`, Administrator-only), the `/admin/audit` and `/admin/configuration` screens. This
closes story 16 in full (Part A was 2026-08-27) and, per the plan's own words, is the last
administrative surface — nothing else is consumed by it.

**Verification:**
- Backend suite **385 passing, 1 skipped by design** at the time this task measured it, of which
  **exactly 12 are this slice's own** (`AuditReadTests` 9, `AuditAndHistoryAreSeparateTests` 3) —
  the remaining growth since story 07's recorded 327 was not made by this task and is not
  attributed to it here; see the discrepancy note in §1.1 and §10.
- Front end unchanged by this slice's own count of specs added (0), measured at **51/51** in this
  task against a recorded 32 after story 07 — same caveat.
- Driven live against real SQL Server and, for the front end, against the real running `web`
  container: coverage by hand (a wrong password, a user created and deactivated, a ticket
  transitioned) all appear correctly in `GET /api/v1/audit`; `POST`/`PATCH`/`PUT`/`DELETE /audit`
  are all **405** and the row count is unchanged after the attempt; the configuration screen has
  **zero** `input`/`select`/`textarea`/`button[type=submit]` elements and the audit screen has
  **exactly its four filter controls and nothing else** (the plan's own DOM query, run live).

**Findings:** none raised — Part B's only judgment call (the `to` date-range filter is inclusive of
the whole selected day) is recorded at the code, not as a numbered finding, because it decides a UI
detail no document specifies rather than a product or contract question.

**Note:** awaiting explicit approval before the next unit of work — this task does not start story
12 or any other story. Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.

#### Story 07 (previous focus, as of 2026-08-31)

**Status:** complete — implemented and verified 2026-08-31, delivered whole rather than in slices,
like stories 05 and 06. Phase 4 has begun.

**What landed:** the assessment now has its one real communication channel and the message model
every future channel adapter plugs into: a customer submits through the web form, customer and agent
exchange replies over ordinary request/response, and **the one status side effect in this API**
fires — an `Inbound` message on a `Pending` ticket reopens it in the same transaction, writing both
a `MessagePosted` and a `StatusChanged` row, the latter carrying `actorKind: User` and the replying
customer as actor (R-13, R-14), proven live against real SQL Server. `direction` and `channel` are
server-derived and a body carrying either is a `400` (PF-7, AP-10). **No inbound HTTP endpoint
exists** (AP-11), so PF-2 stays untouched and open for story 18.

**Verification:** Suite **327 passing, 1 skipped by design** (was 309/0); front end **32 specs**,
unchanged.

**Findings:** three — **I-25, I-26, I-27** — and one of them, I-25, needs a user decision: no
approved document states the priority a customer-submitted ticket is created with. **I-1, I-2, I-12
and I-16 remain the user's call** from earlier stories.

**Note:** Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.

#### Story 06 (previous focus, as of 2026-08-31)

**Status:** complete — implemented and verified 2026-08-31, delivered whole rather than in slices,
like story 05. Phase 3 and the assessment's core loop are closed: a ticket can now be created,
scoped, assigned, transitioned through A-5's graph, escalated, and read back with a full
append-only history, and the customer timeline story 04 left against an empty set is populated.

**What landed:**
- The two rules are enforced separately and fail differently — `403 transition-not-permitted` for
  A-16 authority, `409 illegal-transition` for A-5 legality, the latter carrying
  `allowedTransitions`, proven live.
- **A-21 is applied, not re-implemented**: escalation resolves recipients through
  `IEscalationRecipientPolicy`, verified live on both rungs — with the seeded manager, and with the
  manager unseated, where the `Warning` fired and the escalation still succeeded.

**Verification:** Suite **309 passing, 0 skipped** (was 251 after story 05); front end **32 specs**
(was 23).

**Findings:** three, all about the plan rather than the product — **I-22, I-23, I-24** — and none
needs a product decision. **I-1, I-2, I-12 and I-16 remain the user's call** from earlier stories.

**Note:** awaiting explicit approval before story 07. Phase order per
[00-implementation-plan.md](../../../.squad/plans/00-implementation-plan.md) §3.
