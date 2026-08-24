# Customer Support CRM — Project Progress

> **This file reports state. It does not define it.**
> On any conflict the order of authority is:
> 1. [requirements.md](requirements.md) → 2. [product-scope.md](product-scope.md) →
> 3. the stage document ([architecture.md](architecture.md), [data-model.md](data-model.md),
> [sdd-workflow.md](sdd-workflow.md)) → 4. [story-backlog.md](story-backlog.md) and the story
> intakes → 5. **this file**.
>
> Nothing here is a source of truth. Every line is derived from the files above or from the
> repository itself.

---

## 1. Overall Status

| | |
|---|---|
| **Current SDD stage** | **Stage 6 complete → Stage 7 (API Design) is next, not started** |
| **Current phase** | Design. Implementation has **not** begun |
| **Overall status** | 🟢 On track — no blockers to the next stage |
| **Overall progress** | **23%** (method in §1.1) |
| **SDD pipeline** | **6 of 10 stages complete** (60% of the pipeline) |
| **Code written** | **None.** `backend/` and `frontend/` are empty; `docker-compose.yml` is 0 bytes |
| **Last updated** | 2026-08-24 |
| **Current focus** | Stage 7 — API Design (`docs/api-design.md`) |
| **Next immediate step** | See §10, item 1 |

### 1.1 How the 23% is calculated

Two weighted tracks. The weighting is a **stated planning convention, not a measurement** — it is
recorded here so the number is reproducible rather than invented.

| Track | Contents | Weight | Complete | Contribution |
|---|---|---|---|---|
| **Design & planning** | Tracker rows 1–9 (§2), equally weighted at 3.89% each | 35% | 6 of 9 rows | 6 ÷ 9 × 35 = **23.3%** |
| **Delivery** | Row 10 — the 18 stories implemented *and* verified | 65% | 0 of 18 | 0 ÷ 18 × 65 = **0%** |
| | | **100%** | | **≈ 23%** |

**Why 35/65.** [product-scope.md](product-scope.md) §10 defines done in five items, four of which
concern running software. The SDD chain is the method; the working system is the deliverable, so
delivery carries the larger weight. A design-only project is not most of the way finished.

**Recalculate** whenever a row in §2 completes or a story reaches Verified in §3.

---

## 2. SDD Pipeline Progress

**Document exists ≠ stage complete.** A stage is complete only when its gate in
[sdd-workflow.md](sdd-workflow.md) §4 is satisfied. Both columns are shown.

| # | Stage | Status | Artifact(s) | Doc? | Gate | Note |
|---|---|---|---|---|---|---|
| 1 | Requirements | ✅ Complete | [requirements.md](requirements.md) (14 lines, 56 requirement lines) | ✅ | n/a — given input | Never edited. Includes the stage-2 requirements analysis, delivered in conversation and distilled into scope |
| 2 | Product Scope | ✅ Complete | [product-scope.md](product-scope.md) (483 lines) | ✅ | n/a | T1–T4 tiers, A-1…A-13, 7 open questions. Approved 2026-08-24 |
| 3 | Story Intake / Backlog | ✅ Complete | 18 intakes + [story-backlog.md](story-backlog.md) | ✅ | ✅ Met | All 56 requirement lines mapped to a story |
| 4 | Squad Kit Initialization | ✅ Complete | [.squad/](../.squad/) — config, 18 stories, 13 plan-overview stubs | ✅ | ✅ `squad doctor`: 6 ok, 0 warn, 0 fail | v0.2.0, tracker `none`, agent `claude-code` |
| 5 | Architecture | ✅ Complete | [architecture.md](architecture.md) (646 lines) | ✅ | ✅ Met, re-verified after the AD-15 correction | Approved 2026-08-24 |
| 6 | Data Model | ✅ Complete | [data-model.md](data-model.md) (886 lines) | ✅ | ✅ Met, re-verified after the four clarifications | Approved 2026-08-24. 15 entities |
| 7 | API Design | ⬜ **Not Started** | `docs/api-design.md` | ✖ | ✖ | **Current stage.** Gate: a contract per capability, role access per endpoint (A-4), matching the data model |
| 8 | UI Design | ⬜ Not Started | `docs/ui-design.md` | ✖ | ✖ | Gate: every screen for workspace, portal and admin; RTL implications; phone-width behaviour |
| 9 | Implementation Plans | ⬜ Not Started | `.squad/plans/<feature>/NN-story-*.md` | ✖ (0 plan files) | ✖ | Generated one story at a time via `/squad-plan` |
| 10 | Implementation / Verification | ⬜ Not Started | `backend/`, `frontend/` | ✖ (both empty) | ✖ | No code exists |

