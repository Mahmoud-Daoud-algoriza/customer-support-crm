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
| **Current SDD stage** | **Stage 7 complete → Stage 8 (UI Design) is next, not started** |
| **Current phase** | Design. Implementation has **not** begun |
| **Overall status** | 🟢 On track — **Stage 7 pre-flight passed**. The one blocking finding (PF-1) is resolved; every API contract is designable |
| **Overall progress** | **27%** (method in §1.1) |
| **SDD pipeline** | **7 of 10 stages complete** (70% of the pipeline) |
| **Code written** | **None.** `backend/` and `frontend/` are empty; `docker-compose.yml` is 0 bytes |
| **Last updated** | 2026-08-24 |
| **Current focus** | Stage 8 — UI Design (`docs/ui-design.md`). Stage 7 delivered 66 endpoints across 12 modules |
| **Next immediate step** | See §10, item 1 |

### 1.1 How the 23% is calculated

Two weighted tracks. The weighting is a **stated planning convention, not a measurement** — it is
recorded here so the number is reproducible rather than invented.

| Track | Contents | Weight | Complete | Contribution |
|---|---|---|---|---|
| **Design & planning** | Tracker rows 1–9 (§2), equally weighted at 3.89% each | 35% | 7 of 9 rows | 7 ÷ 9 × 35 = **27.2%** |
| **Delivery** | Row 10 — the 18 stories implemented *and* verified | 65% | 0 of 18 | 0 ÷ 18 × 65 = **0%** |
| | | **100%** | | **≈ 27%** |

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
| 2 | Product Scope | ✅ Complete | [product-scope.md](product-scope.md) | ✅ | n/a | T1–T4 tiers, A-1…A-18, 7 open questions. Approved 2026-08-24; A-14…A-18 added 2026-08-24 |
| 3 | Story Intake / Backlog | ✅ Complete | 18 intakes + [story-backlog.md](story-backlog.md) | ✅ | ✅ Met | All 56 requirement lines mapped to a story |
| 4 | Squad Kit Initialization | ✅ Complete | [.squad/](../.squad/) — config, 18 stories across **14 feature slugs**, 14 plan-overview stubs | ✅ | ✅ `squad doctor`: 6 ok, 0 warn, 0 fail | v0.2.0, tracker `none`, agent `claude-code` |
| 5 | Architecture | ✅ Complete | [architecture.md](architecture.md) | ✅ | ✅ Met, re-verified after the AD-15 correction | Approved 2026-08-24 |
| 6 | Data Model | ✅ Complete | [data-model.md](data-model.md) | ✅ | ✅ Met, re-verified after the four clarifications | Approved 2026-08-24. 15 entities |
| 7 | API Design | ✅ Complete | [api-design.md](api-design.md) | ✅ | ✅ Met | 66 endpoints, 12 modules, 16 API decisions (AP-1…AP-16). Pre-flight passed the same day |
| 8 | UI Design | ⬜ **Not Started** | `docs/ui-design.md` | ✖ | ✖ | **Current stage.** Gate: every screen for workspace, portal and admin; RTL implications; phone-width behaviour |
| 9 | Implementation Plans | ⬜ Not Started | `.squad/plans/<feature>/NN-story-*.md` | ✖ (0 plan files) | ✖ | Generated one story at a time via `/squad-plan` |
| 10 | Implementation / Verification | ⬜ Not Started | `backend/`, `frontend/` | ✖ (both empty) | ✖ | No code exists |

**Numbering note.** These ten rows are this dashboard's structure.
[sdd-workflow.md](sdd-workflow.md) is the canonical pipeline and numbers its stages slightly
differently: its stage 2 is the requirements analysis (folded into row 1 here), its stage 4 is the
user-story stage (rows 3 and 4 here, since squad-kit initialization is tooling rather than a
workflow stage). Rows 5–10 match its stages 5–10 exactly. The workflow document governs.

**Blockers to the pipeline:** none. The Stage 7 pre-flight audit raised one blocking finding, PF-1
(the `Pending` transition set and the effect of a customer reply); it was resolved the same day as
R-13. Six non-blocking findings (PF-2…PF-7) are carried in §6.5.

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

