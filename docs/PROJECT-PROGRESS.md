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
| **Current SDD stage** | **Stage 10 (Implementation) in progress — stories 01, 02, 03 and **04** complete and verified; story 16 **Part A** complete** |
| **Current phase** | Design and planning **complete**. Phase 0 and **Phase 1** delivered. **Phase 2 is under way**: story 16 **Part A** (configuration) is in, and **story 04 is complete across all six slices** — the `Customers` domain module and data layer, `CustomerService` and the **A-19** propagation, notes/timeline/attachments/demo data, all ten endpoints of api-design §5.5, `POST /auth/register`, and now the **customer directory, customer detail and registration screens**. Part B stays at Phase 7 |
| **Overall status** | 🟡 On track with **four blocked acceptance criteria**. The SDD chain is finished end to end; the stage 9 audit found two genuine contradictions (S9-1, S9-4) and scheduled two carried findings (PF-2, PF-4). **None blocks stories 01–04** |
| **Overall progress** | **49.4%** (method in §1.1) |
| **SDD pipeline** | **9 of 10 stages complete** (90% of the pipeline) |
| **Code written** | **Stories 01, 02, 03, story 16 Part A, and story 04 in full (all fourteen plan tasks).** Authentication, the four-role model, per-request identity resolution, user administration, the audit recorder, the two organization read endpoints and all three configuration tiers run. `backend/` holds the five-project solution, `frontend/` the Angular + PrimeNG app (built on PrimeNG Sakai `20.0.0`), `docker-compose.yml` runs three services and **two volumes** — story 04 slice 1 added the attachment volume. **The first business entities exist**: `Customers`, `CustomerNotes` and `Attachments` are mapped and migrated, and `User.CustomerId` carries its foreign key. The whole `Customers` module is **published and now reachable from a screen**: profiles with the **A-19 email propagation**, immutable notes, the timeline read projection and attachments on local disk, behind the **ten endpoints of api-design §5.5**, plus **`POST /auth/register`**. `openapi/v1.json` lists **18 paths** (was 11 before story 04). **Slice 6 adds the front end**: typed `customers` and `attachments` clients, the customer directory at `/workspace/customers` with its URL-borne `q` and `branchId` filters, the four-region customer detail, a shared `AttachmentList` + uploader, and the enabled registration form. Demo customers, portal logins, a note and a file are seeded at startup |
| **Last updated** | 2026-08-30 |
| **Current focus** | **Story 04 is complete — all six slices implemented and verified**, the first T1 business story to land end to end. Slice 6 (plan tasks 11–14) delivered the typed clients, the customer directory, the customer detail screen and the registration form, verified 2026-08-30 against the running stack and driven through a real browser. **Three findings are open and none blocks anything built:** **I-12** — no approved endpoint publishes the attachment size cap that ui-design §8 asks the uploader to show, so the cap line is absent by design and **needs a user decision**; **I-1** and **I-2** from earlier slices remain the user's call. **I-11 is closed** — it was a `sqlcmd` session-setting artefact, not a schema defect. **Awaiting explicit approval before story 05.** Phase order per [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §3 |
| **Next immediate step** | See §10, item 1 |

### 1.1 How the 35% is calculated

Two weighted tracks. The weighting is a **stated planning convention, not a measurement** — it is
recorded here so the number is reproducible rather than invented.

| Track | Contents | Weight | Complete | Contribution |
|---|---|---|---|---|
| **Design & planning** | Tracker rows 1–9 (§2), equally weighted at 3.89% each | 35% | **9 of 9 rows** | 9 ÷ 9 × 35 = **35.0%** |
| **Delivery** | Row 10 — the 18 stories implemented *and* verified | 65% | **4 of 18** | 4 ÷ 18 × 65 = **14.4%** |
| | | **100%** | | **= 49.4%** |

**A story counts only when it is implemented *and* verified in full.** Story 04 now does: all
**fourteen** plan tasks are implemented and every one of the six slices is verified, so the delivery
track moves from 3 to 4 and the total from 45.8% to **49.4%**. Through slices 1–5 it counted for
**nothing** — deliberately, because the row counts stories, not tasks — and this is the update where
that convention pays out rather than the one where it is bent.

**Two qualifications on that count, stated so the number can be argued with rather than trusted.**
Two of story 04's Done Criteria complete in *later* stories **by the plan's own text**, not by
omission here: the interaction timeline renders empty until story 06 fills it, and the **ticket**
half of the attachment criterion is published by story 05 (**S9-2**). Both were written that way
before implementation began. Separately, **finding I-12** leaves one line of one screen unbuilt — the
uploader cannot state the configured size cap, because no approved endpoint publishes it — and that
is a contract gap awaiting the user's decision, not undelivered work. **If the user judges that I-12
should hold story 04 open, the delivery count returns to 3 of 18 and the total to 45.8%.**

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
| 10 | Implementation / Verification | 🟡 **In Progress** | `backend/`, `frontend/` | ✅ (both populated) | Per story | **Current stage.** Stories **01, 02, 03** and **04** complete and verified; story **16 Part A** complete and verified. Stories 05–15, 17, 18 and story 16 Part B not started. *(This row said "No code exists. Begins with story 01" until 2026-08-27 — stale since story 01 landed on 2026-08-25, and contradicted by §1 and §3 of this same document. Corrected during story 04 slice 1.)* |

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

