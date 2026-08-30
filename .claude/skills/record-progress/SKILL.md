---
name: record-progress
description: Update the project's tracking in the same task as the change — the PROJECT-PROGRESS.md status, story, findings, verification and next-step sections, a newest-first entry in docs/CHANGELOG-IMPLEMENTATION.md, and the status cell of the feature's .squad plan 00-overview.md. Use after verify-slice has passed and before asking for approval, and whenever a stage completes, an architecture decision or the data model changes, an open question is opened or answered, a story or plan changes, a blocker is hit, a feature is cut, or execution order is reordered. Not for a routine mid-implementation test run — only for a completed verification whose result is being reported. Tracking only — it touches no source code, no design document, no plan content beyond the status cell, and commits nothing.
---

# Record progress

**When.** In the **same task** as the change it reports. PROJECT-PROGRESS's own `Maintenance` section
governs and lists the triggers: completing a stage, changing an architecture decision or the data
model, opening or resolving a question, adding or changing a story, generating a plan, implementing a
story, running tests, hitting a blocker, cutting a feature, or reordering execution.
**Formatting-only edits warrant no entry**, and neither does a routine mid-implementation test run —
"running tests" here means a completed verification whose result is being reported.

## Precondition — verification must have happened

**A slice or story may be marked Verified only if `verify-slice` ran in this same task and passed.**

- Its evidence rows are the input to §8. **Text already in §8 is not sufficient evidence** — it
  records a *previous* run and proves nothing about this change.
- If `verify-slice` was not run, or it failed, or its guard-revert could not be restored and
  re-proved green: **do not mark Verified.** Implemented may still be recorded where it is true, with
  the verification state stated plainly as not yet done.
- **Every claim written here must trace to a command actually executed in this task.** Never reuse a
  previous slice's evidence, and never infer a result from a plan, a changelog entry, or an existing
  PROJECT-PROGRESS row — PROJECT-PROGRESS §7: *"re-verified by running the commands in §8 … not
  inferred from a plan."*

## Accuracy rules — these override convenience

Verbatim from `Maintenance`; they are this skill's hard constraints:

- Never record progress that did not happen.
- Never mark **Implemented** when only a design exists.
- Never mark **Verified** without evidence named in §8.
- Never mark a stage complete unless its gate ([sdd-workflow.md](../../../docs/sdd-workflow.md) §4)
  is satisfied.
- Never delete a historical decision or blocker. **How it is preserved depends on what it is** — see
  the §6 row below; §6.3 is headed *"Kept, not deleted"* and nothing leaves the document.

## 1. PROJECT-PROGRESS.md — section checklist

Touch only what changed; leave the rest byte-for-byte alone.

| § | Update |
|---|---|
| **§1** | Status table: current SDD stage, current phase, overall status, **Code written**, **Last updated**, **Current focus**, **Next immediate step** |
| **§1.1** | **Recalculate.** Required whenever a §2 row completes or a §3 story reaches Verified. A story counts only when implemented **and** verified **in full** — a slice moves the number by nothing until the story's last slice lands. The 35/65 weighting is a stated convention; keep the arithmetic reproducible |
| **§2** | Only if a pipeline row changed. Fill **both** the doc column and the gate column — *document exists ≠ stage complete* |
| **§3** | The story's Impl / Verified cells with dates, plus `Depends on` and `Blocker` if they moved |
| **§6** | New findings and closures — **see "Historical handling" below.** Never delete, never relocate |
| **§7** | The area row (Backend / Frontend / …) with its evidence |
| **§8** | Add the evidence rows produced by `verify-slice`, unchanged. **No ✅ without a named command and result** |
| **§10** | Next steps, the slice table's state, and an explicit statement of what is awaiting approval |
| `Maintenance` | Untouched unless the rule itself is changing |

**Do not add a changelog entry here.** §9 is a pointer; history lives in the file below.

### Historical handling — §6, matching the document's actual convention