**Numbering note.** These ten rows are this dashboard's structure.
[sdd-workflow.md](sdd-workflow.md) is the canonical pipeline and numbers its stages slightly
differently: its stage 2 is the requirements analysis (folded into row 1 here), its stage 4 is the
user-story stage (rows 3 and 4 here, since squad-kit initialization is tooling rather than a
workflow stage). Rows 5–10 match its stages 5–10 exactly. The workflow document governs.

**Blockers to the pipeline:** none. Stage 7 can start immediately.

---

## 3. Feature / Story Progress

All 18 stories from [story-backlog.md](story-backlog.md). Sequence is the intended execution order;
squad-kit assigns the real `NN` when each plan is generated.

**Every story is at `Intake Complete`.** No plan has been generated, no code written, nothing
verified. The uniformity is real, not a placeholder.

| Seq | Story | Feature | Tier | SDD | Plan | Impl | Verified | Depends on | Blocker |
|---|---|---|---|---|---|---|---|---|---|
| 01 | `solution-skeleton` | platform-foundation | T2 | Intake Complete | Not Started | Not Started | Not Started | — | — |
| 02 | `auth-and-roles` | identity-access | **T1** | Intake Complete | Not Started | Not Started | Not Started | 01 | — |
| 03 | `departments-branches` | organization | **T1**+T2 | Intake Complete | Not Started | Not Started | Not Started | 01 | — |
| 04 | `customer-records` | customer-management | **T1**+T2 | Intake Complete | Not Started | Not Started | Not Started | 01–03 | — |
| 05 | `ticket-core` | ticket-management | **T1** | Intake Complete | Not Started | Not Started | Not Started | 01–04 | — |
| 06 | `ticket-lifecycle` | ticket-management | **T1** | Intake Complete | Not Started | Not Started | Not Started | 05 | — |
| 07 | `ticket-intake-messaging` | ticket-management | T2 | Intake Complete | Not Started | Not Started | Not Started | 05, 06 | — |
| 08 | `agent-dashboard` | agent-workspace | **T1** | Intake Complete | Not Started | Not Started | Not Started | 04–07, 09 | — |
| 09 | `sla-routing-escalation` | sla-automation | T2 | Intake Complete | Not Started | Not Started | Not Started | 03, 05, 06 | ⚠ **OQ-2, OQ-3** |
| 10 | `ai-service-seam` | ai-assist | **T1** | Intake Complete | Not Started | Not Started | Not Started | 01 | — |
| 11 | `ai-ticket-assists` | ai-assist | **T1** | Intake Complete | Not Started | Not Started | Not Started | 05–08, 10 | — |
| 12 | `kb-articles-search` | knowledge-base | T2 | Intake Complete | Not Started | Not Started | Not Started | 02 | — |
| 13 | `portal-self-service` | customer-portal | T2 | Intake Complete | Not Started | Not Started | Not Started | 02, 05–07, 12 | ⚠ **OQ-1** |
| 14 | `tasks-internal-notes` | agent-workspace | T2 | Intake Complete | Not Started | Not Started | Not Started | 06, 08 | — |
| 15 | `management-dashboard` | reporting | T2 | Intake Complete | Not Started | Not Started | Not Started | 03, 05, 06, 09, 13 | ⚠ **OQ-1, OQ-2** |
| 16 | `audit-configuration` | administration | T2 | Intake Complete | Not Started | Not Started | Not Started | 02, 06 | — |
| 17 | `i18n-responsive-branding` | platform-experience | T2+T3 | Intake Complete | Not Started | Not Started | Not Started | 01 | — |
| 18 | `channel-erp-adapters` | integration-seams | T3 | Intake Complete | Not Started | Not Started | Not Started | 04, 07, 09 | — |

