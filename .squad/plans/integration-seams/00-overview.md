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
