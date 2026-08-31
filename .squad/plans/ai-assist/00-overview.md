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

## Implementation progress

| Story | Plan tasks | Status |
|---|---|---|
| **10 — AI service seam** | **1–6** — the one interface and its contracts, `AiUnavailableException`, the deterministic offline fake, the provider adapter, configuration-driven selection with the fake as default, the module README recording the T3-C extension point, and the fourteen tests | ✅ **Done and verified 2026-08-31** |
| **11 — AI ticket assists** | **1–7** — the assist service, the three endpoints, the twelve tests, the `AiAssistPanel`, insertion through the one composer, categorization at creation on a new `/workspace/tickets/new` screen, and the S9-4 marked call site | ✅ **Done and verified 2026-08-31**, **except the S9-4-blocked history recording** |

**Story 10 created no endpoint, no screen and no entity** (DM-5), and **product-scope §9 question 1 —
which provider, and whether data-residency limits apply — is not answered**. The seam is what keeps it
open.

⛔ **Story 11 leaves one Done Criterion open, and it is S9-4, not an omission.** *"The suggested values,
and whether the agent accepted or overrode them, are written to ticket history"* has **no contract path
to carry it**: `POST /tickets` accepts six fields and none of them is this (api-design §5.6, §6.11, §7),
and `data-model.md` §2.7's `AiSuggestionOffered` / `AiSuggestionResolved` types therefore have no writer.
The client-side capture and a single **marked call site** are in place in `ticket-create.component.ts`.
**No field was invented** — the server rejects an unknown member with a `400` (AP-10), so a client that
sent one would break creation outright. The Stage 7 decision must be taken before this closes.

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
