# Company Scope Progress

**What this document is.** A read-only audit that measures the **company's requested CRM scope**
against **what is actually in this repository** — not against what the approved documents plan, and
not against what the tracker reports. It exists to make one distinction visible:

> what the company asked for · what the approved project scope committed to · what is actually built
> · what remains deliberately outside the current scope.

**It defines nothing.** The authority order of [CLAUDE.md](../CLAUDE.md) §1 governs; this file sits
below [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md) as reporting, and it reports a different axis:
company requirement coverage rather than SDD stage state.

**Audit date:** 2026-09-07 · **Second-pass audit**, re-derived from the company PDF ·
**Commit audited:** `1e0c179` (code unchanged since `8d449bf`, *feat: story 18 — channel and ERP
integration seams*) · **Working tree:** clean.

> **Pass 1** (2026-09-07) was derived from [docs/requirements.md](requirements.md) because the
> company PDF had not been supplied. **Pass 2** — this version — was re-derived from the PDF itself.
> What changed is recorded in *[What the second pass changed](#what-the-second-pass-changed)* at the
> end. **No percentage moved**, and the section explains exactly why.

---

## ✅ Source of truth — the company PDF

**Authoritative company source:** `azm_squad_customer_support_crm.pdf` — *"Customer Support CRM .
Core Features"*, **2 pages**, supplied by the company. This PDF, not `requirements.md` and not any
approved project document, is the authority for what the company asked for.

### What it contains

**12 numbered areas and 55 bullets. Nothing else.** Page 1 carries areas 1–6, page 2 carries areas
7–12. Each area is a heading followed by a flat bullet list of feature names. There is no other
content on either page beyond the company logo and a rule below the title.

### Verified against the repository's recorded input

`requirements.md` is the project's transcription of this PDF, recorded under CLAUDE.md §1 as *"the
given input · never edited."* This audit checked that transcription rather than assuming it:

| Check | Result |
|---|---|
| Areas | **12 = 12** ✅ |
| Bullets | **55 = 55** ✅ |
| Title, including its unusual `"CRM . Core Features"` spacing | Preserved exactly ✅ |
| Full text comparison (`diff`, line endings normalized) | **Zero differences** ✅ |
| MD5 of both normalized texts | `082aba3be21050ecc8527baea78904a8` — **identical** ✅ |

**`requirements.md` is a faithful, complete transcription of the company PDF.** Pass 1 was therefore
built on the correct scope by accident of an accurate transcription, and its requirement inventory
needed no correction.

### What the PDF does *not* contain — and why it matters

This is the most consequential finding of the second pass. The PDF carries **no**:

- sub-requirements beneath the 55 bullets
- acceptance criteria or definitions of done
- constraints, non-functional requirements or quality targets
- priorities, phasing, MoSCoW markers, or any "must / should / could"
- feature descriptions, narrative or glossary
- volumes, SLAs, integration targets, or named external systems

**Consequence — read this before reading any tier label below.** Every T1/T2/T3/T4 tier assignment,
every simplification (*"round-robin only"*, *"keyword search"*, *"in-app notifications only"*,
*"branch is an attribute, not a boundary"*), and every one of the 21 assumptions **A-1…A-21** is the
**project's own interpretation of a one-line feature name** — not something the company specified,
and not something the company is recorded anywhere as having agreed to.

That cuts both ways, and this document reports both directions:

- **In the project's favour** — nothing in the PDF contradicts any tier assignment, because the PDF
  says nothing about depth at all. The interpretations are defensible.
- **Against it** — *"Outside current approved scope"* below means **the supplier decided not to
  build it**, and carries **no evidence of company agreement**. A bullet reading `- WhatsApp` is,
  on its face, a request for WhatsApp. This audit therefore never reports a T3 seam as satisfying
  the company bullet it stands behind.

**The 55 bullets are the whole company specification.** Any acceptance criterion cited anywhere in
this repository originates with the project, not the company.

### Why 56 scored lines, not 55

One PDF bullet — §1 *"Notes and attachments"* — names **two** capabilities and is split by
[product-scope.md](product-scope.md) §6 into two independently-tiered lines (Notes = T1,
Attachments = T2). Scoring them together would force one score onto two differently-delivered
things. That gives the **56 requirement lines** this audit scores, matching product-scope's own
count: *"all 56 requirement lines are addressed in some tier."*

**This is the only place where a company bullet was subdivided**, it is a split rather than an
addition, and no requirement was invented: 55 company bullets → 56 scored lines, and the mapping is
one-to-one everywhere else.

---

## Status definitions

Used consistently throughout. A line carries exactly one **Status**; the cross-cutting flags
(*Blocked*, *Implemented but not verified*) may additionally apply.

| Status | Definition | Count |
|---|---|---:|
| ✅ **Fully implemented** | The required behaviour exists in the running application and every layer the requirement needs is implemented. `Overall = 100%`. | **44** |
| 🟡 **Partially implemented** | Some required behaviour exists, but one or more meaningful parts of the company bullet are missing. `0% < Overall < 100%`. | **12** |
| ⬜ **Not implemented** | The requirement is inside the approved scope and has no meaningful implementation evidence. `Overall = 0%`. | **0** |
| 📐 **Outside current approved scope** | The company requested it; the approved project scope (product-scope §5, T3) explicitly declines to build the full feature and commits only to a seam plus a runnable fake. **A supplier-side decision — not a company agreement, and not a development failure.** Always *also* carries a Partial or Fully status reflecting what was actually delivered. | **10** |
| ⛔ **Blocked** | Completion is prevented by a real, already-recorded, unresolved project decision. Not a synonym for "unfinished". | **6** |
| 🔵 **Implemented but not verified** | Implementation evidence exists and automated checks pass, but a required verification — visual/screen checks in particular — has not been performed or recorded. | **14** |

**Where each label appears.** The **Status** column of every traceability table carries the primary
status plus the `📐` and `⛔` flags, because those change how the row should be read. The `🔵`
verification flag lives in that table's **Verification** column instead, so it is not duplicated —
look there for `⚠ Partial` or `🔵 Unrecorded`.

**A seam is never counted as the integration it stands behind.** `IOutboundChannelAdapter` plus a
console adapter is scored as a seam (50% backend), never as email.

**Documentation never raises an implementation score.** SDD/Docs is reported as its own dimension and
is excluded from Overall by construction — see the methodology.

---

## Overall Project Progress

```
Overall:              ██████████████████░░  88%
```

| Dimension | Progress | |
|---|---|---:|
| **Frontend** | `█████████████████░░░` | **84%** |
| **Backend / API** | `██████████████████░░` | **90%** |
| **Database** | `██████████████████░░` | **91%** |
| **User Stories** | `███████████████████░` | **96%** |
| **SDD / Documentation** | `███████████████████░` | **95%** |
| **Overall (delivered)** | `██████████████████░░` | **88%** |

**Read the gap between the columns.** SDD (95%) and User Stories (96%) run ahead of Frontend (84%)
because design and planning are complete for everything, *including the parts that were deliberately
never built*. **Overall is computed from the implementation dimensions only** — documentation and
story coverage are reported but excluded from it, precisely so that paperwork cannot inflate it.

---

## Area dashboard

| # | Area | Overall | Frontend | Backend | Database | User Stories | SDD/Docs |
|---|---|---:|---:|---:|---:|---:|---:|
| 1 | Customer Management | **98%** | 95% | 100% | 100% | 100% | 100% |
| 2 | Ticket Management | **100%** | 100% | 100% | 100% | 100% | 100% |
| 3 | Communication Channels | **53%** | 30% | 60% | 70% | 80% | 85% |
| 4 | Agent Dashboard | **93%** | 90% | 90% | 100% | 95% | 95% |
| 5 | SLA & Automation | **100%** | 100% | 100% | 100% | 100% | 100% |
| 6 | Knowledge Base | **100%** | 100% | 100% | 100% | 100% | 100% |
| 7 | AI Features | **75%** | 75% | 75% | 58% | 85% | 95% |
| 8 | Customer Portal | **100%** | 100% | 100% | 100% | 100% | 100% |
| 9 | Reports & Management | **100%** | 100% | 100% | 100% | 100% | 100% |
| 10 | Security & Administration | **100%** | 100% | 100% | 100% | 100% | 100% |
| 11 | Integrations | **50%** | 25% | 63% | 50% | 94% | 81% |
| 12 | Platform | **90%** | 90% | 88% | 100% | 100% | 85% |
| | **Project (56 requirement lines)** | **88%** | **84%** | **90%** | **91%** | **96%** | **95%** |

```
 1  Customer Management     ███████████████████░   98%
 2  Ticket Management       ████████████████████  100%
 3  Communication Channels  ███████████░░░░░░░░░   53%
 4  Agent Dashboard         ███████████████████░   93%
 5  SLA & Automation        ████████████████████  100%
 6  Knowledge Base          ████████████████████  100%
 7  AI Features             ███████████████░░░░░   75%
 8  Customer Portal         ████████████████████  100%
 9  Reports & Management    ████████████████████  100%
10  Security & Admin        ████████████████████  100%
11  Integrations            ██████████░░░░░░░░░░   50%
12  Platform                ██████████████████░░   90%
```

---

## Summary by status

| Status | Count | Share of 56 |
|---|---:|---:|
| ✅ **Complete** — delivered end to end, evidence in code and passing checks | **44** | 79% |
| 🟡 **Partially delivered** — real delivered surface, a named part absent | **12** | 21% |
| ⬜ **Not implemented** — no delivered surface at all | **0** | 0% |
| ⛔ **Blocked** — work stopped on a recorded, undecided question | **6** | 11% |
| 🔵 **Implemented, verification pending** — built, but a required check has not been run | **14** | 25% |
| 📐 **Carries a deliberately-unbuilt T3 portion** (*outside current approved scope*) | **10** | 18% |

The last three rows **cross-cut** the first two rather than extending them. Complete + Partial +
Not implemented = 56 exactly.

**No company requirement line scores 0% Overall.** Every one of the 56 has at least a seam, a
runnable fake, or a persisted model behind it. That is the most important single number here, and it
is why *53%* for Communication Channels means *"a working web form plus a real adapter seam"* rather
than *"nothing."*

---

## Scoring methodology — reproducible, not subjective

### The unit of measurement

The **requirement line** (56 of them), never the area. Areas hold 4 or 5 lines each, so averaging
areas would silently weight a 4-line area's lines 25% more heavily than a 5-line area's. Every
project-level figure in this document is a **mean over the 56 lines**, not a mean over the 12 areas.

### The scale — six values, no free-hand percentages

Each requirement line is scored on **six dimensions** using exactly these values and no others:

| Score | Meaning |
|---:|---|
| **100%** | Delivered, and exercised by a check that passes today |
| **75%** | Delivered with one named, specific part absent |
| **50%** | The T3 contract only — a real interface plus a runnable fake, no behaviour; **or** half the named sub-parts |
| **25%** | Named in the code as an extension point, with no code path that does the thing |
| **0%** | Absent |
| **n/a** | The approved design assigns this requirement no surface in this layer — *excluded from that dimension's mean; never counted as 0, never as 100* |

`n/a` is used sparingly and always with a stated, already-decided reason: AI summaries persist
nothing (**R-3**), quick replies are configuration rather than a table (**data-model §2.16**), the
public API needs no table of its own.

### How Overall is derived

```
Overall(requirement) = mean( Frontend, Backend, Database )      ← n/a excluded
Overall(area)        = mean( Overall of its requirement lines )
Overall(project)     = mean( Overall of all 56 requirement lines )
```

**User Stories and SDD/Docs are reported but never enter Overall.** This is the rule that enforces
the audit's premise — *a requirement that exists only in documentation is not implemented*. A line
with a written story, an approved data model, a complete plan and no code scores **0% Overall** and
**100% SDD**, and the two columns sitting side by side is the finding.

The three implementation dimensions are unweighted against each other, which encodes the other three
rules directly:

- a backend endpoint with no front end → Backend 100, Frontend 0 → **Overall 50, not 100**
- a UI screen with no backend behaviour → the mirror image, same result
- a table with no application behaviour → Database 100, the rest 0 → **Overall 33**

### Dimension definitions

| Dimension | Scored against |
|---|---|
| **Frontend** | An Angular route, component or shared control a user can actually reach |
| **Backend** | A controller action, Application service or hosted service that performs the behaviour |
| **Database** | An EF Core entity, its configuration, and an applied migration |
| **User Stories** | A **real story id** from [story-backlog.md](story-backlog.md) whose commit has landed **and** whose acceptance criteria are met — an unmet AC costs 25 points |
| **SDD/Docs** | The line is traced product-scope → architecture → data-model → api-design → ui-design → plan, **and** its delivery is recorded in the tracking documents — an unrecorded delivery costs 25 points |

### Known limitation of the method

The 56 lines are treated as **equal weight**. The company scope states no relative priority, and
inventing one would reintroduce exactly the subjectivity this section removes. The consequence is
explicit: *"AI chatbot"* and *"Contact details"* each count for 1/56 of the project figure, though
they are plainly not equal engineering. The per-area tables below carry enough detail to re-weight
from, should a reader want to.

---

## Tier legend

The approved project scope assigns every requirement line to a tier
([product-scope.md](product-scope.md) §2). Tiers are **not** progress — they are the commitment that
was made, and they are what separates *"not delivered"* from *"deliberately not delivered."*

| Tier | Commitment | Lines |
|---|---|---:|
| **T1** | MVP — must genuinely work end to end | 18 |
| **T2** | Simplified but real — happy path, minimal options | 28 |
| **T3** | Architecture/future — a real seam plus a runnable fake, **not** the feature | 10 |
| **T4** | Out of scope — not built, not stubbed, not designed | 0 lines (T4 exclusions sit *outside* the 56) |

---

# Area 1 · Customer Management — 98%

```
Overall:      ███████████████████░   98%
Frontend:     ███████████████████░   95%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Customer profiles | T1 | Yes — in full | **04** `customer-records` | Done | Done | Done | Done | ✅ Verified 2026-08-30 | ✅ Fully implemented |
| Contact details | T1 | Yes — in full | **04** | Done | Done | Done | Done | ✅ Verified 2026-08-30 | ✅ Fully implemented |
| Interaction history | T1 | Yes — in full | **04** + **06** | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |
| Notes | T1 | Yes — in full | **04** | Done | Done | Done | Done | ✅ Verified 2026-08-30 | ✅ Fully implemented |
| Attachments | T2 | Yes — simplified | **04** (+ **05** ticket-scoped) | **Partial** | Done | Done | Done | ✅ Verified 2026-08-30 | 🟡 Partially implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Customer profiles | 100 | 100 | 100 | 100 | 100 | **100** |
| Contact details | 100 | 100 | 100 | 100 | 100 | **100** |
| Interaction history | 100 | 100 | 100 | 100 | 100 | **100** |
| Notes | 100 | 100 | 100 | 100 | 100 | **100** |
| Attachments | 75 | 100 | 100 | 100 | 100 | **92** |
| **Area** | **95** | **100** | **100** | **100** | **100** | **98** |

### Evidence

- **Profiles / contact details** — entity [`Customer.cs`](../backend/src/SupportCrm.Domain/Modules/Customers/Customer.cs); service [`CustomerService.cs`](../backend/src/SupportCrm.Application/Modules/Customers/CustomerService.cs); endpoints `GET /customers`, `GET /customers/{id}`, `POST /customers`, `PATCH /customers/{id}` in [`CustomersController.cs`](../backend/src/SupportCrm.Api/Controllers/CustomersController.cs); screens [`customer-directory.component.ts`](../frontend/src/app/features/workspace/customers/customer-directory.component.ts), [`customer-detail.component.ts`](../frontend/src/app/features/workspace/customers/customer-detail.component.ts), [`create-customer-dialog.component.ts`](../frontend/src/app/features/workspace/customers/create-customer-dialog.component.ts); routes `/workspace/customers`, `/workspace/customers/:id`.
- **Interaction history** — [`CustomerTimelineService.cs`](../backend/src/SupportCrm.Application/Modules/Customers/CustomerTimelineService.cs), `GET /customers/{id}/timeline`. Story 06 is recorded as *"completes story 04's interaction-timeline criterion"* (PROJECT-PROGRESS §3), because the timeline is assembled from ticket activity that story 06 finished writing.
- **Notes** — [`CustomerNote.cs`](../backend/src/SupportCrm.Domain/Modules/Customers/CustomerNote.cs), [`CustomerNoteService.cs`](../backend/src/SupportCrm.Application/Modules/Customers/CustomerNoteService.cs), `GET`/`POST /customers/{id}/notes`. Test: `CustomerNotesAndTimelineTests.cs`.
- **Attachments** — [`Attachment.cs`](../backend/src/SupportCrm.Domain/Modules/Customers/Attachment.cs), [`AttachmentService.cs`](../backend/src/SupportCrm.Application/Modules/Customers/AttachmentService.cs), [`LocalDiskAttachmentStorage.cs`](../backend/src/SupportCrm.Infrastructure/Storage/LocalDiskAttachmentStorage.cs); `GET`/`POST /customers/{id}/attachments`, `GET`/`POST /tickets/{id}/attachments`, `GET /attachments/{id}/content`; shared uploader [`attachment-list.component.ts`](../frontend/src/app/shared/components/attachment-list/attachment-list.component.ts). Tests: `AttachmentServiceTests.cs`, `AttachmentStorageTests.cs`.
- **Migration** — `20260827013912_Customers.cs`.

### Why Attachments is not 100%

Upload, download, the size cap and the `413 attachment-too-large` error all work. **The uploader
cannot display the configured size cap**, because no approved endpoint publishes that value —
`BootstrapConfig`, `CustomerConfig` and `StaffConfig` (api-design §6.9) each list their members
exhaustively and the cap is in none of them. The component records this in place:

> *"It is `null` today, and the hole is deliberate … Adding it would be new contract surface, which
> is the user's call, not a screen's — recorded as finding **I-12**."*
> — [`attachment-list.component.ts:90-101`](../frontend/src/app/shared/components/attachment-list/attachment-list.component.ts#L90-L101)

ui-design §8 asks the uploader to state *"size cap from configuration"*, so this is a real gap
against the approved design, not a preference. **Finding I-12, open, non-blocking.** Frontend 75.

---

# Area 2 · Ticket Management — 100%

```
Overall:      ████████████████████  100%
Frontend:     ████████████████████  100%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Create and track tickets | T1 | Yes — in full | **05** `ticket-core` | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |
| Categories and priorities | T1 | Yes — in full | **05** (+ **16 A** config) | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |
| Assign tickets to agents | T1 | Yes — in full | **05** | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |
| Status and escalation | T1 | Yes — in full | **06** `ticket-lifecycle` | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |
| Ticket history | T1 | Yes — in full | **05** + **06** | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Create and track tickets | 100 | 100 | 100 | 100 | 100 | **100** |
| Categories and priorities | 100 | 100 | 100 | 100 | 100 | **100** |
| Assign tickets to agents | 100 | 100 | 100 | 100 | 100 | **100** |
| Status and escalation | 100 | 100 | 100 | 100 | 100 | **100** |
| Ticket history | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **100** | **100** | **100** | **100** | **100** | **100** |

### Evidence

- **Create and track** — [`Ticket.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/Ticket.cs), [`TicketService.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/TicketService.cs); `GET /tickets`, `GET /tickets/{id}`, `POST /tickets`, `PATCH /tickets/{id}`; screens `ticket-list`, `ticket-detail`, `ticket-create`, plus [`ticket-filter-bar.component.ts`](../frontend/src/app/shared/components/ticket-filter-bar/ticket-filter-bar.component.ts) for status/priority/category/assignee/department filtering. Tests: `TicketCreationTests.cs`, `TicketScopingTests.cs`, `QueueOrderingTests.cs`.
- **Categories and priorities** — fixed enumerations from configuration per **A-6**: [`CategoryOptions.cs`](../backend/src/SupportCrm.Application/Configuration/CategoryOptions.cs), [`PriorityOptions.cs`](../backend/src/SupportCrm.Application/Configuration/PriorityOptions.cs), `TicketPriority` enum; published by `GET /config`. [`priority-chip.component.ts`](../frontend/src/app/shared/components/priority-chip/priority-chip.component.ts).
- **Assignment** — `POST /tickets/{id}/assignment`, [`TicketAssigneePolicy.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/TicketAssigneePolicy.cs), [`ticket-assign.component.ts`](../frontend/src/app/features/workspace/tickets/ticket-assign.component.ts).
- **Status lifecycle** — six-state machine `New · Open · Pending · Resolved · Closed · Cancelled` in [`TicketStatus.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketStatus.cs) and [`TicketLifecycle.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketLifecycle.cs); authority matrix in [`TransitionAuthority.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/TransitionAuthority.cs) (**A-5**, **A-16**); `POST /tickets/{id}/transition`; [`transition-menu.component.ts`](../frontend/src/app/shared/components/transition-menu/transition-menu.component.ts) with the single client-side mirror at `shared/lifecycle/transition-matrix.ts`. Tests: `TicketLifecycleTests.cs`, `TransitionAuthorityTests.cs`, `transition-matrix.spec.ts`.
- **Escalation** — a first-class action, not a status change (**AP-7**): `POST /tickets/{id}/escalate`, [`Escalation.RaiseOneLevel`](../backend/src/SupportCrm.Domain/Modules/Tickets/Escalation.cs), [`escalate-button.component.ts`](../frontend/src/app/shared/components/escalate-button/escalate-button.component.ts).
- **Ticket history** — the append-only spine [`TicketActivity.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketActivity.cs) with 12 activity types, [`TicketActivityRecorder.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/TicketActivityRecorder.cs), `GET /tickets/{id}/activity`, [`ticket-activity-region.component.ts`](../frontend/src/app/features/workspace/tickets/ticket-activity-region.component.ts). Tests: `TicketHistoryTests.cs`, `AuditAndHistoryAreSeparateTests.cs`.
- **Migration** — `20260830204837_Tickets.cs`.

**This is the core loop the assessment rests on, and it is closed.** Two of the three product
decisions that gated it were answered and implemented rather than assumed: **A-20** (SLA due
timestamps freeze on a priority change, R-16) and **A-21** (escalation notification climbs to the
next authority level, R-17).

---

# Area 3 · Communication Channels — 53%

```
Overall:      ███████████░░░░░░░░░   53%
Frontend:     ██████░░░░░░░░░░░░░░   30%
Backend:      ████████████░░░░░░░░   60%
Database:     ██████████████░░░░░░   70%
User Stories: ████████████████░░░░   80%
SDD / Docs:   █████████████████░░░   85%
```

**This is the largest gap between what the company asked for and what the approved scope committed
to.** Four of the five channels are **T3** — *"a real seam plus a runnable fake, not the feature"* —
and that was decided up front in product-scope §5, not discovered late.

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Email | **T3** | **No** — seam only | **18** `channel-erp-adapters` | ⬜ None | Seam + fake | Partial | Done | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope · ⛔ Blocked |
| WhatsApp | **T3** | **No** — seam only | **18** | ⬜ None | Seam + fake | Partial | Done | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope · ⛔ Blocked |
| Live chat | **T3** | **No** — seam only | **07** (polled) · **18** (documented) | Partial | Partial | Done | Done | ✅ Verified 2026-08-31 | 🟡 Partial · 📐 Outside approved scope |
| SMS | **T3** | **No** — seam only | **18** | ⬜ None | Seam + fake | Partial | Done | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope · ⛔ Blocked |
| Web forms | T2 | Yes — simplified | **07** `ticket-intake-messaging` | Done | Done | Done | Done | ✅ Verified 2026-08-31 | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Email | 0 | 50 | 50 | 75 | 75 | **33** |
| WhatsApp | 0 | 50 | 50 | 75 | 75 | **33** |
| Live chat | 50 | 50 | 100 | 75 | 100 | **67** |
| SMS | 0 | 50 | 50 | 75 | 75 | **33** |
| Web forms | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **30** | **60** | **70** | **80** | **85** | **53** |

### Evidence

- **Web forms — the one real, fully working inbound channel.** `POST /portal/tickets` in [`PortalTicketsController.cs`](../backend/src/SupportCrm.Api/Controllers/PortalTicketsController.cs), [`PortalTicketService.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/PortalTicketService.cs), screen [`portal-submit-request.component.ts`](../frontend/src/app/features/portal/portal-submit-request.component.ts) at `/portal/requests/new`, persisted with `MessageChannel.WebForm`.
- **The channel-agnostic message model both halves share** — [`TicketMessage.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketMessage.cs) carries its channel of origin; migration `20260831145045_TicketMessages.cs`. This is why Database scores 50 for Email/WhatsApp/SMS rather than 0: the model a provider adapter would write into exists and works — only that channel's enum value does not, and **DM-6** fixes [`MessageChannel`](../backend/src/SupportCrm.Domain/Modules/Tickets/MessageChannel.cs) at the two real values `WebForm` and `Portal` deliberately.
- **Outbound seam (delivered)** — [`IOutboundChannelAdapter.cs`](../backend/src/SupportCrm.Application/Modules/Integrations/IOutboundChannelAdapter.cs) and the shipped [`ConsoleChannelAdapter.cs`](../backend/src/SupportCrm.Infrastructure/Seams/Channels/ConsoleChannelAdapter.cs), registered in the composition root and selected by `SupportCrm:Channels:Outbound`. It takes **no HTTP dependency of any kind**, and a test asserts that by reflection against a host whose `HttpMessageHandler` fails every request. Tests: `ChannelSeamTests.cs`, `NoIntegrationSurfaceTests.cs`.
- **Inbound seam (blocked)** — [`BlockedInboundChannelIngestion.cs`](../backend/src/SupportCrm.Infrastructure/Seams/Channels/BlockedInboundChannelIngestion.cs) throws rather than guessing an actor. See the blocker below.
- **Contract documentation** — [`integration-seams.md`](integration-seams.md) §1 *"What a real provider adapter would have to add"*, §1.4 *"What is deliberately absent from the API"*.
- **Live chat** — real-time transport confirmed absent by search: no SignalR, no WebSocket, no socket.io anywhere in `backend/src` or `frontend/src`. Delivered behaviour is polled in-portal messaging: `GET`/`POST /tickets/{id}/messages` and `GET`/`POST /portal/tickets/{id}/messages`, [`message-thread.component.ts`](../frontend/src/app/shared/components/message-thread/message-thread.component.ts), [`reply-composer.component.ts`](../frontend/src/app/shared/components/reply-composer/reply-composer.component.ts). Test: `MessagingTests.cs`. Documented as a future consumer in [`integration-seams.md`](integration-seams.md) §3.

### ⛔ Blocker — PF-2 / S9-10, inbound actor attribution

The inbound half of the channel seam **cannot be implemented without a product decision**, and the
code refuses rather than inventing one:

> *"PF-2 / S9-10: inbound actor attribution is undecided. `Ticket.createdByUserId` and
> `TicketMessage.authorUserId` are required (data-model §2.6, §2.8) and `actorKind = System` is
> reserved for the SLA monitor (§2.7, R-14). An inbound channel message has no human actor and no
> approved document says who it is attributed to. Choose option A, B or C from
> `.squad/plans/integration-seams/18-story-channel-erp-adapters.md` before implementing this."*
> — [`BlockedInboundChannelIngestion.cs:28-33`](../backend/src/SupportCrm.Infrastructure/Seams/Channels/BlockedInboundChannelIngestion.cs#L28-L33)

Three options are set out in the plan; **none is chosen**. This blocks **story 18 AC 3** and is the
one skipped test in the backend suite (480 passed, **1 skipped**, 481 total). It affects **Email,
WhatsApp, SMS** here and **Email/SMS & WhatsApp** in Area 11.

### Why Frontend is 0% for Email / WhatsApp / SMS

No user-facing surface for these channels exists, and **none is designed** — api-design publishes no
ingestion endpoint (**AP-11**) and ui-design specifies no channel screen. This is scored 0 rather
than `n/a` deliberately: the *company* asked for these channels as capabilities, and an honest audit
should not let a design decision to omit the UI read as *"this dimension does not apply."* The 33%
Overall each line carries is the accurate statement — *a third of the way there, by design.*

---

# Area 4 · Agent Dashboard — 93%

```
Overall:      ███████████████████░   93%
Frontend:     ██████████████████░░   90%
Backend:      ██████████████████░░   90%
Database:     ████████████████████  100%
User Stories: ███████████████████░   95%
SDD / Docs:   ███████████████████░   95%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Assigned tickets | T1 | Yes — in full | **08** `agent-dashboard` | Done | Done | Done | Done | 🔵 Implemented | ✅ Fully implemented |
| Customer information | T1 | Yes — in full | **08** | Done | Done | Done | Done | 🔵 Implemented | ✅ Fully implemented |
| Tasks and reminders | T2 | Yes — simplified | **14** `tasks-internal-notes` | **Partial** | **Partial** | Done | Partial | ⚠ Partial 2026-09-06 | 🟡 Partial · ⛔ Blocked |
| Quick replies | T1 | Yes — in full | **08** (+ **16 A** config) | Done | Done | n/a | Done | 🔵 Implemented | ✅ Fully implemented |
| Team collaboration | T2 | Yes — simplified | **14** | Done | Done | Done | Done | ⚠ Partial 2026-09-06 | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Assigned tickets | 100 | 100 | 100 | 100 | 100 | **100** |
| Customer information | 100 | 100 | 100 | 100 | 100 | **100** |
| Tasks and reminders | 50 | 50 | 100 | 75 | 75 | **67** |
| Quick replies | 100 | 100 | n/a | 100 | 100 | **100** |
| Team collaboration | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **90** | **90** | **100** | **95** | **95** | **93** |

### Evidence

- **Assigned tickets — the queue, ordered by SLA urgency.** [`agent-queue.component.ts`](../frontend/src/app/features/workspace/tickets/agent-queue.component.ts) at `/workspace/queue`, which is the staff landing route. Ordering is server-side and real: [`TicketService.cs:63-161`](../backend/src/SupportCrm.Application/Modules/Tickets/TicketService.cs#L63-L161) — *"Default sort is SLA urgency — `resolutionDueAt:asc` with breached tickets first."* Tests: `QueueOrderingTests.cs`, `agent-queue.component.spec.ts`.
- **Customer information in context** — [`ticket-customer-panel.component.ts`](../frontend/src/app/features/workspace/tickets/ticket-customer-panel.component.ts), reachable from inside a ticket without losing place, fed by the customer timeline. Recent tickets are derived client-side from the timeline response per **S9-6** — *no filter was invented*.
- **Quick replies** — [`QuickReplyOptions.cs`](../backend/src/SupportCrm.Application/Configuration/QuickReplyOptions.cs), published **staff-only** by `GET /config/staff` (**AP-17**, correction **B-2**), inserted through [`reply-composer.component.ts`](../frontend/src/app/shared/components/reply-composer/reply-composer.component.ts). **Database is `n/a`**: data-model §2.16 lists quick replies under *"Explicitly not modelled"* — they are configuration by decision, so scoring a missing table as 0 would penalise the design for following its own approved rule.
- **Team collaboration = internal notes** (the whole of collaboration in T2-C — no mentions, presence or chat). [`TicketInternalNote.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketInternalNote.cs), `GET`/`POST /tickets/{id}/internal-notes`, [`internal-notes-region.component.ts`](../frontend/src/app/features/workspace/tickets/internal-notes-region/internal-notes-region.component.ts). Never visible to a customer, and that is proven rather than asserted: `InternalNotesAreUnreachableTests.cs`.
- **Tasks** — [`TicketTask.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketTask.cs), `GET`/`POST /tickets/{id}/tasks`, `PATCH /tickets/{id}/tasks/{taskId}`, [`tasks-region.component.ts`](../frontend/src/app/features/workspace/tickets/tasks-region/tasks-region.component.ts). Test: `TicketTaskTests.cs`.
- **Migration** — `20260906153211_TasksAndInternalNotes.cs`.

### ⛔ Blocker — S9-1, the dashboard task region has no endpoint

Tasks work **on a ticket**. They do not work **on the dashboard**, which is what the requirement
*"Tasks and reminders"* on an *Agent Dashboard* asks for. The task region is ticket-scoped —
[`tasks-region.component.ts:270`](../frontend/src/app/features/workspace/tickets/tasks-region/tasks-region.component.ts#L270) takes a required `ticketId` input — and the queue screen records the
absence in place:

> *"…task endpoint exists (api-design §5.6 has only `/tickets/{id}/tasks`), so none is …"*
> — [`agent-queue.component.ts:178`](../frontend/src/app/features/workspace/tickets/agent-queue.component.ts#L178)

**This is a documented contradiction between approved documents, not an implementation oversight.**
ui-design §5.1 and §13, story 14's own AC 2, and the data-model §6 index
`TicketTask(assignedUserId, isDone, dueAt)` — labelled *"Open and overdue tasks on the dashboard"* —
all assume a **cross-ticket** task list; api-design §5.6 publishes only ticket-scoped endpoints.
Story 14 **left plan task 8 unbuilt rather than inventing an endpoint**, which is the correct
behaviour under CLAUDE.md §3.

Scoring: **Database 100** — the entity *and the index that exists precisely for the missing query*
are both present. **Backend 50** and **Frontend 50** — half the surface, ticket-scoped only.

### On "reminders"

T2-C scopes this to *"a due-dated to-do … No calendar, no recurrence, no push reminders."* Due dates
are stored and rendered; there is no reminder alert, and
[`NotificationType`](../backend/src/SupportCrm.Domain/Modules/Sla/NotificationType.cs) carries no
task-due value. **This is the approved scope working as designed, not a defect** — but it is a real
reduction against the company's word *"reminders"*, and it is recorded here rather than glossed.

---

# Area 5 · SLA & Automation — 100%

```
Overall:      ████████████████████  100%
Frontend:     ████████████████████  100%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Response and resolution targets | T2 | Yes — simplified | **05** + **09** `sla-routing-escalation` | Done | Done | Done | Done | 🔵 Implemented | ✅ Fully implemented |
| Automatic assignment | T2 | Yes — simplified | **09** | Done | Done | Done | Done | 🔵 Implemented | ✅ Fully implemented |
| Escalation rules | T2 | Yes — simplified | **09** (+ **06** manual) | Done | Done | Done | Done | 🔵 Implemented | ✅ Fully implemented |
| Alerts and notifications | T2 | Yes — simplified | **09** | Done | Done | Done | Done | 🔵 Implemented | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Response and resolution targets | 100 | 100 | 100 | 100 | 100 | **100** |
| Automatic assignment | 100 | 100 | 100 | 100 | 100 | **100** |
| Escalation rules | 100 | 100 | 100 | 100 | 100 | **100** |
| Alerts and notifications | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **100** | **100** | **100** | **100** | **100** | **100** |

### Evidence

- **Targets** — [`SlaClock.cs`](../backend/src/SupportCrm.Domain/Modules/Sla/SlaClock.cs) and [`SlaTargetOptions.cs`](../backend/src/SupportCrm.Application/Configuration/SlaTargetOptions.cs); `firstResponseDueAt` and `resolutionDueAt` are **required at creation** (data-model §2.6) and **freeze** on a later priority change (**A-20**, R-16). 24/7 clock per **A-3**. UI: [`sla-indicator.component.ts`](../frontend/src/app/shared/components/sla-indicator/sla-indicator.component.ts) + `sla-indicator.component.spec.ts`. Tests: `SlaClockTests.cs`, `SlaEvaluationTests.cs`.
- **Automatic assignment** — round-robin across active agents in the ticket's department: [`RoundRobinAssignmentPolicy.cs`](../backend/src/SupportCrm.Application/Modules/Sla/RoundRobinAssignmentPolicy.cs), registered at [`DependencyInjection.cs:104`](../backend/src/SupportCrm.Infrastructure/DependencyInjection.cs#L104) and **called on the real creation path** at [`TicketService.cs:347`](../backend/src/SupportCrm.Application/Modules/Tickets/TicketService.cs#L347). Test: `AutoAssignmentTests.cs`.
- **Escalation rules** — the single rule of T2-D (flag breached · raise priority one level · notify the department manager): [`SlaEvaluationService.cs`](../backend/src/SupportCrm.Application/Modules/Sla/SlaEvaluationService.cs) driven by the periodic in-process [`SlaMonitorHostedService.cs`](../backend/src/SupportCrm.Api/BackgroundServices/SlaMonitorHostedService.cs), registered at [`Program.cs:180`](../backend/src/SupportCrm.Api/Program.cs#L180) — no broker, no job queue, exactly as scoped. Recipient cascade per **A-21**: [`EscalationRecipientPolicy.cs`](../backend/src/SupportCrm.Application/Modules/Sla/EscalationRecipientPolicy.cs), test `EscalationRecipientPolicyTests.cs`.
- **Alerts and notifications — in-app only, as scoped.** [`Notification.cs`](../backend/src/SupportCrm.Domain/Modules/Sla/Notification.cs) with four types (`TicketAssigned`, `SlaBreached`, `TicketEscalated`, `CustomerReplied`), [`NotificationService.cs`](../backend/src/SupportCrm.Application/Modules/Sla/NotificationService.cs), [`PersistentNotificationPublisher.cs`](../backend/src/SupportCrm.Infrastructure/Notifications/PersistentNotificationPublisher.cs); `GET /notifications`, `POST /notifications/{id}/read`; badge store [`notification.store.ts`](../frontend/src/app/core/notifications/notification.store.ts) and screen [`notification-list.component.ts`](../frontend/src/app/features/workspace/notifications/notification-list.component.ts) at `/workspace/notifications`. Test: `NotificationTests.cs`.
- **Migration** — `20260831193638_Notifications.cs`.

### A note on scoring 100% here

Email/SMS/push delivery of alerts is **not** counted against this area. T2-D scopes notifications to
in-app explicitly and assigns delivery to T3-A, which is already scored down in **Area 3** and
**Area 11**. Docking it here as well would double-count one gap.

### Open, non-blocking — PF-5

`firstRespondedAt` is set only by the first *outbound* message, so a ticket resolved without a reply
is permanently first-response-breached. Story 09 implemented **A-3 exactly as worded and reported
the consequence** rather than changing it, since changing it would change A-3. The queue renders
`null` as *"—"*, never as *"breached"* and never as *"0"* (ui-design §11). Recorded, open,
non-blocking.

---

# Area 6 · Knowledge Base — 100%

```
Overall:      ████████████████████  100%
Frontend:     ████████████████████  100%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| FAQs | T2 | Yes — simplified | **12** `kb-articles-search` | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| Help articles | T2 | Yes — simplified | **12** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| Solutions and guides | T2 | Yes — simplified | **12** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| Search | T2 | Yes — simplified | **12** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| FAQs | 100 | 100 | 100 | 100 | 100 | **100** |
| Help articles | 100 | 100 | 100 | 100 | 100 | **100** |
| Solutions and guides | 100 | 100 | 100 | 100 | 100 | **100** |
| Search | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **100** | **100** | **100** | **100** | **100** | **100** |

### Evidence

- **All three content kinds are one `article` concept distinguished by a type field**, exactly as T2-E specifies — not three subsystems. [`ArticleType.cs`](../backend/src/SupportCrm.Domain/Modules/Knowledge/ArticleType.cs) → `Faq`, `HelpArticle`, `SolutionGuide`. Entity [`KnowledgeArticle.cs`](../backend/src/SupportCrm.Domain/Modules/Knowledge/KnowledgeArticle.cs), migration `20260901143725_KnowledgeArticles.cs`.
- **Authoring** — `GET`/`POST /kb/articles`, `PATCH /kb/articles/{id}`, `POST /kb/articles/{id}/publish`, `POST /kb/articles/{id}/unpublish` in [`KnowledgeController.cs`](../backend/src/SupportCrm.Api/Controllers/KnowledgeController.cs); admin screens [`article-list.component.ts`](../frontend/src/app/features/admin/knowledge/article-list.component.ts) and [`article-editor.component.ts`](../frontend/src/app/features/admin/knowledge/article-editor.component.ts). **No delete route and no version-history route**, because neither exists server-side — the route file says so and T4 excludes versioning.
- **Search** — keyword matching over title and body using the database's own text matching, with **one home** (**AD-13**): [`ArticleSearch.cs`](../backend/src/SupportCrm.Application/Modules/Knowledge/ArticleSearch.cs). Staff screens [`knowledge-search.component.ts`](../frontend/src/app/features/workspace/knowledge/knowledge-search.component.ts) and [`knowledge-reader.component.ts`](../frontend/src/app/features/workspace/knowledge/knowledge-reader.component.ts). Test: `SearchAndSuggestionTests.cs`.
- **Visibility** — [`ArticleVisibility.cs`](../backend/src/SupportCrm.Domain/Modules/Knowledge/ArticleVisibility.cs) with **one home** for constraint 19: `PortalArticleService.PortalVisible`. Public articles reach the customer through `GET /portal/kb/articles`; an internal or unpublished id answers `404` (**AP-4**). Test: `KnowledgeVisibilityTests.cs`.
- **Body rendering** — [`markdown-view.component.ts`](../frontend/src/app/shared/components/markdown-view/markdown-view.component.ts) + spec. Basic markdown, no rich editor, no media library — as scoped.
- **Demo content** — [`KnowledgeSeeder.cs`](../backend/src/SupportCrm.Infrastructure/Persistence/Seeders/KnowledgeSeeder.cs) (196 lines).

---

# Area 7 · AI Features — 75%

```
Overall:      ███████████████░░░░░   75%
Frontend:     ███████████████░░░░░   75%
Backend:      ███████████████░░░░░   75%
Database:     ████████████░░░░░░░░   58%
User Stories: █████████████████░░░   85%
SDD / Docs:   ███████████████████░   95%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Ticket summaries | T1 | Yes — in full | **10** + **11** | Done | Done | n/a | Done | 🔵 Implemented | ✅ Fully implemented |
| Suggested replies | T1 | Yes — in full | **10** + **11** | Done | Done | n/a | Done | 🔵 Implemented | ✅ Fully implemented |
| Automatic categorization | T1 | Yes — in full | **11** `ai-ticket-assists` | **Partial** | **Partial** | **Partial** | Partial | 🔵 Implemented | 🟡 Partial · ⛔ Blocked |
| Suggested solutions | T2 | Yes — simplified | **12** (retrieval) | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| AI chatbot | **T3** | **No** — seam only | **10** (seam) · **18** (documented) | ⬜ None | Extension point | ⬜ None | Done | — | 🟡 Partial · 📐 Outside approved scope |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Ticket summaries | 100 | 100 | n/a | 100 | 100 | **100** |
| Suggested replies | 100 | 100 | n/a | 100 | 100 | **100** |
| Automatic categorization | 75 | 50 | 75 | 75 | 75 | **67** |
| Suggested solutions | 100 | 100 | 100 | 100 | 100 | **100** |
| AI chatbot | 0 | 25 | 0 | 50 | 100 | **8** |
| **Area** | **75** | **75** | **58** | **85** | **95** | **75** |

### Evidence

- **One AI abstraction with a deterministic offline fake**, so the system demos with no live provider — the T1-F requirement: [`IAiAssistService.cs`](../backend/src/SupportCrm.Application/Modules/Ai/IAiAssistService.cs), [`DeterministicFakeAiService.cs`](../backend/src/SupportCrm.Infrastructure/Seams/Ai/DeterministicFakeAiService.cs), [`ProviderAiService.cs`](../backend/src/SupportCrm.Infrastructure/Seams/Ai/ProviderAiService.cs), selected by [`AiOptions.cs`](../backend/src/SupportCrm.Application/Configuration/AiOptions.cs). Tests: `AiSeamShapeTests.cs`, `AiAssistEndpointTests.cs`, `AiOutageDoesNotBlockWorkTests.cs`.
- **Summaries and suggested replies** — `POST /tickets/{id}/ai/summary`, `POST /tickets/{id}/ai/suggested-reply` in [`AiController.cs`](../backend/src/SupportCrm.Api/Controllers/AiController.cs); [`ai-assist-panel.component.ts`](../frontend/src/app/shared/components/ai-assist-panel/ai-assist-panel.component.ts) + spec. **Advisory only, never auto-sent** (**A-8**): the suggested reply lands in the composer for the agent to edit. **Database is `n/a`** by decision **R-3** — *"No entity. Summaries and suggested replies are not stored."*
- **Suggested solutions — retrieval, not generation**, exactly as T2-E specifies: [`SuggestedArticleService.cs`](../backend/src/SupportCrm.Application/Modules/Knowledge/SuggestedArticleService.cs), `GET /tickets/{id}/suggested-articles`, [`suggested-articles-region.component.ts`](../frontend/src/app/features/workspace/tickets/suggested-articles-region/suggested-articles-region.component.ts). **AP-14** honoured — it is served from the Knowledge module, not the AI one.
- **Automatic categorization (partial)** — `POST /ai/classification-suggestion` works, and [`ticket-create.component.ts`](../frontend/src/app/features/workspace/tickets/ticket-create.component.ts) calls it on blur of subject or description, pre-selects category and priority, keeps the *"AI-generated"* label visible after an override, and never auto-fills inline (**A-8**, **UI-6**).

### ⛔ Blocker — S9-4, the AI suggestion is never recorded in ticket history

T1-F requires automatic categorization *"overridable by the agent, **with the suggestion recorded in
ticket history**."* The first two thirds work. **The recording does not exist.**

The data model provides the activity types — [`TicketActivityType.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/TicketActivityType.cs) declares `AiSuggestionOffered` and
`AiSuggestionResolved` — but a repository-wide search finds **those two enum members and nothing
else**: no service writes them, and no request contract can carry the value. The screen holds the
data and cannot send it:

> *"S9-4 — BLOCKED on a Stage 7 decision. The suggestion that was offered and whether the agent kept
> it are known right here, in `suggestion()` and … When the contract is extended, send the captured
> suggestion."*
> — [`ticket-create.component.ts:298-308`](../frontend/src/app/features/workspace/tickets/ticket-create.component.ts#L298-L308)

**A documented contradiction, again between approved documents:** data-model §2.7 supplies the
types, api-design §5.8 says it happens *"when the agent saves the ticket"* — but `POST /tickets`
accepts **no field** that could carry it, §6.11 adds none, and §7 lists none. Blocks **story 11
AC 4**. Scoring: FE 75 (offered, overridable, labelled — capture exists, cannot be sent), BE 50 (the
suggestion endpoint works; no persistence path), DB 75 (the activity types exist and `TicketActivity`
can carry them; nothing writes them).

### AI chatbot — 8%, and that is the honest number

T3-C designs the chatbot as a **future consumer** of the T1-F abstraction and does not build it.
What exists is a named extension point and a written contract:

- [`IAiAssistService.cs:37`](../backend/src/SupportCrm.Application/Modules/Ai/IAiAssistService.cs#L37) — *"The future chatbot (T3-C) would be a **consumer** of this …"* — the single mention of a chatbot anywhere in the codebase.
- [`integration-seams.md`](integration-seams.md) §4 — *"AI chatbot (T3-C) — a future consumer of a **different** seam."*

No conversational surface, no bot, no knowledge grounding, no human handoff — and handoff semantics
remain an open question in product-scope §9. **Backend 25** (extension point named, no code path),
**Frontend 0**, **Database 0** (a chatbot needs conversation storage; none exists). **SDD 100** —
it is thoroughly documented. *That 100 sitting beside an 8 is exactly what this document exists to
show.*

---

# Area 8 · Customer Portal — 100%

```
Overall:      ████████████████████  100%
Frontend:     ████████████████████  100%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Submit tickets | T2 | Yes — simplified | **07** + **13** `portal-self-service` | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| Track requests | T2 | Yes — simplified | **13** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| View history | T2 | Yes — simplified | **13** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| Access FAQs | T2 | Yes — simplified | **12** + **13** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| Submit feedback | T2 | Yes — simplified | **13** | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented · ⚠ OQ-1 open |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Submit tickets | 100 | 100 | 100 | 100 | 100 | **100** |
| Track requests | 100 | 100 | 100 | 100 | 100 | **100** |
| View history | 100 | 100 | 100 | 100 | 100 | **100** |
| Access FAQs | 100 | 100 | 100 | 100 | 100 | **100** |
| Submit feedback | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **100** | **100** | **100** | **100** | **100** | **100** |

### Evidence

The portal is a **separate, simpler surface over the same backend**, behind its own shell and lazy
route tree (**AD-14**) — [`portal.routes.ts`](../frontend/src/app/features/portal/portal.routes.ts):

| Route | Screen | Endpoint |
|---|---|---|
| `/portal/requests` *(landing)* | [`portal-requests.component.ts`](../frontend/src/app/features/portal/portal-requests.component.ts) | `GET /portal/tickets` |
| `/portal/requests/new` | [`portal-submit-request.component.ts`](../frontend/src/app/features/portal/portal-submit-request.component.ts) | `POST /portal/tickets` |
| `/portal/requests/:id` | [`portal-request-detail.component.ts`](../frontend/src/app/features/portal/portal-request-detail.component.ts) | `GET /portal/tickets/{id}`, `/messages`, `/attachments`, `/transition`, `/feedback` |
| `/portal/help` | [`portal-help.component.ts`](../frontend/src/app/features/portal/portal-help.component.ts) | `GET /portal/kb/articles` |
| `/portal/help/:id` | [`portal-help-article.component.ts`](../frontend/src/app/features/portal/portal-help-article.component.ts) | `GET /portal/kb/articles/{id}` |

*"Every route here now points at a screen the design specifies, and §7 names no fifth one."*

- **Feedback** — [`CustomerFeedback.cs`](../backend/src/SupportCrm.Domain/Modules/Tickets/CustomerFeedback.cs), [`CustomerFeedbackService.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/CustomerFeedbackService.cs), `POST /portal/tickets/{id}/feedback`, [`rating-input.component.ts`](../frontend/src/app/shared/components/rating-input/rating-input.component.ts) + spec. Migration `20260901154739_CustomerFeedback.cs`. This is the **sole CSAT input**, feeding Area 9's satisfaction metric.
- **Isolation is proven, not assumed** — `PortalIsolationTests.cs`, `PortalLifecycleTests.cs`, `FeedbackTests.cs`. A customer sees only their own requests; internal notes are unreachable (`InternalNotesAreUnreachableTests.cs`).
- **Registration** — `POST /auth/register` implemented in story 04 slice 5 with all three **A-15** outcomes (**S9-7**); test `RegistrationTests.cs`.

### ⚠ Pending decision — OQ-1, the CSAT rating scale

**Open, and deliberately not answered.** T2-F says *"a one-question satisfaction rating"* and fixes
no scale. Rather than inventing one, the plan's interim behaviour was implemented: the server
validates against the configured `Feedback rating scale` key ([`FeedbackOptions.cs`](../backend/src/SupportCrm.Application/Configuration/FeedbackOptions.cs)) and the control follows it,
with `RatingScaleDto(Min, Max)` published to the dashboard.

**This is not scored as a gap.** The mechanism is complete, configurable and working end to end; the
open item is a *product* decision about which numbers to configure. It is listed under **Blocked /
Pending Decisions** below so it is not lost.

---

# Area 9 · Reports & Management — 100%

```
Overall:      ████████████████████  100%
Frontend:     ████████████████████  100%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Ticket reports | T2 | Yes — simplified | **15** `management-dashboard` | Done | Done | Done | Done | ⚠ **Partial 2026-09-06** | ✅ Fully implemented |
| SLA performance | T2 | Yes — simplified | **15** | Done | Done | Done | Done | ⚠ **Partial 2026-09-06** | ✅ Fully implemented |
| Agent performance | T2 | Yes — simplified | **15** | Done | Done | Done | Done | ⚠ **Partial 2026-09-06** | ✅ Fully implemented |
| Customer satisfaction | T2 | Yes — simplified | **15** | Done | Done | Done | Done | ⚠ **Partial 2026-09-06** | ✅ Fully implemented |
| Management dashboards | T2 | Yes — simplified | **15** | Done | Done | Done | Done | ⚠ **Partial 2026-09-06** | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Ticket reports | 100 | 100 | 100 | 100 | 100 | **100** |
| SLA performance | 100 | 100 | 100 | 100 | 100 | **100** |
| Agent performance | 100 | 100 | 100 | 100 | 100 | **100** |
| Customer satisfaction | 100 | 100 | 100 | 100 | 100 | **100** |
| Management dashboards | 100 | 100 | 100 | 100 | 100 | **100** |
| **Area** | **100** | **100** | **100** | **100** | **100** | **100** |

### Evidence

One dashboard, one endpoint, four metric groups — the fixed metric set of T2-G, with **no report
builder, no scheduling and no export**, which the code enforces rather than merely omits:

| Metric group | Query | Requirement |
|---|---|---|
| Ticket counts by status/priority/category/department | [`TicketCountsQuery.cs`](../backend/src/SupportCrm.Application/Modules/Reporting/TicketCountsQuery.cs) | §9.1 |
| SLA attainment, met vs breached per priority | [`SlaAttainmentQuery.cs`](../backend/src/SupportCrm.Application/Modules/Reporting/SlaAttainmentQuery.cs) | §9.2 |
| Agent performance — assigned / resolved / avg resolution | [`AgentPerformanceQuery.cs`](../backend/src/SupportCrm.Application/Modules/Reporting/AgentPerformanceQuery.cs) | §9.3 |
| Satisfaction — average rating and response count | [`SatisfactionQuery.cs`](../backend/src/SupportCrm.Application/Modules/Reporting/SatisfactionQuery.cs) | §9.4 |

- **Endpoint** — `GET /reports/dashboard?departmentId=&branchId=` in [`ReportsController.cs`](../backend/src/SupportCrm.Api/Controllers/ReportsController.cs), composed by [`DashboardReportService.cs`](../backend/src/SupportCrm.Application/Modules/Reporting/DashboardReportService.cs). The two filters combine, and `branchId` resolves **through the customer** because `Ticket` has no branch column.
- **Screen** — [`reports.component.ts`](../frontend/src/app/features/workspace/reports/reports.component.ts) at `/workspace/reports`, gated `roleAtLeast('Manager')` in the router **and** independently by the endpoint, which returns `403` to an Agent regardless of what the router permitted.
- **Aggregation happens on the server** — proven, not asserted: `AggregationHappensOnTheServerTests.cs`. Also `DashboardMetricsTests.cs`, `DashboardAccessTests.cs`.
- **The "no export" rule is enforced by a whitelist**: an unknown query parameter is a `400`, not silently ignored (**AP-15**) — so `?format=csv` fails loudly rather than being quietly dropped.
- **PF-4 / S9-9 closed 2026-09-06 (R-18)** — *"tickets assigned"* means **currently** assigned, encoded once in `AgentPerformanceQuery`. The decision landed **before** story 15 was implemented, so the method was written once and correctly.

### 🔵 Why this area is "verification pending"

PROJECT-PROGRESS §3 records story 15 as *"Implemented — all 6 tasks · **Partial** 2026-09-06 —
backend, data layer and live stack verified; **front-end screen checks OUTSTANDING**."* The
implementation dimensions are scored 100 because the code, the endpoint, the screen and the tests
all exist and pass. **The status is `Verification pending`, not `Complete`** — see the Verification
Pending section below.

---

# Area 10 · Security & Administration — 100%

```
Overall:      ████████████████████  100%
Frontend:     ████████████████████  100%
Backend:      ████████████████████  100%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   ████████████████████  100%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Users and roles | T1 | Yes — in full | **02** `auth-and-roles` | Done | Done | Done | Done | ✅ Verified 2026-08-26 | ✅ Fully implemented |
| Permissions | T1 | Yes — in full | **02** (+ **03** scoping) | Done | Done | Done | Done | ✅ Verified 2026-08-26 | ✅ Fully implemented |
| Audit logs | T2 | Yes — simplified | **02** (write) + **16 B** (read) | Done | Done | Done | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |
| System configuration | T2 | Yes — simplified | **16 A** + **16 B** | Done | Done | n/a | Done | ✅ Verified 2026-09-01 | ✅ Fully implemented |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Users and roles | 100 | 100 | 100 | 100 | 100 | **100** |
| Permissions | 100 | 100 | 100 | 100 | 100 | **100** |
| Audit logs | 100 | 100 | 100 | 100 | 100 | **100** |
| System configuration | 100 | 100 | n/a | 100 | 100 | **100** |
| **Area** | **100** | **100** | **100** | **100** | **100** | **100** |

### Evidence

- **Four fixed roles** (**A-4**) — [`UserRole.cs`](../backend/src/SupportCrm.Domain/Modules/Identity/UserRole.cs): `Customer=0`, `Agent=1`, `Manager=2`, `Administrator=3`. [`User.cs`](../backend/src/SupportCrm.Domain/Modules/Identity/User.cs), [`UserAdminService.cs`](../backend/src/SupportCrm.Application/Modules/Identity/UserAdminService.cs); `GET`/`POST /users`, `GET`/`PATCH /users/{id}`, `POST /users/{id}/deactivate`; screens [`user-directory`](../frontend/src/app/features/admin/users/user-directory.component.ts), [`user-detail`](../frontend/src/app/features/admin/users/user-detail.component.ts), [`create-user-dialog`](../frontend/src/app/features/admin/users/create-user-dialog.component.ts).
- **Permission enforcement on the server, not merely hidden UI** — the T1-D requirement, and the thing this project is most careful about. [`AuthorizationPolicies.cs`](../backend/src/SupportCrm.Api/Auth/AuthorizationPolicies.cs), [`CurrentUserMiddleware.cs`](../backend/src/SupportCrm.Api/Auth/CurrentUserMiddleware.cs), department scoping in [`TicketScope.cs`](../backend/src/SupportCrm.Application/Modules/Tickets/TicketScope.cs). The router file states the contract explicitly: *"**Guards hide; they do not protect.** Every endpoint behind these screens independently returns `403` to a caller whose role is insufficient."* Tests: `AuthorizationTests.cs`, `TicketScopingTests.cs`, `PortalIsolationTests.cs`, `CustomerAccessTests.cs`, `BranchIsNotABoundaryTests.cs`.
- **Authentication** — JWT asserting identity only; role and department are resolved per request from the authoritative user record, never from the token (**AD-15**, R-1). [`JwtTokenIssuer.cs`](../backend/src/SupportCrm.Infrastructure/Security/JwtTokenIssuer.cs), [`AuthService.cs`](../backend/src/SupportCrm.Application/Modules/Identity/AuthService.cs); `POST /auth/login`, `POST /auth/register`, `GET /auth/me`.
- **Audit logs** — append-only, written by a **single** service: [`AuditEntry.cs`](../backend/src/SupportCrm.Domain/Modules/Administration/AuditEntry.cs), [`IAuditRecorder.cs`](../backend/src/SupportCrm.Application/Modules/Administration/IAuditRecorder.cs) / [`AuditRecorder.cs`](../backend/src/SupportCrm.Application/Modules/Administration/AuditRecorder.cs), read via `GET /audit` and [`audit-log.component.ts`](../frontend/src/app/features/admin/audit/audit-log.component.ts) at `/admin/audit`. Audit and ticket history are deliberately separate concerns, and a test enforces it: `AuditAndHistoryAreSeparateTests.cs`. Also `AuditRecordingTests.cs`, `AuditReadTests.cs`.
- **System configuration** — file/environment based across 17 options classes, validated at startup by [`ConfigurationValidator.cs`](../backend/src/SupportCrm.Application/Configuration/ConfigurationValidator.cs), published in three tiers by [`ConfigController.cs`](../backend/src/SupportCrm.Api/Controllers/ConfigController.cs) (`/config/bootstrap` anonymous, `/config` authenticated, `/config/staff` staff-only — **AP-17**), and shown read-only at `/admin/configuration`. **Database is `n/a` by decision**: T2-I says *"no configuration UI — changing configuration is a redeploy"*, and `NoConfigurationEntityTests.cs` asserts that no configuration entity exists. Also `ConfigurationTierTests.cs`, `ConfigurationValidationTests.cs`.

---

# Area 11 · Integrations — 50%

```
Overall:      ██████████░░░░░░░░░░   50%
Frontend:     █████░░░░░░░░░░░░░░░   25%
Backend:      █████████████░░░░░░░   63%
Database:     ██████████░░░░░░░░░░   50%
User Stories: ███████████████████░   94%
SDD / Docs:   ████████████████░░░░   81%
```

**The second large company-vs-scope gap.** Three of four lines are **T3** — an interface plus a
runnable fake, by decision.

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| APIs | T2 | Yes — simplified | **01** `solution-skeleton` | Done | Done | n/a | Done | ✅ Verified 2026-08-25 | ✅ Fully implemented |
| ERP | **T3** | **No** — seam only | **18** | ⬜ None | Seam + no-op | Partial | Done | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope |
| Email, SMS & WhatsApp | **T3** | **No** — seam only | **18** | ⬜ None | Seam + fake | Partial | Done | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope · ⛔ Blocked |
| External systems | **T3** | **No** — seam only | **18** | ⬜ None | Seam + no-op | Partial | Done | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| APIs | 100 | 100 | n/a | 100 | 100 | **100** |
| ERP | 0 | 50 | 50 | 100 | 75 | **33** |
| Email, SMS & WhatsApp | 0 | 50 | 50 | 75 | 75 | **33** |
| External systems | 0 | 50 | 50 | 100 | 75 | **33** |
| **Area** | **25** | **63** | **50** | **94** | **81** | **50** |

### Evidence

- **APIs — complete, and this *is* the API deliverable** (T2-L: no separate partner API, no API keys, no rate limiting). The application's own HTTP API, **66 published operations** across 18 controllers, self-documented: `AddOpenApi()` at [`Program.cs:240`](../backend/src/SupportCrm.Api/Program.cs#L240), `MapOpenApi()` and `MapScalarApiReference()` at lines 257–258. Consumed by the Angular front end, which is the intended and only consumer. Uniform error envelope (**AP-2**), unknown-member rejection (**AP-10**, **AP-15**). Tests: `PlatformEndpointTests.cs`, `UnmappedRequestMemberTests.cs`, `ModelStateProblemDetailsTests.cs`.
- **ERP / external systems — the delivered T3 contract.** [`IExternalSystemGateway.cs`](../backend/src/SupportCrm.Application/Modules/Integrations/IExternalSystemGateway.cs) plus the shipped [`NoOpExternalSystemGateway.cs`](../backend/src/SupportCrm.Infrastructure/Seams/Erp/NoOpExternalSystemGateway.cs) — *"`LookupExternalReferenceAsync` returns `null` and `PushCustomerChangedAsync` completes without a side effect. There is **no connection, no field mapping, no sync strategy and no conflict resolution**, and no credential is read."* Test: `ErpSeamTests.cs`.
- **The one persisted trace** — `Customer.externalReference`: optional, settable through no endpoint, **and with no writer anywhere**, which is what *"unused by default"* means. This is why Database scores 50, not 100 and not 0: the column exists, nothing uses it.
- **No system is named, deliberately.** Product-scope §9 questions 2 and 3 stay open and requirements §11.4 *"External systems"* is unbounded on purpose — *"cannot be scoped further without a named system"* (T3-D).
- **Email/SMS/WhatsApp** — same seam as Area 3; see that area for the adapter, the fake and the PF-2 blocker. `NoIntegrationSurfaceTests.cs` asserts that no integration HTTP surface was introduced.
- **Contract documentation** — [`integration-seams.md`](integration-seams.md) §2 *"What a real implementation would have to add"*, §5 *"What no seam introduced."*

### Why User Stories scores 94% here but Overall only 50%

**Story 18 exists, was planned, was implemented and has landed** (`8d449bf`). It delivered exactly
what T3-D and T3-A promised — the interfaces, the fakes, the tests and the contract document. By the
project's own commitment, this area is **done**. By the *company's* requirement, an integration that
connects to nothing is a third of the way there.

**Both numbers are correct.** Reporting only the first would be the misleading one, and reporting
only the second would call a deliberate decision a failure.

---

# Area 12 · Platform — 90%

```
Overall:      ██████████████████░░   90%
Frontend:     ██████████████████░░   90%
Backend:      ██████████████████░░   88%
Database:     ████████████████████  100%
User Stories: ████████████████████  100%
SDD / Docs:   █████████████████░░░   85%
```

| Company requirement (PDF bullet) | Tier | In approved scope | Story | Frontend | Backend | DB | SDD | Verification | Status |
|---|---|---|---|---|---|---|---|---|---|
| Arabic & English | T2 | Yes — simplified | **17 A** (scaffold) + **17 B** (translation) | Done | Done | n/a | Partial | 🔵 Unrecorded | ✅ Fully implemented |
| Web and mobile friendly | **T3** | **No** — seam only | **17 B** | Done | n/a | n/a | Partial | 🔵 Unrecorded | ✅ Fully implemented · 📐 native apps outside scope |
| Multi-department | T1 | Yes — in full | **03** `departments-branches` | Done | Done | Done | Done | ✅ Verified 2026-08-27 | ✅ Fully implemented |
| Multi-branch | T2 | Yes — simplified | **03** | Done | Done | Done | Done | ✅ Verified 2026-08-27 | ✅ Fully implemented |
| Custom branding | **T3** | **No** — seam only | **17 B** | **Partial** | **Partial** | n/a | Partial | 🔵 Unrecorded | 🟡 Partial · 📐 Outside approved scope |

| Requirement | FE | BE | DB | US | SDD | **Overall** |
|---|---:|---:|---:|---:|---:|---:|
| Arabic & English | 100 | 100 | n/a | 100 | 75 | **100** |
| Web and mobile friendly | 100 | n/a | n/a | 100 | 75 | **100** |
| Multi-department | 100 | 100 | 100 | 100 | 100 | **100** |
| Multi-branch | 100 | 100 | 100 | 100 | 100 | **100** |
| Custom branding | 50 | 50 | n/a | 100 | 75 | **50** |
| **Area** | **90** | **88** | **100** | **100** | **85** | **90** |

### Evidence

- **Arabic & English** — real dictionaries, not stubs: [`en.json`](../frontend/src/assets/i18n/en.json) (22 KB) and [`ar.json`](../frontend/src/assets/i18n/ar.json) (28 KB), 20 top-level namespaces each (`auth`, `nav`, `admin`, `customers`, `tickets`, `portal`, `queue`, `notifications`, `ai`, `knowledge`, `reports`, …). Transloco throughout; [`direction.service.ts`](../frontend/src/app/core/i18n/direction.service.ts) for RTL; [`language-switcher.component.ts`](../frontend/src/app/shared/components/language-switcher/language-switcher.component.ts). Backend side: [`LocalizationOptions.cs`](../backend/src/SupportCrm.Application/Configuration/LocalizationOptions.cs) published via `/config/bootstrap`. Specs: `dictionaries.spec.ts`, `language-switch-state.spec.ts`, `primeng-arabic.spec.ts`. **Database is `n/a`** — user-generated content is stored as authored and is *not* translated (T2-J, explicit).
- **RTL is enforced mechanically, not by review** — `npm run lint:styles` fails on physical CSS properties; logical properties only (ui-design §10.2). **Verified this audit: exit 0.**
- **Web and mobile friendly = responsive web** (**A-1**, T3-F), and it is measured rather than claimed: [`responsive-phone-width.spec.ts`](../frontend/src/app/shared/responsive-phone-width.spec.ts) narrows the Karma iframe to **390 px** and asserts `document.body.scrollWidth <= window.innerWidth` across the agent queue, ticket detail and portal request detail — **in `ltr` and `rtl` both**, because *"a layout that overflows only in Arabic is the failure mode logical properties exist to prevent."* The spec fails loudly rather than passing vacuously if the frame is inaccessible. **Backend and Database are `n/a`** — responsiveness has no server or persistence surface. Native iOS/Android and PWA offline are **T4**, explicitly excluded.
- **Multi-department — a first-class dimension and the real security boundary** (**A-2**). [`Department.cs`](../backend/src/SupportCrm.Domain/Modules/Organization/Department.cs), `GET /departments`, [`department-filter.component.ts`](../frontend/src/app/shared/components/department-filter/department-filter.component.ts) + spec, scoping in `TicketScope`, category→department mapping at creation (**A-14**). Test: `OrganizationEndpointTests.cs`.
- **Multi-branch — an organizational attribute, *not* a security boundary**, exactly as T2-K scopes it. [`Branch.cs`](../backend/src/SupportCrm.Domain/Modules/Organization/Branch.cs), `GET /branches`, branch on `Customer` and `User`, `branchId` filter on the reports dashboard. The boundary distinction is proven: `BranchIsNotABoundaryTests.cs`.
- **Migration** — `20260826193949_InitialSchema.cs` (departments, branches, users, audit).

### Why Custom branding is 50%

**Delivered:** [`BrandingOptions.cs`](../backend/src/SupportCrm.Application/Configuration/BrandingOptions.cs) — `ProductName`, `LogoUrl`, `PrimaryColor` — resolved from configuration
rather than hardcoded, published anonymously by `GET /config/bootstrap`, and consumed at runtime by
[`runtime-config.service.ts`](../frontend/src/app/core/config/runtime-config.service.ts),
[`auth-shell.component.ts`](../frontend/src/app/layout/auth-shell/auth-shell.component.ts),
`app.topbar.ts` and `app.config.ts`. *"Branding is read at runtime and never compiled into a
component or a stylesheet."*

**Not delivered:** a single default brand is all there is. **Customising it requires a redeploy** —
no branding UI, no upload, no per-tenant or per-branch themes, no CSS theming engine (T3-E, and
multi-tenancy is T3-G / not built). Against the company word *"Custom"*, that is half: the
*mechanism* is real, the *customisation* is not. **FE 50, BE 50, Overall 50.**

---

## Key Findings

### ✅ Fully Delivered

Six of the twelve company areas are genuinely complete — every requirement line delivered end to
end, with code, screens, persistence and passing checks:

| Area | Why it qualifies |
|---|---|
| **2 · Ticket Management** | The full loop: create, track, filter, categorize, prioritize, assign, transition through a six-state machine with an enforced authority matrix, escalate as a first-class action, and an append-only 12-type history. The T1 core the assessment rests on. |
| **5 · SLA & Automation** | Targets frozen at creation (A-20), round-robin auto-assignment actually called on the creation path, a periodic in-process breach sweep with the A-21 recipient cascade, and in-app notifications with a live badge. |
| **6 · Knowledge Base** | FAQs, help articles and solution guides as one typed article concept; keyword search with a single home (AD-13); public/internal visibility with a single home (constraint 19); admin authoring; portal reading. |
| **8 · Customer Portal** | All five routes point at designed screens. Submit, track, thread, help centre, feedback — with isolation proven by test rather than asserted. |
| **9 · Reports & Management** | All four metric groups, server-side aggregation (proven), department and branch filters, Manager+ gate enforced twice. *Verification pending — see below.* |
| **10 · Security & Administration** | Four fixed roles, server-side permission enforcement independent of the router, append-only audit with a single writer, three-tier configuration validated at startup. |

**Area 1 · Customer Management at 98%** is complete in substance — profiles, contact details,
interaction history, notes and attachments all work — and misses 100% only on the attachment
uploader's inability to display a size cap that no endpoint publishes (finding **I-12**).

### 🟡 Partially Delivered

| # | Requirement | Overall | What is there | What is missing |
|---|---|---:|---|---|
| 1.5 | Attachments | 92% | Upload, download, size cap enforced, `413` handled inline | The uploader cannot **show** the cap — no endpoint publishes it (**I-12**) |
| 3.3 | Live chat | 67% | Two-sided messaging over the full message model, both surfaces | Real-time transport, presence, chat queueing — no WebSocket/SignalR anywhere (T3-B) |
| 4.3 | Tasks and reminders | 67% | Ticket-scoped tasks: create, assign, due-date, mark done; the cross-ticket index exists | The **dashboard** cross-ticket task region — no endpoint (**S9-1**). Reminder alerts (scoped out by T2-C) |
| 7.3 | Automatic categorization | 67% | Suggestion offered on blur, pre-selects category and priority, override tracked, "AI-generated" label persists | **Recording the suggestion in ticket history** — the enum values exist, nothing writes them (**S9-4**) |
| 12.5 | Custom branding | 50% | Product name, logo and primary colour resolved from config at runtime | Any way to customise without a redeploy — no UI, no per-tenant themes (T3-E) |
| 3.1 · 3.2 · 3.4 · 11.3 | Email · WhatsApp · SMS | 33% | Outbound adapter interface + shipped console adapter, channel-agnostic message model, contract document | Real sending, provider adapters, and **all** inbound ingestion (**PF-2**) |
| 11.2 · 11.4 | ERP · External systems | 33% | Gateway interface + registered no-op, `Customer.externalReference` column | Any connection, field mapping, sync or conflict resolution — and no system is named (open) |
| 7.5 | AI chatbot | 8% | The AI abstraction named as the future extension point; documented in two places | The chatbot itself — no conversational surface, no grounding, no handoff |

### 📐 Outside Current Approved Scope

**These are decisions, not development failures.** Ten of the 56 company requirement lines are
assigned **T3** by [product-scope.md](product-scope.md) §5, which commits to *"a real seam plus a
runnable fake, not the feature"* — and every one of those seams was built and tested:

| Company requirement | Tier | Committed | Delivered against that commitment |
|---|---|---|---|
| Email · WhatsApp · SMS (§3, §11) | T3-A | Adapter interface + console adapter + contract doc | ✅ Met — outbound. ⛔ Inbound blocked on PF-2 |
| Live chat (§3) | T3-B | Polled messaging; real-time acknowledged as future | ✅ Met |
| AI chatbot (§7) | T3-C | The AI abstraction as the named extension point | ✅ Met |
| ERP · External systems (§11) | T3-D | Gateway interface + no-op fake | ✅ Met |
| Custom branding (§12) | T3-E | One default brand from configuration | ✅ Met |
| Web and mobile friendly (§12) | T3-F | Responsive web (A-1); native/PWA excluded | ✅ Met — and measured at 390 px in both directions |

**Additionally outside the 56 lines entirely** — [product-scope.md](product-scope.md) §8 lists the
**T4** exclusions: native apps and push, real-time chat transport, chatbot flows and handoff, real
provider sending, live ERP connections, multi-tenancy, a workflow/rules engine, a report builder and
PDF/Excel export, CSAT/NPS beyond the closing rating, KB versioning and multilingual variants,
customer merge/import, ticket merge/split/link, contracts and entitlements, telephony, any third
language — plus the technical exclusions (microservices, brokers, CQRS, Kubernetes, caching layers,
search engines, vector databases) and the quality attributes not pursued (performance targets,
security hardening beyond baseline, compliance programs, WCAG certification, exhaustive coverage).

**None of these is scored above**, because the company scope's 56 lines do not ask for them; they are
listed so their absence reads as a recorded decision.

### ⛔ Blocked / Pending Decisions

Only genuine blockers already present in the repository. **All three blockers are documented
contradictions or gaps between approved documents — in every case the implementation stopped and
recorded the gap rather than inventing behaviour**, which is the correct outcome under CLAUDE.md §3.

| ID | What is blocked | Requirement lines | Evidence |
|---|---|---|---|
| **S9-1** | The dashboard **cross-ticket** task region. ui-design §5.1/§13, story 14 AC 2 and the data-model §6 index `TicketTask(assignedUserId, isDone, dueAt)` all assume it; api-design §5.6 publishes only ticket-scoped endpoints. Story 14 left plan task 8 unbuilt. | **4.3** | [`agent-queue.component.ts:178`](../frontend/src/app/features/workspace/tickets/agent-queue.component.ts#L178) |
| **S9-4** | Recording whether the AI suggestion was accepted or overridden. `AiSuggestionOffered`/`AiSuggestionResolved` exist in the enum; **no code writes them** and `POST /tickets` carries no field that could. Blocks story 11 AC 4. | **7.3** | [`ticket-create.component.ts:298-308`](../frontend/src/app/features/workspace/tickets/ticket-create.component.ts#L298-L308) |
| **PF-2 / S9-10** | Inbound channel actor attribution. `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required, `actorKind = System` is reserved for the SLA monitor (R-14), and an inbound message has no human actor. Three options set out in story 18's plan; **none chosen.** Blocks story 18 AC 3. | **3.1 · 3.2 · 3.4 · 11.3** | [`BlockedInboundChannelIngestion.cs`](../backend/src/SupportCrm.Infrastructure/Seams/Channels/BlockedInboundChannelIngestion.cs) — the **1 skipped test** in the backend suite |

**Pending product decision, not blocking:**

| ID | Question | Status |
|---|---|---|
| **OQ-1** | What is the CSAT rating scale? T2-F fixes none. | 🔴 Open. **Interim behaviour implemented rather than a scale invented**: the server validates against the configured `Feedback rating scale` key and `RatingScaleDto(Min, Max)` is published to the dashboard. Feedback works end to end today; only the configured numbers are unmade. Reaches **8.5** and **9.4**. |

**Open, non-blocking findings** carried in the repository and unchanged by this audit: **I-12**
(attachment cap not published), **PF-5** (`firstRespondedAt` semantics — A-3 implemented as worded,
consequence reported), **F-1** (the transition matrix is duplicated client-side by approved decision
**UI-3**, confined to one file), **S9-11** (ui-design's header cites 65 endpoints and AP-1…AP-18
while api-design has 66 and AP-19 — documentation staleness only).

### 🔵 Verification Pending

Implemented, but a required check has **not** been recorded. **These are scored at their
implementation value and flagged here** — the code exists and the automated suites pass, but the
project's own verification standard has not been met, and PROJECT-PROGRESS §8 requires evidence
naming a command and its result before anything is marked Verified.

| Story | Requirement lines | What is outstanding |
|---|---|---|
| **14** `tasks-internal-notes` | 4.3, 4.5 | PROJECT-PROGRESS §3: *"⚠ Partial 2026-09-06 — backend, data layer and live stack verified; **front-end screen checks OUTSTANDING**."* |
| **15** `management-dashboard` | 9.1 – 9.5 | Same: *"backend, data layer and live stack verified; **front-end screen checks OUTSTANDING**."* |
| **17 Part B** `i18n-responsive-branding` | 12.1, 12.2, 12.5 | **No verification recorded at all.** Commit `22de7e2` has landed; no CHANGELOG entry, no §8 evidence row, and PROJECT-PROGRESS §3 still shows the story as `Not Started`. |
| **18** `channel-erp-adapters` | 3.1, 3.2, 3.4, 11.2, 11.3, 11.4 | **No verification recorded at all.** Commit `8d449bf` has landed and `docs/integration-seams.md` was added; no CHANGELOG entry, no §8 evidence row, §3 still shows `Not Started`. |

**14 requirement lines** sit in this state.

### 📋 Tracking accuracy finding (raised by this audit)

**[PROJECT-PROGRESS.md](PROJECT-PROGRESS.md) §3 understates delivered work.** It records stories
**08, 09, 10, 11, 17 and 18 as `Not Started`**, and all six have landed commits with working code:

| Story | §3 says | Commit | Code that exists |
|---|---|---|---|
| 08 `agent-dashboard` | Not Started | `eef55aa`, `a853cdd` | `agent-queue.component.ts`, `/workspace/queue`, `QueueOrderingTests.cs` |
| 09 `sla-routing-escalation` | Not Started | `a853cdd` | `SlaEvaluationService`, `SlaMonitorHostedService`, `RoundRobinAssignmentPolicy`, Notifications migration |
| 10 `ai-service-seam` | Not Started | `a853cdd` | `IAiAssistService`, `DeterministicFakeAiService`, `ProviderAiService` |
| 11 `ai-ticket-assists` | Not Started | `a853cdd` | `AiController` (3 endpoints), `TicketAiAssistService`, `ai-assist-panel` |
| 17 `i18n-responsive-branding` | Not Started | `22de7e2`, `da0ffaa` | `ar.json`/`en.json`, `direction.service.ts`, `responsive-phone-width.spec.ts` |
| 18 `channel-erp-adapters` | Not Started | `8d449bf` | `Seams/Channels/*`, `Seams/Erp/*`, `integration-seams.md`, 5 seam test files |

§3 carries a standing caveat that §10 narrates 08–11 as complete, so part of this is known. **The
CHANGELOG has no entry for stories 17 Part B or 18** — its newest entry is story 15 (2026-09-06),
while both later commits have landed.

**This is a reporting gap, not an implementation gap**, and it is recorded here rather than fixed:
correcting PROJECT-PROGRESS is a tracking task governed by its own Maintenance section, and this
audit was scoped to change nothing but this file. It is the reason the SDD/Docs dimension is 95% and
not 100%, and it cost the nine requirement lines owned by stories 17 B and 18 a 25-point deduction
each.

### 🔬 Evidence — checks run for this audit

Every check is from [README.md](../README.md) § *Checks*, run against the audited commit. No
substitutions. **All five were re-run for the second pass** and returned the same results as the
first.

| Command | Result (re-run 2026-09-07, second pass) |
|---|---|
| `dotnet build backend/SupportCrm.sln` | ✅ **Build succeeded · 0 Warning(s) · 0 Error(s)** (`TreatWarningsAsErrors` is on) |
| `dotnet test backend/SupportCrm.sln` | ✅ **Passed! Failed: 0, Passed: 480, Skipped: 1, Total: 481** — the 1 skip is `InboundIngestionTests.cs:47`, whose `Skip` reason is the PF-2 statement verbatim |
| `cd frontend && npm run build` | ✅ exit 0 — application bundle generation complete |
| `cd frontend && npm run lint:styles` | ✅ exit 0 — no physical CSS properties (logical properties only, ui-design §10.2) |
| `npx ng test --watch=false --browsers=ChromeHeadless` | ✅ **TOTAL: 99 SUCCESS** (99 of 99) |

**PDF verification performed for the second pass:**

| Check | Result |
|---|---|
| PDF read in full (both pages, text layer and rendered images) | ✅ 12 areas · 55 bullets · no other content |
| `diff` of PDF text against `docs/requirements.md` (normalized) | ✅ **zero differences** |
| MD5 of both normalized texts | ✅ `082aba3be21050ecc8527baea78904a8` — identical |
| Blocker re-verification in code (S9-1, S9-4, PF-2) | ✅ all three still present — see the Blocked section |
| `git status` before and after | ✅ only `docs/COMPANY-SCOPE-PROGRESS.md` modified |

**Not run, and therefore not claimed:** the live-stack verification of README § *Run it*
(`docker compose up --build`), which is what proves case-insensitive collation, `DateTimeOffset`
ordering and filtered indexes; and the **front-end screen checks** outstanding for stories 14, 15,
17 B and 18. Nothing in this document is marked Verified on the strength of the five commands above
alone — they establish that the code builds, passes its suites and lints clean, which is what they
are cited for.

### 🔎 Evidence — structural counts

| Measure | Count | How established |
|---|---:|---|
| Company requirement lines analyzed | **56** | `requirements.md` 55 bullets, with §1 *"Notes and attachments"* split per product-scope §6 |
| Stories in the backlog | **18** | [story-backlog.md](story-backlog.md) |
| Stories with a landed commit | **18** | `git log --oneline` — every story id appears |
| Published API operations | **66** | HTTP verb attributes across 16 controllers (+1 base class); matches api-design's 66 exactly |
| EF Core entities with a `DbSet` | **15** | [`IApplicationDbContext.cs`](../backend/src/SupportCrm.Application/Abstractions/IApplicationDbContext.cs) — matches data-model §2.1–2.15 exactly |
| Applied migrations | **8** | `Infrastructure/Migrations/` |
| Backend test files | **53** | `backend/tests/SupportCrm.Tests/**`, excluding fixtures and harnesses |
| Angular route entries | **39** (33 named + 6 redirects) | 5 route files: root, auth, workspace, admin, portal |
| Angular components | **54** | `features/` + `layout/` + `shared/components/` |
| Frontend spec files | **17** | `**/*.spec.ts` |
| i18n dictionaries | **2** (en 22 KB, ar 28 KB) | `frontend/src/assets/i18n/` |
| Seeders | **5** | `Infrastructure/Persistence/Seeders/` — organization, identity, customers, tickets (645 lines), knowledge |
| Real-time transport | **0** | No SignalR / WebSocket / socket.io match in `backend/src` or `frontend/src` — confirms T3-B is not built |

---

## Reproducing every number in this document

1. Take the **56 requirement lines** — `requirements.md`'s 55 bullets with §1 *"Notes and
   attachments"* split into two, per product-scope §6.
2. For each line, read its six dimension scores from that area's numeric table above. Every score is
   one of `100 · 75 · 50 · 25 · 0 · n/a`, and each non-100 score has a named reason in the prose
   beneath its table.
3. `Overall(line) = mean(Frontend, Backend, Database)`, excluding `n/a`.
4. `Area(dimension) = mean` of that dimension over the area's lines, excluding `n/a`.
   `Area(Overall) = mean` of the lines' Overall values.
5. `Project(dimension) = mean` over all **56** lines, excluding `n/a` — **not** a mean of the 12 area
   figures, which would mis-weight the 4-line areas.

Denominators, for checking — a dimension's denominator is 56 minus its `n/a` lines: Frontend **56**,
Backend **55** (mobile), Database **48** (summaries, suggested replies, quick replies, system
configuration, APIs, i18n, mobile, branding), User Stories **56**, SDD/Docs **56**, Overall **56**.

Project totals: Overall **4950 / 56 = 88.4%** · Frontend **4700 / 56 = 83.9%** ·
Backend **4925 / 55 = 89.5%** · Database **4375 / 48 = 91.1%** · User Stories **5375 / 56 = 96.0%** ·
SDD/Docs **5325 / 56 = 95.1%**.

Line-status tally, for checking: **44** lines at Overall 100 · **12** between 0 and 100 · **0** at 0.

---

## What the second pass changed

The first pass was built from [requirements.md](requirements.md) with the PDF unavailable, and said
so at the top. The second pass read the PDF and re-derived the document from it.

### What did not change, and why

**No percentage moved. No requirement was added, removed or re-tiered. No status flipped.**

That is not the second pass declining to do its job — it is the finding. `requirements.md` proved to
be a **byte-identical transcription** of the PDF (zero `diff` differences, matching MD5), and the PDF
contains **no** sub-requirements, acceptance criteria, constraints or priorities beyond its 55
bullets. There was nothing additional to fold in. Every repository finding was independently
re-verified against the code rather than copied forward, and all held:

| First-pass claim | Re-verified how | Result |
|---|---|---|
| Overall 88 · FE 84 · BE 90 · DB 91 · US 96 · SDD 95 | Recomputed from the 56 scored lines | ✅ Unchanged |
| 44 complete · 12 partial · 0 not implemented | Recounted from the tables | ✅ Unchanged |
| **S9-1** — no cross-ticket task endpoint | Searched controllers for a standalone `/tasks` route | ✅ Confirmed — only `{id}/tasks` at `TicketsController.cs:375,392,416` |
| **S9-4** — AI suggestion never recorded | Searched for any writer of `AiSuggestionOffered`/`Resolved` | ✅ Confirmed — the enum declaration and one comment; no writer |
| **PF-2** — inbound attribution undecided | Read the seam and the skipped test | ✅ Confirmed — `BlockedInboundChannelIngestion.cs:41` throws; `InboundIngestionTests.cs:47` is the 1 skip |
| PROJECT-PROGRESS §3 drift on 08–11, 17, 18 | Re-read §3, spot-checked a file per story | ✅ Confirmed — all six still read `Not Started`; all six have code |
| Build / test / lint all green | Re-ran all five commands | ✅ Confirmed — identical results |

### What did change

| Change | Why |
|---|---|
| The `⚠ Source-of-truth limitation` section became **`✅ Source of truth — the company PDF`** | The PDF is now the cited authority, named by filename, with the verification evidence that `requirements.md` matches it |
| **New finding: the PDF carries no acceptance criteria, constraints or priorities** | This makes every tier assignment and all 21 assumptions the *project's* interpretation of a one-line feature name. It is the single most important thing the PDF told us, and the first pass could not have known it |
| **`Outside current approved scope` re-framed** | It now reads explicitly as *a supplier-side decision with no evidence of company agreement*, rather than as a settled exclusion. A bullet reading `- WhatsApp` is, on its face, a request for WhatsApp |
| **New `Status definitions` section** | Six explicit definitions with counts, and a stated rule for where each flag appears |
| **`In approved scope` column added to all 12 traceability tables** | Separates *"the company asked"* from *"the project committed"* on every one of the 56 rows |
| **Status labels normalized across all 56 rows** | They now use the six defined labels only, and carry the `📐` / `⛔` flags explicitly. Counts reconcile: 44 / 12 / 0, with 10 outside-scope and 6 blocked |
| Header, audit date, evidence tables | Record the second pass, its commit, and the PDF checks |

### What a reader should take from this

The company's specification is **55 feature names on two pages**. Everything else in this
repository — the tiers, the assumptions, the simplifications, the definitions of done — is the
project's reading of those names. **88% measures delivery against that reading.** Where the reading
narrowed a bullet (round-robin only, in-app alerts only, keyword search only, branch as an attribute,
a seam instead of an integration), this document says so at the line that was narrowed, so the
company's own question — *"is this what we asked for?"* — can be answered bullet by bullet rather
than in aggregate.

---

## What this document does not do

- It **creates no story, feature, requirement or acceptance criterion**, and recommends implementing
  nothing.
- It **changes no scope**. Every tier assignment is read from [product-scope.md](product-scope.md) §6.
- It **changes no API contract, no database schema and no application code.**
- It **treats no T2 or T3 item as implemented without evidence** — every claim above names a file, a
  route, an endpoint, a migration, a test or a command result.
- It **leaves [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md), [product-scope.md](product-scope.md),
  [story-backlog.md](story-backlog.md) and [requirements.md](requirements.md) untouched**, including
  the PROJECT-PROGRESS §3 inaccuracy it reports. Correcting that file is a tracking task under its
  own Maintenance section.
