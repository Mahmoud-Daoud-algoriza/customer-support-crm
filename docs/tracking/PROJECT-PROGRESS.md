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

## How to read this dashboard

This file is the **entry point**, not the whole picture. Everything that used to live here as one
1,775-line document has been split by responsibility into `progress/` and its subfolders — **no
information was summarized, merged, reworded or dropped in the split**; every file below is a
verbatim extract of what this document used to say in that section. If you are new to the project
and want *"where are we now, what's done, what's left"*, read in this order:

1. **[progress/overall-status.md](progress/overall-status.md)** — the current snapshot (§1).
2. **[progress/story-progress.md](progress/story-progress.md)** — the 18-story table (§3).
3. **[progress/next-steps/](progress/next-steps/)** — what happened on each story and what's next (§10).
4. Everything else on demand, following the links in each section below.

Section numbers (`§1`…`§10`) are unchanged throughout the project's other documents
(`CLAUDE.md`, the `.claude/skills/*`, the story plans) — a citation like "§6.8" still means the
same content, it now just lives at the linked path instead of inline in this file.

---

## 1. Overall Status

**Moved to [progress/overall-status.md](progress/overall-status.md).** Current SDD stage, current
phase, overall status, overall progress, the SDD pipeline percentage, code written, last updated,
current focus, next immediate step, and the frozen "previous focus" snapshots for stories 07, 06,
12, 13, 16 Part B and 14.

### 1.1 How the 35% is calculated