**Split-in-time exceptions** ([story-backlog.md](story-backlog.md)): story 17's i18n/RTL
*scaffolding* belongs with story 01 (retrofitting every component later costs more); story 16's
*configuration* half is consumed by 05, 08, 09 and 17 and must be defined early. Only the
translation pass and the audit-log UI sit at their listed positions.

**Nothing is Cut.** If a story is cut under time pressure, the cut order in
[story-backlog.md](story-backlog.md) applies, the cut is recorded in
[product-scope.md](product-scope.md) per its §10 rule, and the row above changes to
`Cut / Not Building` with a change-log entry.

---

## 4. Scope Coverage

Progress view only — [product-scope.md](product-scope.md) holds the definitions.

**Read the Design column carefully.** ✅ means architecture (stage 5) *and* the data model
(stage 6) cover the item. **API design and UI design are pending for every single item**, so
nothing is fully designed yet. `n/a` means the item has no data-model footprint by design
(front-end or configuration only).

### T1 — must genuinely work (6 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T1-A Customer management | 04 | ✅ | ⬜ | ⬜ | ⬜ |
| T1-B Ticket management | 05, 06 | ✅ | ⬜ | ⬜ | ⬜ |
| T1-C Agent dashboard | 08 | ✅ | ⬜ | ⬜ | ⬜ |
| T1-D Users, roles, permissions | 02 | ✅ | ⬜ | ⬜ | ⬜ |
| T1-E Multi-department routing | 03 | ✅ | ⬜ | ⬜ | ⬜ |
| T1-F Ticket-facing AI | 10, 11 | ✅ (no entity — DM-5) | ⬜ | ⬜ | ⬜ |

### T2 — simplified but real (12 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T2-A Attachments | 04 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-B Web form + portal messaging | 07 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-C Tasks & internal notes | 14 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-D SLA & automation | 09 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-E Knowledge base | 12 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-F Customer portal | 13 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-G Reports & dashboards | 15 | ✅ (aggregates, no entity) | ⬜ | ⬜ | ⬜ |
| T2-H Audit logs | 16 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-I System configuration | 16 | ✅ (config, n/a to model) | ⬜ | ⬜ | ⬜ |
| T2-J Arabic & English | 17 | ✅ (front end, n/a to model) | ⬜ | ⬜ | ⬜ |
| T2-K Multi-branch | 03 | ✅ | ⬜ | ⬜ | ⬜ |
| T2-L Public API | 01 | ✅ (n/a to model) | ⬜ | ⬜ | ⬜ |

### T3 — seam plus fake, designed not delivered (7 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T3-A External channels | 18 | ✅ | ⬜ | ⬜ | ⬜ |
| T3-B Live chat | 18 | ✅ documented, not building | — | — | — |
| T3-C AI chatbot | 10 (seam), 18 (doc) | ✅ documented, not building | — | — | — |
| T3-D ERP & external systems | 18 | ✅ | ⬜ | ⬜ | ⬜ |
| T3-E Custom branding | 17 | ✅ (config) | ⬜ | ⬜ | ⬜ |
| T3-F Mobile / responsive | 17 (asserted in 08, 13) | ✅ | ⬜ | ⬜ | ⬜ |
| T3-G Multi-tenancy | 03 | ✅ boundary noted, not building | — | — | — |

### T4 — excluded

Not built, not stubbed, not designed. [product-scope.md](product-scope.md) §8 lists them in three
groups — product exclusions, technical exclusions (microservices, brokers, CQRS, event sourcing,
Kubernetes, cache, search engine, vector database, native apps), and quality attributes not
pursued. [architecture.md](architecture.md) §8 restates them so they survive into planning.
**Status: excluded by decision. No tracking required.** A change here is a scope change.

---

## 5. Architecture / Technical Decisions

