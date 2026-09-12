[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

4. **Stories 01, 02 and 03 are done; phase 1 is closed.** The phase-1 interleave of S9-12 played out
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

5. **Story 04 left one marker, and story 05 consumed both of its own.** Slice 6's is
   `AttachmentListComponent.maxSizeBytes` — the shaped hole for **I-12**, which needs nothing but a
   published cap to fill and changes no other line when it arrives. **Story 05 has consumed both of the
   markers left for it:** the `Skip` on `BranchIsNotABoundaryTests.Ticket_has_no_branch_member` is
   **removed and the test runs**, and story 03's verification step 6 now has the ticket data it
   needed. Story 05 leaves **one new shaped hole** — `TicketAssignComponent`'s directory guard, the
   marker for **I-16**, which needs nothing but an agent-readable endpoint to fill. **Story 17 Part B inherits one measurement** from slice 6's RTL check — the
   staff shell sidebar does not mirror in Arabic, which its task 3 already owns by name.

5a. **Both placeholders are gone.** The create-user dialog and the user-detail form now bind a
   selector populated from `GET /departments` instead of taking a department id as free text, and the
   avatar menu shows the department **name**. **Two markers remain for story 05**, both deliberate and
   both cross-referenced at their call sites: the `Skip` on
   `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`, which cannot assert on a `Ticket` type
   that does not exist yet, and story 03 verification step 6, which names the `/workspace/tickets`
   department filter. Story 05 task 10 owns both.

3. **Everything through story 06 is committed; story 07 is not, by instruction.** Stories 04, 05
   and 06 are in history on a linear chain — slice 1 with story 16 Part A as `54abd75`, slice 2
   `a4230b9`, slice 3 `5c716b1`, the cross-cutting error layer `60b58e6`, slice 4 `346d28b`, the
   I-9 / I-10 fixes `cf22841`, slice 5 `c0c0043`, the operating manual and change-log extraction
   `6e431dd`, **slice 6 as `ca2b306`**, then `d3f3df5`, `6812e91` (A-20), `a633923` (**story 05**),
   `e3a111c`, `b13b7d8` (A-21) and `cd1629b` (**story 06**). **Story 07's 39 changed paths are in
   the working tree and uncommitted** — CLAUDE.md §3 commits only when asked, and the user asked for
   no commit. *(This item read "Nothing is uncommitted" through story 06, when it was true; it is
   updated here rather than left to contradict §8's Working tree row.)*

3a. **One cross-cutting front-end change sits alongside the story work.** The HTTP error-handling
   layer landed 2026-08-28 between slices 3 and 4 and is committed as `60b58e6`
   ([CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md)). It
   is **not** part of story 04 and closes none of its Done Criteria. **It added the front end's
   first toast surface** — PrimeNG `MessageService` + `<p-toast>` at the root — because none
   existed. Two things to know before slice 6 builds screens: the toast is the **transport-level**
   surface only and must not be mistaken for the A-13 notification centre (story 09), and `503` is
   deliberately excluded from it so story 11's AI panel can degrade locally as §9 requires.

4. **Answer the outstanding decisions, earliest first.** None blocked stories 01–03. **OQ-5, which
   gated story 04, was answered on 2026-08-27 (A-19), and OQ-2, which gated story 05, was answered
   on 2026-08-30 (A-20 — the due timestamps freeze; see R-16).** **No open question now gates story
   05.** The earliest remaining is needed before story 11:

   | When needed | Decision | Kind |
   |---|---|---|
   | ~~Before **story 04**~~ | ~~**OQ-5**~~ — **answered 2026-08-27 by A-19**: yes, atomically | ✅ Closed |
   | ~~Before **story 05**~~ | ~~**OQ-2**~~ — **answered 2026-08-30 by A-20**: they **freeze** | ✅ Closed |
   | Before **story 11** | **S9-4** — how is AI suggestion acceptance/override carried to the server? | Contract (Stage 7) |
   | Before **story 13** | **OQ-1** — what is the CSAT rating scale? | Product |
   | Before **story 14** | **S9-1** — publish a cross-ticket task endpoint, or drop the dashboard task region? | Contract (Stage 7) **or** Stage 8 cut |
   | Before **story 15** | **PF-4 / S9-9** — "tickets assigned": currently or ever? | Metric semantics |
   | Before **story 18** | **PF-2 / S9-10** — who is the actor on an inbound channel message? | Product |
   | Non-blocking | **PF-5** (`firstRespondedAt` null), **F-1**, **N-5**, **S9-11** | — · ~~OQ-3~~ **closed 2026-08-31 by A-21** |

5. **Optionally apply the S9-11 header correction** to `ui-design.md` — it cites *"65 endpoints,
   AP-1…AP-18"* where `api-design.md` now has **66** and **AP-19**. It is an edit to an approved
   document, so it is **offered, not taken**. Nothing depends on it.

6. **Consider scope realism.** 18 stories against a 9–12 hour budget is ambitious. Now that the
   plans exist, the honest option is to **implement 01–11 (the T1 core plus the AI slice) and treat
   12–18 as planned-not-built**, which is a stronger SDD demonstration than eighteen half-finished
   stories. The cut order in [story-backlog.md](story-backlog.md) already ranks them, and four of
   the seven cuttable stories are the ones carrying blocked decisions. **Any cut is recorded in
   [product-scope.md](product-scope.md) per its §10 rule and reflected here.**

