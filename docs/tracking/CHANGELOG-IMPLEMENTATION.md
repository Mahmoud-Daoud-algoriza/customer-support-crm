# Implementation Change Log — Customer Support CRM

> **This file reports history. It does not define state.**
> It holds the change log that was [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md) §9 until
> 2026-08-30, moved here unchanged so the dashboard stays small. The authority order is
> unchanged and is stated at the top of [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md): nothing here
> is a source of truth, and on any conflict the approved documents and the repository win.
>
> **Current** status, findings, blockers and next steps are **not** here — they are in
> [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md) §1, §3, §6, §7, §8 and §10.
>
> **Read a dated entry, not this file.** Find the entry you need by its date or story heading
> and read that range only.
>
> **Each entry records what happened, when, and in what order — not the full detail of any one
> fact.** A decision's full rationale lives once, in `resolved-decisions.md` (or
> `design-stage-findings.md` for a pre-implementation one); a finding's full write-up lives once,
> in `implementation-findings.md`; verification evidence lives once, in `verification/`. An entry
> cites the id and links to it rather than restating it — if the two ever disagree, the linked
> file wins.

---

Newest first. Every meaningful project change gets an entry.

### 2026-09-06 (latest) — story 15: management dashboard and operational reports

**Phase 7's reporting half.** Delivered **whole rather than in slices** — all six plan tasks —
with **no blocked criterion**, because the decision that gated it (PF-4 / S9-9) was taken first.
Requirements §9 in full at T2-G: one management dashboard with a small fixed metric set, **no
report builder, no scheduling, no export**.

**Decision — R-18** (PF-4 / S9-9, *"tickets assigned"*): the product owner chose **currently
assigned**, closed before a line of this story was written. Full rationale, rejected alternative
and why it was taken up front rather than deferred: [resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).

**Changed.** New `Application/Modules/Reporting/` (`DashboardReportService` + four query types —
`TicketCountsQuery`, `SlaAttainmentQuery`, `AgentPerformanceQuery`, `SatisfactionQuery`). **No
entity, no migration** (§9 introduces none — data-model §7). The filter applies once, in
`Filtered`; `branchId` resolves through the customer (Ticket has no branch column); and
`TicketScope.ForCaller` is deliberately not composed (AD-5). "Empty is not zero" is implemented
three times — null rating average, null resolution average, null attainment percent, never `0`/
`100`. One endpoint, `GET /reports/dashboard`, `RequireManager`. Front end: `ReportsClient`, the
`/workspace/reports` route behind a Manager+ guard, the four ui-design §5.7 regions, PrimeNG table
and tag only (no charting dependency). Seed data: a second `CustomerFeedback` row so §9.4's average
is a real average. Full by-layer detail: [implementation-progress.md](progress/implementation-progress.md) §7.

**Findings — I-38, I-39, I-40, all informational.** Resolution average computed over a projection
(no cross-provider date-arithmetic expression exists); AP-15's unknown-parameter refusal enforced
on this endpoint only (the same gap on other endpoints is reported, not fixed); three "meaningful
null" fields opted back into serialization to match §6.8's own example. Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-09-06.** Backend, data layer and live stack: suite **451 passing** (+21), build
clean, front end **66 specs** unchanged, `openapi/v1.json` now publishes the complete 66-operation
contract. Full evidence: [verification/story-15.md](progress/verification/story-15.md).
⚠ **Not verified: the front-end screen checks** (plan step 8) — no browser-driving capability was
available. **Story 15 does not count toward §1.1** until they are covered.

**Deliberately not touched.** No approved document edited. No migration, no export/schedule/
date-range surface, no charting library. The AP-15 gap on other endpoints (I-39) and the stale §3
rows for stories 08–11 were both left as they are — neither is this story's work.

---

### 2026-09-06 — story 14: tasks and internal notes

**Phase 7 continues.** Delivered **whole but not in full**: plan tasks 1–7, 9 and 10 are
implemented; **task 8 is not built**; front-end screen checks are outstanding. Requirements §4.3
and §4.5 at T2-C — a due-dated to-do attached to a ticket, and internal notes as the whole of
collaboration.

**Changed.** `TicketInternalNote` (`Domain/Modules/Tickets/`) sits in its own table no
customer-facing query names (data-model §2.9) — the visibility rule is structural, so
`TicketInternalNoteService` needs no exclusion logic. `TicketTask.SetDone` is the only writer of
`completedAt` (§5 constraint 23). `TicketActivity.InternalNotePosted` mirrors story 07's
`MessagePosted` — accepts neither a type nor a visibility, so §2.7's invariants can't be spelled
wrongly at a call site. Five `RequireAgent` endpoints on the existing staff controller (api-design
§5.6); **no `/portal` counterpart exists or may exist** (AP-5). Migration `TasksAndInternalNotes`
(two tables, two indexes, including `TicketTask(assignedUserId, isDone, dueAt)` — created
regardless of how S9-1 is decided). Front end: the internal-notes region (always-rendered
visibility marker, its own endpoint, no client-side filter) and the tasks region (overdue rows
by colour + label, no calendar/recurrence/reminder). Seed data: one note and one task on
`PaymentsCardDeclined`. Full by-layer detail: [implementation-progress.md](progress/implementation-progress.md) §7.

⛔ **S9-1 stopped task 8 — the correct outcome, not a shortfall.** No endpoint publishes a
cross-ticket task list (api-design §5.6 publishes none); `TicketTaskService.ListForUserAsync` is
written, marked, and referenced by no controller. **AC 2 is open pending the user's Option A /
Option B decision.** Status: [open-questions.md](progress/open-questions-and-findings/open-questions.md), [story-progress.md](progress/story-progress.md).