All approved. Full text in [architecture.md](architecture.md) §7 and [data-model.md](data-model.md) §1.

| ID | Decision | Status | Why it matters | Source |
|---|---|---|---|---|
| AD-1 | Layered modular monolith, single deployable | ✅ Approved | Rules out microservices for every downstream plan | arch §7 |
| AD-2 | Four projects (Api / Application / Domain / Infrastructure) | ✅ Approved | Layering is compiler-enforced, not conventional | arch §7 |
| AD-3 | No repository or unit-of-work over EF Core | ✅ Approved | `DbContext` is both; avoids dead indirection | arch §7 |
| AD-4 | Domain free of EF attributes | ✅ Approved | Lifecycle and SLA rules stay unit-testable | arch §7 |
| AD-5 | Access scoping as an explicit Application helper | ✅ Approved | Global query filters fail open; this is testable | arch §4.3, §7 |
| AD-6 | SLA breach detection as a periodic hosted service | ✅ Approved | No broker or scheduler enters the stack | arch §7 |
| AD-7 | JWT bearer, **token asserts identity only** | ✅ Approved (amended) | Original version carried role/department — corrected | arch §4.1, §7 |
| AD-8 | Migrations + seed at API startup | ✅ Approved | One-command demo; knowingly not production practice | arch §7 |
| AD-9 | Runtime i18n library, not compile-time | ✅ Approved | T2-J needs a live switcher without losing state | arch §7 |
| AD-10 | Ticket history separate from audit log | ✅ Approved | Different actors, questions and visibility rules | arch §7 |
| AD-11 | Seam interfaces owned by Application | ✅ Approved | Makes AI / channels / ERP genuinely swappable | arch §7 |
| AD-12 | AI seam can only return suggestions | ✅ Approved | A-8's advisory rule becomes structural | arch §7 |
| AD-13 | Keyword search in SQL Server | ✅ Approved | No search engine, no vector database | arch §7 |
| AD-14 | One Angular app with role-based lazy areas | ✅ Approved | Shared i18n, branding and API client | arch §7 |
| AD-15 | Role, department, active status resolved **per request** | ✅ Approved | Closes the stale-claim authorization hole | arch §4.1.1, §7 |
| DM-1 | `User` and `Customer` are separate, linked 1:0..1 | ✅ Approved | A-4 role vs A-2 department/branch cannot share a row | model §1 |
| DM-2 | Assignment = field + history, no entity | ✅ Approved | Avoids a third representation to keep in sync | model §1 |
| DM-3 | SLA state on `Ticket`; targets are configuration | ✅ Approved | No `SlaPolicy` rows, no rules engine | model §1 |
| DM-4 | Content lives once; activity is the ordering spine | ✅ Approved | One timeline query; bodies never duplicated | model §1 |
| DM-5 | No AI entity | ✅ Approved | Only categorization needs persistence — as history | model §1 |
| DM-6 | Seams persist one field (`externalReference`) | ✅ Approved | No adapter config, webhook or receipt storage | model §1 |

---

## 6. Open Questions / Blockers

### 6.1 Active — must be answered before the named story is implemented

| ID | Question | Impact | Blocks | Status |
|---|---|---|---|---|
| **OQ-1** | What is the CSAT rating scale? T2-F says "a one-question satisfaction rating" and fixes no scale | Determines portal control, server validation, and what the §9.4 average means. The model **encodes no range** | Story 13, and the satisfaction tile in story 15 | 🔴 Open — product decision |
| **OQ-2** | On a priority change, do SLA due dates recompute from `createdAt` or stay frozen? A-3 is silent, and T2-D escalation changes priority routinely | Materially different §9.2 attainment numbers; recompute can breach a ticket as a consequence of the escalation the breach triggered | Story 09, and §9.2 in story 15 | 🔴 Open — business rule |
| **OQ-3** | Who is notified on breach when a department has no manager? T2-D says "notify the department manager"; absence is uncovered | Breach flag and priority raise are unaffected; only the recipient is undetermined. **No fallback invented** | Story 09 | 🔴 Open — product decision |

