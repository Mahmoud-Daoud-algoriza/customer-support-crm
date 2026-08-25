# ai-assist — plan overview

Entry point for the **ai-assist** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 10 | [10-story-ai-service-seam.md](10-story-ai-service-seam.md) | AI service abstraction with deterministic offline fake | — | Story 01 only — **parallelizable** |
| 11 | [11-story-ai-ticket-assists.md](11-story-ai-ticket-assists.md) | Ticket summaries, suggested replies and auto-categorization | — | Stories 10, 05–08 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/ai-assist/`](../../stories/ai-assist/).

## Dependency notes

**Phase 5**, but **10 can run in parallel from Phase 1** — it depends only on Story 01, touches no
file another story edits, and creates **no entity and no migration** (DM-5).

- **10** makes A-8's advisory rule **structural**: the interface has no method that can send,
  transition or assign, and a reflection test fails the build if one is added (AD-12). Its
  deterministic offline fake is what makes *"runs with no external accounts or credentials"* true
  (product-scope §10 item 5).
- **11** consumes it. ⛔ **S9-4 blocks its AC 4:** no contract path exists to record whether an AI
  suggestion was accepted or overridden — `POST /tickets` accepts no such field and no recording
  endpoint exists. The client-side capture and a marked call site are in place; **do not invent a
  field.**
- Both are **T1 and cannot be cut.** If time is short, reduce to the smallest working version of all
  three assists rather than polishing one.