**Stories 01, 02, 03 and 04 are Implemented and Verified** — 01–03 close phase 1, and **04 is the
first T1 business story delivered end to end**. **Stories 05–18 are at `Plan Complete`.** All 18 plans exist. The `Depends on` column below is the **execution** dependency from
[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §4, which supersedes the
earlier reading in three places (S9-7, S9-8, S9-12).

| Seq | Story | Feature | Tier | SDD | Plan | Impl | Verified | Depends on | Blocker |
|---|---|---|---|---|---|---|---|---|---|
| 01 | `solution-skeleton` | platform-foundation | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-25, re-run 2026-08-26 | — | — |
| 02 | `auth-and-roles` | identity-access | **T1** | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-26 | 01, **03 data** | — |
| 03 | `departments-branches` | organization | **T1**+T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-27 | 01, 02 | — · tasks 1–3 landed early with story 02, tasks 4–8 after it, per S9-12 |
| 04 | `customer-records` | customer-management | **T1**+T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 14 tasks | ✅ **Verified** 2026-08-30 — all six slices | 01–03, **16 A** | — · **OQ-5 closed 2026-08-27 (A-19)**, and **A-19 is implemented and proven** · sliced at the user's instruction; slice map in [00-overview.md](../.squad/plans/customer-management/00-overview.md) · **two Done Criteria complete in later stories by the plan's own text** — the timeline fills in story 06, the ticket half of the attachment criterion in story 05 (S9-2) · **finding I-12 open**, needing a user decision |
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
| 16 | `audit-configuration` | administration | T2 | Intake Complete | **Plan Complete** | 🟡 **Part A done** | ✅ **Part A verified** 2026-08-27 | **A:** 01, 03 · **B:** 02, 06 | Part B at Phase 7 (needs story 06's lifecycle actions) |
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
| **S9-7** | `POST /auth/register` cannot be implemented in story 02 — A-15 needs a `Customer`, a `Branch` and a default branch | ✅ **Implemented in story 04 slice 5** (2026-08-28), with all three A-15 outcomes |
| **S9-8** | **Story 08 does not depend on story 09**, contrary to §3's earlier reading. Due dates are required at creation, so SLA ordering is available from story 05 | Queue built once, correctly. **No fallback ordering and no swap to record.** §3 and the [story-backlog.md](story-backlog.md) cut order corrected |
| **S9-11** | **[ui-design.md](ui-design.md)'s header is stale** — it cites *"65 endpoints, AP-1…AP-18"* while [api-design.md](api-design.md) has **66** and **AP-19** | 🟡 **Documentation staleness only.** Every screen maps to an endpoint that exists. **A one-line header correction, not applied — it is an edit to an approved document and is offered rather than taken** |
| **S9-12** | Story 02 depends on story 03's entities (`POST /users` needs a `departmentId`) | Phase 1 executes them as an **adjacent interleaved pair**. Both intakes already say *"planned together or in immediate sequence"* |
| **S9-13** | [api-design.md](api-design.md) §10.1 assigns `/config` and `/config/staff` to story 01, whose AC forbids endpoints beyond health — and both need authentication story 01 lacks | Story 01 delivers `/health` and the anonymous `/config/bootstrap`; the two authenticated tiers land in **story 16 Part A**, which §10.1 also lists |

**F-1 is deliberately still open.** [ui-design.md](ui-design.md) **UI-3** is approved and says the
transition menu is computed client-side; the server remains the authority and a wrong offer gets
`403`/`409`. [sdd-workflow.md](sdd-workflow.md) gate 9 → 10 requires no decision here. Story 06's
plan confines the duplicated matrix to **one file**, `shared/lifecycle/transition-matrix.ts`, so
closing F-1 later deletes exactly one file.

### 6.8 Implementation findings — story 04 slices 1–6, and the I-9 / I-10 fixes (2026-08-27 / 30)

Two gaps in the approved documents surfaced while building story 04's first slice. **Neither is
resolved by invention.** Each records what was done, why, and what the user has to decide if the
choice is wrong.

| ID | Finding | What was done, and why it is not a product decision |
|---|---|---|
| **I-1** | **The attachment storage root has no home in any approved document.** Story 04's plan task 2 requires `LocalDiskAttachmentStorage` to write "under a **configured** root", but [architecture.md](architecture.md) §6.3's configuration table does not list such a key, and story 16 Part A's plan states *"no new configuration key beyond architecture §6.3's table and the attachment cap is introduced"* — so Part A did not add one either | Added **`SupportCrm:Attachments:StorageRoot`** to the existing `AttachmentOptions`, defaulting to `App_Data/attachments` and overridden in Compose to an absolute path on a new named volume. **It is deployment plumbing, not a product rule:** it answers no open question, is validated only for presence, and is returned to no client — neither it nor `Attachment.storagePath` appears in any response (api-design §6.7). The alternative was a path constant in the storage class, which architecture §6.3's own reasoning forbids. **If architecture §6.3's table is meant to be exhaustive, this is a one-row amendment to an approved document and therefore the user's to take** |
| **I-2** | **[data-model.md](data-model.md) §6 declares no index for `CustomerNote`**, while declaring `TicketInternalNote(ticketId, createdAt)` for the analogous per-parent, newest-first read. `GET /customers/{id}/notes` is exactly that shape (api-design §5.5) | **No index was added.** §6's stated rule is *"everything below exists because a named query in a story needs it — no speculative indexing"*, and the foreign key on `CustomerNote.CustomerId` is indexed as a matter of course by §6's own opening line, so the query is already served. Adding the composite would be a physical decision this document reserves to itself. **Recorded rather than taken:** if the asymmetry is an omission, it is a §6 amendment |

Slice 4 added one, and it is **pre-existing rather than introduced**.

| ID | Finding | What was done, and why |
|---|---|---|
| **I-9** | **AP-10 was not actually enforced.** It says server-derived fields are *"never accepted in a request body… A request containing one is `400`, so a client is never misled into thinking it worked"*, with "accepting and ignoring them" as the **rejected** alternative. But `System.Text.Json` ignores unmapped members unless `UnmappedMemberHandling.Disallow` is configured, and nothing configured it — so `PATCH /customers/{id}` with `externalReference`, and `PATCH /users/{id}` with `email`, both returned `200` and silently dropped the field. **Verified against the running API on both endpoints; `/users` had behaved this way since Story 02** | ✅ **CLOSED 2026-08-28, on the user's instruction, between slices 4 and 5.** `UnmappedMemberHandling.Disallow` is now set once on the MVC JSON options in `Program.cs`, so every request model enforces AP-10 by the shape it already had — no attribute, no filter, no per-endpoint check. **The blast radius is six endpoints, not the 23 this row first estimated:** only six bind a JSON body, and the other seventeen are `GET`s, a bodyless `POST` or the multipart upload. All six are covered by the new `UnmappedRequestMemberTests` (16 tests), and both named cases are re-verified against real SQL Server as `400`. **Two smaller findings came out of the fix — I-10 and I-11 below** |

**Closing I-9 surfaced two more.** Neither is introduced by the fix; both were found by it.

| ID | Finding | What was done, and why |
|---|---|---|
| **I-10** | **A model-state `400` did not match the AP-2 / §6.12 error contract.** §6.12 fixes the envelope as `{ type, title, status, detail, instance, errors? }` with `type` a **stable slug**; AP-2 exists so the front end can localize by `type` (T2-J). The API had **two divergent `400` shapes**: `ValidationException` → `ProblemDetailsExceptionHandler` emitted the contract shape, while `[ApiController]`'s automatic model-state response emitted `type = https://tools.ietf.org/html/rfc9110#section-15.5.1`, no `detail`, no `instance`, and `errors` whose messages named .NET internals (`SupportCrm.…PatchUserRequest`, ``System.Nullable`1[System.Guid]``, byte offsets). Its `errors` keys were inconsistent too — camelCase field names for DataAnnotations and query binding, JSON paths (`$.email`) plus a spurious `"request"` entry for anything the JSON reader rejected. **User-visible:** Transloco's default missing-key handler returns the key, so a failed `POST /users` rendered the literal `errors.https://tools.ietf.org/html/rfc9110#section-15.5.1` in the dialog | ✅ **CLOSED 2026-08-28, on the user's instruction, after a focused investigation.** `Errors/ModelStateProblemDetails.cs` registers two things once: `JsonOptions.AllowInputFormatterExceptionMessages = false` (the framework's own purpose-built leak control) and an `InvalidModelStateResponseFactory` that emits the §6.12 envelope with `validation-failed`, normalizes `$.email` → `email`, and drops the action's `request` parameter key. **Nothing was invented:** `error.interceptor.spec.ts` already pinned that exact shape and both i18n dictionaries already shipped an `errors.validation-failed` string — the server was the only part not complying, and **the front end needed no change**. **Seven producers** are covered by `ModelStateProblemDetailsTests` (27 tests), including a raw-body scan for `SupportCrm.`, `System.`, generic arity, parser internals and the RFC URL. **Pre-existing, not caused by I-9** — every model-state `400` had this shape since Story 02 |
| **I-11** | **No row can be deleted from `Customers` on real SQL Server.** `DELETE FROM Customers` fails at plan compilation with `Msg 8624, Internal Query Processor Error: The query processor could not produce a query plan` — by primary key, with no child rows, and with `OPTION (RECOMPILE)`. Isolated by comparison: `Branches`, `Departments` and `AuditEntries` all compile a delete plan fine, and `Customers` is the **only** parent whose referencing index is a **filtered unique index** (`IX_Users_CustomerId`, `WHERE CustomerId IS NOT NULL`) | ✅ **CLOSED 2026-08-30 during slice 6 verification — the diagnosis was wrong, and the schema is fine.** The cause is **`SET QUOTED_IDENTIFIER OFF`**, which is `sqlcmd`'s default and which SQL Server refuses to combine with a **filtered index**; the `Msg 8624` was that refusal surfacing as a plan-compilation failure. Proven by running the **same `DELETE` on the same row** with only the session setting varied, both inside a rolled-back transaction: `QUOTED_IDENTIFIER OFF` → `Msg 8624`; `QUOTED_IDENTIFIER ON` → **`deleted: 1`**. On a simpler statement SQL Server names it outright — `Msg 1934 … 'QUOTED_IDENTIFIER' … filtered indexes`. **No index change is needed and none was made**, so the *"requires a decision"* this row carried is withdrawn. **The application was never affected**: `Microsoft.Data.SqlClient` sets `QUOTED_IDENTIFIER ON` by default, which is also why the suite never saw this. **What remains true:** a customer with a linked login still cannot be deleted, but for an ordinary and correct reason — its `User` is referenced by an append-only `AuditEntry` (`FK_AuditEntries_Users_ActorUserId`), and audit rows are not deleted (AD-10). The leftover dev rows below are therefore permanent by design, not by defect |

**None of these blocks a later slice**, and each is visible in the code at the point it matters:
I-1 in `AttachmentOptions.StorageRoot`'s doc comment, I-2 in `CustomerNoteConfiguration`'s, I-3 on
`User.ChangeEmail`, I-5 on `User.CreateCustomerUser`, I-6 on `AttachmentUpload`, I-7 in
`SupportCrmDbContext.ApplySqliteDateTimeOffsetWorkaround`, I-8 in the Dockerfile, **I-9 on the
`UnmappedMemberHandling` line in `Program.cs`** — whose comment is the longest in that file
precisely because a one-line setting is where a contract rule is easiest to delete by accident — and
from slice 6, **I-12 on `AttachmentListComponent.maxSizeBytes`** and **I-13 on
`AttachmentsClient.download`**.

**Three remain the user's call** — I-1 (a configuration key architecture §6.3's table does not
list), I-2 (an index data-model §6 declares for `TicketInternalNote` but not for `CustomerNote`), and
**I-12** (the attachment cap that ui-design §8 asks the uploader to show, which no §6.9 payload
publishes). **I-9, I-10 and I-11 are all closed:** AP-10 is enforced, the `400` that enforces it
carries the AP-2 slug and the §6.12 envelope, and I-11 turned out to be a `sqlcmd` session setting
rather than a schema defect — proven on 2026-08-30 by varying only `QUOTED_IDENTIFIER` on the same
statement. The rest are implementation consequences with no product content.