**Moved to [progress/progress-calculation.md](progress/progress-calculation.md).** The 35/65
weighting, the design-and-planning and delivery tracks, and the running commentary on why the
percentage moved (or didn't) at each story.

---

## 2. SDD Pipeline Progress

**Moved to [progress/sdd-pipeline.md](progress/sdd-pipeline.md).** The ten-row pipeline-stage
table, the numbering note against `sdd-workflow.md`, and the pipeline-level blockers.

---

## 3. Feature / Story Progress

**Moved to [progress/story-progress.md](progress/story-progress.md).** The 18-story table (Seq,
Story, Feature, Tier, SDD, Plan, Impl, Verified, Depends on, Blocker), the split-in-time
exceptions, the execution-order corrections, and the "nothing is Cut" rule.

---

## 4. Scope Coverage

**Moved to [progress/scope-coverage.md](progress/scope-coverage.md).** The T1–T4 tier tables, the
scope-item → plan-file matrix (§4.1), and the T4 exclusions.

---

## 5. Architecture / Technical Decisions

**Moved to [progress/architecture-decisions.md](progress/architecture-decisions.md).** The AD-*
and DM-* decision table and the one known (cosmetic) ordering issue in `architecture.md` §7.

---

## 6. Open Questions / Blockers

**Split across [progress/open-questions-and-findings/](progress/open-questions-and-findings/),
one file per register:**

| File | Contents |
|---|---|
| [open-questions.md](progress/open-questions-and-findings/open-questions.md) | 6.1 Active open questions (OQ-*) that gate a named story, and 6.2 the questions carried from product-scope that are non-blocking by design |
| [resolved-decisions.md](progress/open-questions-and-findings/resolved-decisions.md) | 6.3 — every resolved decision (R-*), kept rather than deleted |
| [design-stage-findings.md](progress/open-questions-and-findings/design-stage-findings.md) | 6.5 pre-flight findings (PF-*), 6.6 post-flight findings (B-*, N-*, F-*), and 6.7 the stage-9 traceability audit (S9-*) |
| [implementation-findings.md](progress/open-questions-and-findings/implementation-findings.md) | 6.8 — every implementation finding (I-1…I-40) raised while building stories 04 through 15, and the two deliberate plan deviations (D-1, D-2) |
| [decision-traceability.md](progress/open-questions-and-findings/decision-traceability.md) | 6.4 — which documents and story intakes each resolved decision actually changed |

---

## 7. Implementation Progress

**Moved to [progress/implementation-progress.md](progress/implementation-progress.md).** The
per-area table (Backend, Frontend, Database, Configuration, Seed data, Tests, Docker/infrastructure)
and the running endpoint-count narrative from 13 paths through 66.

---

## 8. Verification

Only entries with real evidence are marked verified. Every ✅ named a command that was run and what
it returned. **Split into [progress/verification/](progress/verification/):**

| File | Contents |
|---|---|
| [story-15.md](progress/verification/story-15.md) | Story 15 (`management-dashboard`) — 2026-09-06, all rows recorded in that story's own task |
| [story-14.md](progress/verification/story-14.md) | Story 14 (`tasks-internal-notes`) — 2026-09-06, all rows recorded in that story's own task |
| [earlier-stories-and-gates.md](progress/verification/earlier-stories-and-gates.md) | Stories 01–13 and 16 A/B, the SDD stage gates, and the canonical-command / plan-verification-step evidence for each |

---

## 9. Change Log

**Moved.** The implementation change log lives in
[CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md) as of 2026-08-30 — every entry from
2026-08-24 onward, newest first, unchanged. It was relocated to keep this dashboard small enough
to read in full; nothing was summarized, merged or dropped.

**It is not duplicated here.** New entries go in that file, in the same task as the change (see
Maintenance). This document keeps the *current* picture — §1, §3, §6, §7, §8, §10 — and the
changelog keeps the narrative history behind it.

---

## 10. Current Next Steps

**Split into [progress/next-steps/](progress/next-steps/), one file per story entry, in the same
reverse-chronological order the section always had, plus one file for the cross-cutting notes that
aren't about a single story:**

| File | Contents |
|---|---|
| [story-15.md](progress/next-steps/story-15.md) | `management-dashboard` — implemented, verification partial |
| [story-14.md](progress/next-steps/story-14.md) | `tasks-internal-notes` — implemented, verification partial, S9-1 open |
| [story-13.md](progress/next-steps/story-13.md) | `portal-self-service` — complete |
| [story-12.md](progress/next-steps/story-12.md) | `kb-articles-search` — complete |
| [story-16.md](progress/next-steps/story-16.md) | `audit-configuration` — complete in full (Parts A and B) |
| [story-11.md](progress/next-steps/story-11.md) | `ai-ticket-assists` — complete, S9-4 leaves one Done Criterion open |
| [story-10.md](progress/next-steps/story-10.md) | `ai-service-seam` — complete |
| [story-09.md](progress/next-steps/story-09.md) | `sla-routing-escalation` — complete |
| [story-08.md](progress/next-steps/story-08.md) | `agent-dashboard` — complete |
| [story-07.md](progress/next-steps/story-07.md) | `ticket-intake-messaging` — complete |
| [story-06.md](progress/next-steps/story-06.md) | `ticket-lifecycle` — complete, plus the OQ-3 / A-21 foundation built ahead of it |
| [story-05.md](progress/next-steps/story-05.md) | `ticket-core` — complete |
| [story-04.md](progress/next-steps/story-04.md) | `customer-records` — complete across all six slices |
| [operational-notes.md](progress/next-steps/operational-notes.md) | Phase-1 interleave record (stories 01–03), the shaped-hole marker notes, commit history, the outstanding-decisions queue, and the scope-realism note — none of these is about a single story |

---

## Maintenance

**This is a living document.** It must be updated in the *same task* as any meaningful project
change: completing a stage, changing an architecture decision or the data model, opening or
resolving a question, adding or changing a story, generating a plan, implementing a story, running
tests, hitting a blocker, cutting a feature, or reordering execution. Formatting-only edits do not
warrant an entry.

**The narrative entry for that change goes in
[CHANGELOG-IMPLEMENTATION.md](CHANGELOG-IMPLEMENTATION.md)** — newest first, in the same task — and
**not** in this file (§9). This document, together with the `progress/` files it links to, carries
only the current picture: §1, §3, §6, §7, §8, §10. As of this split, that material lives under
`progress/` (see each section above for the exact path) rather than inline — edit the linked file
directly; there is nothing to keep in sync by hand beyond keeping this index's links correct.

**Accuracy rules that override convenience:** never record progress that did not happen; never mark
Implemented when only a design exists; never mark Verified without evidence named in §8; never mark
a stage complete unless its gate is satisfied; never delete a historical decision or blocker — move
it to §6.3 instead.

**Recalculate §1.1** whenever a §2 row completes or a §3 story reaches Verified.