**This table is not the whole decision inventory.** It covers technical decisions (AD-*) and
modelling decisions (DM-*). **Business** decisions are the numbered assumptions A-1…A-18 in
[product-scope.md](product-scope.md) §7; the five most recent (**A-14…A-18**) are recorded as
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
| **OQ-2** | On a priority change, do SLA due dates recompute from `createdAt` or stay frozen? A-3 is silent, and T2-D escalation changes priority routinely | Materially different §9.2 attainment numbers; recompute can breach a ticket as a consequence of the escalation the breach triggered | Story 09, and §9.2 in story 15 | 🔴 Open — business rule |
| **OQ-3** | Who is notified on breach when a department has no manager? T2-D says "notify the department manager"; absence is uncovered | Breach flag and priority raise are unaffected; only the recipient is undetermined. **No fallback invented** | Story 09 | 🔴 Open — product decision |

**None of these blocks Stage 7.** All three are implementation-time decisions. OQ-4, which did
block one contract, was resolved on 2026-08-24 — see R-11 below.

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
| R-11 | **OQ-4** — what is the customer cancellation window? | **Assignment is not the start of work.** A ticket may be assigned while still `New`; `New → Open` is an agent deliberately starting work. The customer's window runs from creation until an agent picks the ticket up, so it is real rather than theoretical | 2026-08-24 | **A-18** |

### 6.5 Pre-flight findings carried forward (non-blocking)

Raised by the Stage 7 pre-flight audit on 2026-08-24. None blocks an API contract; each is handled
inside the API document or deferred to the story named.

| ID | Finding | Handle in |
|---|---|---|
| **PF-2** | `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required with no System actor, but story 18's inbound fake adapter creates tickets with no human actor | **Avoided in Stage 7** (AP-11 publishes no ingestion endpoint) but **still open for story 18** |
| **PF-3** | OQ-1 leaves the CSAT scale undecided and `architecture.md` §6.3 has **no** configuration key for it | **Handled in api-design §5.7** — `rating` validates against `feedback.ratingScale` from `GET /config`. ⚠ **Requires adding `Feedback:RatingScale` to architecture §6.3 — flagged for approval, not applied** |
| **PF-4** | "Tickets assigned" in the agent-performance metric is undefined — currently assigned vs. ever assigned. Same response shape either way | Story 15 |
| **PF-5** | `firstRespondedAt` is set only by the first outbound message, so a ticket resolved without a reply is permanently first-response-breached | Story 09 |
| **PF-6** | A-15 covers registration when a **Customer profile** exists for the email, not when a **User** already does | ✅ **Closed** — api-design §5.2 states `409 user-already-exists` |
| **PF-7** | `TicketMessage.direction` has no stated derivation rule and must not be client-settable | ✅ **Closed** — api-design §7 derives it from author role and omits it from every request model |

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
| **R-14 / A-5** automatic transition attributed to the replying customer | `product-scope.md` A-5 (new attribution rule); `data-model.md` §2.6 invariant 2b, §2.7 `actorKind` note, §2.8 invariants, §5 constraint 9a | `ticket-lifecycle` |
| **R-13 / A-5** customer reply reopens a `Pending` ticket | `product-scope.md` A-5 (transition graph + `Pending` bullet), A-16 (`-> Open` row); `data-model.md` §2.6 invariant 2b, §2.8 invariants, §5 constraint 9a | `ticket-lifecycle`, `ticket-intake-messaging`, `portal-self-service` |
| **R-12 / DM-7** `CustomerFeedback` owned by `Tickets` | `data-model.md` **new DM-7**, §1 preamble, §2.15 ownership, §3 entity list; `architecture.md` §1 (`customer-portal` row) | — (no intake asserted an owning module) |
| **R-11 / A-18** assignment is not the start of work | `product-scope.md` A-5, **new A-18**, §9 (question 8 closed); `data-model.md` §2.6 field, **new invariant 2a**, 11b, §8 | `ticket-lifecycle`, `ticket-core`, `sla-routing-escalation`, `portal-self-service` |

`sdd-workflow.md` was touched by the A-14…A-18 set only to widen the assumption range it cites.
No decision so far has changed [requirements.md](requirements.md), which is never edited.

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
| Working tree | ✅ Tracked | **9 commits** on `main`, all pushed to `origin/main` as of 2026-08-24. Only this tracker is typically uncommitted mid-task. **`git status` is the live source — this row is a snapshot, not a claim to be trusted over the repository** |

---

## 9. Change Log

Newest first. Every meaningful project change gets an entry.

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

1. **Approve or reject the `Feedback:RatingScale` configuration key** for architecture §6.3
   (api-design §9, item 1). Small, but it touches an approved document.
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
