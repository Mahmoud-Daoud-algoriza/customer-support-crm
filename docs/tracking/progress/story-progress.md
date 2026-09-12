[← Back to PROJECT-PROGRESS.md](../PROJECT-PROGRESS.md)

## 3. Feature / Story Progress

All 18 stories from [story-backlog.md](story-backlog.md). Sequence is the intended execution order;
squad-kit assigns the real `NN` when each plan is generated.

**Stories 01, 02, 03, 04, 05, 06, 07, 12, 13 and 16 are Implemented and Verified** — 01–03 close phase 1, **04
is the first T1 business story delivered end to end**, **05 and 06 close phase 3 with the ticket
core and its lifecycle, the loop the whole assessment rests on**, and **07 opens phase 4 with the
one real communication channel and the message model every other channel plugs into**. **Story 14 is Implemented with verification partial** — see its row. **Stories
08–13 and 15–18 are at `Plan Complete`** (with the standing caveat below that §10 narrates 08–11 as complete). All 18 plans exist. The `Depends on` column below is the **execution** dependency from
[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §4, which supersedes the
earlier reading in three places (S9-7, S9-8, S9-12).

| Seq | Story | Feature | Tier | SDD | Plan | Impl | Verified | Depends on | Blocker |
|---|---|---|---|---|---|---|---|---|---|
| 01 | `solution-skeleton` | platform-foundation | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-25, re-run 2026-08-26 | — | — |
| 02 | `auth-and-roles` | identity-access | **T1** | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-26 | 01, **03 data** | — |
| 03 | `departments-branches` | organization | **T1**+T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** | ✅ **Verified** 2026-08-27 | 01, 02 | — · tasks 1–3 landed early with story 02, tasks 4–8 after it, per S9-12 |
| 04 | `customer-records` | customer-management | **T1**+T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 14 tasks | ✅ **Verified** 2026-08-30 — all six slices | 01–03, **16 A** | — · **OQ-5 closed 2026-08-27 (A-19)**, and **A-19 is implemented and proven** · sliced at the user's instruction; slice map in [00-overview.md](../.squad/plans/customer-management/00-overview.md) · **two Done Criteria complete in later stories by the plan's own text** — the timeline fills in story 06, the ticket half of the attachment criterion in story 05 (S9-2) · **finding I-12 open**, needing a user decision |
| 05 | `ticket-core` | ticket-management | **T1** | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 13 tasks | ✅ **Verified** 2026-08-31 | 01–04, **16 A** | — · **OQ-2 closed 2026-08-30 (A-20)**, and **A-20 is implemented and proven** · delivered **whole, not sliced**, so there is no slice map · **closes S9-2** (the ticket attachment endpoints) and **S9-5** (both SLA timestamps required at creation) · **finding I-16 open**, needing a user decision |
| 06 | `ticket-lifecycle` | ticket-management | **T1** | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 11 tasks | ✅ **Verified** 2026-08-31 | 05 | — · **OQ-3 closed 2026-08-31 (A-21)**, and **A-21 is applied through the shared policy, not re-implemented** · delivered **whole, not sliced** · **completes story 04's interaction-timeline criterion** · **finding I-23 ✅ CLOSED 2026-09-01 by story 13**, which published `POST /portal/tickets/{id}/transition` and asserts A-16's customer column over HTTP · **F-1 stays open by design** |
| 07 | `ticket-intake-messaging` | ticket-management | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 10 tasks | ✅ **Verified** 2026-08-31 | 05, 06, 04, 16 A | — · delivered **whole, not sliced** · **supplies the trigger** story 06 built and left uncalled — `ApplyAutomaticCustomerReplyTransitionAsync` now has its one caller (R-13, R-14) · **no inbound HTTP endpoint** (AP-11), so **PF-2 stays untouched for story 18** · **finding I-25 open**, needing a user decision on the priority a customer submission is created with |
| 08 | `agent-dashboard` | agent-workspace | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 04–07, 16 A | ⛔ **S9-1** *(task region only)* |
| 09 | `sla-routing-escalation` | sla-automation | T2 | Intake Complete | **Plan Complete** | Not Started | Not Started | 03, 05, 06, 16 A | ⚠ PF-5 · **OQ-2 closed 2026-08-30 (A-20)**, **OQ-3 closed 2026-08-31 (A-21)** — task 4 calls the same `IEscalationRecipientPolicy` story 06 does |
| 10 | `ai-service-seam` | ai-assist | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 01 — **parallel** | — |
| 11 | `ai-ticket-assists` | ai-assist | **T1** | Intake Complete | **Plan Complete** | Not Started | Not Started | 10, 05–08 | ⛔ **S9-4** |
| 12 | `kb-articles-search` | knowledge-base | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 11 tasks | ✅ **Verified** 2026-09-01 | 02; task 4 needs 05 | — · delivered **whole, not sliced** · **AD-13 has one home**, `Application/Modules/Knowledge/ArticleSearch.cs`, and **constraint 19 has one home**, `PortalArticleService.PortalVisible` · **AP-14 honoured**: §7.4 is a Knowledge endpoint and a test asserts no `/ai` route serves it · **story 13 may now consume the public articles** requirements §8.4 needs |
| 13 | `portal-self-service` | customer-portal | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 12 tasks | ✅ **Verified** 2026-09-01 | 02, 04–07, 12 | ⚠ **OQ-1 still open, and deliberately not answered** — the plan's own interim behaviour (ui-design §11) was implemented instead: the server validates against the configured `Feedback rating scale` key and the control renders from it, so **no scale exists in the schema, the Domain, the service, the tests or the UI** · delivered **whole, not sliced** · **completes the eleven-endpoint §5.7 portal path space** · **closes I-23** · **findings I-35 and I-36 open**, both needing a user decision |
| 14 | `tasks-internal-notes` | agent-workspace | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — plan tasks **1–7, 9, 10**; **task 8 not built (S9-1)** | ⚠ **Partial** 2026-09-06 — backend, data layer and live stack verified; **front-end screen checks OUTSTANDING** | 06, 08 | ⛔ **S9-1 — now due and blocking AC 2.** No cross-ticket task endpoint exists, so the dashboard task region was **not built**: `TicketTaskService.ListForUserAsync` is written, marked and referenced by no controller, and story 08's queue marker is untouched. **Needs the user's Option A / Option B decision; do not invent an endpoint.** · ⚠ **UI verification outstanding** — plan step 7 and the ui-design §10 browser pass could not be run (no browser-driving capability); listed in §8. **The story is not verified in full and does not count in §1.1.** · **finding I-37 raised** (informational) · **two deliberate deviations recorded in §6.8** |
| 15 | `management-dashboard` | reporting | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented** — all 6 tasks | ⚠ **Partial** 2026-09-06 — backend, data layer and live stack verified; **front-end screen checks OUTSTANDING** | 03, 05, 06, 09, 13 | ✅ **PF-4 / S9-9 CLOSED 2026-09-06 by the product owner (R-18)** — *currently assigned*; encoded once in `AgentPerformanceQuery`, so no method throws and no criterion is blocked. · ⚠ **UI verification outstanding** — plan step 8 could not be run (no browser-driving capability); listed in §8. **Not verified in full and does not count in §1.1.** · ⚠ **OQ-1 still open and deliberately not answered** — the tile renders the configured scale beside the average and hardcodes no denominator · **findings I-38, I-39, I-40 raised**, all informational · **closes the api-design path space at 66 operations** · **its story-13 dependency is met** — `CustomerFeedback` exists and one seeded rating is present, so §9.4's satisfaction tile has a non-trivial average and **rows that are absent must be read as "no response", not as zero** (data-model §2.15) |
| 16 | `audit-configuration` | administration | T2 | Intake Complete | **Plan Complete** | ✅ **Implemented in full** — Part A (tasks 1–5) and **Part B (tasks 6–11)** | ✅ **Verified** — Part A 2026-08-27, **Part B 2026-09-01** | **A:** 01, 03 · **B:** 02, 06 | — · Part B is **the last administrative surface** per the plan — nothing else is consumed by it |
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

