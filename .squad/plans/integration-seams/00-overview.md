# integration-seams — plan overview

Entry point for the **integration-seams** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 18 | [18-story-channel-erp-adapters.md](18-story-channel-erp-adapters.md) | Channel and ERP integration seams with fakes | — | Stories 07, 09, 04 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/integration-seams/`](../../stories/integration-seams/).

## Implementation progress

Story 18 was delivered **whole** — every unblocked plan task in one unit. **Backend only: no
endpoint, no screen, no entity, no migration.**

| Story | Plan tasks | Status |
|---|---|---|
| **18 — Channel and ERP seams** | **1, 2, 3, 5, 6, 7** — the outbound adapter interface, the console/log adapter, `SupportCrm:Channels` and `SupportCrm:Erp` selection, the ERP gateway with its no-op, [`docs/integration-seams.md`](../../../docs/integration-seams.md), and the four test files | ✅ **Implemented 2026-09-07.** Backend build warning-free; suite **480 passing, 1 skipped** (30 this story's own). Verified against real SQL Server: the startup log names the **console** adapter and the **no-op** gateway with no integration configuration present |
| **18 — task 4, inbound ingestion** | **4** | ⛔ **BLOCKED — PF-2 / S9-10.** The normalized shape and the ingestion interface exist; the implementation throws with the reason and the acceptance test is **skipped with the reason**. **No attribution was invented.** Options A, B and C are in the plan and in [`docs/integration-seams.md`](../../../docs/integration-seams.md) §1.3 — the decision is the product owner's |

**One acceptance criterion of eight remains open**, and it is the blocked one: *"an inbound message
delivered through the fake adapter creates or updates a ticket using the SAME message model."* The
other seven are met.

## Dependency notes

**Phase 8. The safest single story to cut — nothing depends on it.**

- Delivers the **outbound channel-adapter interface with a console/log implementation**, the
  **no-op ERP gateway**, and `docs/integration-seams.md` — the written note, per seam, of what a
  real implementation would have to add. **That document is the eighth acceptance criterion, not a
  nicety.**
- ⛔ **PF-2 / S9-10 blocks the inbound half.** The fake inbound adapter has **no actor**, while
  `Ticket.createdByUserId` and `TicketMessage.authorUserId` are required and `actorKind = System` is
  reserved for the SLA monitor (R-14). Three options are set out in the plan; **none is chosen**.
  The ingestion implementation throws with the reason and its test is **skipped with the reason**.
- **No endpoint, no screen, no entity** (AP-11, §8.3, DM-6). **Do not name an ERP product** —
  product-scope §9 questions 2 and 3 stay open.
- T3. If cut, `docs/product-scope.md` §5 already documents these as designed-not-delivered, so the
  cut costs traceability, not correctness.
