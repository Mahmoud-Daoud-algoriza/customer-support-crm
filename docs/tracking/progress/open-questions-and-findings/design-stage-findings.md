[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

### 6.5 Pre-flight findings carried forward (non-blocking)

Raised by the Stage 7 pre-flight audit on 2026-08-24. None blocks an API contract; each is handled
inside the API document or deferred to the story named.

| ID | Finding | Handle in |
|---|---|---|
| **PF-2** | `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required with no System actor, but story 18's inbound fake adapter creates tickets with no human actor | **Avoided in Stage 7** (AP-11 publishes no ingestion endpoint); **still open, and now scheduled: it blocks story 18 AC 3** (**S9-10**). Three options are set out in [18-story-channel-erp-adapters.md](../.squad/plans/integration-seams/18-story-channel-erp-adapters.md); **none is chosen**. The ingestion implementation throws with the reason and its test is skipped with the reason |
| **PF-3** | OQ-1 leaves the CSAT scale undecided and `architecture.md` §6.3 had **no** configuration key for it | ✅ **Closed 2026-08-25** — the `Feedback rating scale` key is now an approved entry in architecture §6.3, with its **values deliberately undecided** because OQ-1 remains open. `GET /config` publishes it; the feedback endpoint validates against it |
| ~~**PF-4**~~ | ~~"Tickets assigned" in the agent-performance metric is undefined — currently assigned vs. ever assigned. Same response shape either way~~ | ✅ **CLOSED 2026-09-06 by the product owner — see R-18 in §6.3.** It means **currently assigned**. *(Previously recorded as: still open, and it blocks story 15's `agentPerformance.assignedCount`* (**S9-9**). [api-design.md](api-design.md) §9 item 2 required it to be pinned **before story 15 was planned**; it was not, so the plan isolates the decision to `AgentPerformanceQuery.AssignedCount`, which throws until it is recorded. **The label stays exactly as T2-G words it, with no clarifying tooltip**.) **The label rule survived the decision** and is honoured: the column reads *"Tickets assigned"* with no clarifying tooltip, because that is the wording T2-G fixes regardless of which reading it carries |
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
| **S9-1** | **The dashboard task region has no endpoint.** [ui-design.md](ui-design.md) §5.1 and §13, story 14's own AC, and [data-model.md](data-model.md) §6's index `TicketTask(assignedUserId, isDone, dueAt)` *"Open and overdue tasks on the dashboard"* all assume a **cross-ticket** task list. [api-design.md](api-design.md) §5.6 publishes only ticket-scoped task endpoints | Story 14 AC 2; story 08's queue region | 🔴 **Contradiction — NOW DUE AND BLOCKING (2026-09-06).** Story 14 was implemented up to it and **stopped**: plan task 8 is not built, `TicketTaskService.ListForUserAsync` is written, marked `// S9-1: no endpoint publishes this. Do not add a route until the decision is recorded.` and **referenced by no controller**, and story 08's marker at `agent-queue.component.ts:175` is untouched. **Story 14's AC 2 is open and the story cannot close it without this decision.** Either publish `GET /tasks?assignedUserId=me&isDone=false` (**Option A** — a Stage 7 change, 66 → 67; the data model's index already supports it and the query already exists) **or** drop the region (**Option B** — a Stage 8 change to ui-design §5.1 and §13, recorded per product-scope §10, deleting the marker and the method). **No endpoint was invented.** The decision is the user's |
| **S9-4** | **No contract path records AI suggestion acceptance or override.** Story 11's AC requires it, [data-model.md](data-model.md) §2.7 provides `AiSuggestionOffered`/`AiSuggestionResolved`, and [api-design.md](api-design.md) §5.8 says it happens *"when the agent saves the ticket"* — but `POST /tickets` accepts **no field** that could carry it, §6.11 adds none, and §7 lists none | Story 11 AC 4 | 🔴 **Contradiction.** Either add a request field or a recording endpoint. Both are Stage 7 changes |
| ~~**S9-9**~~ | **PF-4 was required to be pinned *before* story 15 was planned** ([api-design.md](api-design.md) §9 item 2) and was not | ~~Story 15 `agentPerformance.assignedCount`~~ | ✅ **CLOSED 2026-09-06 (R-18).** The process gap was real and is recorded, but it cost nothing in the end: the decision was taken **before** story 15 was implemented, so the method was written once, correctly, and never throws. **One correction to how this finding was scoped:** it was recorded as *"isolated to one method"*, which is true of **where the decision lives** but not of **what it blocked** — `assignedCount` is composed into the single §6.8 response, so a throwing method would have made every `GET /reports/dashboard` a `500`. PF-4 gated the whole endpoint, not one field |
| **S9-10** | **PF-2 comes due in story 18.** The fake inbound adapter has **no actor**, while `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required and `actorKind = System` is reserved for the SLA monitor (R-14) | Story 18 AC 3 | 🟠 **Carried finding, now scheduled.** Three options set out; none chosen |

**Non-blocking — resolved from the approved documents, recorded for visibility**

| ID | Finding | Resolution |
|---|---|---|
| **S9-2** | `GET`/`POST /tickets/{id}/attachments` is assigned to **no story** in [api-design.md](api-design.md) §10.1, though §5.6 lists both and story 04's AC requires them | Shared service and the AP-19 download in story 04; the two ticket-scoped endpoints in story 05. Read from the intakes |
| **S9-3** | **OQ-2 and OQ-3 reach further than §6.1 records.** OQ-2's first code path is story 05's `PATCH` priority, not story 09's escalation. OQ-3 reaches story 06's **manual** escalation | ✅ **Both halves are now answered, and the widening is what made each answer due when it was.** §6.1 was updated at the time; **OQ-2 closed 2026-08-30 (A-20, R-16)** and was implemented by story 05, **OQ-3 closed 2026-08-31 (A-21, R-17)** before story 06 began. The finding did its job: each question was decided at the story that first reached it, not at the story §6.1 had filed it against |
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

