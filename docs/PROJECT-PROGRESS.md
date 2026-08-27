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
| **Current SDD stage** | **Stage 10 (Implementation) in progress — stories 01, 02 and 03 complete and verified** |
| **Current phase** | Design and planning **complete**. Phase 0 delivered. **Phase 1 is complete**: stories 02 and 03 are both in, including story 03's two endpoints and its front-end filters |
| **Overall status** | 🟡 On track with **four blocked acceptance criteria**. The SDD chain is finished end to end; the stage 9 audit found two genuine contradictions (S9-1, S9-4) and scheduled two carried findings (PF-2, PF-4). **None blocks stories 01–04** |
| **Overall progress** | **45.8%** (method in §1.1) |
| **SDD pipeline** | **9 of 10 stages complete** (90% of the pipeline) |
| **Code written** | **Stories 01, 02 and 03.** Authentication, the four-role model, per-request identity resolution, user administration, the audit recorder and the two organization read endpoints all run. Eleven endpoints exist. `backend/` holds the five-project solution, `frontend/` the Angular + PrimeNG app (built on PrimeNG Sakai `20.0.0`), `docker-compose.yml` runs three services. Two endpoints exist: `/api/v1/health` and `/api/v1/config/bootstrap` — **no business entity, screen or endpoint beyond those** |
| **Last updated** | 2026-08-27 |
| **Current focus** | Story 03 complete and verified; **phase 1 is closed**. **Awaiting explicit approval before the next story.** Story 16 Part A is the gate before stories 04 and 05, because a ticket cannot be created without the category list, the category→department map and the SLA targets. Phase order per [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §3 |
| **Next immediate step** | See §10, item 1 |

### 1.1 How the 35% is calculated

Two weighted tracks. The weighting is a **stated planning convention, not a measurement** — it is
recorded here so the number is reproducible rather than invented.

| Track | Contents | Weight | Complete | Contribution |
|---|---|---|---|---|
| **Design & planning** | Tracker rows 1–9 (§2), equally weighted at 3.89% each | 35% | **9 of 9 rows** | 9 ÷ 9 × 35 = **35.0%** |
| **Delivery** | Row 10 — the 18 stories implemented *and* verified | 65% | **3 of 18** | 3 ÷ 18 × 65 = **10.8%** |
| | | **100%** | | **= 45.8%** |

**The design-and-planning track is fully consumed.** Every remaining percentage point is delivery.
This is the point the weighting was chosen to make honest: **a fully planned project with no running
code is 35% done, not 90%** — and one enabling story that carries no business behaviour moves it by
exactly one eighteenth of the delivery track, not more.

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
| 1 | Requirements | ✅ Complete | [requirements.md](requirements.md) — 56 requirement lines | ✅ | n/a — given input | Never edited. Includes the stage-2 requirements analysis, delivered in conversation and distilled into scope |
| 2 | Product Scope | ✅ Complete | [product-scope.md](product-scope.md) | ✅ | n/a | T1–T4 tiers, A-1…A-19, 7 open questions. Approved 2026-08-24; A-14…A-18 added 2026-08-24; **A-19 added 2026-08-27** (closes OQ-5) |
| 3 | Story Intake / Backlog | ✅ Complete | 18 intakes + [story-backlog.md](story-backlog.md) | ✅ | ✅ Met | All 56 requirement lines mapped to a story |
| 4 | Squad Kit Initialization | ✅ Complete | [.squad/](../.squad/) — config, 18 stories across **14 feature slugs**, 14 plan overviews (**filled at stage 9**) | ✅ | ✅ `squad doctor`: 6 ok, 0 warn, 0 fail | v0.2.0, tracker `none`, agent `claude-code` |
| 5 | Architecture | ✅ Complete | [architecture.md](architecture.md) | ✅ | ✅ Met, re-verified after the AD-15 correction | Approved 2026-08-24 |
| 6 | Data Model | ✅ Complete | [data-model.md](data-model.md) | ✅ | ✅ Met, re-verified after the four clarifications and after the §6.1 amendment | Approved 2026-08-24. 15 entities. **Amended 2026-08-26:** §6.1 fixes string lengths and collation — the one physical decision this document makes, because the unique indexes it declares cannot be built without it |
| 7 | API Design | ✅ Complete | [api-design.md](api-design.md) | ✅ | ✅ Met | **66** endpoints, 12 modules, 19 API decisions (AP-1…AP-19). Pre-flight passed 2026-08-24; post-flight 2026-08-25 closed two blocking defects; **N-1 refinement 2026-08-25 added the full payload catalogue** |
| 8 | UI Design | ✅ Complete | [ui-design.md](ui-design.md) | ✅ | ✅ Met | 24 screens across 4 surfaces, 12 UI decisions (UI-1…UI-12), every screen mapped to endpoints that exist |
| 9 | Implementation Plans | ✅ Complete | **18** × `.squad/plans/<feature>/NN-story-*.md` + [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) + 14 updated `00-overview.md` + [00-index.md](../.squad/plans/00-index.md) | ✅ | ✅ Met for story 01; per-story thereafter | Completed 2026-08-25. `squad status`: 18 stories, **18 plan files**, next `NN` 19. A full traceability and consistency audit produced **13 findings (S9-1…S9-13)**, of which **4 block a named acceptance criterion** (§6.7) |
| 10 | Implementation / Verification | ⬜ **Not Started** | `backend/`, `frontend/` | ✖ (both empty) | ✖ | **Current stage.** No code exists. Begins with story 01 |

**Numbering note.** These ten rows are this dashboard's structure.
[sdd-workflow.md](sdd-workflow.md) is the canonical pipeline and numbers its stages slightly
differently: its stage 2 is the requirements analysis (folded into row 1 here), its stage 4 is the
user-story stage (rows 3 and 4 here, since squad-kit initialization is tooling rather than a
workflow stage). Rows 5–10 match its stages 5–10 exactly. The workflow document governs.

**Blockers to the pipeline:** none — the pipeline itself is complete. **Blockers inside stage 10
exist:** four acceptance criteria cannot be met until a decision is recorded (S9-1, S9-4, PF-4,
PF-2 — §6.7), and three open questions gate individual stories (OQ-1, OQ-2, OQ-3 — §6.1;
**OQ-5 was closed on 2026-08-27 by A-19**).
**None of the eight blocks stories 01–04**; the earliest is **OQ-2, which blocks story 05**.

The Stage 7 pre-flight audit raised one blocking finding, PF-1 (the `Pending` transition set and
the effect of a customer reply); it was resolved the same day as R-13. The remaining pre-flight and
post-flight findings are carried in §6.5 and §6.6.

---

## 3. Feature / Story Progress

All 18 stories from [story-backlog.md](story-backlog.md). Sequence is the intended execution order;
squad-kit assigns the real `NN` when each plan is generated.

**Stories 01, 02 and 03 are Implemented and Verified**, which closes phase 1. **Stories 04–18 are at
`Plan Complete`.** All 18 plans exist. The `Depends on` column below is the **execution** dependency from
[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §4, which supersedes the
earlier reading in three places (S9-7, S9-8, S9-12).

| Seq | Story | Feature | Tier | SDD | Plan | Impl | Verified | Depends on | Blocker |
|---|---|---|---|---|---|---|---|---|---|
| 01 | `solution-skeleton` | platform-foundation | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-25, re-run 2026-08-26 | — | — |
| 02 | `auth-and-roles` | identity-access | **T1** | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-26 | 01, **03 data** | — |
| 03 | `departments-branches` | organization | **T1**+T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-27 | 01, 02 | — · tasks 1–3 landed early with story 02, tasks 4–8 after it, per S9-12 |
| 04 | `customer-records` | customer-management | **T1**+T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 01–03, **16 A** | — · **OQ-5 closed 2026-08-27 (A-19)** |
| 05 | `ticket-core` | ticket-management | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 01–04, **16 A** | ⚠ **OQ-2** |
| 06 | `ticket-lifecycle` | ticket-management | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 05 | ⚠ OQ-3 *(no-manager branch only)* |
| 07 | `ticket-intake-messaging` | ticket-management | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 05, 06, 04, 16 A | — |
| 08 | `agent-dashboard` | agent-workspace | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 04–07, 16 A | ⛔ **S9-1** *(task region only)* |
| 09 | `sla-routing-escalation` | sla-automation | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 03, 05, 06, 16 A | ⚠ **OQ-2**, OQ-3, PF-5 |
| 10 | `ai-service-seam` | ai-assist | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 01 — **parallel** | — |
| 11 | `ai-ticket-assists` | ai-assist | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 10, 05–08 | ⛔ **S9-4** |
| 12 | `kb-articles-search` | knowledge-base | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 02; task 4 needs 05 | — |
| 13 | `portal-self-service` | customer-portal | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 02, 04–07, 12 | ⚠ **OQ-1** |
| 14 | `tasks-internal-notes` | agent-workspace | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 06, 08 | ⛔ **S9-1** |
| 15 | `management-dashboard` | reporting | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 03, 05, 06, 09, 13 | ⛔ **PF-4**, ⚠ OQ-1 |
| 16 | `audit-configuration` | administration | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | **A:** 01, 03 · **B:** 02, 06 | — |
| 17 | `i18n-responsive-branding` | platform-experience | T2+T3 | Intake Complete | **Plan Complete** | Not Started | Not Started | **A:** 01 · **B:** 02–16 | — |
| 18 | `channel-erp-adapters` | integration-seams | T3 | Intake Complete | **Plan Complete** | Not Started | Not Started | 04, 07, 09 | ⛔ **PF-2** |

**Split-in-time exceptions** ([story-backlog.md](story-backlog.md)): story 17's i18n/RTL
*scaffolding* belongs with story 01 (retrofitting every component later costs more); story 16's
*configuration* half must be defined early. Only the translation pass and the audit-log UI sit at
their listed positions. **The stage 9 audit widened story 16's consumer list** — story 04 needs the
default branch (A-15) and the attachment cap, and story 13 needs the rating-scale key — so
configuration executes in **phase 2, before story 04**.

**Execution order differs from `NN` order in three places**, each a dependency the approved
documents already imply, none a business change: **03's data layer runs before 02** (S9-12),
**16 Part A runs before 04 and 05** (S9-13), and **`POST /auth/register` is implemented in 04, not
02** (S9-7). **Story 08 does *not* depend on 09** — the earlier reading was wrong, because SLA due
dates are required at creation (S9-8).

**Nothing is Cut.** If a story is cut under time pressure, the cut order in
[story-backlog.md](story-backlog.md) applies — **note the S9-8 correction recorded there: cutting
story 09 does not cost the queue its SLA ordering**, only the population of the breach flags — the cut is recorded in
[product-scope.md](product-scope.md) per its §10 rule, and the row above changes to
`Cut / Not Building` with a change-log entry.

---

## 4. Scope Coverage

Progress view only — [product-scope.md](product-scope.md) holds the definitions.

**The Design column is now fully earned.** ✅ means all four design stages — architecture (5),
data model (6), API design (7) and UI design (8) — cover the item. `n/a` means the item has no
data-model footprint by design (front-end or configuration only).

**The Plan column is complete for every item.** ✅ means a stage-9 plan file covers it, with the
plan named in §4.1 below. **Impl and Verified remain ⬜ everywhere** — no code exists.

### T1 — must genuinely work (6 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T1-A Customer management | 04 | ✅ | ✅ | ⬜ | ⬜ |
| T1-B Ticket management | 05, 06 | ✅ | ✅ | ⬜ | ⬜ |
| T1-C Agent dashboard | 08 | ✅ | ✅ | ⬜ | ⬜ |
| T1-D Users, roles, permissions | 02 | ✅ | ✅ | ⬜ | ⬜ |
| T1-E Multi-department routing | 03 | ✅ | ✅ | ⬜ | ⬜ |
| T1-F Ticket-facing AI | 10, 11 | ✅ (no entity — DM-5) | ✅ | ⬜ | ⬜ |

### T2 — simplified but real (12 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T2-A Attachments | 04 | ✅ | ✅ | ⬜ | ⬜ |
| T2-B Web form + portal messaging | 07 | ✅ | ✅ | ⬜ | ⬜ |
| T2-C Tasks & internal notes | 14 | ✅ | ✅ | ⬜ | ⬜ |
| T2-D SLA & automation | 09 | ✅ | ✅ | ⬜ | ⬜ |
| T2-E Knowledge base | 12 | ✅ | ✅ | ⬜ | ⬜ |
| T2-F Customer portal | 13 | ✅ | ✅ | ⬜ | ⬜ |
| T2-G Reports & dashboards | 15 | ✅ (aggregates, no entity) | ✅ | ⬜ | ⬜ |
| T2-H Audit logs | 16 | ✅ | ✅ | ⬜ | ⬜ |
| T2-I System configuration | 16 | ✅ (config, n/a to model) | ✅ | ⬜ | ⬜ |
| T2-J Arabic & English | 17 | ✅ (front end, n/a to model) | ✅ | ⬜ | ⬜ |
| T2-K Multi-branch | 03 | ✅ | ✅ | ⬜ | ⬜ |
| T2-L Public API | 01 | ✅ (n/a to model) | ✅ | ⬜ | ⬜ |

### T3 — seam plus fake, designed not delivered (7 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T3-A External channels | 18 | ✅ | ✅ | ⬜ | ⬜ |
| T3-B Live chat | 18 | ✅ documented, not building | — | — | — |
| T3-C AI chatbot | 10 (seam), 18 (doc) | ✅ documented, not building | — | — | — |
| T3-D ERP & external systems | 18 | ✅ | ✅ | ⬜ | ⬜ |
| T3-E Custom branding | 17 | ✅ (config) | ✅ | ⬜ | ⬜ |
| T3-F Mobile / responsive | 17 (asserted in 08, 13) | ✅ | ✅ | ⬜ | ⬜ |
| T3-G Multi-tenancy | 03 | ✅ boundary noted, not building | — | — | — |

### 4.1 Scope item → plan file

Every T1, T2 and T3 item now has a named plan. The full matrix, including endpoints, entities and
screens per story, is [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §8.2.

| Scope item | Plan(s) |
|---|---|
| T1-A | 04 |
| T1-B | 05, 06 |
| T1-C | 08 |
| T1-D | 02 (+ ticket scoping in 05) |
| T1-E | 03 (+ enforcement in 05) |
| T1-F | 10, 11 |
| T2-A | 04 (+ ticket endpoints in 05) |
| T2-B | 07 |
| T2-C | 14 |
| T2-D | 09 |
| T2-E | 12 |
| T2-F | 13 |
| T2-G | 15 |
| T2-H | **02** (write path) + **16 Part B** (read surface) |
| T2-I | **16 Part A** |
| T2-J | **17 Part A** + **17 Part B** |
| T2-K | 03, 04, 15 |
| T2-L | 01 |
| T3-A | 18 |
| T3-B | 18 (documented) |
| T3-C | 10 (extension point documented), 18 (documented) |
| T3-D | 18 |
| T3-E | 01 (loader) + 17 Part B (proof) |
| T3-F | 08, 13, 17 Part B |
| T3-G | 03 (boundary noted, not built) |

### T4 — excluded

Not built, not stubbed, not designed. [product-scope.md](product-scope.md) §8 lists them in three
groups — product exclusions, technical exclusions (microservices, brokers, CQRS, event sourcing,
Kubernetes, cache, search engine, vector database, native apps), and quality attributes not
pursued. [architecture.md](architecture.md) §8 restates them so they survive into planning.
**Status: excluded by decision. No tracking required.** A change here is a scope change.

---

## 5. Architecture / Technical Decisions

All approved. Full text in [architecture.md](architecture.md) §7 and [data-model.md](data-model.md) §1.

**This table is not the whole decision inventory.** It covers technical decisions (AD-*) and
modelling decisions (DM-*). **Business** decisions are the numbered assumptions A-1…A-19 in
[product-scope.md](product-scope.md) §7; the six most recent (**A-14…A-19**) are recorded as
R-6…R-11 in §6.3, with their document and story impact traced in §6.4.

**Known issues.** One remains, and it is cosmetic: in [architecture.md](architecture.md) §7 the
**AD-15 row renders above AD-14** — AD-15 was inserted at the wrong position during the token-staleness correction. No
content is affected; ordering only. Recorded here so it is not lost.
*(The `CustomerFeedback` "Portal module" mismatch flagged on 2026-08-24 is **resolved** — see
R-12 in §6.3. It is no longer an open issue.)*

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
| **OQ-2** | On a priority change, do SLA due dates recompute from `createdAt` or stay frozen? A-3 is silent, and T2-D escalation changes priority routinely | Materially different §9.2 attainment numbers; recompute can breach a ticket as a consequence of the escalation the breach triggered | **Story 05** *(its `PATCH /tickets/{id}` is the **first** code path that changes a priority — widened by **S9-3**)*, story 09, and §9.2 in story 15 | 🔴 Open — business rule. **The earliest blocker in the whole implementation sequence** |
| **OQ-3** | Who is notified on breach when a department has no manager? T2-D says "notify the department manager"; absence is uncovered | Breach flag and priority raise are unaffected; only the recipient is undetermined. **No fallback invented** | **Story 06** *(the **manual** escalation action has the same undefined recipient — widened by **S9-3**)* and story 09 | 🔴 Open — product decision. **Non-blocking on the happy path:** every seeded department has a manager |

| ~~**OQ-5**~~ | ~~When `Customer.email` is changed, does a linked portal login's sign-in email change with it?~~ | Raised 2026-08-25 by the N-2 correction | **✅ Closed 2026-08-27 by A-19** — see R-15 below | ✅ Answered |

**None of these blocked Stage 7, 8 or 9.** All are implementation-time decisions, and stage 9
scheduled each against the story and the single method that encodes it. OQ-4 was resolved on
2026-08-24 (see R-11) and **OQ-5 on 2026-08-27 (see R-15)**.

**Earliest-first order for stage 10:** **OQ-2** (story 05) → **S9-4** (story 11) → **OQ-1**
(story 13) → **S9-1** (story 14) → **PF-4** (story 15) → **PF-2** (story 18). Restated in §10.
**OQ-5 has left this list**: it gated story 04, which was the nearest of all of them, and it is
answered.

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
| R-6 | How does a customer-submitted ticket get its department? | **Category → department mapping in configuration**, applied at creation before assignment. Customers choose a category, never a department | 2026-08-24 | **A-14** |
| R-7 | Self-registration: which branch, and what if the email already has a profile? | **System default branch** (configured). An existing profile is **linked**, not duplicated — the `User` is created and points at it | 2026-08-24 | **A-15** |
| R-8 ⚠ | Who may Cancel, Close and Escalate? **Partly superseded by R-10 — see the cancel row there.** | **Full authority matrix fixed.** Customer: create, reopen own, cancel own before work begins. Agent: open, pending, resolve, close, escalate. Manager: the same across all departments. Administrator: unrestricted. **Closure is manual only — no automatic closure.** As first recorded, this withheld cancel from agents and managers; **that half was corrected the same day — see R-10.** The rest stands | 2026-08-24 | **A-16** (superseded in part) |
| R-9 | Does the creation contract carry a customer urgency input? | **Yes — `isUrgent`, a boolean.** Customer input only; does not set priority; agents and the AI suggestion may use it when deciding priority. Persisted on `Ticket` | 2026-08-24 | **A-17**, model §2.6 |
| R-10 | May agents and managers cancel a ticket? | **Yes** — corrected 2026-08-24. Agents and Managers may cancel any non-terminal ticket (Manager across all departments); Administrators unrestricted. Customers may cancel their own **only while `New`**. Supersedes the first version of A-16, which withheld cancel from agents and managers | 2026-08-24 | **A-16** |
| R-14 | Who is the actor on the automatic `Pending -> Open` status change, which has no invoking user? | **The replying customer**, with `actorKind = User`. Their reply caused the transition, and A-5 requires every transition to carry an actor. **Not** a `System` actor — the SLA monitor stays the only system actor, and attributing a customer-caused change to the system would make ticket history less truthful. Approved 2026-08-24 after being flagged as a derived detail rather than applied silently. **No new entity or field**; `TicketActivity` already carries `actorUserId` and `actorKind` | 2026-08-24 | **A-5**, model 2.6 inv. 2b, 2.7, 2.8, constraint 9a |
| R-13 | **PF-1** — is `Pending -> Open` legal, and what does a customer reply do to a `Pending` ticket? | **Both answered: the transition is legal and a customer reply triggers it automatically.** The reply is the trigger; no agent action is required. It fires from `Pending` only — a reply on `New` leaves it `New`, and a reply on `Resolved` does not reopen it. Recorded as a same-transaction status change with a `StatusChanged` history entry attributed to the replying customer | 2026-08-24 | **A-5**, A-16, model 2.6 inv. 2b, 2.8, constraint 9a |
| R-12 | Which backend module owns `CustomerFeedback`? The data model labelled it "Portal", which is not one of the ten modules | **The existing `Tickets` module.** Feedback is domain behaviour attached to a ticket, offered when the ticket reaches `Resolved`; `customer-portal` is a front-end and planning concern, not a backend module. **No new module; the ten-module architecture is unchanged**, and the entity's shape, fields and relationships are untouched — an ownership label only | 2026-08-24 | **DM-7**, model §2.15, arch §1 |
| R-15 | **OQ-5** — does changing `Customer.email` change a linked portal login's sign-in email? | **Yes, and atomically.** A customer's email and their portal sign-in are one address: the two rows change in the same unit of work, and `User.email`'s existing case-insensitive uniqueness applies to the new value across all users — a collision rejects the whole operation and writes neither row. Divergence is not a reachable state, which also keeps A-15's three registration outcomes exhaustive. **Amended the same day:** the propagation writes one **`UserEmailChanged`** audit entry, actor = the calling agent, target = the linked user, in that same unit of work — Story 02's recorder unchanged, one new action constant, no schema change | 2026-08-27 | **A-19** |
| R-11 | **OQ-4** — what is the customer cancellation window? | **Assignment is not the start of work.** A ticket may be assigned while still `New`; `New → Open` is an agent deliberately starting work. The customer's window runs from creation until an agent picks the ticket up, so it is real rather than theoretical | 2026-08-24 | **A-18** |

### 6.5 Pre-flight findings carried forward (non-blocking)

Raised by the Stage 7 pre-flight audit on 2026-08-24. None blocks an API contract; each is handled
inside the API document or deferred to the story named.

| ID | Finding | Handle in |
|---|---|---|
| **PF-2** | `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required with no System actor, but story 18's inbound fake adapter creates tickets with no human actor | **Avoided in Stage 7** (AP-11 publishes no ingestion endpoint); **still open, and now scheduled: it blocks story 18 AC 3** (**S9-10**). Three options are set out in [18-story-channel-erp-adapters.md](../.squad/plans/integration-seams/18-story-channel-erp-adapters.md); **none is chosen**. The ingestion implementation throws with the reason and its test is skipped with the reason |
| **PF-3** | OQ-1 leaves the CSAT scale undecided and `architecture.md` §6.3 had **no** configuration key for it | ✅ **Closed 2026-08-25** — the `Feedback rating scale` key is now an approved entry in architecture §6.3, with its **values deliberately undecided** because OQ-1 remains open. `GET /config` publishes it; the feedback endpoint validates against it |
| **PF-4** | "Tickets assigned" in the agent-performance metric is undefined — currently assigned vs. ever assigned. Same response shape either way | **Still open, and it blocks story 15's `agentPerformance.assignedCount`** (**S9-9**). [api-design.md](api-design.md) §9 item 2 required it to be pinned **before story 15 was planned**; it was not, so the plan isolates the decision to `AgentPerformanceQuery.AssignedCount`, which throws until it is recorded. **The label stays exactly as T2-G words it, with no clarifying tooltip** |
| **PF-5** | `firstRespondedAt` is set only by the first outbound message, so a ticket resolved without a reply is permanently first-response-breached | **Still open and non-blocking.** Story 09 implements the sweep **exactly as A-3 words it** and **reports** the consequence rather than changing it — changing it would be a change to A-3. Story 08 renders `null` as **"—"**, never "breached" and never "0" ([ui-design.md](ui-design.md) §11) |
| **PF-6** | A-15 covers registration when a **Customer profile** exists for the email, not when a **User** already does | ✅ **Closed** — api-design §5.2 states `409 user-already-exists` |
| **PF-7** | `TicketMessage.direction` has no stated derivation rule and must not be client-settable | ✅ **Closed** — api-design §7 derives it from author role and omits it from every request model |

### 6.6 Stage 7 post-flight findings (2026-08-25)

The post-flight review of `api-design.md` found two blocking defects and five non-blocking ones.

| ID | Finding | Status |
|---|---|---|
| **B-1** | `api-design` made the feedback contract depend on a `Feedback:RatingScale` config key that architecture §6.3 did not have — the contract was not implementable from the approved documents | ✅ **Closed** — key approved and added, values left open (OQ-1) |
| **B-2** | `GET /config` returned **quick replies and SLA targets to every authenticated caller, including Customers**. No source authorizes that: customers do not set priority (A-6), do not choose a department (A-14), and their ticket payload carries no SLA field | ✅ **Closed** — configuration split into three audience tiers (public / customer-safe / staff-only), AP-17 |
| **N-1** | Response shapes defined for 5 payload types; **eleven** returned resource types have none, and three request bodies are unstated | ✅ **Closed 2026-08-25.** `api-design.md` §6 is now a full payload catalogue — 29 shapes across 12 subsections, plus the three missing request bodies. Closing it exposed **F-2** (below) |
| **N-2** | `api-design` asserted that a customer's `email` is immutable. **No approved source supports it** — A-10 makes email an identifier, not an immutable one | ✅ **Closed** — rule removed; uniqueness validation (`409 customer-email-in-use`) kept, since that *is* in the model. Raised **OQ-5** rather than inventing the linked-login consequence |
| **N-3** | `GET /auth/me/permissions` and `POST /notifications/read-all` traced to no requirement | ✅ **Closed** — both removed (AP-18). Roles are fixed and hierarchical (A-4), so a client derives capability from the role `/auth/me` already returns; A-13 asks for a list and a badge, not a bulk action |
| **N-4** | `hasFeedback` on the portal ticket payload is derived but was not declared as such | ✅ **Closed** — added to the server-derived field table (api-design §7) |
| **N-5** | `data-model.md` §2.15 types `rating` as "an ordinal value" while OQ-1's own candidate list includes a binary thumbs up/down | 🟡 **Recorded, not fixed.** Resolving it would either retype an approved field or narrow an open question — both would pre-empt OQ-1. It predates `api-design.md` |

| **F-2** | **AP-13 promised an authorized attachment download endpoint that the catalogue never defined**, while story 04 requires a file to be "downloaded again". A genuine contradiction, found while closing N-1 | ✅ **Closed 2026-08-25** — `GET /attachments/{attachmentId}/content` added as **AP-19**, one endpoint for every role, authorizing through the owning ticket or customer. Endpoint count 65 → 66 |
| **F-1** | The ticket payload does not expose `allowedTransitions`, so the transition menu must reimplement the A-5 legality set and the A-16 authority matrix client-side (ui-design UI-3, §12). The server remains the authority; a wrong offer gets `403`/`409` | 🟡 **Non-blocking. Re-checked 2026-08-25 during the N-1 refinement and deliberately left open.** The contract is *sufficient* — the server is the authority and a wrong offer gets `403`/`409` — but the client still duplicates the matrix. Adding it was in reach while §6 was being rewritten; **not applied, because it changes what an endpoint returns and that is a decision to take explicitly, not a side effect of a documentation pass** |

Endpoint count moved **66 → 65**: `/config/staff` added, `/auth/me/permissions` and
`/notifications/read-all` removed.

### 6.7 Stage 9 audit findings (2026-08-25)

A full traceability and consistency audit was run while generating the eighteen plans, across
[product-scope.md](product-scope.md), [architecture.md](architecture.md),
[data-model.md](data-model.md), [api-design.md](api-design.md), [ui-design.md](ui-design.md),
[story-backlog.md](story-backlog.md), [sdd-workflow.md](sdd-workflow.md) and all 18 intakes.
**Thirteen findings. Nothing was resolved by invention.** Full detail in
[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §7.

**Blocking — a named acceptance criterion cannot be met until a decision is recorded**

| ID | Finding | Blocks | Nature |
|---|---|---|---|
| **S9-1** | **The dashboard task region has no endpoint.** [ui-design.md](ui-design.md) §5.1 and §13, story 14's own AC, and [data-model.md](data-model.md) §6's index `TicketTask(assignedUserId, isDone, dueAt)` *"Open and overdue tasks on the dashboard"* all assume a **cross-ticket** task list. [api-design.md](api-design.md) §5.6 publishes only ticket-scoped task endpoints | Story 14 AC 2; story 08's queue region | 🔴 **Contradiction.** Either publish `GET /tasks?assignedUserId=me&isDone=false` (a Stage 7 change, 66 → 67) **or** drop the region (a Stage 8 change, recorded per product-scope §10) |
| **S9-4** | **No contract path records AI suggestion acceptance or override.** Story 11's AC requires it, [data-model.md](data-model.md) §2.7 provides `AiSuggestionOffered`/`AiSuggestionResolved`, and [api-design.md](api-design.md) §5.8 says it happens *"when the agent saves the ticket"* — but `POST /tickets` accepts **no field** that could carry it, §6.11 adds none, and §7 lists none | Story 11 AC 4 | 🔴 **Contradiction.** Either add a request field or a recording endpoint. Both are Stage 7 changes |
| **S9-9** | **PF-4 was required to be pinned *before* story 15 was planned** ([api-design.md](api-design.md) §9 item 2) and was not | Story 15 `agentPerformance.assignedCount` | 🟠 **Process gap.** Isolated to one method, which throws until decided |
| **S9-10** | **PF-2 comes due in story 18.** The fake inbound adapter has **no actor**, while `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required and `actorKind = System` is reserved for the SLA monitor (R-14) | Story 18 AC 3 | 🟠 **Carried finding, now scheduled.** Three options set out; none chosen |

**Non-blocking — resolved from the approved documents, recorded for visibility**

| ID | Finding | Resolution |
|---|---|---|
| **S9-2** | `GET`/`POST /tickets/{id}/attachments` is assigned to **no story** in [api-design.md](api-design.md) §10.1, though §5.6 lists both and story 04's AC requires them | Shared service and the AP-19 download in story 04; the two ticket-scoped endpoints in story 05. Read from the intakes |
| **S9-3** | **OQ-2 and OQ-3 reach further than §6.1 records.** OQ-2's first code path is story 05's `PATCH` priority, not story 09's escalation. OQ-3 reaches story 06's **manual** escalation | Both stories carry the block. **Neither question is answered.** §6.1 updated |
| **S9-5** | Three entity placements deviate from [data-model.md](data-model.md) §7's story→entity map: `AuditEntry` (→ 02), `TicketActivity` (→ 05), SLA due-date computation (→ 05) | Placement rule adopted: **the entity lands with the story that first writes it; the read surface with the story that owns it.** No business rule and no schema changed |
| **S9-6** | `GET /tickets` has no `customerId` filter, yet [ui-design.md](ui-design.md) §5.3's customer panel shows *"recent tickets"* | Derived client-side from the timeline response, which carries `ticketId` and `ticketSubject`. **No filter invented** |
| **S9-7** | `POST /auth/register` cannot be implemented in story 02 — A-15 needs a `Customer`, a `Branch` and a default branch | Implemented in story 04 |
| **S9-8** | **Story 08 does not depend on story 09**, contrary to §3's earlier reading. Due dates are required at creation, so SLA ordering is available from story 05 | Queue built once, correctly. **No fallback ordering and no swap to record.** §3 and the [story-backlog.md](story-backlog.md) cut order corrected |
| **S9-11** | **[ui-design.md](ui-design.md)'s header is stale** — it cites *"65 endpoints, AP-1…AP-18"* while [api-design.md](api-design.md) has **66** and **AP-19** | 🟡 **Documentation staleness only.** Every screen maps to an endpoint that exists. **A one-line header correction, not applied — it is an edit to an approved document and is offered rather than taken** |
| **S9-12** | Story 02 depends on story 03's entities (`POST /users` needs a `departmentId`) | Phase 1 executes them as an **adjacent interleaved pair**. Both intakes already say *"planned together or in immediate sequence"* |
| **S9-13** | [api-design.md](api-design.md) §10.1 assigns `/config` and `/config/staff` to story 01, whose AC forbids endpoints beyond health — and both need authentication story 01 lacks | Story 01 delivers `/health` and the anonymous `/config/bootstrap`; the two authenticated tiers land in **story 16 Part A**, which §10.1 also lists |

**F-1 is deliberately still open.** [ui-design.md](ui-design.md) **UI-3** is approved and says the
transition menu is computed client-side; the server remains the authority and a wrong offer gets
`403`/`409`. [sdd-workflow.md](sdd-workflow.md) gate 9 → 10 requires no decision here. Story 06's
plan confines the duplicated matrix to **one file**, `shared/lifecycle/transition-matrix.ts`, so
closing F-1 later deletes exactly one file.

### 6.4 Decision → document → story traceability

Which documents and which story intakes each resolved decision actually changed. "—" means nothing
needed amending, because nothing in that artifact contradicted the decision.

| Decision | Documents changed | Story intakes changed |
|---|---|---|
| **R-1 / AD-15** token asserts identity only | `architecture.md` §4.1, **new §4.1.1**, §3 flow, §4.3, AD-7, **new AD-15** | — |
| **R-2** branch derived, not stored | `data-model.md` §2.3, §4, §8 | — |
| **R-3 / DM-5** no AI entity | `data-model.md` §1 | — |
| **R-4 / DM-1** `User` and `Customer` separate | `data-model.md` §1, §2.1, §2.4 | — |
| **R-5** CSAT scale assumption withdrawn | `data-model.md` §2.15, §8 (reopened as OQ-1) | — |
| **R-6 / A-14** category → department routing | `product-scope.md` A-6, **new A-14**; `architecture.md` §6.3 (new config key); `data-model.md` invariant 11a | — |
| **R-7 / A-15** self-registration branch + linking | `product-scope.md` A-9, **new A-15**; `architecture.md` §6.3 (new config key); `data-model.md` DM-1, §2.4 | — |
| **R-8 / A-16** transition authority matrix | `product-scope.md` A-5, **new A-16**; `data-model.md` invariant 11b | `ticket-lifecycle`, `portal-self-service` |
| **R-9 / A-17** `isUrgent` customer input | `product-scope.md` A-6, **new A-17**; `data-model.md` §2.6 (**new field**) | — |
| **R-10** agents and managers may cancel | `product-scope.md` A-16 (cancel row + consequences) | `ticket-lifecycle`, `portal-self-service` |
| **Stage 8 UI design** | `ui-design.md` (new); `sdd-workflow.md` | All 18 — §13 maps every story to its screens (10 and 18 have none) |
| **B-1** feedback rating-scale config key | `architecture.md` §6.3 | — (story 13 consumes it) |
| **B-2 / AP-17** configuration split by audience | `api-design.md` §5.1, §3 | — (stories 01, 08, 13, 16 consume it) |
| **N-2 / OQ-5** customer email is patchable | `api-design.md` §5.5 | — (story 04; **no longer blocked** — see R-15) |
| **R-15 / A-19** a customer's email and their portal sign-in are one address, **and the login change is audited** | `product-scope.md` **new A-19** (incl. the audit bullet); `api-design.md` §5.5 (box rewritten + audit note), §9 item 3, §10.3; `data-model.md` §2.4 invariant, **new §5 constraints 1a and 1b**, §2.14 (`UserEmailChanged` note + action list), §8 resolved list; `ui-design.md` §5.5, §11 (row retired); `sdd-workflow.md` + 4 source-of-truth lines | `customer-records` (task 3); `audit-configuration` (task 6 inventory) |
| **R-14 / A-5** automatic transition attributed to the replying customer | `product-scope.md` A-5 (new attribution rule); `data-model.md` §2.6 invariant 2b, §2.7 `actorKind` note, §2.8 invariants, §5 constraint 9a | `ticket-lifecycle` |
| **R-13 / A-5** customer reply reopens a `Pending` ticket | `product-scope.md` A-5 (transition graph + `Pending` bullet), A-16 (`-> Open` row); `data-model.md` §2.6 invariant 2b, §2.8 invariants, §5 constraint 9a | `ticket-lifecycle`, `ticket-intake-messaging`, `portal-self-service` |
| **R-12 / DM-7** `CustomerFeedback` owned by `Tickets` | `data-model.md` **new DM-7**, §1 preamble, §2.15 ownership, §3 entity list; `architecture.md` §1 (`customer-portal` row) | — (no intake asserted an owning module) |
| **R-11 / A-18** assignment is not the start of work | `product-scope.md` A-5, **new A-18**, §9 (question 8 closed); `data-model.md` §2.6 field, **new invariant 2a**, 11b, §8 | `ticket-lifecycle`, `ticket-core`, `sla-routing-escalation`, `portal-self-service` |

`sdd-workflow.md` was touched by the A-14…A-18 set only to widen the assumption range it cites.
No decision so far has changed [requirements.md](requirements.md), which is never edited.

---

## 7. Implementation Progress

**Stories 01, 02 and 03 are implemented and verified.** Every claim below was re-verified by
running the commands in §8 on 2026-08-27, not inferred from a plan.

| Area | Status | Evidence |
|---|---|---|
| Backend | ✅ Stories 01, 02, 03 | `dotnet build`: **0 warnings, 0 errors** with `TreatWarningsAsErrors`. `SupportCrm.Domain` still has **0 project and 0 package references** and no EF attribute (AD-2, AD-4) |
| Frontend | ✅ Stories 01, 02, 03 | Angular 20.1.2 + PrimeNG 20.0.0 on Sakai `20.0.0`. `npm run build` succeeds; `npm run lint:styles` clean. Sign-in, guards, role redirect, staff shell avatar menu and the admin user screens all exercised in a real browser |
| Database | ✅ Schema live | **`InitialSchema` applied to real SQL Server**: `Branches`, `Departments`, `Users`, `AuditEntries`. `Users.Email` carries `SQL_Latin1_General_CP1_CI_AS`; `Users(CustomerId)` is a filtered unique index; `Users.CustomerId` has no FK yet (Story 04 adds it) |
| Seed data | ✅ Running at startup | 2 departments (both with a manager), 2 branches, 4 staff users — Administrator, Manager and **two Agents in different departments**, so Story 05's scoping tests have material. Password from configuration; no credential in source |
| Tests | ✅ 32 passing, 1 skipped by design, plus 4 front-end specs | Backend: `AuthorizationTests`, `UserAdminValidationTests`, `AuditRecordingTests`, `PlatformEndpointTests`, `OrganizationEndpointTests`. Includes the **AD-15 deactivation regression** and the indistinguishability of a wrong password from a deactivated account. `BranchIsNotABoundaryTests` is **skipped until story 05** creates `Ticket`. Front end: `department-filter.component.spec.ts` — the repo's first specs; the karma target and its dependencies were already configured by story 01 |
| Docker / infrastructure | ✅ Complete for the stack | `docker compose up --build` brings **db, api, web** up; the API waits for the database's health check |

**Endpoints that exist (11):** `/health`, `/config/bootstrap`, `/auth/login`, `/auth/me`, the five
`/users` routes, and `GET /departments` and `GET /branches`. `POST /auth/register` is **deferred to
Story 04** by S9-7. **Neither organization route has a write verb**, and neither ever will: T2-I
makes departments and branches seeded configuration.

**Next:** story 16 Part A, then stories 04 and 05 — **not started, and blocked on explicit approval.**

---

## 8. Verification

Only entries with real evidence are marked verified. Every ✅ below names the command that was run
and what it returned.

**The whole story-01 suite was re-run on 2026-08-26** from the same working tree, after the
containers had been stopped and Docker restarted, and produced identical results — so the rows
below record a reproducible outcome, not a one-off.

| Check | Status | Evidence / date |
|---|---|---|
| `squad doctor` | ✅ Passing | Re-run 2026-08-24: **6 ok · 0 warn · 0 fail · 7 skip** (skips are planner and tracker, both disabled by design) |
| `squad status` | ✅ Consistent | Re-run 2026-08-25: **18 stories, 18 plan files, next `NN` 19** |
| Stage gate 8 → 9 | ✅ Met | [ui-design.md](ui-design.md) covers all 24 screens with RTL and phone-width behaviour |
| **Stage gate 9 → 10** | ✅ **Met for story 01** | All 18 plans cite concrete paths and runnable verification commands. The third clause — *prerequisites already implemented* — is **per story** and is satisfied one story at a time during stage 10 |
| **Stage 9 traceability audit** | ✅ Run 2026-08-25 | 18 intakes × 66 endpoints × 15 entities × 24 screens reconciled. **13 findings (S9-1…S9-13)**, 4 blocking. Endpoint, entity and screen totals in the plans match the design documents exactly |
| Stage gate 3 → 4 | ✅ Met | All 56 requirement lines mapped to a story |
| Stage gate 5 → 6 | ✅ Met | Re-verified after the AD-15 correction |
| Stage gate 6 → 7 | ✅ Met | Re-verified after the four clarifications |
| Story verification | ✅ **Stories 01, 02 and 03 passed** | Story 01: all 8 steps, 2026-08-25 (re-run 2026-08-26). Story 02: all 6 steps, 2026-08-26. Story 03: **5 of its 6 steps, 2026-08-27** — step 6 names the story-05 ticket list and could not be run as written (see its row). Details in the rows below |
| Unit / integration tests | ✅ **32 passing, 1 skipped** | `dotnet test backend/SupportCrm.sln` 2026-08-27: **32 passed, 0 failed, 1 skipped**. The skip is `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`, **skipped by design** until story 05 creates the `Ticket` type it asserts about. Story 03 adds the two organization endpoints' role matrix, the `managerUserId`-is-absent-not-null contract check, and the no-write-verb lock. Front end: `npx ng test --watch=false --browsers=ChromeHeadless` — **4 passed**, the repo's first specs, on the karma target story 01 already configured. Story 02 adds the AD-15 regression (a user deactivated after their token was issued is `401` on the very next request), the wrong-password / deactivated-account indistinguishability check, token-claim absence, `passwordHash` absence from every response path, and the audit actor-attribution rules of §2.14 |
| Backend build | ✅ Clean | `dotnet build backend/SupportCrm.sln` 2026-08-25: **0 warnings, 0 errors** — and `TreatWarningsAsErrors` is on, so 0 warnings is enforced, not observed |
| Frontend build | ✅ Clean | `npm ci && npm run build` 2026-08-25. `npm run lint:styles` clean; the physical-property ban was **proved to fire** by a throwaway probe file (3 errors), then removed — the rule is enforced, not vacuous |
| API verification | ✅ Passed | 2026-08-25 against containerized SQL Server: `GET /api/v1/health` → `200 {"status":"ok","database":"reachable","utcNow":"…Z"}`. `openapi/v1.json` lists **exactly two paths** — `/api/v1/health` and `/api/v1/config/bootstrap` — and nothing else. Scalar UI at `/scalar/v1` → `200` |
| **Story 02 — auth and roles** | ✅ Passed 2026-08-26 | Live against containerized SQL Server. Token claims: **`sub`, `jti`, `iat`, `exp`, `iss`, `aud` and nothing else** — no role, department, email or active flag. Role gating on `/users`: anonymous `401` · Agent `403` · Manager `403` · Administrator `200`. Case-insensitive sign-in (`ADMIN@SupportCRM.LOCAL`) `200`, exercising the collation SQLite cannot verify. Wrong password returns `type: invalid-credentials`. Audit rows confirm a successful sign-in is attributed and a failed one carries the submitted identifier with a null actor |
| **Story 03 — departments and branches** | ✅ Passed 2026-08-27 | Live against containerized SQL Server on a **wiped volume**: `InitialSchema` applies from scratch, `OrganizationSeeder` writes 4 rows, the manager second pass assigns 2. `GET /api/v1/departments` returns `Billing` and `Technical`; `GET /api/v1/branches` returns `Head Office` and `North Branch`. Role matrix on **both** endpoints: anonymous `401` · Customer `403` · Agent `200` · Manager `200` · Administrator `200`. `POST`, `PATCH`, `PUT` and `DELETE` are `405` on both — **no write endpoint exists** (T2-I). An unknown sort field is `400` (AP-15). A department whose `ManagerUserId` is null serializes as `{"id","name"}` with **no `managerUserId` key**. `openapi/v1.json` publishes both paths with `get` and nothing else |
| **Story 03 — step 6 not runnable** | 🟡 Deferred to story 05 | The step reads *"the ticket-list department filter is disabled for a seeded Agent and enabled for the seeded Manager"* — and `/workspace/tickets` is **story 05**. An ordering fact, not a gap: story 03 task 8 built the rule into `shared/components/department-filter/` precisely so story 05 cannot re-implement it, and `department-filter.component.spec.ts` asserts it (Agent pinned and disabled with a hint; Manager and Administrator enabled). **Story 05 must run the step against the real screen** |
| **Story 03 — branch is not a boundary** | ✅ Passed 2026-08-27 | `grep -ri "branchid" backend/src/SupportCrm.Domain/Modules/Tickets/` returns **nothing**. Every `Branch` reference across the backend was read: each is a payload field, a foreign-key-target check, a seeder, or the read endpoint — **none is in an authorization predicate** (A-2, data-model §5 constraint 6) |
| **Story 02 — role UI** | ✅ Passed 2026-08-26 | Driven through the Chrome DevTools Protocol: all three seeded staff roles sign in, land on `/workspace`, and show display name, role and department in the avatar menu. The **Administration** section renders for Administrator only. An Agent deep-linking to `/admin/users` lands on `/403`; an Administrator sees all four users with no `passwordHash` anywhere in the DOM |
| UI verification | ✅ Passed | 2026-08-25, driven through the Chrome DevTools Protocol against the running `web` container: the shell renders the configured product name (`Support CRM`), `--app-brand-primary: #0B5FFF` from `/config/bootstrap`, the health result and the language switcher. Clicking العربية moved `<html>` from `dir="ltr" lang="en"` to `dir="rtl" lang="ar"` with every label translated. **No reload and no state loss**: a `window` marker and a DOM attribute set before the switch both survived it, and the health timestamp did not change |
| Docker startup | ✅ Passed | `cp .env.example .env` then `docker compose up --build` 2026-08-25: **db (healthy) · api · web** all running. The SPA reaches the API through the nginx `/api` proxy, and deep links fall back to `index.html` |
| Working tree | 🟡 Uncommitted | **21 commits** on `main`, head `9ad60a8`; the **story-03 implementation is in the working tree and not yet committed** at the user's request. **`git status` is the live source — this row is a snapshot, not a claim to be trusted over the repository** |

---

## 9. Change Log

Newest first. Every meaningful project change gets an entry.

### 2026-08-27 (latest) — OQ-5 answered: A-19, and the login change is audited

- **The product owner chose Option A**, and it is recorded as **A-19** in
  [product-scope.md](product-scope.md) §7 — the register A-14…A-18 already use for exactly this
  class of decision (a business question no source answered, decided after a design stage found it).
  **When `Customer.email` changes, the linked portal `User.email` is set to the same value.**
- **Atomic by the existing rule, not a new one.** [architecture.md](architecture.md) §3 already says
  *"one unit of work per request, owned by the Application service, committed once"* — both rows
  change in one commit, so no committed state has them differing. `architecture.md` needed no
  change beyond its assumption-range citation.
- **`User.email` uniqueness is untouched and applies to the propagated value** across all users,
  staff included. A collision returns **`409 user-already-exists`** — **PF-6's existing slug for
  PF-6's existing rule** — and writes **neither** row. No new problem type was minted.
- **Closing it removed a latent gap in `api-design.md` §5.2.** Had divergence been allowed, a
  profile could hold an address matching a registration attempt while already carrying a login under
  a different one — a state none of A-15's three outcomes covered, and one §5 constraint 3 forbids
  resolving by linking a second login. A-19 makes that state unreachable, so the three outcomes stay
  exhaustive.
- **`ui-design.md` §11 released the warning it was holding.** That row said *"add a warning only once
  OQ-5 is answered"*; §5.5 now specifies a persistent helper line on the email field, stated **before**
  the save because A-9 excludes account recovery. It is unconditional: the `Customer` payload carries
  no "has a login" field and **none was added** — inventing contract surface to vary one sentence was
  rejected.
- **Two pre-existing contradictions were found by the consistency check and fixed**, neither related
  to OQ-5: `api-design.md` §10.3 still traced A-10 to *"email immutability"* eight weeks after N-2
  removed that rule, and `ui-design.md`'s source-of-truth line still cited *"65 endpoints,
  AP-1…AP-18"* after AP-19 took it to 66.
- **Amended the same day, on the product owner's instruction: the propagated login-email change is
  audited**, as part of A-19 and inside the same unit of work.
  - **Action code `UserEmailChanged`**, following `UserRoleChanged` / `UserDepartmentChanged`
    exactly. **One new constant in `AuditAction`** — `AuditEntry`, `AuditTargetType`,
    `IAuditRecorder` and the migration are untouched, and no endpoint is added. `AuditAction`'s
    own comment already declared the set open, and data-model §2.14 already gave its actions as
    examples.
  - **Actor is the agent who issued the `PATCH`**, resolved from `ICurrentUser` the ordinary way.
    The `actorUserId` override is explicitly **not** used — it exists for the anonymous
    successful-sign-in case alone.
  - **Target is the linked `User`, not the customer.** The audited fact is that a sign-in
    identifier changed; the profile edit beside it is business data, not a security event (AD-10),
    and is not audited.
  - **The entry records no email address, old or new** — `AuditEntry` has no value columns, exactly
    as `UserRoleChanged` records that a role changed without recording which. Adding them would be
    a schema change *and* would copy a personal identifier into a log that is never deleted, so it
    was rejected. Recorded in data-model §2.14 so nobody re-opens it as an oversight.
  - **Written only on a real change**, and never on a rejection: no entry for an absent `email`, an
    unchanged address, a customer with no linked login, or either `409`. **No `Failure` row exists
    for this action**, matching every user-administration call site in Story 02 — only a failed
    *sign-in* is recorded as `Failure`.
  - **Atomic without new machinery:** `RecordAsync` adds to the change tracker and does not commit,
    so the single `SaveChangesAsync` commits both rows and the entry together.
  - **Story 16's audit-coverage inventory (task 6) gained the row**, so its completeness check does
    not later flag `UserEmailChanged` as an unaccounted action.
- **Nothing was implemented.** Story 04 remains Not Started.

### 2026-08-27 — story 03 completed

- **Story 03 tasks 4–8 implemented**, closing the story and phase 1. `OrganizationQueryService` and
  `DepartmentValidator` in Application; `DepartmentsController` and `BranchesController` in Api, one
  `GET` each behind `RequireAgent`, **with no write verb on either** (T2-I). Front end:
  `organization.client.ts` with both lists `shareReplay`-cached for the session, and
  `shared/components/department-filter/` carrying the `disabledForOwnDepartment` rule so the story-05
  ticket list cannot re-implement it.
- **Both placeholders the previous entry flagged are gone.** The create-user dialog and the
  user-detail form now bind a selector populated from `GET /departments` instead of taking a
  department id as free text, and the avatar menu shows the department **name** rather than its id.
- **`IdentitySeeder`'s manager second pass now calls `DepartmentValidator`** rather than restating the
  eligibility rule inline — what story 03 task 4 always intended, and what the seeder's own comment
  forward-referenced. The throw is caught, so an ineligible demo manager leaves the department
  without one (a legal state) instead of taking the API down.
- **One defect found and fixed, inherited from story 02.**
  `CreateUserDialogComponent.departmentMissing` was a `computed` over a **plain** field, so it cached
  against `touched` alone: after one failed submit, the warning and the disabled submit button could
  never clear whichever department the administrator then picked. The field is now a signal. It was
  reached by replacing the input it guarded.
- **Verification step 6 could not be run as written.** It names the `/workspace/tickets` department
  filter, and the ticket list is **story 05** — an ordering fact, not a gap. The rule it checks was
  verified instead by `department-filter.component.spec.ts`. **Story 05 must run the step against the
  real screen**, and remove the `Skip` on `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`.
- **OQ-3 remains open and remains unanswered.** `DepartmentValidator` constrains only a manager that
  *is* set and says nothing about a department without one; the seeded managers are still a demo
  convenience, commented as such at both ends.
- **Two implementation choices not fixed by the approved documents**, both following the `/users`
  precedent: the sort whitelist for the two organization endpoints is `name` only (api-design §2.1
  requires *a* whitelist and names none for them), and the front-end client requests `pageSize=100`,
  the contract's documented maximum — so more than 100 departments would be truncated, which is a
  question for api-design and ui-design rather than a client-side loop.
- **Verified against real SQL Server on a wiped volume:** `InitialSchema` applies, `OrganizationSeeder`
  writes 4 rows, the manager second pass assigns 2, and `GET /api/v1/departments` returns `Billing`
  and `Technical`. Live role matrix on both endpoints — anonymous `401`, Customer `403`, Agent,
  Manager and Administrator `200`; `POST`/`PATCH`/`PUT`/`DELETE` all `405`; an unknown sort field
  `400`. A department with a null `ManagerUserId` serializes with **no `managerUserId` key at all**.
  `grep -ri "branchid"` over `Domain/Modules/Tickets/` returns nothing, and **no `Branch` reference
  anywhere in the backend sits in an authorization predicate**. Stories 01 and 02 re-checked live.

### 2026-08-26 — story 02 implemented

- **Story 02 (`auth-and-roles`) complete and verified.** Email + password sign-in, the four fixed
  hierarchical roles as policies, per-request identity resolution, Administrator user management, and
  the single audit recorder. Nine endpoints now exist.
- **AD-15 is enforced by shape, not by discipline.** `ITokenIssuer.Issue(Guid userId)` has no
  parameter through which a role, department or active flag could reach the token, and
  `CurrentUserMiddleware` re-reads all three from the authoritative row on every authenticated
  request — refusing a missing or deactivated user with **`401` before authorization runs**, and
  replacing the principal's role claim so the endpoint gate and the row scoping read the same
  vintage. Verified: the issued token carries `sub`, `jti`, `iat`, `exp`, `iss`, `aud` and nothing
  else, and a user deactivated after their token was minted is `401` on the very next request.
- **The single `InitialSchema` migration was created here**, containing all four tables
  (`Branches`, `Departments`, `Users`, `AuditEntries`), exactly as both story plans and
  `00-implementation-plan.md` §6 require. Applied to real SQL Server; `Users.Email` carries the
  case-insensitive collation and a mixed-case sign-in was exercised against it.
- **Story 03 task 3 (`OrganizationSeeder`) was executed early, with the user's approval**, because
  Story 02 task 10 cannot seed a staff user without a department — the ordering Story 03's own
  prerequisites describe. Story 03 is otherwise untouched: no query service, no controllers, no
  endpoints.
- **Two defects found by this story's own verification, both contract violations, both fixed:**
  - **Enums were serializing as integers**, which api-design §2 forbids outright. It surfaced as
    `POST /users` rejecting `role: "Agent"`, so the contract breach and a functional break were one
    bug. `JsonStringEnumConverter` added.
  - **A successful sign-in recorded `actorUserId = null`.** data-model §2.14 allows null for exactly
    one reason — "no user could be resolved, a failed sign-in" — so a success must be attributed.
    `IAuditRecorder` gained an explicit actor override for the one case where the caller knows the
    actor but the request has no identity yet, `POST /auth/login` being anonymous. Tests now lock
    both directions.
- **Two front-end bugs found and fixed in the browser, not in review:** an `inject()` after an
  `await` in the app initializer threw **NG0203** and left the page blank; and a deep link to a
  guarded route bounced a signed-in user to sign-in, because
  `withEnabledBlockingInitialNavigation()` runs the router's initial navigation before the bootstrap
  `loadMe()` resolves. The guards now resolve identity on demand, which removes the provider-ordering
  dependency instead of papering over it.
- **Deviations from the plan's letter, all deliberate and reported:** `UnauthorizedException` added
  to the Story 01 exception family (the plan names it; 401 had no member yet); `IApplicationDbContext`
  introduced so Application can orchestrate persistence without naming `SupportCrmDbContext` — AD-3
  still holds, it wraps nothing and adds no method; `ICurrentUser` gained `IsAuthenticated` because
  the audit recorder must handle the actorless failed sign-in; `IAuditRecorder` takes an
  `AuditOutcome` enum rather than a `string`, so an invalid outcome cannot reach the column.
- **Three values no approved document fixes**, all placed in configuration rather than in code and
  all flagged: JWT `AccessTokenMinutes` (60), `Issuer`/`Audience` (`SupportCrm`), and the seeded demo
  password (development default in `appsettings.Development.json`; any other environment must supply
  it or startup fails). Plus the `GET /users` sort whitelist, which api-design §2.1 requires to exist
  but does not enumerate for this endpoint.
- **`POST /auth/register` remains deferred to Story 04** (S9-7). Its route and component are
  scaffolded with submit disabled and a note, rather than calling an endpoint that does not exist.
- **Nothing was committed** — the working tree is left for review.

### 2026-08-26 — string-length convention closed

- **[data-model.md](data-model.md) §6.1 added — string column length, collation and index
  eligibility.** Closes the finding raised by story 03: no approved document stated a string length
  anywhere, yet the model declares four *unique* indexes on string columns, and SQL Server cannot
  build one over `nvarchar(max)`. Every story would have invented its own widths, and nothing kept
  `User.email` and `Customer.email` — the same address in two tables — the same size.
- **Five tiers, and a tier for every one of the 39 string fields in §2.** `Code` 64 · `Name` 200 ·
  `Email` 256 · `Line` 512 · `Text` max. Implementers pick a tier, never a number. Verified
  mechanically: 39 string fields declared in §2, 39 assigned, **no field unassigned and no stale
  entry**.
- **The index-key rule is the point of the section.** A column in an index key must be `Code`,
  `Name` or `Email`; `Text` can never be indexed. Checked against every index the model requires —
  §6's list plus the four §2 unique constraints — the widest key is `User(email)`/`Customer(email)`
  at **512 bytes, 30% of SQL Server's 1700-byte limit**, and `Ticket.status` is the only string
  inside a composite key.
- **This amends the document's own scope, and the amendment is recorded rather than slipped in.**
  The header previously said physical lengths were deliberately left to implementation, and the §8
  gate row asserted "conceptual and logical only". Both now state the single exception and why it
  was unavoidable. §2 is untouched; no DDL was added.
- **Collation decided:** `User.email` and `Customer.email` declare
  `SQL_Latin1_General_CP1_CI_AS` explicitly. The SQL Server 2022 image already defaults to
  case-insensitive, so this changes no behaviour — it is declared because "two addresses differing
  only in case are the same address" is a **product rule** (A-9, A-10) and must not depend on a
  server default a different deployment could change.
- **Three tier choices carry a stated reason** where the obvious pick was wrong:
  `Attachment.contentType` is `Name` not `Code`, because real MIME types exceed 64 characters;
  `AuditEntry.actorDescriptor` is `Email` but the recorder **truncates rather than throws**, since a
  failed sign-in with an absurd identifier must still be recorded; `User.passwordHash` is `Line` for
  headroom against a future hashing algorithm.
- **[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §6 now points at §6.1**
  so no plan re-invents lengths, and records the pinned `dotnet-ef` local tool.
- **Two comments in story 03's EF configurations corrected.** They said the length was an
  implementation choice not fixed by the data model — no longer true. They now cite the `Name` tier
  and say *"do not pick a length here; pick a tier."* No behaviour changed: 200 was already the
  value, which is why the `Name` tier was set to 200.
- **No migration was created and story 02 was not started.**

### 2026-08-26 (later) — story 03 data layer

- **Story 03 tasks 1–2 implemented** — `Department` and `Branch` domain entities plus their EF
  configuration and `DbSet`s. This is the slice S9-12 names as Story 02's prerequisite: `POST /users`
  needs a `departmentId` that already exists. Nothing else of story 03 was built, and no story 02
  code was written.
- **The `InitialSchema` migration was deliberately *not* generated, and story 03's
  `OrganizationSeeder` was deliberately *not* written.** Both plans place the migration after story
  02 task 3 so that one migration creates all four tables (`Departments`, `Branches`, `Users`,
  `AuditEntries`), and
  [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §6 lists no separate
  Organization or Identity migration. The seeder follows the migration because it queries
  `Departments`: registering it against a database with no tables would crash the API at startup and
  regress story 01. **This is a sequencing consequence of the approved plans, not a deviation** —
  see §10 item 1.
- **Schema verified without committing a migration.** A throwaway migration was generated, its
  `Up()` read — `Departments` and `Branches`, `nvarchar(200)` unique names, nullable
  `ManagerUserId` **with no foreign key** — and then reverted, leaving the tree migration-free.
- **`Department.ManagerUserId` carries no foreign key**, per data-model §2.2: the real rule (exists,
  active, role `Manager` or `Administrator`) is cross-row and conditional, which an FK cannot
  express, and a second FK would create a create-order cycle with `User.DepartmentId`. Reasoning is
  in `DepartmentConfiguration`, at the point a reader would ask.
- **Implementation choice, flagged because no document fixes it:** entity name columns are
  `nvarchar(200)`. **No approved document states a string length anywhere**, and SQL Server cannot
  build a unique index over `nvarchar(max)`, so a bound was unavoidable. If a length convention is
  wanted, it belongs in [data-model.md](data-model.md) §6 and should be applied consistently before
  story 04 adds `Customer`.
- **Added `backend/dotnet-tools.json`** pinning `dotnet-ef` **10.0.11** as a local tool. The tool was
  not installed, so the plans' own `dotnet ef migrations add` command had nothing to run. A pinned
  local manifest keeps it reproducible for a fresh clone.
- **OQ-3 remains open and unanswered.** No fallback escalation recipient exists in the code. The
  `Department.ManagerUserId` doc comment states the gap and names story 09 as the deadline.

### 2026-08-26

- **Stage 10 begins — story 01 (`solution-skeleton`) implemented and verified.** First code in the
  repository. The five-project backend solution, the Angular + PrimeNG front end, and the
  three-service Compose stack all exist and run. Two endpoints only — `GET /api/v1/health` and
  `GET /api/v1/config/bootstrap` — and **no business entity, screen or endpoint beyond them**.
  Evidence for every claim is in §8.
- **Front end is built on the PrimeNG Sakai template, tag `20.0.0` (MIT), not scaffolded from
  scratch.** This **supersedes task 8 of the story-01 plan**, which said `ng new frontend`. Taken on
  explicit user instruction. The template was stripped of its demo pages and services; its layout
  chrome (topbar, sidebar, footer, configurator) was kept and re-pointed at runtime branding, and
  the folder tree of [architecture.md](architecture.md) §2.2 was added alongside it.
- **Angular and PrimeNG are pinned to Sakai 20.0.0's own lockfile resolutions** — Angular
  **20.1.2**, PrimeNG **20.0.0**, `@primeuix/themes` **1.2.1** — rather than the looser
  "Angular 20 / PrimeNG 20" of
  [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §6. Exact pins, so
  `npm ci` is reproducible and no dependency drifts within `^20` on a later install. Sakai master
  (21.0.0, Angular 21) was **considered and rejected**: it would have contradicted the approved
  plan, and the user chose the tag that matches it.
- **Story 17 Part A delivered here**, per the split-in-time exception in
  [story-backlog.md](story-backlog.md): Transloco (AD-9), `en`/`ar` dictionaries with a
  programmatically checked-identical key set, `DirectionService` (document `dir`/`lang`, PrimeNG
  locale, choice persisted to browser storage), the language switcher in all three shells, the
  `property-disallowed-list` stylelint rule, and the three breakpoint mixins.
- **Contract correction — lowercase OpenAPI paths.** The default `[controller]` token published
  `/api/v1/Health` and `/api/v1/Config/bootstrap`, which do not match
  [api-design.md](api-design.md) §5.1. Fixed with a route-token transformer
  (`Api/Routing/SlugifyParameterTransformer.cs`) rather than `RouteOptions.LowercaseUrls`, because
  that setting only affects generated links — the route template, and therefore the published
  document, would have kept the class name's casing. Every future controller inherits the
  behaviour.
- **Bug found and fixed during UI verification.** The `en`/`ar` dictionaries used `health.database`
  as both a label and a namespace prefix, so `health.database.reachable` rendered as a raw key. A
  key cannot be a string and an object. Split into `health.databaseLabel` and `health.db.*`; a
  key-parity check across both dictionaries now runs whenever they are edited.
- **Four further deviations from the plan, all deliberate and recorded** in
  [.squad/plans/platform-foundation/00-overview.md](../.squad/plans/platform-foundation/00-overview.md):
  the stylelint logical-property rule is scoped to project-authored stylesheets with the vendored
  Sakai stylesheets ignored (they are upstream code this project does not edit);
  `frontend/proxy.conf.json` was added so `ng serve` shares an origin with the API, with CORS kept
  as the plan specified; the temporary health screen sits in `features/platform/`, to be removed
  with story 02's role redirect; and `.dockerignore` was added because host `bin/`/`obj/` were
  overwriting each image's own restore.
- **No migration was generated, by design.** Story 01 introduces no entity, so there is nothing to
  migrate; the first migration belongs to story 03. The schema will only ever be created and
  changed by **EF Core migrations** generated from [data-model.md](data-model.md) — never by
  hand-written SQL.
- **Recalculated §1.1**: delivery moves from 0 of 18 to **1 of 18**, so overall progress goes
  **35% → 38.6%**. One enabling story that carries no business behaviour is worth exactly one
  eighteenth of the delivery track and no more.
- **Stopped at the story-01 boundary.** The plan's own closing instruction and the user's standing
  constraint both require explicit approval before story 02.

### 2026-08-25

- **Stage 9 complete — the implementation plans exist.** Eighteen `NN-story-*.md` plan files
  generated into `.squad/plans/<feature>/`, one per story, in the backlog's execution order, so the
  generated `NN` prefixes match the intended sequence exactly. Each plan follows squad-kit's story
  document pattern — `Prerequisites`, `Story Goal`, `Context — Read These Files First`,
  `Product rules (from story)`, `Backend Tasks` / `Frontend Tasks`, `Verification Steps`,
  `Done Criteria` — with concrete file paths, type names, signatures and runnable commands.
  `squad status`: **18 stories, 18 plan files, next `NN` 19**; `squad doctor`:
  **6 ok · 0 warn · 0 fail · 7 skip**.
- **Added `.squad/plans/00-implementation-plan.md`** — the programme-level artifact squad-kit does
  not model: 14 workstreams, 9 phases, the dependency graph, what may run in parallel and what may
  not (**EF Core migrations are the serialization constraint** — one `DbContext`, one migration
  chain), the conventions all eighteen plans share, the audit findings, and full traceability from
  each plan back to stories, endpoints, entities and screens.
- **Filled all 14 `00-overview.md` stubs** and rewrote `.squad/plans/00-index.md`.
- **Ran a full traceability and consistency audit** across every approved document and all 18
  intakes. **13 findings, S9-1…S9-13** (§6.7). **Four block a named acceptance criterion:**
  - **S9-1** — the dashboard task region assumed by `ui-design.md` §5.1/§13, story 14's AC and
    `data-model.md` §6's index has **no endpoint** in `api-design.md`. **Contradiction.**
  - **S9-4** — story 11's AC requires AI suggestion acceptance/override to reach ticket history,
    and **no request field or endpoint exists to carry it**. **Contradiction.**
  - **S9-9 / PF-4** — `api-design.md` §9 required PF-4 to be pinned *before* story 15 was planned;
    it was not.
  - **S9-10 / PF-2** — the inbound channel adapter has **no actor**; the finding comes due in
    story 18.
  **None was resolved by invention.** Each is isolated to a single named method or region, which
  throws or stays empty until the decision is recorded.
- **Nine non-blocking findings resolved from the approved documents and recorded** — S9-2, S9-3,
  S9-5, S9-6, S9-7, S9-8, S9-11, S9-12, S9-13 (§6.7). Three of them corrected statements elsewhere
  in this tracker and in `story-backlog.md`: OQ-2 and OQ-3 reach one story earlier than recorded
  (S9-3); story 08 does **not** depend on story 09 (S9-8); story 16's configuration half is
  consumed by stories 04 and 13 as well (recorded in `story-backlog.md`).
- **No open question was answered and no business decision was changed.** OQ-1, OQ-2, OQ-3, OQ-5,
  F-1, PF-2, PF-4, PF-5 and N-5 all stand exactly as they did before stage 9.
- **`sdd-workflow.md` updated** — stage 9 marked complete, gate 9 → 10 restated as a per-story gate
  and its programme-plan artifact recorded. **`story-backlog.md` updated** — plan and phase columns,
  the execution-order refinements, the S9-8 correction to the cut order, and the story-state table.
- **Overall progress 31% → 35%.** The design-and-planning track is now fully consumed; every
  remaining point is delivery.
- **Closed N-1 — the API payload catalogue.** A focused Stage 7 refinement, not Stage 9.
  `api-design.md` §6 grew from 5 payload shapes to **29 across 12 subsections**: identity and
  access, organization, customers, tickets, knowledge base, notifications, attachments, reporting,
  administration and configuration, AI assists, the three previously unstated request bodies
  (`POST /auth/login`, `POST /kb/articles`, `PATCH /kb/articles/{id}`), and Problem Details.
  Every field traces to `data-model.md`; display-name projections are labelled as projections.
  Three fields are stated as never appearing in any response: `passwordHash`, `storagePath`, and
  raw actor ids where a display name suffices.
  **A genuine contradiction surfaced and was fixed (F-2):** AP-13 promised an authorized attachment
  download endpoint that the catalogue never defined, while story 04 requires "downloaded again".
  `GET /attachments/{attachmentId}/content` was added as **AP-19** — one endpoint for every role,
  the single deliberate exception to AP-5's portal split, because a byte stream has no DTO to vary
  and the authorization question is identical. Endpoint count **65 → 66**.
  **No open question was resolved:** the `ratingScale` values in the config example are labelled
  illustrative placeholders (OQ-1), `assignedCount` stays unqualified (PF-4), `firstRespondedAt` is
  documented as possibly null on a resolved ticket (PF-5).
  **F-1 re-checked and deliberately left open** — see §6.6.
  *Files:* `docs/api-design.md`, `docs/PROJECT-PROGRESS.md`.
- **Completed Stage 8 — UI Design.** `docs/ui-design.md`: **24 screens** across four surfaces —
  2 auth, 8 workspace, 7 admin, 4 portal, 3 status — with a route tree, three shells, a shared
  component inventory, and empty/loading/error conventions. Every screen carries its route, roles,
  responsibilities and **the API endpoints it consumes**; all of them verified to exist in
  `api-design.md` (the two endpoints removed by AP-18 appear nowhere). Twelve UI decisions
  (UI-1…UI-12) recorded with rationale.
  **Lifecycle rules honoured visibly:** the transition menu offers only what A-5 and A-16 allow;
  assignment does not change the status chip (A-18); replying to a `Pending` request reopens it
  automatically from the response (R-13) and the activity region shows the **customer** as the
  actor (R-14); portal cancel is offered only while `New`, which A-18 makes a real window.
  RTL (§10.2) and phone-width behaviour for the three T3-F surfaces (§10.3) are specified.
  **Open questions were not resolved:** §11 marks seven dependencies — OQ-1 (the feedback control's
  shape), OQ-3 (what the escalate dialog may claim), OQ-5 (the email field), PF-4, PF-5, N-1, and
  OQ-2 which turns out to have no UI dependency at all.
  **One finding, F-1 (non-blocking):** the ticket payload exposes no `allowedTransitions`, so the
  menu duplicates the authority matrix client-side. Reported, not applied.
  Gate 8 -> 9 met.
  *Files:* `docs/ui-design.md` (new), `docs/sdd-workflow.md`, `docs/PROJECT-PROGRESS.md`.
- **Stage 7 post-flight review, and the corrections it required.** Reviewed `api-design.md` against
  every authoritative source. **Two blocking defects found and closed:**
  **B-1** — the feedback contract depended on a `Feedback:RatingScale` configuration key that
  architecture §6.3 did not have, so the approved contract was not implementable from the approved
  documents. The key is now an approved entry **with its values deliberately undecided**, because
  OQ-1 is still open and the scale must not be invented.
  **B-2** — `GET /config` handed **quick replies and SLA targets to every authenticated caller,
  including Customers**. Configuration is now split into three audience tiers — public,
  customer-safe, staff-only — recorded as AP-17, with `403` for a customer reaching the staff tier.
  **Three non-blocking items also closed:** the unsupported email-immutability rule removed (N-2,
  replaced with the uniqueness validation the model actually supports), two endpoints that traced to
  no requirement removed (N-3, AP-18), and `hasFeedback` declared as a derived field (N-4).
  **One new open question:** **OQ-5** — whether changing a customer's email also changes a linked
  portal login's sign-in email. Raised rather than invented; blocks story 04 only.
  **Deferred by decision:** N-1, the eleven undefined response shapes, tracked for completion
  **before Stage 9**; N-5 recorded without change because fixing it would pre-empt OQ-1.
  Endpoint count 66 → 65. No new contradiction introduced.
  *Files:* `docs/architecture.md` §6.3, `docs/api-design.md`, `docs/PROJECT-PROGRESS.md`.

### 2026-08-24

- **Completed Stage 7 — API Design.** `docs/api-design.md`: 66 endpoints across 12 modules,
  covering every T1/T2 story. Role gate stated on every endpoint; department scoping expressed as
  `404` rather than `403` so the boundary does not leak existence (AP-4); a separate `/portal` path
  space for the Customer role (AP-5); lifecycle changes as an action endpoint carrying the target
  status, with A-5 legality and A-16 authority producing distinct `409` and `403` (AP-6); the
  automatic `Pending -> Open` on customer reply, with R-14 attribution, as the one status side
  effect in the API (§5.7); thirteen classes of server-derived field excluded from request models
  (§7). Sixteen technical decisions recorded as AP-1…AP-16 with rationale and rejected alternative.
  **No business rule was invented.** Gate 7 -> 8 met.
  *Notable:* PF-2 is **avoided** rather than solved — AP-11 publishes no inbound-channel endpoint,
  so no contract needs a system actor the model cannot express; the gap remains for story 18.
  PF-6 and PF-7 are **closed** by the contract. PF-3 is handled but **requires a
  `Feedback:RatingScale` key that architecture §6.3 does not have — raised for approval, not
  applied**. PF-4's metric semantics remain undecided by design.
  *Files:* `docs/api-design.md` (new), `docs/sdd-workflow.md`, `docs/PROJECT-PROGRESS.md`.
- **Approved the actor attribution for the automatic `Pending -> Open` transition.** When PF-1 was
  propagated, the transition had no invoking user while A-5 requires every transition to carry an
  actor; the customer was chosen and **flagged as a derived detail rather than applied silently**.
  Now approved and recorded as a rule: the **replying customer** is the actor, `actorKind = User`,
  never `System`. The SLA monitor remains the only system actor. The wording in the data model was
  changed from reasoning ("since their action caused it") to an approved rule citing A-5, and the
  `actorKind` field note now states the boundary where the enum is defined.
  **No new entity or field** — `TicketActivity` already carries `actorUserId` and `actorKind`.
  *Files:* `docs/product-scope.md` (A-5), `docs/data-model.md` (§2.6, §2.7, §2.8, §5),
  `.squad/stories/ticket-management/ticket-lifecycle/intake.md` (new acceptance criterion),
  `docs/PROJECT-PROGRESS.md`.
- **Resolved PF-1 — a customer reply reopens a `Pending` ticket.** The Stage 7 pre-flight found
  that `Pending -> Open` appeared in no source, while `Pending` means "awaiting customer input",
  A-13 defines a `CustomerReplied` notification, and A-3 keeps the SLA clock running — so a replied-to
  ticket could not leave `Pending`. **Decision: the transition is legal and the customer's reply
  triggers it automatically**, in the same transaction as the message, from `Pending` only.
  Two contracts were blocked and are now designable: the ticket transition endpoint's legal-transition
  table, and the post-message endpoint's status side effect.
  *Consistency re-checked after propagation:* transition graph, A-16 authority matrix (the customer
  still cannot invoke `-> Open` directly), notification behaviour (no new type — A-13's four stand,
  and the transition raises none of its own), SLA behaviour (unchanged; `Pending` never paused the
  clock), and a search for the old three-arrow transition set, which no longer appears anywhere.
  *Derived detail, flagged not invented:* A-5 requires every transition to carry an actor, so the
  automatic `StatusChanged` row is attributed to the **replying customer**. **Approved as R-14 the
  same day** and recorded as a rule.
  *Files:* `docs/product-scope.md` (A-5 graph and `Pending` bullet, A-16 `-> Open` row),
  `docs/data-model.md` (§2.6 invariant 2b, §2.8 invariants, §5 constraint 9a), three story intakes
  (`ticket-lifecycle`, `ticket-intake-messaging`, `portal-self-service`), `docs/PROJECT-PROGRESS.md`.
- **Ran the Stage 7 pre-flight audit.** Re-read every authoritative source and checked traceability
  end to end, all 18 stories against the architecture and data model, and the fifteen named subject
  areas. Verdict at the time: 🔴 NOT READY on one blocking finding (PF-1, above). Six non-blocking
  findings recorded in §6.5; three cosmetic ones noted. All numbering, stage numbering, module
  ownership, active-vs-resolved question state and story blockers verified consistent.
  *Files:* none — the audit modified nothing.
- **Resolved the `CustomerFeedback` module-ownership mismatch.** The data model labelled the entity
  as owned by a "Portal" module, which does not exist — [architecture.md](architecture.md) §1
  defines ten backend modules and `customer-portal` is a front-end area. **Decision: the existing
  `Tickets` module owns it**, because feedback is domain behaviour attached to a ticket and is
  offered when the ticket reaches `Resolved`. **No new module was created and the ten-module
  architecture is unchanged**; the entity's shape, fields, relationships and invariants are
  untouched. Recorded in the data model as **DM-7** so the reasoning travels with the label.
  While there, one adjacent looseness was fixed for consistency: `Notification` was labelled "SLA"
  and "SLA/notifications module" and now reads `Sla`, matching the module list.
  *Why:* found during the §1 wording correction and reported as needing a decision before the
  Stage 7 pre-flight, since the feedback contract depends on it.
  *Files:* `docs/data-model.md` (**new DM-7**, §1 preamble, §2.12, §2.15, §3),
  `docs/architecture.md` §1 (`customer-portal` row), `docs/PROJECT-PROGRESS.md`.
  *Not changed:* `sdd-workflow.md` — its inventory counts the A-\* assumptions, and this is a
  DM-level modelling decision, so the range A-1…A-18 is still correct.
- **Corrected loose wording in `architecture.md` §1** about the relationship between feature slugs
  and backend modules. The text claimed the ten modules matched "the feature slugs already in
  story-backlog.md"; there are **fourteen** slugs. The section now maps the ten slugs that do have
  a backend module, names the four that do not — `platform-foundation`, `agent-workspace`,
  `customer-portal`, `platform-experience` — as front-end areas or cross-cutting platform
  concerns, and states that a feature slug is a unit of planning while a module is a unit of code
  organization.
  *Why:* found by the tracker audit and reported as inaccurate but unfixed; corrected now on
  request. **Documentation wording only — no architecture, module boundary, business decision or
  data-model change.**
  *Files:* `docs/architecture.md` §1, `docs/PROJECT-PROGRESS.md`.
- **Audited this tracker against every project document** ahead of the Stage 7 pre-flight.
  Checked the twelve things it must track, then cross-read `product-scope.md`,
  `architecture.md`, `data-model.md`, `sdd-workflow.md`, all 18 intakes and the repository state.
  **Six discrepancies found, all in this file, all fixed:** the plan-overview stub count was 13 and
  is 14 (14 feature slugs, not the 11 previously reported); line counts cited for
  `architecture.md` and `data-model.md` had gone stale and were removed rather than re-pinned;
  the working-tree row still claimed 5 commits, clean, nothing pushed, when there are 6 commits,
  all pushed, with 9 files uncommitted; a historical change-log entry repeated the feature
  miscount; §5 read as though it were the complete decision inventory when the A-* business
  decisions live in §6.3; and the decision → document → story traceability required of this
  tracker did not exist, so §6.4 was added.
  **No stale status, missing decision, missing open question, mis-stated blocker, or unsupported
  claim was found** — the stage numbering matches `sdd-workflow.md`, the three active open
  questions match `data-model.md` §8, and every story blocked by an open decision is marked.
  *Why:* the tracker is the single progress source of truth and had drifted on facts about itself.
  *Files:* `docs/PROJECT-PROGRESS.md` only. No project document was modified.
- **Corrected the cancel authority and resolved OQ-4.** Agents and Managers may now cancel
  (Administrators were already unrestricted); customers may cancel their own ticket only while it
  is `New`. OQ-4 answered by **A-18**: automatic assignment does not mean work has started, a
  ticket may be assigned while still `New`, and `New → Open` is the agent starting work.
  *Why:* the first version of A-16 withheld cancel from agents and managers, and the cancellation
  window it described could have been zero because auto-assignment runs at creation.
  *Contradiction found and fixed:* A-5 and the `ticket-lifecycle` intake both defined `New` as
  "created, **unassigned**" and `Open` as "assigned and being worked" — directly incompatible with
  A-18. Both were rewritten, and the data model's `assignedUserId` note ("Null while `New`") with
  them; status and assignee are now explicitly independent.
  *Files:* `docs/product-scope.md` (A-5, A-16, **A-18**, §9), `docs/data-model.md` (§2.6 field and
  invariants 2a/11b, OQ register), `docs/architecture.md` and `docs/sdd-workflow.md` (assumption
  range), four story intakes (`ticket-lifecycle`, `ticket-core`, `sla-routing-escalation`,
  `portal-self-service`), `docs/PROJECT-PROGRESS.md`.
- **Answered four blocking business decisions ahead of Stage 7** and recorded them as assumptions
  **A-14…A-17** in the product scope: category→department routing, self-registration branch and
  profile-linking, the ticket-transition authority matrix with manual-only closure, and the
  `isUrgent` customer input. Consequences propagated: two configuration keys added to the
  architecture (category→department map, default branch), `Ticket.isUrgent` added to the data
  model, and `Customer`/DM-1 amended for the linking rule.
  *Why:* a pre-Stage-7 review found four questions that no source answered and that each changed
  an API contract; none was in the open-question register.
  *Files:* `docs/product-scope.md`, `docs/architecture.md`, `docs/data-model.md`,
  `docs/sdd-workflow.md`, `docs/PROJECT-PROGRESS.md`.
- **Opened OQ-4** — the boundary of "before work begins" for customer cancellation. Auto-assignment
  at creation may leave a zero-length cancellation window, so A-16's rule needs one more
  clarification before the portal cancellation contract can be written.
  *Files:* `docs/product-scope.md` (§9 question 8), `docs/data-model.md` (§8 register).
- **Reviewed all design documents for contract-blocking ambiguities** before starting Stage 7.
  Found four blockers (recorded above), one partial (OQ-1, deferrable through configuration) and
  eight non-blocking items with the technique for designing around each. Established that OQ-2 and
  OQ-3 do not affect any contract shape.
  *Files:* none — analysis only.
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
  assumptions and 7 open questions; squad-kit v0.2.0 initialized; 18 story intakes across 14
  feature slugs; SDD workflow and backlog documents written.
  *Files:* `docs/product-scope.md`, `docs/sdd-workflow.md`, `docs/story-backlog.md`, `.squad/**`.
  *Commit:* `ba89afd`.
- **Stage 1 — Requirements received** and analysed (functional and non-functional requirements,
  actors, workflows, ambiguities, assumptions). Analysis delivered in conversation and distilled
  into the product scope. *Files:* `docs/requirements.md`. *Commit:* `c08899c`.

---

## 10. Current Next Steps

1. **Stories 01, 02 and 03 are done; phase 1 is closed — awaiting explicit approval before the next
   step.** The phase-1 interleave of S9-12 played out exactly as designed:

   | Order | Work | State |
   |---|---|---|
   | 1 | Story 03 tasks 1–2 — `Department`, `Branch`, EF configuration | ✅ Done |
   | 2 | Story 02 tasks 1–3 — `User`, `AuditEntry`, their configuration | ✅ Done |
   | 3 | **`InitialSchema` migration** — one migration, all four tables | ✅ Done |
   | 4 | Story 03 task 3 — `OrganizationSeeder` (`Order = 10`) | ✅ Done (executed early, approved) |
   | 5 | Story 02 task 10 — `IdentitySeeder` (`Order = 20`) + manager second pass | ✅ Done |
   | 6 | Story 02 tasks 4–14 — auth, endpoints, front end | ✅ Done |
   | 7 | Story 03 tasks 4–8 — the two `Agent`-gated endpoints and the filters | ✅ Done |

   **Story 16 Part A is the gate before stories 04 and 05**, because a ticket cannot be created
   without the category list, the category→department map and the SLA targets.

2. **Both placeholders are gone.** The create-user dialog and the user-detail form now bind a
   selector populated from `GET /departments` instead of taking a department id as free text, and the
   avatar menu shows the department **name**. **Two markers remain for story 05**, both deliberate and
   both cross-referenced at their call sites: the `Skip` on
   `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`, which cannot assert on a `Ticket` type
   that does not exist yet, and story 03 verification step 6, which names the `/workspace/tickets`
   department filter. Story 05 task 10 owns both.

3. **Commit the working tree.** The story-03 implementation is verified but deliberately
   uncommitted, at the user's request, for review (§8, Working tree).

4. **Answer the outstanding decisions, earliest first.** None blocked stories 01–03. **OQ-5, which
   gated story 04, was answered on 2026-08-27 (A-19), so nothing now blocks story 04.** The earliest
   remaining is needed before story 05:

   | When needed | Decision | Kind |
   |---|---|---|
   | ~~Before **story 04**~~ | ~~**OQ-5**~~ — **answered 2026-08-27 by A-19**: yes, atomically | ✅ Closed |
   | Before **story 05** | **OQ-2** — do SLA due dates recompute or freeze on a priority change? **The earliest blocker.** | Business rule |
   | Before **story 11** | **S9-4** — how is AI suggestion acceptance/override carried to the server? | Contract (Stage 7) |
   | Before **story 13** | **OQ-1** — what is the CSAT rating scale? | Product |
   | Before **story 14** | **S9-1** — publish a cross-ticket task endpoint, or drop the dashboard task region? | Contract (Stage 7) **or** Stage 8 cut |
   | Before **story 15** | **PF-4 / S9-9** — "tickets assigned": currently or ever? | Metric semantics |
   | Before **story 18** | **PF-2 / S9-10** — who is the actor on an inbound channel message? | Product |
   | Non-blocking | **OQ-3** (no-manager branch), **PF-5** (`firstRespondedAt` null), **F-1**, **N-5**, **S9-11** | — |

5. **Optionally apply the S9-11 header correction** to `ui-design.md` — it cites *"65 endpoints,
   AP-1…AP-18"* where `api-design.md` now has **66** and **AP-19**. It is an edit to an approved
   document, so it is **offered, not taken**. Nothing depends on it.

6. **Consider scope realism.** 18 stories against a 9–12 hour budget is ambitious. Now that the
   plans exist, the honest option is to **implement 01–11 (the T1 core plus the AI slice) and treat
   12–18 as planned-not-built**, which is a stronger SDD demonstration than eighteen half-finished
   stories. The cut order in [story-backlog.md](story-backlog.md) already ranks them, and four of
   the seven cuttable stories are the ones carrying blocked decisions. **Any cut is recorded in
   [product-scope.md](product-scope.md) per its §10 rule and reflected here.**

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