Slice 2 added two more. Both are **implementation consequences with no product content**, recorded
so they are not mistaken for silent decisions.

| ID | Finding | What was done, and why |
|---|---|---|
| **I-3** | **A-19 needs a `User.email` mutator that Story 02 deliberately did not provide.** Story 02 left `User` without one because docs/api-design.md §5.3 makes email unpatchable through `PATCH /users/{id}`, and the plan's A-19 box requires `CustomerService.UpdateAsync` to *"set both `Customer.Email` and `User.Email`"* | Added **`User.ChangeEmail`**. The restriction §5.3 states lives in the **request model** — where AP-10 puts every such restriction — not in the absence of a domain mutator, and the plan says so itself: *"`PatchUserRequest` gains no `email` property"*. It does not, and a test asserts it. The mutator's doc comment names its one legitimate caller, and states that uniqueness is a cross-row rule it cannot check |
| **I-4** | **SQLite cannot `ORDER BY` a `DateTimeOffset`**, so the `createdAt` sort that docs/api-design.md §5.5 names cannot execute on the hermetic test host. SQL Server can | **The implementation was not changed to suit the test provider.** The whitelist test asserts what AP-15 is actually about — that `createdAt` is *accepted* and an unlisted field is a `400`. **Superseded by I-7**, which found the same limitation blocking a *mandated* ordering and fixed it at the provider boundary |

Slice 3 added four more.