**None of these blocks Stage 7.** All three are implementation-time decisions.

### 6.2 Carried from product scope — non-blocking by design

[product-scope.md](product-scope.md) §9 holds seven open questions (AI provider and data
residency, ERP product, unbounded "external systems", tenancy model, real SLA policy, chatbot
handoff, anonymous submission). [architecture.md](architecture.md) §9 confirms the architecture
resolves none of them and places each behind a seam or configuration point **so they can stay
open**. They do not block the assessment. Question 5 (real SLA policy) is the parent of OQ-2.

### 6.3 Resolved Decisions

Kept, not deleted.

| ID | Question | Resolution | Date | Where |
|---|---|---|---|---|
| R-1 | Should the JWT carry role and department? | **No.** They can go stale when an Administrator moves or demotes a user; resolved per request from the authoritative user record | 2026-08-24 | AD-15, arch §4.1.1 |
| R-2 | Does a `Ticket` need its own branch relationship? | **No.** Derived `Ticket → Customer → Branch`. Audit of every source found no requirement for one; the `departments-branches` AC already says "the customer's branch" | 2026-08-24 | model §2.3 |
| R-3 | Do AI features need persistence? | **No entity.** Summaries and suggested replies are not stored; the categorization record is ticket history | 2026-08-24 | DM-5 |
| R-4 | Is `Customer` a role or an entity? | **Both, separately.** `User` (login, role, department) and `Customer` (profile, branch), linked 1:0..1 | 2026-08-24 | DM-1 |
| R-5 | Was a CSAT 1–5 scale assumed? | **Withdrawn.** The assumption was removed rather than hardened into a constraint; reopened as OQ-1 | 2026-08-24 | model §2.15 |

---

## 7. Implementation Progress

**No implementation exists.** Verified against the repository on 2026-08-24, not inferred.

| Area | Status | Evidence |
|---|---|---|
| Backend | ⬜ Not Started | `backend/` contains 0 files |
| Frontend | ⬜ Not Started | `frontend/` contains 0 files |
| Database | ⬜ Not Started | No migrations, no schema; the model is a design document |
| Tests | ⬜ Not Started | No test project |
| Docker / infrastructure | ⬜ Not Started | `docker-compose.yml` exists but is **0 bytes** |

Design documents describe all five. **A design document is not an implementation** and must never
be counted as one in this section.

Implementation begins after stage 9 produces a plan for story 01.

---

## 8. Verification

Only entries with real evidence are marked verified.

| Check | Status | Evidence / date |
|---|---|---|
| `squad doctor` | ✅ Passing | Re-run 2026-08-24: **6 ok · 0 warn · 0 fail · 7 skip** (skips are planner and tracker, both disabled by design) |
| `squad status` | ✅ Consistent | 18 stories, 0 plan files, next NN 01 |
| Stage gate 3 → 4 | ✅ Met | All 56 requirement lines mapped to a story |
| Stage gate 5 → 6 | ✅ Met | Re-verified after the AD-15 correction |
| Stage gate 6 → 7 | ✅ Met | Re-verified after the four clarifications |
| Story verification | ⬜ Not Run | No story implemented |
| Unit / integration tests | ⬜ Not Run | No tests exist |
| Backend build | ⬜ Not Run | No backend |
| Frontend build | ⬜ Not Run | No frontend |
| API verification | ⬜ Not Run | No API |
| UI verification | ⬜ Not Run | No UI |
| Docker startup | ⬜ Not Run | `docker-compose.yml` is empty |
| Working tree | ✅ Clean | 5 commits on `main`, nothing uncommitted, nothing pushed |

---

## 9. Change Log

Newest first. Every meaningful project change gets an entry.

### 2026-08-24

- **Created this progress tracker.** No other file modified.
  *Files:* `docs/PROJECT-PROGRESS.md` (new).