**Nothing is relocated and nothing is deleted.** Cross-references from §1, §7, §8 and the changelog
point at these ids where they already sit, and moving a row breaks them.

- **An `I-*` finding is closed in place**, in the §6.x block it already lives in, by editing its own
  row to `✅ CLOSED <date>` plus what closed it and why. It does **not** move to §6.3. Precedent:
  I-9 and I-10, both still in §6.8.
- **An answered `OQ-*` gets both halves.** Strike it through in place in §6.1 with
  `✅ Closed <date> by <what answered it> — see R-nn below`, **and** add an `R-*` row to §6.3 carrying
  the full resolution, its date and where it landed. Precedent: OQ-5 — struck through in §6.1 and
  recorded as R-15 in §6.3. Doing only one half loses the record.
- **A new `I-*` finding** takes the next free number and is appended to the **current implementation-
  findings block** (§6.8 today). If that block's heading no longer covers the new work, **widen the
  heading** to say what it now spans — a new §6.x block is opened only for a new stage or audit, as
  §6.5–§6.8 were. Choosing where a row goes is bookkeeping; **it is never an occasion to decide a
  product question.**
- **Never answer an open question yourself.** Record only a resolution the **user** has taken, citing
  what they decided and when. *"Ambiguity gets recorded, not resolved silently"* (sdd-workflow §6),
  and assumptions A-1…A-n are fixed — disagreeing with one is a product-scope edit the user makes.

## 2. docs/CHANGELOG-IMPLEMENTATION.md

Add the new entry at the **top**, directly under the header block and above the previous newest.

- Heading: `### YYYY-MM-DD — story NN slice N: <subject>`, matching the existing entries. **Take the
  date from the actual current system date** — never carry over the previous entry's, and never
  infer it. The same rule governs every date this skill writes: §1 `Last updated`, §3 Verified
  dates, and §6 closure dates.
- **Move the `(latest)` marker** off the previous entry's heading and onto the new one.
- Body, following the established pattern: what changed and in which files · why, with the decision
  id cited (`A-*`, `AD-*`, `AP-*`, data-model constraint numbers, `UI-*`, `S9-*`, `PF-*`, `OQ-*`,
  `I-*`) · findings recorded · what was verified, including against real SQL Server · the test count
  with its delta · what was deliberately **not** touched.
- **Never edit, reorder, summarize or delete an existing entry.** This file is append-at-the-top
  only.

## 3. The feature's slice map

In `.squad/plans/<feature>/00-overview.md`, update **the status cell only** of the slice's row in the
*Implementation progress* table — the story plan's Done Criteria requires this map kept current.

**Nothing else in that file, or in any other plan file, may be changed** — not its design, decisions,
dependency notes, scope or task descriptions.

## 4. Consistency pass before you finish

- §1 *Code written*, §3, §7 and §8 must agree with each other. Where they disagree, the repository
  wins: check `git status` and `git log` rather than trusting a row.
- The §8 working-tree row is explicitly *"a snapshot, not a claim to be trusted over the
  repository"* — refresh it against `git log`, don't reason from it.
- Re-read the §1 and §10 headline sentences last: they are the two that most often survive a slice
  they no longer describe.

**Pre-existing drift is reported, not corrected.** A row that was already stale *before* this change
is outside this update's scope: **list it for the user** — the section, what it says, what is
actually true — and **leave it exactly as it is**. Correct it only when the user explicitly asks.
When they do, record the correction rather than rewriting silently; §2's row 10 carries the
precedent for how a corrected row says so.

## Not this skill

No source code. No design document (`docs/requirements.md`, `product-scope.md`, `architecture.md`,
`data-model.md`, `api-design.md`, `ui-design.md`, `sdd-workflow.md`). No plan content beyond the
status cell in §3. No commit, no push, no change to the authority order, and no start on the next
slice — that waits for explicit user approval (CLAUDE.md §4).