| ID | Finding | What was done, and why |
|---|---|---|
| **I-5** | **Task 9 could not be built without a piece of task 7.** Task 9 requires *"at least two \[customers] with a linked portal `User`"*, but `User.CreateStaff` refuses the `Customer` role (DM-1) and the plan assigns **`User.CreateCustomerUser`** to **task 7**, which slice 3 excluded. A secondary ambiguity came with it: the plan says *"`branchId` is **always** the configured default (A-15, DM-1)"*, but A-15 and api-design §7 both govern **`Customer.branchId`**, while data-model §2.1 calls **`User.branchId`** a *"staff location"* | **The factory moved into slice 3; registration did not**, and its shape was fully determined by DM-1 so nothing was invented. ✅ **The branch ambiguity is resolved in slice 5: `User.branchId` stays null.** Four approved documents agree and none says otherwise — A-15 (*"`Customer.branchId` is required (A-2). A self-registering customer is assigned the system default branch"*), data-model §2.4 (the default is `Customer.branchId`'s), data-model §2.1 (`User.branchId` is a *"staff location. Reporting attribute only"*) and architecture §6.3 (the configured value is *"assigned to self-registering **customers**"*). `RegisterAsync` therefore leaves the factory's optional `branchId` at its default, exactly as the seeder already did, and `RegistrationTests.The_login_gets_no_branch_of_its_own` asserts both halves together — proven live too: all three registered logins have `BranchId = NULL` while their profiles carry a branch |
| **I-6** | **`IFormFile` cannot cross into the Application layer.** The plan sketches `UploadForCustomerAsync(customerId, IFormFile)`, but `IFormFile` is `Microsoft.AspNetCore.Http` and `SupportCrm.Application` carries **no ASP.NET Core reference** — docs/architecture.md §2.1 keeps that layer free of HTTP concerns (AD-2) | Introduced an Application-owned `AttachmentUpload(Stream, FileName, ContentType, SizeBytes)`. **This follows the plan rather than departing from it:** the plan's own `IAttachmentStorage`, built in slice 1, already takes a `Stream` for exactly this reason, so the `IFormFile` in task 6 is a signature sketch the plan contradicts elsewhere. The controller maps one to the other in a single line in slice 4 |
| **I-7** | **SQLite cannot `ORDER BY` a `DateTimeOffset` at all** — and unlike I-4, this blocks a **mandated** ordering, not an optional sort. The notes list and the attachment list are *newest first* by contract (api-design §5.5), and Story 06's timeline will be too, so on the test host those reads could not execute and would have **no automated coverage whatsoever** | Under SQLite **and only SQLite**, every `DateTimeOffset` is stored as UTC ticks, applied in `OnModelCreating` behind a provider check — **the mirror of the `IsSqlServer()` collation guard that already exists there**, and justified by the same sentence: *"the guard is about the provider, not about the rule"*. It is lossless here because every timestamp comes from `TimeProvider.GetUtcNow()` and serializes as UTC. **Production is provably untouched:** `dotnet ef migrations has-pending-model-changes` reports no model change, and the columns are still `datetimeoffset` on the running SQL Server |
| **I-8** | **A real deployment defect, found only by running the stack.** The image runs as a non-root user (`USER $APP_UID`) but the `supportcrm-attachments` volume was created root-owned, so the first startup that actually wrote a file — slice 3's seeder — crashed the host with `Access to the path '/var/lib/supportcrm/attachments/2026' is denied`. Slice 1 added the volume; nothing had written to it until now | The Dockerfile now creates and `chown`s the directory **before** the `USER` switch, so Docker initializes the named volume with the runtime user's ownership. Verified: the volume root is now owned by the app UID and the seeded file is present. **Caveat worth knowing:** this self-heals only because the volume was still empty — Docker seeds ownership from the image only into an *empty* volume, so an existing populated volume would need `docker volume rm` |

Slice 6 added two, both about the **front end's contract with the server**. Neither is resolved by
invention, and neither blocks anything built.

| ID | Finding | What was done, and why |
|---|---|---|
| **I-12** | **No approved endpoint publishes the attachment size cap.** [ui-design.md](ui-design.md) §8 requires the uploader to show *"size cap from configuration"* and story 04 task 13 repeats it — but the cap is in **none** of the three configuration payloads of [api-design.md](api-design.md) §6.9, each of which enumerates its members exhaustively (`BootstrapConfig`, `CustomerConfig`, `StaffConfig`). The gap is visible in the code itself: `AttachmentOptions.MaxSizeBytes`'s own doc comment asserts the cap is *"published to clients"*, and no endpoint does so | **The screen states no cap, and says why at the point it would.** `AttachmentListComponent.maxSizeBytes` is an optional input defaulting to `null`; while it is null the cap line is simply absent, and **`413 attachment-too-large` still surfaces inline on the uploader as a translated sentence**, so the story's Done Criterion — *"an oversized upload is rejected with a clear message"* — is met without it. Publishing the cap would mean **adding a member to `StaffConfig`**, which is new contract surface in an approved document and therefore **the user's to take, not a screen's**. The input's doc comment names the one-line fix, so whoever publishes the cap changes nothing else. **Needs a user decision** |
| **I-13** | **The plan's stated download mechanism does not hold for this codebase's token transport.** Task 11 specifies *"`downloadUrl(attachmentId)` returning the API path (**the auth interceptor supplies the bearer token**)"*. It does not: `authInterceptor` sets `Authorization` on an `HttpRequest`, and the token is read from `localStorage` (AD-7), so a browser navigation to that bare path — an `<a href>`, a `window.open` — sends no credential and is answered `401` | **The plan's named surface exists exactly as written, and the sentence was made true rather than the surface changed.** `AttachmentsClient.downloadUrl(attachmentId)` is there and is the **one** place that path is built — from the attachment **id**, never a storage path (AP-19, api-design §6.7). Alongside it, `download()` fetches the bytes through `ApiClientBase.getBlob`, so the request goes through `HttpClient` and the interceptor genuinely does supply the token. A spec pins `responseType: 'blob'` for this reason. **Informational** — the plan's rationale was wrong about a mechanism, not about the contract, and nothing product-facing turns on it |

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

**Stories 01, 02, 03 and 04 are implemented and verified, story 16 Part A with them.** Story 04 is
complete across all fourteen plan tasks. Every claim below was re-verified by running the commands in
§8 on 2026-08-30, not inferred from a plan.

| Area | Status | Evidence |
|---|---|---|
| Backend | ✅ Stories 01, 02, 03, 16 A, **04 in full**, + the I-9 and I-10 fixes | `dotnet build`: **0 warnings, 0 errors** with `TreatWarningsAsErrors`. **AP-10 is enforced globally** since 2026-08-28 — `UnmappedMemberHandling.Disallow` on the MVC JSON options, so an unmapped request member is a `400` on all six body-binding endpoints (finding I-9, closed) — and **AP-2 now holds for every error path**: `ModelStateProblemDetails` gives the model-state `400` the same `validation-failed` slug and §6.12 envelope the exception path already had, with no .NET internals in the payload (finding I-10, closed). `SupportCrm.Domain` still has **0 project and 0 package references** and no EF attribute (AD-2, AD-4) |
| Frontend | ✅ Stories 01, 02, 03 **and 04** + error-handling layer | Angular 20.1.2 + PrimeNG 20.0.0 on Sakai `20.0.0`. `npm run build` succeeds; `npm run lint:styles` clean. Sign-in, guards, role redirect, staff shell avatar menu and the admin user screens all exercised in a real browser. A cross-cutting HTTP error-handling layer landed 2026-08-28: `errorInterceptor` applies the cross-cutting half of ui-design §9's status table — `401` ends the session, `403` routes to `/403` without ending it, `5xx` and network failures raise one translated toast, and `400`/`409`/`413`/`422`/`404`/`503` pass through untouched for the feature to render inline. **Story 04 slice 6 adds the first business screens**: typed `CustomersClient` and `AttachmentsClient` (no component calls `HttpClient` — architecture §2.2), the **customer directory** at `/workspace/customers` with `q`, `branchId` and `page` in the URL (**UI-9**), the **four-region customer detail** each with its own loading, empty and error state (§9), a shared **`AttachmentList` + uploader** in `shared/` for the ticket and portal surfaces to reuse, and the **registration form** Story 02 scaffolded, now enabled. All driven through a real browser against the running `web` container — 18 UI checks and 3 phone-width checks, §8 |
| Database | ✅ Schema live | **`InitialSchema` and `Customers` both applied to real SQL Server**: `Branches`, `Departments`, `Users`, `AuditEntries`, **`Customers`, `CustomerNotes`, `Attachments`**. `Users.Email` **and `Customers.Email`** both carry `SQL_Latin1_General_CP1_CI_AS` at `nvarchar(256)` — the same address, the same width, the same collation, as §6.1 requires. `Users(CustomerId)` is a filtered unique index and **now has its foreign key to `Customers`**, closing the DM-1 link story 02 left open. `CK_Attachments_OwnerXor` is present and **proven to refuse both a no-owner and a both-owners row** (§5 constraint 20) |
| Configuration | ✅ Validated at startup | Seven option types bound and `ValidateOnStart`. Six checks: every category maps to an existing department (A-14), every priority has an SLA target with positive hours (A-3), `DefaultBranchId` is an existing branch (A-15), `Priorities` equals the A-6 levels in order, `Min < Max` on the rating scale (structural only — **OQ-1 stays open**), and a positive attachment cap. **All six proven to stop the host** by starting the real API with each value broken in turn |
| Seed data | ✅ Running at startup | 2 departments (both with a manager), 2 branches, 4 staff users — Administrator, Manager and **two Agents in different departments**, so Story 05's scoping tests have material. **Story 04 slice 3 adds `CustomerSeeder` (`Order = 30`)**: 4 customers spread across **both** branches (which is what makes Story 05's "branch is not a boundary" test meaningful), **2 with a portal login and 2 deliberately without** — both DM-1 shapes, so A-19 is demonstrable by hand — plus one note and one attachment. A seeded portal login signs in successfully. Password from configuration; no credential in source |
| Tests | ✅ **220 passing**, 1 skipped by design, plus **23 front-end specs** | Backend: `AuthorizationTests`, `UserAdminValidationTests`, `AuditRecordingTests`, `PlatformEndpointTests`, `OrganizationEndpointTests`, and story 16 Part A's `ConfigurationTierTests` (8), `ConfigurationValidationTests` (10) and `NoConfigurationEntityTests` (18). Includes the **AD-15 deactivation regression** and the indistinguishability of a wrong password from a deactivated account. `BranchIsNotABoundaryTests` is **skipped until story 05** creates `Ticket`. Story 04 slice 1 adds `CustomerDataLayerTests` (14) and `AttachmentStorageTests` (8); slice 2 adds `CustomerServiceTests` (24), which walks the **whole A-19 case table**; slice 3 adds `CustomerNotesAndTimelineTests` (9), `AttachmentServiceTests` (11) and `CustomerSeederTests` (6); slice 4 adds `CustomerAccessTests` (15), the **first endpoint suite** for this story — **87 tests** in all. The cross-cutting **I-9 fix adds `UnmappedRequestMemberTests` (16)**, which covers **all six** JSON-body endpoints for AP-10 and pins the three behaviours the fix deliberately left alone (query-string binding, case-insensitive matching, empty `PATCH` bodies). The **I-10 fix adds `ModelStateProblemDetailsTests` (27)**, which walks **seven** distinct producers of a model-state `400` — data annotations, four JSON-reader failure modes, query binding, and the `ValidationException` path they must agree with — asserting the whole §6.12 envelope, camelCase keys, and a **raw-body scan** proving no `SupportCrm.`, `System.`, generic arity, parser internal or RFC URL survives. Slice 5 adds `RegistrationTests` (22), organized around A-15's three outcomes and asserting each at the **row** level — a `201` that quietly wrote a second customer would pass a status check and break A-10. Slice 6 adds **no backend test** — it changed no backend file, and the suite's staying at exactly 220 is itself the evidence for that. Front end: `department-filter.component.spec.ts` (4), the cross-cutting `error.interceptor.spec.ts` (12), and slice 6's `customers.client.spec.ts` (**7**, new) — **23 specs**, on the karma target story 01 already configured. The slice-6 specs are deliberately about **the wire**, which is where a typed client can be wrong in a way no screen reveals until it reaches a server: the list filters carry the API's own names, a null filter is **omitted** rather than sent empty, a `PATCH` body carries only what it was given, the multipart part is named `file`, the download URL is built from the **id**, and the download goes through `HttpClient` so the bearer token is actually sent (**I-13**) |
| Docker / infrastructure | ✅ Complete for the stack | `docker compose up --build` brings **db, api, web** up; the API waits for the database's health check. Story 04 slice 1 adds a **second named volume**, `supportcrm-attachments`, mounted at `/var/lib/supportcrm/attachments` — attachment bytes cannot live in the image (T2-A). **Slice 3 fixed a real defect in that mount** (finding **I-8**): the image runs as a non-root user, the volume was created root-owned, and the first startup that actually wrote a file failed with `Access to the path … is denied`. The directory is now created and `chown`ed in the Dockerfile **before** the `USER` switch, so Docker initializes the volume with the right ownership |