- **Clarified four points in the data model** after review, without changing the model.
  Branch derivation `Ticket → Customer → Branch` stated explicitly with a full audit of the
  sources confirming no requirement asks for a ticket-level branch; the CSAT 1–5 assumption
  **withdrawn** so no range is encoded (reopened as OQ-1); SLA due-date recomputation
  **un-decided**, with both readings documented (OQ-2); missing-department-manager behaviour
  documented as unresolved with **no fallback invented** (OQ-3). Open items became a register
  naming the story each one blocks.
  *Why:* three assumptions were hardening into implementation constraints the requirements do not
  support. *Files:* `docs/data-model.md`. *Commit:* `a149b01`.
- **Completed Stage 6 — Data Model.** 15 entities, six ownership decisions (DM-1…DM-6) resolved
  before modelling, 14 candidate entities explicitly not modelled. Gate 6 → 7 met.
  *Files:* `docs/data-model.md` (new), `docs/sdd-workflow.md`. *Commit:* `a149b01`.
- **Corrected the JWT authorization design.** The token had carried role and department, which go
  stale when an Administrator moves, demotes or deactivates a user — a silent, fail-open
  confidentiality defect. The token now asserts identity only; role, department and active status
  are resolved per request from the authoritative user record. AD-7 amended, AD-15 added,
  §4.1.1 written. Gate 5 → 6 re-verified.
  *Why:* raised in review before Stage 6 began. *Files:* `docs/architecture.md`. *Commit:* `e51e1aa`.
- **Completed Stage 5 — Architecture.** Layered modular monolith, front-end structure, three
  enforcement points, department scoping mechanism, three integration seams, configuration
  strategy, Compose runtime, 14 decisions. Also corrected an off-by-one in the stage references
  inside all 18 intakes.
  *Files:* `docs/architecture.md` (new), `docs/sdd-workflow.md`, 18 intakes. *Commit:* `bbcff22`.
- **Completed Stages 2–4 — Product Scope, Story Backlog, Squad Kit.** Tiered scope with 13
  assumptions and 7 open questions; squad-kit v0.2.0 initialized; 18 story intakes across 11
  feature slugs; SDD workflow and backlog documents written.
  *Files:* `docs/product-scope.md`, `docs/sdd-workflow.md`, `docs/story-backlog.md`, `.squad/**`.
  *Commit:* `ba89afd`.
- **Stage 1 — Requirements received** and analysed (functional and non-functional requirements,
  actors, workflows, ambiguities, assumptions). Analysis delivered in conversation and distilled
  into the product scope. *Files:* `docs/requirements.md`. *Commit:* `c08899c`.

---

## 10. Current Next Steps

1. **Stage 7 — API Design** → `docs/api-design.md`. Gate: a contract for every capability the
   stories need, role-based access stated per endpoint (A-4), matching the data model exactly.
2. **Stage 8 — UI Design** → `docs/ui-design.md`. Every screen for workspace, portal and admin;
   RTL implications; phone-width behaviour.
3. **Decide OQ-1, OQ-2, OQ-3** — needed before stories 09, 13 and 15 are implemented, not before
   stages 7 and 8. Raising them early avoids a stall mid-implementation.
4. **Stage 9 — plan story 01** (`solution-skeleton`) via `/squad-plan`, then plan forward in the
   backlog order.
5. **Stage 10 — implement story 01**, then 02–06 (the T1 core) in sequence.
6. **Consider scope realism before stage 9.** 18 stories against a 9–12 hour budget is ambitious;
   generating plans only for stories 01–11 and treating 12–18 as planned-not-built is a live
   option. Any such cut is recorded in [product-scope.md](product-scope.md) and reflected here.

---

## Maintenance

**This is a living document.** It must be updated in the *same task* as any meaningful project
change: completing a stage, changing an architecture decision or the data model, opening or
resolving a question, adding or changing a story, generating a plan, implementing a story, running
tests, hitting a blocker, cutting a feature, or reordering execution. Formatting-only edits do not
warrant an entry.

**Accuracy rules that override convenience:** never record progress that did not happen; never mark
Implemented when only a design exists; never mark Verified without evidence named in §8; never mark
a stage complete unless its gate is satisfied; never delete a historical decision or blocker — move
it to §6.3 instead.

**Recalculate §1.1** whenever a §2 row completes or a §3 story reaches Verified.