**Findings — I-37** (informational: no default sort fixed for the tasks endpoint, soonest-due-first
implemented) and **I-16 reused, not re-raised** (the task-assignee picker hits the same
no-agent-readable-staff-directory gap as story 05; story 05's resolution applied, not a second
answer invented). **Two deviations, D-1 and D-2**, recorded rather than folded in silently. Full
write-ups: [implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-09-06.** Backend suite **430 passing, 0 skipped** (+13, and story 07's deliberate
skip is now filled rather than left vacuous). Front end unchanged at 66 specs. The guard is
load-bearing: reverting `Visibility = Internal` turned exactly 2 tests red. Full evidence:
[verification/story-14.md](progress/verification/story-14.md).
⚠ **Not verified: the front-end screen checks** — no browser-driving capability was available.
**Story 14 does not count toward §1.1** until they are covered, which is why the overall figure
stays at 71.1% rather than 74.7%.

**Deliberately not touched.** No approved document edited. Task 8 not built (S9-1), no endpoint
invented for it. No `/portal` route/DTO/client method for notes or tasks. No thirteenth
`TicketActivity` type. No edit/delete path for a note; no patchable field for a task beyond
`{ isDone }`. The stale §3 rows for stories 08–11 predate this story and were left as they are.

---

### 2026-09-01 — story 13: customer portal self-service and feedback

**Phase 6 closes.** Delivered whole: all twelve plan tasks, requirements §8 in full — submit,
track, view history, access FAQs, submit feedback — as a separate, simpler surface for the
Customer role over the same backend.

**Changed.** `CustomerFeedback` (`Modules/Tickets/`, DM-7 — no eleventh backend module).
Write-once by construction: private setters, no mutator, no rating constant anywhere in the
Domain (OQ-1). Migration `CustomerFeedback`: unique index on `TicketId`, FK `Restrict` (a rating
is a reporting fact; deleting one with its ticket would silently change §9.4's average), no check
constraint on `Rating` (a range would encode OQ-1 into the schema). `CustomerFeedbackService`'s
four preconditions run scope → reached-`Resolved` (read from `ResolvedAt`, not current status) →
one-per-ticket → configured range, in that fixed order (AP-4). `PortalTicketsController` gains the
remaining six §5.7 endpoints, taking the portal path space to eleven — complete and matching
api-design endpoint for endpoint. Front end: `layout/portal-shell/` as its own shell (two
destinations, no sidebar, no bell — ui-design §4.2); the five-region request detail;
`shared/components/rating-input/` taking `{ min, max }`. Full by-layer detail:
[implementation-progress.md](progress/implementation-progress.md) §7.

**One defect found by driving the real browser, and fixed:** `ReplyComposer` showed its staff
placeholder to the customer; it now takes a `placeholderKey`.

⚠ **OQ-1 is not answered here.** The plan's interim behaviour (ui-design §11) was implemented
instead — no scale in the schema, the Domain, the service, the tests or the UI. Status:
[open-questions.md](progress/open-questions-and-findings/open-questions.md).

**Findings — I-35** (no payload carries the "last update" timestamp ui-design §7.1 asks for;
nothing invented, the card shows what the payload does carry) and **I-36** (the feedback endpoint's
second `409` needed its own slug, `feedback-not-available`, on the precedent story 07 set). **I-23
is CLOSED** — this story published the portal transition endpoint story 06 left undeliverable.
Full write-ups: [implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-09-01.** Suite **417 passing, 1 skipped by design** (+16); front end **66 SUCCESS**
(+4). Against real SQL Server: another customer's ticket `404` and byte-identical to a missing
one; feedback `201` then `409`; a rating of `999` inserted directly was **accepted** (proving OQ-1
is not in the schema); 22 browser checks at 390px, all passing. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Deliberately not touched.** No story 14 work — `TicketInternalNote` does not exist, and
`InternalNotesAreUnreachableTests` stays skipped with its `Assert.Fail` body intact. No story 15
work beyond the seeded rating it will read. No approved document edited. OQ-1 not answered.

---

### 2026-09-01 — story 12: knowledge base, search and suggested solutions

**Phase 6 opens.** Delivered whole: all eleven plan tasks, requirements §6 in full plus §7.4, at
the depth T2-E fixes.

**Changed.** `KnowledgeArticle` (`Modules/Knowledge/`) with `ArticleType`/`ArticleVisibility` —
one entity with a `type`, no navigation to `Ticket` (data-model §2.13; AD-13 computes suggestions
at read time), no versioning, no delete method. `ArticleSearch` is the one implementation of AD-13
— `EF.Functions.Like` over title/body, weighted scoring, built as one SQL expression so the
database filters and orders in a single statement. `PortalArticleService.PortalVisible` is the one
implementation of data-model §5 constraint 19 — composed before the id comparison, so an internal
article is `404`, never `403` (AP-4). `SuggestedArticleService` scopes through `TicketScope` first
and references no `IAiAssistService` (AP-14). One configuration key,
`SupportCrm:Knowledge:SuggestedArticleCount` — deliberately no weight/threshold/synonym key
(AD-13 excludes tuning). Migration `KnowledgeArticles`: no search index, no full-text catalogue
(data-model §6 — a database upgrade this story does not take). Seeder: 10 articles, one
public-but-unpublished. Six staff/admin routes plus two portal routes plus one ticket-scoped
suggestion route; **no delete route exists anywhere**. Full by-layer detail:
[implementation-progress.md](progress/implementation-progress.md) §7.

**One implementation choice recorded at the code, not as a finding:** keyword *extraction* for
§7.4 (drop words under three characters and a short stop-word list) — decides retrieval noise, not
a product question.

**Verified 2026-09-01.** Suite **401 passed, 1 skipped** (+16); front end **62 SUCCESS** (+11).
Against real SQL Server: internal/unpublished/missing all `404` with byte-identical Problem
Details; case-insensitive matching confirmed. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Deliberately not touched.** No search engine, vector store, versioning, review workflow,
scheduled publishing, media library or delete path (architecture §8, T2-E).

---

### 2026-09-01 — story 16 Part B: audit log and read-only configuration

**Story 16 is now complete in full.** Part A (configuration) landed early by design; Part B —
`GET /audit`, the `/admin/audit` screen and the read-only `/admin/configuration` screen — is the
story's own last administrative surface.

**Changed.** `AuditQueryService`: one read method, newest-first, filtered by actor/action/date
range, using the indexes data-model §6 already carries. `AuditController`: one route,
`GET /audit`, `RequireAdministrator` — **no write, update, patch or delete action exists**. Every
action `AuditAction` names already had a live call site before this task; Part B is a pure read
surface. Front end: `/admin/audit` (URL-bound filters, `p-paginator`, zero row actions) and
`/admin/configuration` (reads through the **existing** config clients, zero form elements). Full
by-layer detail: [implementation-progress.md](progress/implementation-progress.md) §7.

**One implementation choice recorded at the code:** the audit screen's `to` filter is inclusive of
the whole selected day — a UI detail, not a product question.

**Verified.** Build clean; suite **385 passing, 1 skipped** (12 this slice's own). Live: every
write verb `405` with the audit row count unchanged; a raw-JSON sweep found no `passwordHash` or
`storagePath`; driven live in a real browser (Playwright) against the running stack — both screens
render correctly, a non-Administrator deep-link redirects to `/403`. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**A bookkeeping gap in `PROJECT-PROGRESS.md` was found and disclosed, not fixed:** its narrative
already carried stories 08–11 as complete, but that never reached the headline percentage, the
story table or the evidence rows. See [overall-status.md](progress/overall-status.md) §1.1 for the
disclosure. **Nothing was invented to close that gap** — reconciling four other stories' tracking
is outside this story's scope.

**Not touched.** No design document. No entity, migration, endpoint or configuration key beyond
`GET /audit`. No change to Part A's contracts, confirmed by re-running `ConfigurationTierTests`
unchanged.

---

### 2026-08-31 — story 07: web form intake and in-portal messaging, delivered whole

**Phase 4 opens with the one real communication channel**, and the message model every other
channel plugs into. Delivered whole — ten plan tasks. An authenticated customer can submit through
the web form, customer and agent exchange replies, and the one status side effect in this API
fires: an inbound message on a `Pending` ticket returns it to `Open` in the same transaction.

**Changed.** `TicketMessage` (private setters, no mutator — §5 constraint 16), `MessageChannel`,
`MessageDirection`; `TicketActivity.MessagePosted` (a factory taking a message id and no activity
type, so §2.7's "set if and only if" is a signature, not a checked rule); `Ticket.MarkFirstResponded`
(the "set once" half lives on the entity). `TicketMessageService.PostAsync` is the one ingestion
method the staff endpoint, the portal endpoint and story 18's future adapter all share — it does
not branch on channel (architecture §5.2). `PortalTicketService.SubmitAsync` calls the extracted
`TicketService.CreateCoreAsync` rather than duplicating creation. `direction`/`channel` are
server-derived (PF-7) and refused in any request body (AP-10). No inbound HTTP endpoint (AP-11),
so PF-2 stays open for story 18. New `ticket-terminal` error slug. Front end: `MessageThread` as
one component in two configurations (staff/portal), `ReplyComposer` with one insertion point
(UI-7, A-8), a separate `PortalClient` sharing no DTO with the staff client. Nothing polls (T3-B).
Full by-layer detail: [implementation-progress.md](progress/implementation-progress.md) §7.

**Findings — I-25** (needs a decision: no document states the priority a customer-submitted
ticket is created with; implemented as `Medium`, a named constant), **I-26** (fixed: the seeded
demo thread was written into the future, sorting a reply above the question it answered — spread
changed from minutes to seconds), **I-27** (informational: `MessageChannel.WebForm` is written by
no path in this story; stands as declared seam surface for story 18). Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-08-31.** Suite **327 passed, 1 skipped** (+18 — the one skip is story 14's stub,
deliberately left for it to implement). The guard is load-bearing: hard-coding the PF-7 direction
derivation turned 4 tests red, each naming a different consequence. Live: R-13/R-14 proven by hand
(reply returns `statusChanged: true`, history shows both rows at the same timestamp, actor is the
replying customer); 28 published paths, no inbound route. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Deliberately not touched.** No story 13, 14, 18 or 09 work — portal list/detail/transition,
internal notes, the inbound adapter and the persistent notification publisher are all absent by
design. No design document edited. OQ-1 not answered.

---

### 2026-08-31 — story 06: ticket lifecycle, delivered whole

**Phase 3 closes**, and with it the core loop the assessment stands or falls on. Delivered whole —
eleven plan tasks. A ticket can now be created, scoped, assigned, moved through A-5's graph,
escalated, and read back with a full append-only history.

**Changed.** `TicketLifecycle` (A-5's graph, as data), `Escalation.RaiseOneLevel`,
`IllegalTransitionException`, and `Ticket.TransitionTo` — the only writer of `Status` in the
codebase. `TransitionAuthority` (A-16) kept structurally apart from legality, so the API's two
refusals are distinguishable: `403 transition-not-permitted` vs `409 illegal-transition` (AP-4,
AP-6). Escalation is its own endpoint (AP-7). `ApplyAutomaticCustomerReplyTransitionAsync` fires
only from `Pending`, attributed to the replying customer (R-13/R-14) — built with no caller yet;
story 07 supplies it. A-21 (escalation recipient cascade) is applied through the shared
`IEscalationRecipientPolicy`, not re-implemented. No migration needed — the lifecycle writes
columns the schema already carries. Three endpoints (`/transition`, `/escalate`, `/activity`).
Front end: `TransitionMenu`, `EscalateButton`, `shared/lifecycle/transition-matrix.ts` (F-1's one
file), the activity region, a populated customer timeline. Full by-layer detail:
[implementation-progress.md](progress/implementation-progress.md) §7.

**A contract gap was found and fixed while verifying:** `409 illegal-transition` initially carried
no `detail` (a Domain exception the handler didn't fill); the handler now admits it.

**Findings — I-22, I-23, I-24, all about the plan rather than the product** (I-23 closed
2026-09-01 by story 13). None needs a product decision. Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-08-31.** Suite **309 passed** (+50) — the full 6×6 legality matrix green. Front end
**32 specs** (+9). Live: `New → Closed` `409` with `allowedTransitions`; escalating a `Medium`
ticket raises priority without moving status or due timestamps; A-21's fallback rung exercised by
clearing a department's manager. The guard is load-bearing: removing the A-5 legality check turned
6 of 309 tests red. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Deliberately not touched.** No story 09 work (SLA sweep, `Notification` entity — the logging
publisher ships as specified). No story 13 work (no portal transition route, I-23). F-1 stays
open — `allowedTransitions` was not added to the payload. OQ-1 and product-scope §9 question 5
remain open.

---

### 2026-08-31 — OQ-3 answered: A-21, escalation climbs to the next authority level

**A decision, its documentation, and the one shared seam it needed. Story 06 has NOT been
started** — the policy below has no caller yet.

**Decision — R-21** (recorded as **A-21**): T2-D's escalation notifies "the department manager",
but that reference is optional (data-model §2.2), so OQ-3 asked what happens when it is unset.
**The product owner chose a cascade**: department manager (if set and still eligible) → every
active Manager → every active Administrator → nobody — and escalation is never blocked by a
missing recipient. Full rationale and rejected alternatives:
[resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).

**Code — one seam, ahead of both callers.** `IEscalationRecipientPolicy` /
`EscalationRecipientPolicy` in `Application/Modules/Sla/`, because the rule has two callers
(story 06's manual escalate, story 09's automatic sweep) and 00-implementation-plan §6's
one-implementation-per-shared-rule principle applies. The `Warning` log level (the two plans had
specified different levels for the same condition) now lives inside the policy, so the two paths
cannot diverge.

**Finding — I-21**: A-21's rung 1 doesn't literally cover a manager who is no longer eligible;
implemented as "set *and still eligible*", falling through otherwise — flagged for the user as a
one-line reversal if rung 1 was meant literally. Full write-up:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-08-31.** Suite **259 passed** (+8, all A-21's), every rung asserted including the
three the seeded demo cannot reach. The guard is load-bearing: reverting the fallback turned 6 of
8 tests red. Front end untouched. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Documents amended:** product-scope §7 (A-21), data-model §2.2/§8, api-design §5.6/§6.2,
ui-design §5.3/§11 (the escalate dialog may now say a manager will be notified). Traceability:
[decision-traceability.md](progress/open-questions-and-findings/decision-traceability.md).

**Deliberately not touched.** No contract surface — no endpoint, payload, field, slug, column or
migration. No story 06 work. OQ-1 and F-1 remain open, as does product-scope §9 question 5.

---

### 2026-08-31 — story 05: ticket core, delivered whole

**The second T1 business story to land end to end**, and the one the assessment's core loop rests
on. Delivered whole — thirteen plan tasks, no slice map. Code was committed as `a633923` with
tracking deliberately left behind; **this entry is that tracking**.

**Changed.** `Ticket`, `TicketActivity` and their enums; `Modules/Sla/SlaClock.cs`
(architecture §2.1 puts SLA target calculation in the Domain). `TicketScope` — the single
department-scoping helper every later ticket query composes (AD-5). `TicketService` with
category→department auto-assignment (A-14). `SlaClock.OnPriorityChanged` is a deliberate no-op
that `PatchAsync` still calls, so stories 06 and 09 share one seam rather than three call sites
(A-20). `Ticket` has no branch column and never may have one — derived `Ticket → Customer →
Branch` (A-2/T2-K). Out-of-scope tickets are `404`, not `403`, on read and write (AP-4). Seven
operations on `TicketsController`. Front end: the workspace ticket list/detail/assign control,
shared `StatusChip`/`PriorityChip`/`TicketFilterBar`. Full by-layer detail:
[implementation-progress.md](progress/implementation-progress.md) §7.

**Two audit findings closed**: S9-2 (ticket-attachment routes) and S9-5 (both SLA timestamps
required at creation).

**Findings — I-14 through I-20, seven in all, none resolved by invention. I-16 needs a user
decision** (no approved endpoint lets an Agent list their department's staff — kept as "Assign to
me" for everyone, a real picker for Administrators only). I-17 (a filtered-index predicate needed
two `<>` comparisons, not `NOT IN`, on SQL Server), I-18/I-19 (front-end defects found only by
driving the real screen), I-20 (a verification-plan wording issue, not a code defect — the intent
proven three stronger ways instead). Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified 2026-08-31.** Suite **251 passed** (+31, and the suite's one by-design skip is gone).
Live: an out-of-department ticket `404` on every verb; A-20 proven (`High → Urgent` moved neither
due timestamp); A-18 proven (assigning a `New` ticket left status `New`). Demo data restored and
re-confirmed. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Deliberately not touched.** No approved document edited — no new endpoint for I-16, plan
verification step 3's wording left as approved (I-20). Story 06's `TransitionTo`, the A-5/A-16
machinery, escalation and `/activity` are not here by design.

---

### 2026-08-30 — OQ-2 answered: A-20, the SLA due timestamps freeze

**A decision record only. No source code was written, no plan file was touched, no story was
started.**

**Decision — R-16** (recorded as **A-20**): on a priority change, do the SLA due timestamps
recompute or stay frozen? **The product owner chose freeze** — recompute was rejected because it
lets an escalation tighten a deadline retroactively, breaching a ticket as a consequence of the
escalation that caused it. Full rationale: [resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).

**No document needed a code-level change**: `api-design.md` and `ui-design.md` write the same
fields either way — ui-design §11 had already established there is no UI dependency. One visible
consequence worth knowing before story 05's queue is built: under freeze, `resolutionDueAt:asc`
does not re-order when a ticket is escalated.

**Tracking updated:** OQ-2 struck through in [open-questions.md](progress/open-questions-and-findings/open-questions.md), R-16 recorded in [resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).

**§9 question 5 (real SLA policy) is untouched and stays open** — A-20 settles only what happens
to two timestamps on a priority change.

**Deliberately not touched: the plan files.** Story 05's and story 09's ⚠ blocked-decision boxes
and `SlaClock.OnPriorityChanged`'s `NotImplementedException("OQ-2")` still read as blocked —
rewriting plans is separate approved work.

---

### 2026-08-30 — story 04 slice 6: the customer front end

Plan tasks 11–14 — story 04's last slice, making the `Customers` module reachable by a person
rather than only `curl`. **Front end only**: no backend file changed, `openapi/v1.json` still
lists 18 paths.

**Changed.** `core/api/customers.client.ts` and `attachments.client.ts` (no component calls
`HttpClient` directly — architecture §2.2). `UserSummary` landed in `identity.model.ts` per
api-design §6.1. The customer directory renders ui-design §5.4's five columns with `q`/`branchId`
in the URL (UI-9). A create dialog was added to the directory as a judgment call (neither task 12
nor ui-design §5.4 names one, but the plan's Done Criteria require it). The customer detail is
four independently-loading regions; both `409`s for a taken email render inline on that field. The
registration screen replaces story 02's placeholder — no branch or role selector, both fixed
server-side (A-15). Full by-layer detail: [implementation-progress.md](progress/implementation-progress.md) §7.

**Findings — I-12** (needs a decision: no configuration payload publishes the attachment size
cap) and **I-13** (informational: the plan's stated download mechanism was wrong about how the
bearer token reaches a bare URL; the client itself is exactly as specified). **I-11 closed and its
diagnosis corrected** — a `sqlcmd` session-setting artifact, not a schema defect. Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified against the running stack and driven through a real browser.** Upload/download
round-trips the same bytes; 18 of 18 CDP checks pass; Arabic sets `dir="rtl"` and this slice's CSS
mirrors, measured. The guard is load-bearing: reverting the multipart part name turned 1 of 23
specs red. Front-end suite **23 passing** (+7); backend **220 passing, 1 skipped — unchanged**,
which is the evidence no backend file moved. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**Deliberately not touched:** no backend file, no design document, no plan content beyond the
slice's status cell.

---

### 2026-08-28 — story 04 slice 5: customer self-registration

Plan task 7, deferred from Story 02 because registration is the first point at which a `Customer`,
a `Branch` and the configured default branch all exist (A-15). **Backend only.**

**Changed.** `RegisterRequest`, `AuthService.RegisterAsync`, `POST /auth/register` on
`AuthController`. A-15's three outcomes and only those three: new profile + login; agent-created
profile linked, not duplicated (keeps A-10 true); existing login → `409 user-already-exists`. One
`SaveChangesAsync` commits profile, login and audit entry together. `Location` targets
`/api/v1/auth/me` — the only route the new token may read the created identity through.

**Finding I-5 closed, by reading rather than deciding:** the default branch does not also set
`User.branchId` — four approved documents agree it stays null. Full write-up:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified against real SQL Server**, including case-insensitive linking (SQLite cannot test it).
Suite: **220 passing, 1 skipped** (+22). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-28 — cross-cutting: AP-2 for model-state errors, finding I-10 closed

**Not a story task.** The fix the investigation below proposed, approved and implemented same day.
**No endpoint added, no status code moved, front end deliberately untouched.**

**Changed.** `Errors/ModelStateProblemDetails.cs`: `AllowInputFormatterExceptionMessages = false`
(the framework's own leak control) plus an `InvalidModelStateResponseFactory` emitting the §6.12
envelope with the `validation-failed` slug, normalizing keys (`$.email` → `email`) and dropping the
JSON reader's spurious `request` entry. Nothing was invented — `error.interceptor.spec.ts` and
both i18n dictionaries already expected this exact shape; **the front end needed no change**.

**Verified live across all seven producers** against real SQL Server, leak-scanned on the raw
response text. The guard is load-bearing: removing the registration turns 36 of 43 tests in the
two error suites red. Suite: **198 passing, 1 skipped** (+27). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

**One deliberate non-change: `traceId`.** `AddProblemDetails()` adds it to every error alike, so it
is a uniform RFC 9457 extension member, not an I-10 deviation.

**A gap recorded, not folded in:** no component yet reads `problem.errors` for inline field
rendering — a front-end story's work.

---

### 2026-08-28 — investigation: I-10, the model-state `400` contract

**Investigation only. No code changed.** I-9 is approved and closed; this is the finding it
surfaced.

**The API had two `400` shapes for the same error class** — the approved §6.12 envelope from
`ValidationException`, and `[ApiController]`'s framework-default model-state response (a URL as
`type`, no `detail`, .NET internals in `errors`, inconsistent key casing). **User-visible**:
Transloco's missing-key fallback rendered the literal RFC URL as translation text. The expected
shape was already pinned twice — in `error.interceptor.spec.ts` and both i18n dictionaries — so
the server was the only non-conforming part.

**Recommendation: fix it now, before slice 5.** Full finding write-up:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).
✅ **Approved and implemented the same day** — see the cross-cutting entry above.

---

### 2026-08-28 — cross-cutting: AP-10 enforced, finding I-9 closed

**Not a story task.** A cross-cutting fix requested between story 04 slices 4 and 5. **No
endpoint added, no request/response model changed shape.**

**Changed.** One setting: `UnmappedMemberHandling.Disallow` in `Program.cs`'s `AddJsonOptions`.
AP-10 says a server-derived field in a request body must be `400`, not silently dropped;
`System.Text.Json` was silently dropping it. The blast radius is the six endpoints that bind a
JSON body, not all 23 routes. Three things deliberately did not move: query-string binding,
case-insensitive property matching, response serialization.

**Two findings came out of the fix, neither introduced by it — I-10 and I-11** (see the
investigation entry above and story 04 slice 6). Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).

**Verified live against real SQL Server, before and after.** Reverting the one line turns 12 of
16 new tests red. Suite: **171 passing, 1 skipped** (+16). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-28 — story 04 slice 4: the customer endpoints

Plan tasks 8 and 10. `CustomersController` (nine routes, `RequireAgent`) and
`AttachmentsController` (the AP-19 download, authorized by owner reachability, no role policy).
`openapi/v1.json` goes from 11 to 17 paths. `POST /auth/register` deliberately left `404` — it
needs slice 5's `RegisterAsync`.

**The `IFormFile` boundary is the upload action** — the resolution of finding I-6. `201` targets
were chosen, not defaulted, and recorded as a judgment call: `POST /customers` points at the
customer; `POST /customers/{id}/attachments` points at the download; `POST /customers/{id}/notes`
points at the collection, because §5.5 publishes no single-note endpoint and inventing one for a
`Location` target would be contract surface no requirement asks for (**AP-18**). A-19 proven live: patching a
customer's email moved their portal sign-in with it and wrote exactly one audit entry.

**Finding I-9 (pre-existing)** — AP-10 was not enforced; not fixed in this slice (cross-cutting),
closed same day in the entry above. Suite: **155 passing, 1 skipped** (+15). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-28 — cross-cutting: front-end HTTP error handling

**Not a story task.** Requested between story 04 slices 3 and 4, scoped to the front end's HTTP
layer. **No API contract changed.**

**Changed.** `errorInterceptor` now applies ui-design §9's cross-cutting status table: `401` ends
the session; `403` routes to `/403` without ending it; `5xx`/network failures raise one translated
toast; `400`/`409`/`413`/`422`/`404`/`503` pass through untouched for the feature to render inline.
**The `401` rule was tightened rather than rewritten:** a `401` arriving while the user is already
inside `/auth/*` now clears the session without navigating, rather than discarding a half-typed
sign-in form. PrimeNG's `MessageService`/`p-toast` — the first toast mechanism in the app — was added at the
root; it is transport-level only, not story 09's notification centre.

**Verified:** `error.interceptor.spec.ts`, 12 specs. Front end **16 passing** (+12). Backend
untouched: 140 passing, 1 skipped. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-28 — story 04 slice 3: notes, timeline, attachments and demo data

Plan tasks 4, 5, 6 and 9 — `CustomerNoteService`, `CustomerTimelineService`, `AttachmentService`,
`CustomerSeeder`. **No endpoint published yet** (that's task 8, slice 4).

Notes are immutable by construction (§5 constraint 16 — no update/delete method, not merely no
route). The timeline is a read projection against an empty ticket set, returning a well-formed
empty page today with a marker for story 06 to fill in. `AttachmentService`'s ticket half fails
closed through a seam story 05 completes. `CustomerSeeder` (`Order = 30`) seeds 4 customers, 2
with a portal login, so A-19 is demonstrable by hand.

**Four findings, none resolved by invention — I-5, I-6, I-7, I-8.** Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).
Suite: **140 passing, 1 skipped** (+26). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-27 — story 04 slice 2: `CustomerService` and the A-19 propagation

Plan task 3 only — the user narrowed this slice to the service; controllers moved to their own
slice (six slices now, not five). **No endpoint published yet.**

`CustomerService`'s four profile operations (api-design §5.5). **A-19 is the substance of this
slice** — `UpdateAsync` handles its six cases and writes one `UserEmailChanged` audit entry against
the linked user, in the same `SaveChangesAsync` that makes the propagation atomic. `User.ChangeEmail`
added (Story 02 deliberately omitted it) — this does not make email patchable through
`PATCH /users/{id}`, asserted by test. `openTicketCount` is a literal `0` with a `// Story 05:`
marker.

**Deliberately not built:** every service, controller and front-end file beyond `CustomerService`
itself — each verified absent. Suite: **114 passing, 1 skipped** (+24). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-27 — story 04 slice 1: customers domain and data layer

Plan tasks 1 and 2 only, at the user's instruction to deliver story 04 in slices with approval
between each.

**Changed.** Three Domain entities (`Customer`, `CustomerNote`, `Attachment`), each structural
invariant asserted by a test — `CustomerNote` has no public setter and no instance method at all;
`Attachment`'s two factories make the owner-XOR rule unconstructible to violate. Migration
`Customers`: `CK_Attachments_OwnerXor`, unique email index, and `FK_Users_Customers_CustomerId`
closing the DM-1 link story 02 left open. `IAttachmentStorage` seam (AD-11) — the on-disk name is
server-generated, proven against seven path-traversal attempts. One new configuration key,
`SupportCrm:Attachments:StorageRoot` (finding I-1 — see below).

**Deliberately not built:** every service, controller, `POST /auth/register`, seeder and front-end
file. The `UserEmailChanged` audit constant was **not** added — it belongs to task 3.

**Story 16 Part A is still uncommitted.** It was reported as committed before this slice began;
`git log` shows head `634bad2`, a docs commit, with none of Part A's files in `HEAD`. **Story 02's
`AddCustomerRoleUserAsync` test helper was updated, not worked around** — it now creates a real
`Customer` through the domain factory and links to it, rather than inserting a `Users` row with an
arbitrary `CustomerId`; five tests failed on the new FK before this change and pass after it.

**Findings I-1 and I-2**, both recorded rather than resolved by invention (a configuration key and
an index neither architecture §6.3 nor data-model §6 lists). Full write-ups:
[implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md).
Suite: **90 passing, 1 skipped** (+22). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-27 — story 16 Part A implemented

Tasks 1–5 only. Seven option types, `ConfigurationValidator`, `GET /config`, `GET /config/staff`.
**Part B untouched** — no audit endpoint, no admin screens.

**Startup validation is the acceptance criterion, proven by starting the real API** with each of
six checks broken in turn — every one stops the host with a named message. AP-17's audience split
(public/customer-safe/staff-only) is enforced and proven live (B-2's regression fix). OQ-1
untouched — validation checks `Min < Max` and nothing else, so any scale shape passes.

**One plan contradiction resolved without inventing anything:** `PriorityOptions.ApprovedLevels`
holds the A-6 names directly, because story 05's `TicketPriority` enum doesn't exist yet at this
point in execution order — marked at the code for story 05 to replace.

**Two implementation choices no document fixes, both recorded at the code:** list-bearing sections
bind through an `Items` (or `Levels`) property, because `AddOptions<T>().Bind()` binds a section to
an object and a bare JSON array is not one; and the attachment cap defaults to 10 MiB, a number no
document states.

Suite: **68 passing, 1 skipped** (36 new). Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-27 — OQ-5 answered: A-19, and the login change is audited

**Decision — R-15** (recorded as **A-19**): does changing `Customer.email` change a linked portal
login's sign-in email? **Yes, atomically** — one unit of work, existing uniqueness rule applies
across all users, a collision rejects both writes. Full rationale:
[resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).

**ui-design §11's held warning was released**: a persistent helper line on the email field,
unconditional (the `Customer` payload has no "has a login" field and none was added).

**Two pre-existing contradictions found and fixed**, unrelated to OQ-5: a stale A-10 cross-reference
in api-design §10.3, and an endpoint-count citation in ui-design.

**Amended same day, on the product owner's instruction: the propagated email change is audited** —
one new `UserEmailChanged` action constant, actor is the issuing agent, target is the linked
`User`, no address recorded (matching `UserRoleChanged`'s convention), written only on a real
change. Story 16's audit-coverage inventory gained the row.

**Nothing was implemented.** Story 04 remains Not Started.

---

### 2026-08-27 — story 03 completed

Tasks 4–8 implemented, closing the story and phase 1. `OrganizationQueryService`,
`DepartmentValidator`, `DepartmentsController`/`BranchesController` (one `GET` each, `RequireAgent`,
**no write verb on either** — T2-I). Front end: `organization.client.ts`,
`shared/components/department-filter/`. Both placeholders the previous entry flagged (free-text
department id, id-not-name in the avatar menu) are gone.

**One inherited defect found and fixed**: `CreateUserDialogComponent.departmentMissing` was a
`computed` over a plain field, so it never re-evaluated after one failed submit — now a signal.

**Two implementation choices not fixed by the approved documents, both following the `/users`
precedent:** the sort whitelist for the two organization endpoints is `name` only; the front-end
client requests `pageSize=100`, the contract's documented maximum, so more than 100 departments
would be truncated.

**Verification step 6 could not be run as written** (it names the story-05 ticket list) — verified
instead via `department-filter.component.spec.ts`; story 05 must run it against the real screen.
OQ-3 remains open.

Verified against real SQL Server on a wiped volume — full role matrix on both endpoints, a
department with a null manager serializes with no `managerUserId` key at all. Full evidence:
[verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).

---

### 2026-08-26 — story 02 implemented

**Story 02 (`auth-and-roles`) complete and verified.** Email + password sign-in, four hierarchical
roles as policies, per-request identity resolution, Administrator user management, one audit
recorder. Nine endpoints.

**AD-15 is enforced by shape, not discipline**: `ITokenIssuer.Issue(Guid userId)` carries no role/
department, and `CurrentUserMiddleware` re-reads all three from the authoritative row every
request — a user deactivated after their token was minted is `401` on the very next call. The
single `InitialSchema` migration (all four initial tables) was created here. Story 03 task 3
(`OrganizationSeeder`) was executed early, with the user's approval, because Story 02 cannot seed a
staff user without a department.

**Two contract defects found by this story's own verification, both fixed:** enums were
serializing as integers (api-design §2 forbids this); a successful sign-in recorded a null actor
(data-model §2.14 allows null for exactly one reason, and this wasn't it). **Two front-end bugs
found in the browser**: an `inject()` after `await` threw `NG0203`; a deep link to a guarded route
raced the bootstrap identity load.

**Deviations from the plan's letter, recorded rather than silently taken:** `UnauthorizedException`
added to the Story 01 exception family (401 had no member yet); `IApplicationDbContext` introduced
so Application can orchestrate persistence without naming `SupportCrmDbContext` (AD-3 still holds);
`ICurrentUser` gained `IsAuthenticated` for the audit recorder's actorless failed-sign-in case;
`IAuditRecorder` takes an `AuditOutcome` enum rather than a `string`.

**Three values no approved document fixes, placed in configuration and flagged:** JWT
`AccessTokenMinutes` (60), `Issuer`/`Audience` (`SupportCrm`), and the seeded demo password (a
development default in `appsettings.Development.json`; any other environment must supply it or
startup fails).

`POST /auth/register` remains deferred to Story 04 (S9-7) — its route is scaffolded with submit
disabled. **Nothing was committed** — the working tree was left for review.

---

### 2026-08-26 — string-length convention closed

**[data-model.md](data-model.md) §6.1 added** — string column length, collation and index
eligibility. Closes the finding story 03 raised: no approved document stated a string length
anywhere, yet the model declares four unique indexes on string columns, and SQL Server cannot
build one over `nvarchar(max)`.

**Five tiers** — `Code` 64 · `Name` 200 · `Email` 256 · `Line` 512 · `Text` max — assigned to all
39 string fields in §2, verified mechanically (39 assigned, none stale). The index-key rule (a
column in an index key must be `Code`, `Name` or `Email`) checked against every index the model
requires. **Collation decided**: `User.email`/`Customer.email` declare
`SQL_Latin1_General_CP1_CI_AS` explicitly, because "same address, same case-insensitivity" is a
product rule (A-9, A-10) and must not depend on a server default. Three tier choices carry a
stated reason where the obvious pick was wrong (`Attachment.contentType`, `AuditEntry.actorDescriptor`,
`User.passwordHash`).

**This amends the document's own scope**, recorded rather than slipped in — the header and the §8
gate row previously said physical lengths were out of scope. `00-implementation-plan.md` §6 now
points at §6.1. **No migration was created and story 02 was not started.**

---

### 2026-08-26 (later) — story 03 data layer

**Story 03 tasks 1–2 implemented** — `Department` and `Branch` entities, EF configuration, `DbSet`s
— the prerequisite S9-12 names for Story 02's `POST /users`. Nothing else of story 03 was built.

**The `InitialSchema` migration was deliberately *not* generated**, and the `OrganizationSeeder`
deliberately not written — both plans place the migration after story 02 task 3 so one migration
creates all four tables; this is a sequencing consequence of the approved plans, not a deviation.
Schema verified without committing a migration: a throwaway migration was generated, read, and
reverted. `Department.ManagerUserId` carries no foreign key (the real eligibility rule is
cross-row and conditional) — reasoning is in `DepartmentConfiguration`.

**Implementation choice flagged because no document fixes it:** entity name columns are
`nvarchar(200)` — resolved by the string-length convention entry above. Added
`backend/dotnet-tools.json` pinning `dotnet-ef` **10.0.11**. **OQ-3 remains open and unanswered.**

---

### 2026-08-26

- **Stage 10 begins — story 01 (`solution-skeleton`) implemented and verified.** The five-project
  backend solution, the Angular + PrimeNG front end, and the three-service Compose stack all exist
  and run. Two endpoints only, no business entity, screen or endpoint beyond them. Evidence:
  [verification/earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md).
- **Front end built on the PrimeNG Sakai template, tag `20.0.0`**, not scaffolded from scratch —
  superseding task 8 of the story-01 plan, taken on explicit user instruction. Angular/PrimeNG
  pinned to Sakai's own lockfile resolutions (20.1.2 / 20.0.0 / 1.2.1) rather than the plan's looser
  range; Sakai master (21.0.0) was considered and rejected as contradicting the approved plan.
- **Story 17 Part A delivered here**, per the split-in-time exception in
  [story-backlog.md](story-backlog.md): Transloco, en/ar dictionaries, `DirectionService`, the
  language switcher, the logical-property stylelint rule, three breakpoint mixins.
- **Contract correction — lowercase OpenAPI paths**, fixed with a route-token transformer rather
  than `RouteOptions.LowercaseUrls` (the latter only affects generated links, not the published
  route template).
- **Bug found and fixed during UI verification**: an i18n key doubled as both a label and a
  namespace prefix; split, and a key-parity check now runs on every edit.
- **Four further deviations, all deliberate and recorded** in
  [.squad/plans/platform-foundation/00-overview.md](../.squad/plans/platform-foundation/00-overview.md):
  stylelint scoped away from vendored Sakai stylesheets; `frontend/proxy.conf.json` added; the
  temporary health screen sits in `features/platform/`; `.dockerignore` added.
- **No migration was generated, by design** — story 01 introduces no entity.
- **Recalculated §1.1**: delivery moves from 0 of 18 to 1 of 18. Current figure and method:
  [overall-status.md](progress/overall-status.md), [progress-calculation.md](progress/progress-calculation.md).
- **Stopped at the story-01 boundary** pending explicit approval before story 02.

---

### 2026-08-25

- **Stage 9 complete — the implementation plans exist.** Eighteen `NN-story-*.md` plan files, one
  per story, plus `.squad/plans/00-implementation-plan.md` (14 workstreams, 9 phases, the
  dependency graph — EF Core migrations as the serialization constraint). `squad status`: 18
  stories, 18 plans; `squad doctor`: 6 ok, 0 fail.
- **A full traceability and consistency audit across every approved document and all 18 intakes —
  13 findings, S9-1…S9-13.** Four block a named acceptance criterion (**S9-1** — no dashboard-task
  endpoint; **S9-4** — no field carries AI-suggestion acceptance to history; **S9-9/PF-4** —
  "tickets assigned" left unpinned; **S9-10/PF-2** — the inbound channel adapter has no actor).
  Nine non-blocking findings resolved from the approved documents. **None was resolved by
  invention.** Full register: [design-stage-findings.md](progress/open-questions-and-findings/design-stage-findings.md).
- **`sdd-workflow.md` and `story-backlog.md` updated** — stage 9 marked complete, the S9-8
  execution-order correction, the story-state table. **Overall progress 31% → 35%.**
- **Closed N-1 — the API payload catalogue.** `api-design.md` §6 grew from 5 payload shapes to 29.
  **A genuine contradiction surfaced and was fixed (F-2)**: the promised attachment-download
  endpoint didn't exist in the catalogue; added as **AP-19**. OQ-1, PF-4 and PF-5 were **not**
  resolved by this pass. *Files:* `docs/api-design.md`.
- **Completed Stage 8 — UI Design.** `docs/ui-design.md`: 24 screens across four surfaces, twelve
  UI decisions (UI-1…UI-12). Lifecycle rules honoured visibly (A-5, A-16, R-13, R-14). **One
  finding, F-1 (non-blocking)**: no `allowedTransitions` field, so the transition menu duplicates
  the authority matrix client-side. Gate 8 → 9 met.
- **Stage 7 post-flight review — two blocking defects found and closed.** **B-1** — the feedback
  contract depended on a configuration key architecture §6.3 didn't have; added, values left
  undecided (OQ-1 stays open). **B-2** — `GET /config` handed staff-only data to every
  authenticated caller; split into three audience tiers as **AP-17**. Three non-blocking items also
  closed (N-2, N-3/AP-18, N-4). **One new open question, OQ-5.** Endpoint count 66 → 65.

---

### 2026-08-24

- **Completed Stage 7 — API Design.** `docs/api-design.md`: 66 endpoints across 12 modules. Role
  gate on every endpoint; department scoping as `404` not `403` (AP-4); a separate `/portal` path
  space (AP-5); the automatic `Pending → Open` transition as the one status side effect (§5.7).
  Sixteen decisions, AP-1…AP-16. **No business rule was invented.** PF-2 is avoided rather than
  solved (no inbound-channel endpoint, so no contract needs a system actor). Gate 7 → 8 met.
- **Approved the actor attribution for the automatic `Pending → Open` transition**: the replying
  customer, `actorKind = User`, never `System` — recorded as a rule rather than applied silently.
  No new entity or field.
- **Resolved PF-1 — a customer reply reopens a `Pending` ticket, decided as R-13/R-14.** The
  transition is legal and fires automatically, attributed to the replying customer. Consistency
  re-checked across the transition graph, A-16, notifications and SLA behaviour. Full rationale:
  [resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).
- **Ran the Stage 7 pre-flight audit** — 🔴 NOT READY on PF-1 (above), six non-blocking findings,
  three cosmetic. Full register: [design-stage-findings.md](progress/open-questions-and-findings/design-stage-findings.md).
- **Resolved the `CustomerFeedback` module-ownership mismatch — decided as DM-7/R-12.** The
  existing `Tickets` module owns it; no new module. `Notification`'s stray "SLA" label corrected to
  `Sla` while there.
- **Corrected loose wording in `architecture.md` §1** about feature slugs vs. backend modules
  (fourteen slugs, ten with a module) — documentation wording only.
- **Audited this tracker against every project document** — six discrepancies found and fixed, all
  in this file (stale counts, a miscounted commit row, a repeated feature-count error).
- **Corrected the cancel authority and resolved OQ-4 — decided as A-18/R-11.** Agents and Managers
  may cancel; customers only while `New`. A contradiction between A-5's "New = unassigned" and
  A-18's "may be assigned while New" was found and both texts rewritten.
- **Answered four blocking business decisions ahead of Stage 7 — A-14…A-17**: category→department
  routing, self-registration branch/linking, the transition authority matrix (manual-only
  closure), and the `isUrgent` input. Full rationale for each:
  [resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md).
- **Opened OQ-4** — the boundary of "before work begins" for customer cancellation (resolved same
  day, above).
- **Reviewed all design documents for contract-blocking ambiguities** before Stage 7 — four
  blockers (above), one partial (OQ-1), eight non-blocking items designed around.
- **Created this progress tracker.**
- **Clarified four points in the data model without changing it**: branch derivation stated
  explicitly; the CSAT 1–5 assumption withdrawn (reopened as OQ-1); SLA recompute-vs-freeze left
  undecided (opened as OQ-2); missing-department-manager behaviour left undecided, no fallback
  invented (opened as OQ-3).
- **Completed Stage 6 — Data Model.** 15 entities, six ownership decisions (DM-1…DM-6), 14
  candidate entities explicitly not modelled. Gate 6 → 7 met.
- **Corrected the JWT authorization design — decided as R-1.** The token asserts identity only;
  role/department/active status resolve per request from the authoritative record (AD-7 amended,
  AD-15 added). Gate 5 → 6 re-verified.
- **Completed Stage 5 — Architecture.** Layered modular monolith, front-end structure, three
  enforcement points, department scoping mechanism, three integration seams, 14 decisions.
- **Completed Stages 2–4 — Product Scope, Story Backlog, Squad Kit.** Tiered scope with 13
  assumptions and 7 open questions; squad-kit v0.2.0; 18 story intakes across 14 feature slugs.
- **Stage 1 — Requirements received** and analysed (functional and non-functional requirements,
  actors, workflows, ambiguities, assumptions). Analysis delivered in conversation and distilled
  into [requirements.md](requirements.md).