**Endpoints that exist (13):** `/health`, `/config/bootstrap`, **`/config`**, **`/config/staff`**,
`/auth/login`, `/auth/me`, the five `/users` routes, and `GET /departments` and `GET /branches`. `POST /auth/register` is **deferred to
Story 04** by S9-7. **Neither organization route has a write verb**, and neither ever will: T2-I
makes departments and branches seeded configuration.

**Endpoints that exist (24):** the thirteen from stories 01–03 and 16 Part A, plus story 04 slice
4's **ten** — `GET`/`POST /customers`, `GET`/`PATCH /customers/{id}`, `GET /customers/{id}/timeline`,
`GET`/`POST /customers/{id}/notes`, `GET`/`POST /customers/{id}/attachments`, and
`GET /attachments/{attachmentId}/content` — plus slice 5's **`POST /auth/register`** (anonymous).
`openapi/v1.json` publishes **18 paths** (was 17, and 11 before story 04).
**`POST /auth/register` closes S9-7**, which deferred it out of Story 02 because A-15 needs a
`Customer`, a `Branch` and a configured default branch, and none of the three existed then.

**Slice 6 added no endpoint, and the path count proves it** — still **18**, re-measured on
2026-08-30 against the running API. A front-end slice must add none, and this one added none.

**Next:** **story 05** (`ticket-core`) — **blocked on explicit approval**, and carrying **OQ-2**,
which must be answered before it starts. Story 16 **Part B** stays at Phase 7: it reads audit rows
that stories 05 and 06 have yet to write.

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
| **Story 16 Part A — configuration** | ✅ Passed 2026-08-27 | All four Part A verification steps. Build clean; the three suites **36 passing**. **Step 3, fail-fast by hand, run for all six checks against real SQL Server** — each starts the real API with one value broken and the host stops with a non-zero exit and a message naming the value: a dangling category department names `billing` and cites A-14; a dangling `DefaultBranchId` cites A-15; a fifth priority level, an inverted rating scale, a zero attachment cap and a priority with no SLA target all name their section. **Step 4, tier check with a real Customer token:** `/config` returns exactly `categories` and `feedback`, `departmentId` appears **nowhere** in the body, and `/config/staff` is `403`. `openapi/v1.json` now publishes 11 paths, `/config` and `/config/staff` among them, each with `get` and nothing else |
| **Story 04 slice 6 — the front end** | ✅ Passed 2026-08-30 | Plan tasks 11, 12, 13 and 14, and **story 04's last slice**. All four canonical commands run in the verification task: `dotnet build` **0 warnings / 0 errors**; `dotnet test` **220 passed, 0 failed, 1 skipped — delta 0**, which is the expected and required result for a slice that changed no backend file; `npm run build` clean and `npm run lint:styles` exit 0; `npx ng test --watch=false --browsers=ChromeHeadless` **23 passed** (was 16, **+7**). **The plan's own step 3** — `--filter "FullyQualifiedName~Customers|FullyQualifiedName~Registration"` — **109 passed, 0 failed**. **Step 4, the round-trip against the running stack:** `POST /customers/{id}/attachments` → `201`, then `GET /attachments/{id}/content` → `200` with `Content-Type: text/plain`, `Content-Disposition: attachment; filename=slice6.txt` and `cmp` reporting the bytes **identical**. **Step 6 regression:** `/auth/me` `200`, `/departments` `200`, `/users` `200` as Administrator and `403` as Agent (correct — Administrator-only). **Step 7, the front end, driven through the Chrome DevTools Protocol against the running `web` container — 18 of 18 checks:** the directory renders the five §5.4 columns and 9 seeded rows; `?branchId=…` returns **5 rows all "Head Office"** and `?q=amina` returns **1**, both read from the URL (**UI-9**); the detail screen shows **four regions**, an **empty timeline as an empty state with 0 error states**, the **A-19 helper line present and unconditional**, and **0 edit or delete controls** on notes; a profile was saved, a note added (1 → 2) and a file downloaded, each through the screen's own controls. **Registration:** exactly four fields, **0 selects** (A-15 fixes role and branch server-side), a real `201` that signed the new customer in and landed them on `/portal`, and a `409` rendering *"An account already exists for that email. Sign in instead."* **Guards:** a `Customer` deep-linking to `/workspace/customers` and an `Agent` deep-linking to `/admin/users` both land on `/403`; signed out, the same deep link goes to `/auth/login?returnUrl=%2Fworkspace%2Fcustomers`. **Slice boundary:** `openapi/v1.json` still lists **18 paths** — a front-end slice must add no endpoint, and none was added |
| **Story 04 slice 6 — the guard is load-bearing** | ✅ Passed 2026-08-30 | The §4.1 technique, applied to this slice's own guard. Reverted the single line `form.append('file', …)` to `'attachment'` — the mistake a reader would actually make, and one the server would answer with a `400`. **1 of 23 specs went red**, and the 22 that stayed green are exactly the nothing-legitimate-broke checks. **Restored from a byte-copy and the restore confirmed three ways**: `grep` shows `form.append('file', file, file.name)`, `prettier --check` reports the file clean, and the suite is **23/23 green** again. `git status` afterwards showed only the slice's intended changes, with no trace of the revert |
| **Story 04 slice 6 — no path leaks to a screen** | ✅ Passed 2026-08-30 | The §4.3 technique, done against **the values actually stored in the database** rather than only the property names. Read from SQL Server: `2026/08/d0cf8e69…txt` and `2026/08/a3668fec…txt`. Swept all four customer-detail region responses — profile, `/timeline`, `/notes`, `/attachments` — for `storagePath` and `passwordHash` (**0 hits each**) **and** for both stored GUID fragments and the `2026/08` prefix (**0 occurrences**), over the raw response text. The enforcement is structural: `AttachmentMetadata` has nowhere to put a path, on the server or in the client type |
| **Story 04 slice 6 — RTL** | 🟡 **Partial — 3 of 4; the fourth is story 17's** | Arabic sets `dir="rtl"` and `lang="ar"`, and the UI **translates** — *"Interaction timeline"* → *"سجل التفاعلات"*. **This slice's own CSS mirrors, measured rather than assumed**: `.app-attachments__uploader`'s child moves from `startGap=0 / endGap=744` in LTR to `startGap=744 / endGap=0` in RTL, which a physical `left` could not have produced. **The staff shell sidebar does not mirror** — its left edge stays at 28 px in both directions. That is `layout/`, not this slice, and **story 17 Part B task 3 owns it by name**: *"The layout mirrors, not merely the text alignment: **sidebar**, drawers, table column order"*. Recorded here so story 17 inherits a measurement rather than a suspicion |
| **Story 04 slice 6 — phone width** | ✅ Passed 2026-08-30 | At **390 px**, driven through CDP: the customer directory, the customer detail and the registration screen each report `scrollWidth 390 = clientWidth 390` — **no horizontal body scroll** (T3-F, ui-design §10.3). Wide content scrolls inside its own `.app-scroll-x` container instead of the page body |
| **Story 04 slice 6 — demo data restored** | ✅ Passed 2026-08-30 | Everything the verification mutated was put back **and re-read**, not merely reverted: the seeded phone patched back to `+20 100 555 0101` and re-read from the API; the verification note and attachment removed and the counts re-read as `notes=1 attachments=1`, the seeded values; the orphaned blob removed from the attachments volume. **One row could not be removed and is disclosed rather than hidden:** the customer `slice6.verify.1788107299323@example.com`, created by driving the real registration form for step 5. Deleting it would require deleting its `UserCreated` audit entry, which is append-only by design (AD-10) — so the customer count is **10, was 9**, in the same way slice 5's verification left three rows |
| **Finding I-11 — re-tested and closed** | ✅ Passed 2026-08-30 | Not a slice check; a correction found while restoring demo data. The **same `DELETE` on the same row**, run twice inside a rolled-back transaction with only the session setting varied: `SET QUOTED_IDENTIFIER OFF` → `Msg 8624, Internal Query Processor Error`; `SET QUOTED_IDENTIFIER ON` → **`deleted: 1`**. `customers=10` unchanged afterwards, so the rollback held. On a simpler statement SQL Server names the cause outright — `Msg 1934 … 'QUOTED_IDENTIFIER' … filtered indexes`. **I-11 was a `sqlcmd` session-setting artefact, not a schema defect**; no index change is needed, and the application never issued the failing form because `Microsoft.Data.SqlClient` sets `QUOTED_IDENTIFIER ON` by default |
| **Story 04 slice 5 — registration** | ✅ Passed 2026-08-28 | Plan task 7 and its route. Build clean; **22 slice-5 tests pass and the whole suite is 220/0/1.** **A-15's three outcomes are the suite's structure**, each asserted at the ROW level rather than by status code: a brand-new email creates both rows with the customer in the configured default branch; an **agent-created profile is linked, not duplicated** — one customer row, the agent's id, and the agent's chosen branch **not** overwritten by the default; a second registration is `409 user-already-exists` (PF-6) with the row counts unchanged either side. **A staff address is also `409`** and creates no customer, so a portal profile cannot be attached to a staff login. **The configured branch is proven to come from configuration**, on a host of its own whose `DefaultBranchId` points at a branch the test creates — a hardcoded id would land the customer elsewhere. **Finding I-5 resolved and pinned:** `User.branchId` is null while `Customer.branchId` carries the default, asserted together in one test. **The `201` body is an `AuthToken` whose token works** — it resolves through `GET /auth/me` — and the `Location` is `/api/v1/auth/me`, followed successfully **with the token the response just issued**, so it is not merely well-formed but reachable by its recipient. **AP-10 holds on the new endpoint:** `branchId`, `role`, `customerId` and `isActive` in the body are each `400 validation-failed` with the field named, and nothing is written. **A-4 holds:** the new `Customer`-role token is `403` on `/customers` and `/users`, `200` on `/auth/me`. **One `UserCreated` audit entry per registration**, self-attributed with a null descriptor — and the customer profile beside it is deliberately unaudited (AD-10); a refused registration writes nothing. **Against real SQL Server** (`docker compose up -d --build api`): all three outcomes reproduced, and **the case-insensitive path SQLite cannot test** — registering `SLICE5.CASE@EXAMPLE.COM` linked to the `slice5.case@example.com` profile (A-10, the §6.1 collation), while a differently-cased duplicate is `409`. Database confirms all three logins as `Role=Customer, DepartmentId=NULL, BranchId=NULL` with one customer row per address. **Regression:** all fourteen pre-existing routes answer as before, `POST /departments` still `405` (T2-I); `openapi/v1.json` now lists **18 paths** and `RegisterRequest` publishes exactly `email`, `password`, `fullName`, `phone`. **Front end untouched** — the registration screen is task 14, slice 6: build clean, 16/16 specs, `lint:styles` clean |
| **Cross-cutting — AP-2 for model-state errors (finding I-10)** | ✅ Passed 2026-08-28 | Not a story task; requested after I-9 and before slice 5, following a focused investigation the user approved. Two registrations in `Errors/ModelStateProblemDetails.cs`. Build clean; **27 new tests pass and the whole suite is 198/0/1.** **Verified live against real SQL Server across all seven producers** — data annotations, unmapped member, type mismatch, malformed JSON, empty body, root garbage, query binding — each now returning `type: validation-failed`, `title: Invalid request`, a `detail`, an `instance` of `METHOD /path`, and `errors` keyed by **camelCase field name**. **The leak is gone:** `$.email` → `email`, the `request` parameter key is dropped, and `SupportCrm.Application.Modules.Identity.PatchUserRequest`, ``System.Nullable`1[System.Guid]``, `LineNumber` and `BytePositionInLine` no longer appear — asserted over the **raw response text**, not a parsed field, so a leak anywhere in the envelope fails the test. **The two paths agree:** a `ValidationException` `400` (AP-15's sort whitelist) and a model-state `400` now carry the same slug, title and status, which one test asserts by comparing them directly. **The slug is proven renderable:** a test reads **both shipped i18n dictionaries** and fails if the server ever emits a `type` they have no string for. **The guard is load-bearing:** removing the one registration turns **36 of the 43** tests in the two error suites red. **Regression:** every success path unchanged (login, both patches, empty patch, note, multipart upload); **every other slug untouched** — `customer-email-in-use`, `not-found`, `invalid-credentials` and the `403` role denial all verified live; `openapi/v1.json` still **17 paths**. **Front end deliberately not touched** — `error.interceptor.spec.ts` already encoded this shape, so its passing unchanged *is* the proof the server conformed rather than the client being bent to fit; `npm run build` clean, **16/16 specs**. Demo data restored: the verification note and attachment removed, seeded values re-read |
| **Cross-cutting — AP-10 enforced (finding I-9)** | ✅ Passed 2026-08-28 | Not a story task; requested between slices 4 and 5. One setting in `Program.cs`: `UnmappedMemberHandling.Disallow` on the MVC JSON options. Build clean; **16 new tests pass and the whole suite is 171/0/1.** **The defect was reproduced live before the fix** — on the running pre-fix image `PATCH /customers/{id}` with `externalReference` and `PATCH /users/{id}` with `email` both returned `200`. **After `docker compose up -d --build api` both return `400`**, each naming the refused member at its JSON path (`$.externalReference`, `$.email`) in an `application/problem+json` body. **The guard is proven to be load-bearing:** reverting the one line turns **12 of the 16** new tests red, and the 4 that stay green are exactly the nothing-legitimate-broke checks that must pass either way. **All six body-binding endpoints covered** — `POST /auth/login` (anonymous, so the rule is proven not to be authorization-gated), `POST /customers`, `PATCH /customers/{id}`, `POST /customers/{id}/notes`, `POST /users`, `PATCH /users/{id}` — and the refusal is proven **whole**: a legitimate `fullName` or `displayName` sent beside the illegal member is **not** applied, and no row is written. **Three non-changes pinned by test:** query-string binding (an unknown query key is still ignored; AP-15's sort whitelist still `400`s from its own path), case-insensitive property matching (a `PascalCase` body still binds), and empty `PATCH` bodies (still `200`). **Regression against real SQL Server:** login, both patches, an empty patch, a note, two creates, a `PascalCase` create, a multipart upload and both AP-15 query cases all as before; `openapi/v1.json` still lists **17 paths** and the six request schemas publish exactly their mapped fields. Seeded rows touched by the sweep were patched back and re-read. **Front end:** unchanged — its request interfaces already mirror the server models; `npm run build` clean, **16/16 specs pass**. **Two findings recorded, neither introduced by the fix:** I-10 (a model-state `400` has no stable AP-2 `type` slug — pre-existing since Story 02, **needs a decision**) and I-11 (`Customers` rows cannot be deleted on SQL Server, `Msg 8624`, because the referencing index is filtered — so two verification rows remain in the dev database) |
| **Story 04 slice 4 — controllers** | ✅ Passed 2026-08-28 | Plan tasks 8 and 10. Build clean; **15 slice-4 tests pass and the whole suite is 155/0/1.** The suite is the first for this story that speaks HTTP, and it covers plan task 10 point by point: the directory is `403` to a Customer, `401` anonymous and `200` to Agent, Manager and Administrator (the A-4 hierarchy), and **every** customer route carries the same gate, not just the list; a duplicate email is `409 customer-email-in-use` on create and on patch, and a collision with a **staff** user is `409 user-already-exists` with **neither row written**; the timeline is a `200` empty page; an oversized upload is `413 attachment-too-large` and **leaves no metadata row**; and **AP-4 is proven by comparison** — a real attachment id the caller cannot reach and a fictional one return the *same status and the same body*. `storagePath` and `passwordHash` are swept for across five routes, searching for the value actually stored in the database as well as the property name. **Against real SQL Server** (`docker compose up --build api`): all ten routes answer, the role matrix holds with a genuine seeded portal token, the seeded attachment downloads with `Content-Type: text/plain` and its **original** file name in `Content-Disposition`, and **A-19 was exercised end to end** — patching Amina's email through `PATCH /customers/{id}` moved her sign-in with it (old address `401`, new address `200`), wrote **exactly one** `UserEmailChanged` entry with the calling agent as actor and the linked user as target, and recorded **no address** in it; the seeded value was then patched back and re-verified. **Regression:** all seven pre-existing endpoints `200`, `POST /departments` still `405` (T2-I), `POST /auth/register` still `404`, front end untouched |
| **Story 04 slice 3 — notes, timeline, attachments, seed** | ✅ Passed 2026-08-28 | Plan tasks 4, 5, 6 and 9. Build clean; **26 slice-3 tests pass and the whole suite is 140/0/1.** **Notes:** author and timestamp server-set and re-read from the row, newest first, and the service's public surface asserted to be exactly `AddAsync` + `ListAsync` — the plan's *"no update and no delete method, not merely no endpoint"* made into a test that a future `EditAsync` breaks. **Timeline:** an empty page (not an error) for a customer with no tickets — the intake's acceptance criterion, met now; `404` for an unknown customer; and notes present but **absent from the projection**, so a later "enrichment" that joined them would fail. **Attachments:** a real byte round-trip through `LocalDiskAttachmentStorage`; the original filename survives while the on-disk name does not; the cap rejects at `cap+1` with `attachment-too-large` and **writes no row**, and accepts at exactly `cap`; an empty file is `400`, not `500`; **AP-4 proven by comparison** — a `Customer`-role caller and a genuinely missing id produce the *same* message and slug; all three staff roles reach a customer-owned file (A-4 hierarchy); the ticket half **fails closed** until Story 05; and `storagePath` appears in neither the DTO's properties nor a serialized payload. **Seeder:** the three seeders run in real `Order`; 4 customers across **both** seeded branches, 2 portal logins in DM-1 shape and 2 profiles deliberately without one, a note and an attachment; the seeded login's email equals its customer's (A-19's invariant in the demo data); running twice changes nothing; and the seeded file reads back through the same storage the endpoint will use. **Against real SQL Server** (`docker compose up --build api`): the seeder ran at startup and produced exactly that data, a **seeded portal login signs in (`200`)**, newest-first `ORDER BY` works natively, and the timestamp columns are still `datetimeoffset` — `dotnet ef migrations has-pending-model-changes` reports **no model change**, so the SQLite-only guard of finding **I-7** genuinely does not touch production. **Regression:** all seven existing endpoints `200`. **Slice boundary checked:** all six task-8/task-7 routes are `404` and `openapi/v1.json` still lists 11 paths |
| **Story 04 slice 2 — `CustomerService` and A-19** | ✅ Passed 2026-08-27 | Plan task 3 only. Build clean; **24 slice-2 tests pass and the whole suite is 114/0/1.** **The A-19 case table is walked case by case**, each its own test: email absent; the same address in a different case (a no-op, and explicitly *not* a `409` against the customer's own record); a collision with another **customer** (`customer-email-in-use`); a collision with a **staff user** (`user-already-exists`, PF-6's slug); the propagation itself; and a profile-only customer. **The two rejection cases re-read both rows afterwards** and assert neither was written — not merely that an exception was thrown. **The propagation is proven in both directions:** exactly one `UserEmailChanged` entry with `actorUserId` = the calling agent and `targetId` = the **linked user**, and a counted **zero** new entries for each of the four non-propagating cases. **One commit** is proven at runtime by counting `DbContext.SavedChanges` during the successful patch — it fires once. A separate test drives the propagation **twice**, which fails for any implementation that finds the login by matching the old email instead of by `User.CustomerId`. `AuditEntries.Add(` still appears **only** in `AuditRecorder.cs`, and no explicit transaction exists anywhere in `backend/src`. **Against real SQL Server:** `ORDER BY CreatedAt` works (SQLite refuses `DateTimeOffset` in `ORDER BY`, so the `createdAt` sort is verified here rather than in the suite), and the case-insensitive `WHERE Email = …` that both duplicate checks rely on matches a differently-cased row in **both** `Customers` and `Users`. **Slice boundary checked:** `/customers`, `/auth/register` and `/attachments/{id}/content` are each `404`, and `openapi/v1.json` still lists 11 paths |
| **Story 04 slice 1 — customers domain and data layer** | ✅ Passed 2026-08-27 | Plan tasks 1 and 2 only. Build clean with `TreatWarningsAsErrors`. **22 slice tests pass and the whole suite is 90/0/1.** **Against real SQL Server** (`docker compose up --build api`, migration applied at startup): the three tables exist; `IX_Customers_Email` is unique and `IX_Customers_BranchId` is not; `Customers.Email` and `Users.Email` are both `nvarchar(256)` with `SQL_Latin1_General_CP1_CI_AS`; `FK_Users_Customers_CustomerId` exists. **Two product rules proven by attempted violation, inside a rolled-back transaction:** inserting `case.test@EXAMPLE.COM` after `Case.Test@Example.com` is refused with error **2601** — the case-insensitive uniqueness of A-10, which SQLite cannot verify — and an attachment with **neither** owner and one with **both** are each refused with error **547**, `CK_Attachments_OwnerXor` (§5 constraint 20). **Regression:** `/health`, `/config/bootstrap`, `/auth/login`, `/auth/me`, `/users`, `/departments`, `/branches` and `/config/staff` all `200` with a real Administrator token, and `storagePath`/`passwordHash` appear in none of their bodies. **Slice boundary checked, not assumed:** `/customers`, `/attachments/{id}/content` and `/auth/register` are each `404`. Plan verification steps 4, 5 and 7 are **not runnable in this slice** — they need the endpoints and screens of later slices |
| Story verification | ✅ **Stories 01, 02 and 03 passed** | Story 01: all 8 steps, 2026-08-25 (re-run 2026-08-26). Story 02: all 6 steps, 2026-08-26. Story 03: **5 of its 6 steps, 2026-08-27** — step 6 names the story-05 ticket list and could not be run as written (see its row). Details in the rows below |
| Unit / integration tests | ✅ **220 passing, 1 skipped**, plus **23 front-end specs** | `dotnet test backend/SupportCrm.sln` re-run **2026-08-30** after story 04 slice 6: **220 passed, 0 failed, 1 skipped** — **unchanged from slice 5**, which is the point: slice 6 changed no backend file, so a moved number would have meant something was wrong. (220 after slice 5; 198 after the I-10 fix; 171 after the I-9 fix; 155 after slice 4; 140 after slice 3; 114 after slice 2; 90 after slice 1; 68 before story 04.) The skip is `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`, **skipped by design** until story 05 creates the `Ticket` type it asserts about. Story 03 adds the two organization endpoints' role matrix, the `managerUserId`-is-absent-not-null contract check, and the no-write-verb lock. Front end: `npx ng test --watch=false --browsers=ChromeHeadless` **2026-08-30** — **23 passed** (16 before slice 6; 4 when story 03 added the repo's first specs), on the karma target story 01 already configured. Story 02 adds the AD-15 regression (a user deactivated after their token was issued is `401` on the very next request), the wrong-password / deactivated-account indistinguishability check, token-claim absence, `passwordHash` absence from every response path, and the audit actor-attribution rules of §2.14 |
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
| Working tree | ✅ **Clean at `ca2b306`** | Re-read from the repository on **2026-08-30**, not carried over: `git status --short` is **empty** and `main` head is **`ca2b306`**, *"feat: story 04 slice 6 — customer screens and the registration form"* — 17 files, +1696/−26. The chain stays linear: `54abd75` (story 16 Part A + slice 1) → `a4230b9` (slice 2) → `5c716b1` (slice 3) → `60b58e6` (front-end error layer) → `346d28b` (slice 4) → `cf22841` (the I-9 / I-10 fixes) → `c0c0043` (slice 5) → `6e431dd` (this manual, the workflow skills, and the change-log extraction) → `ca2b306`. **Nothing has been rewritten, reordered or squashed.** `6e431dd` is where §9's 900 lines went — **moved to [CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md), not deleted**, which the file diff confirms line for line. **`git status` is the live source — this row is a snapshot, not a claim to be trusted over the repository** |

---

## 9. Change Log

**Moved.** The implementation change log lives in
[CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md) as of 2026-08-30 — every entry from
2026-08-24 onward, newest first, unchanged. It was relocated to keep this dashboard small enough
to read in full; nothing was summarized, merged or dropped.

**It is not duplicated here.** New entries go in that file, in the same task as the change (see
Maintenance). This document keeps the *current* picture — §1 status, §3 story state, §6 questions
and findings, §7 implementation, §8 verification evidence, §10 next steps — and the changelog
keeps the narrative history behind it.

---

## 10. Current Next Steps

0. **Story 04 is complete — all six slices done and verified. Awaiting explicit approval before
   story 05.** Story 04 was delivered in slices at the user's instruction; the map lives in
   [.squad/plans/customer-management/00-overview.md](../.squad/plans/customer-management/00-overview.md).

   | Slice | Plan tasks | State |
   |---|---|---|
   | **1 — domain and data layer** | 1, 2 | ✅ **Done and verified 2026-08-27** |
   | **2 — `CustomerService`, carrying A-19** | 3 | ✅ **Done and verified 2026-08-27** |
   | **3 — notes, timeline, attachments, seed** | 4, 5, 6, 9 | ✅ **Done and verified 2026-08-28** |
   | **4 — controllers** | 8, 10 | ✅ **Done and verified 2026-08-28** |
   | **— cross-cutting: AP-10 / finding I-9** | none — a contract fix, not a plan task | ✅ **Done and verified 2026-08-28** |
   | **— cross-cutting: AP-2 / finding I-10** | none — a contract fix, not a plan task | ✅ **Done and verified 2026-08-28** |
   | **5 — registration** | 7 — `RegisterAsync`, the route, the three A-15 outcomes | ✅ **Done and verified 2026-08-28** |
   | **6 — front end** | 11, 12, 13, 14 | ✅ **Done and verified 2026-08-30** |

   **Thirteen findings have come out of the six slices and the two contract fixes, and none is
   resolved by invention** — all are in §6.8. **Three remain the user's call:** the plan requires a
   "configured root" for attachment storage that no approved document supplies (**I-1**); data-model
   §6 declares no index for `CustomerNote` where it declares one for the analogous
   `TicketInternalNote` (**I-2**); and **no §6.9 payload publishes the attachment size cap** that
   ui-design §8 asks the uploader to show, so the cap line is absent while `413` still reads as a
   translated sentence (**I-12**, new in slice 6). **Three are closed:** AP-10 is enforced globally
   (**I-9**), every model-state `400` now carries the AP-2 slug and §6.12 envelope (**I-10**), and
   **I-11 is withdrawn** — the `Msg 8624` on a `Customers` delete was `sqlcmd`'s
   `QUOTED_IDENTIFIER OFF` meeting a filtered index, not a schema defect, proven on 2026-08-30 by
   varying only that setting on the same statement. **I-5 was closed by slice 5.** The rest are
   implementation consequences with no product content (**I-3, I-4, I-6, I-7, I-13**) plus one real
   deployment defect found by running the stack and fixed (**I-8**).

   **One dev row is disclosed rather than cleaned up.** Driving the real registration form for the
   plan's verification step 5 created the customer `slice6.verify.1788107299323@example.com`, and it
   cannot be removed: deleting its login would mean deleting an append-only `UserCreated` audit entry
   (AD-10). The dev database therefore holds **10 customers, not the seeded 9** — the same situation
   slice 5's three verification rows left, and harmless for the same reason.

1. **Stories 01, 02 and 03 are done; phase 1 is closed.** The phase-1 interleave of S9-12 played out
   exactly as designed:

   | Order | Work | State |
   |---|---|---|
   | 1 | Story 03 tasks 1–2 — `Department`, `Branch`, EF configuration | ✅ Done |
   | 2 | Story 02 tasks 1–3 — `User`, `AuditEntry`, their configuration | ✅ Done |
   | 3 | **`InitialSchema` migration** — one migration, all four tables | ✅ Done |
   | 4 | Story 03 task 3 — `OrganizationSeeder` (`Order = 10`) | ✅ Done (executed early, approved) |
   | 5 | Story 02 task 10 — `IdentitySeeder` (`Order = 20`) + manager second pass | ✅ Done |
   | 6 | Story 02 tasks 4–14 — auth, endpoints, front end | ✅ Done |
   | 7 | Story 03 tasks 4–8 — the two `Agent`-gated endpoints and the filters | ✅ Done |
   | 8 | **Story 16 Part A tasks 1–5** — option types, startup validation, `/config`, `/config/staff` | ✅ Done |

   **Story 16 Part A was the gate before stories 04 and 05** — a ticket cannot be created without
   the category list, the category→department map and the SLA targets. **It is now in, so nothing
   blocks story 04.** Story 16 **Part B** stays at Phase 7: it reads audit rows that stories 04–06
   have yet to write, and building it now would test an empty log.

2. **Story 04 leaves one new marker, and story 05's two still stand.** Slice 6's is
   `AttachmentListComponent.maxSizeBytes` — the shaped hole for **I-12**, which needs nothing but a
   published cap to fill and changes no other line when it arrives. The two for story 05 are
   unchanged: the `Skip` on `BranchIsNotABoundaryTests.Ticket_has_no_branch_member` and story 03's
   verification step 6. **Story 17 Part B inherits one measurement** from slice 6's RTL check — the
   staff shell sidebar does not mirror in Arabic, which its task 3 already owns by name.

2a. **Both placeholders are gone.** The create-user dialog and the user-detail form now bind a
   selector populated from `GET /departments` instead of taking a department id as free text, and the
   avatar menu shows the department **name**. **Two markers remain for story 05**, both deliberate and
   both cross-referenced at their call sites: the `Skip` on
   `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`, which cannot assert on a `Ticket` type
   that does not exist yet, and story 03 verification step 6, which names the `/workspace/tickets`
   department filter. Story 05 task 10 owns both.

3. **Nothing is uncommitted.** The whole of story 04 is in history on a linear chain — slice 1 with
   story 16 Part A as `54abd75`, slice 2 `a4230b9`, slice 3 `5c716b1`, the cross-cutting error layer
   `60b58e6`, slice 4 `346d28b`, the I-9 / I-10 fixes `cf22841`, slice 5 `c0c0043`, the operating
   manual and change-log extraction `6e431dd`, and **slice 6 as `ca2b306`**. `git status` is clean
   (§8, Working tree).

3a. **One cross-cutting front-end change sits alongside the story work.** The HTTP error-handling
   layer landed 2026-08-28 between slices 3 and 4 and is committed as `60b58e6`
   ([CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md)). It
   is **not** part of story 04 and closes none of its Done Criteria. **It added the front end's
   first toast surface** — PrimeNG `MessageService` + `<p-toast>` at the root — because none
   existed. Two things to know before slice 6 builds screens: the toast is the **transport-level**
   surface only and must not be mistaken for the A-13 notification centre (story 09), and `503` is
   deliberately excluded from it so story 11's AI panel can degrade locally as §9 requires.

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

**The narrative entry for that change goes in
[CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md)** — newest first, in the same task — and
**not** in this file (§9). This document carries only the current picture: §1, §3, §6, §7, §8, §10.

**Accuracy rules that override convenience:** never record progress that did not happen; never mark
Implemented when only a design exists; never mark Verified without evidence named in §8; never mark
a stage complete unless its gate is satisfied; never delete a historical decision or blocker — move
it to §6.3 instead.

**Recalculate §1.1** whenever a §2 row completes or a §3 story reaches Verified.
