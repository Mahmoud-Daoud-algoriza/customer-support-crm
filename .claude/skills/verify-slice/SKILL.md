---
name: verify-slice
description: Verify a completed story slice before it is reported — run the canonical build, test and lint commands, execute the plan's own verification steps against real SQL Server, apply the project's four proof techniques, check the slice boundary and regressions, and produce the PROJECT-PROGRESS §8 evidence rows. Use after implementing a slice and before updating tracking or asking for approval. Verification only — it edits no tracking document, commits nothing, and starts no further work.
---

# Verify a slice

**When.** After a slice's implementation is finished, before `record-progress` and before asking for
approval. Not for mid-implementation spot checks.

**Read first:** the story plan's `## Verification Steps` and `## Done Criteria`, and the slice row in
`.squad/plans/<feature>/00-overview.md`. They define what this slice claims; everything below proves
the claim or refuses it.

## 1. Canonical commands

**Do not substitute alternatives, and do not invent new ones.** Each is sourced below.

```bash
dotnet build backend/SupportCrm.sln                        # 0 warnings — TreatWarningsAsErrors is on
dotnet test  backend/SupportCrm.sln [--filter "FullyQualifiedName~<Area>"]
cd frontend && npm run build && npm run lint:styles
npx ng test --watch=false --browsers=ChromeHeadless        # front-end specs
```

| Command | Source |
|---|---|
| `dotnet build` | [README.md](../../../README.md) § *Checks*; 00-implementation-plan §9 — the two agree |
| `dotnet test` (the `--filter` form) | 00-implementation-plan §9; the story plan's own steps |
| `npm run build && npm run lint:styles` | [README.md](../../../README.md) § *Checks* |
| `npx ng test --watch=false --browsers=ChromeHeadless` | PROJECT-PROGRESS §8 — the karma target story 01 configured. **Not in README and not in 00-implementation-plan §9**; §8 is its only source |

**Where the two sources differ, README is canonical** (CLAUDE.md §5). 00-implementation-plan §9 gives
the front-end checks as `npm ci && npm run build` and `npx stylelint "src/**/*.scss"`; README's
`npm run build && npm run lint:styles` supersedes both — use README's form.

Record the suite result as `passed/failed/skipped` **with its delta** against the previous run. A
skip must be a deliberate, named skip — say which story removes it.

## 2. The plan's own verification steps

Run **every** step in the plan's `## Verification Steps`, in order.

A step that cannot run in this slice is **declared, not skipped**: state why, and name the slice or
story that will run it. Precedents in PROJECT-PROGRESS §8 — *"Story 03 — step 6 not runnable"* and
*"plan verification steps 4, 5 and 7 are not runnable in this slice"* — are the required shape.

## 3. Against real SQL Server

```bash
docker compose up -d --build api
```

**`docker compose down -v` is destructive** — it discards the database volume and every seeded and
hand-entered dev row with it. It is **not** a routine alternative to the command above. Use it only
when the check actually requires a from-scratch schema (story 03 was verified *"on a wiped volume"*),
say so in the evidence, and confirm with the user first.

The SQLite test host cannot prove these, so they are proven here or not at all:

- **Case-insensitive uniqueness and matching** (A-10, data-model §6.1 collation).
- **`DateTimeOffset` ordering** — SQLite refuses it in `ORDER BY`.
- **Filtered and unique indexes**, check constraints, and foreign keys as actually created.
- **The migration applying from scratch**, and seeders running in real `Order`.

## 4. The four proof techniques

Apply each **as applicable** — where the slice's own rules make it meaningful, and not otherwise. A
technique that does not apply to this slice is stated as not applicable, not quietly omitted. These
are the project's established methods (00-implementation-plan §9; every §8 evidence row).

1. **The guard is load-bearing.** Revert the one line that enforces the rule and show how many tests
   go red — and that the ones staying green are exactly the nothing-legitimate-broke checks.

   **This temporary source edit is explicitly in scope for this skill, and it is the only one.** It
   carries four obligations, all of which must be met before verification is finished:
   1. **Restore** the reverted line to exactly its prior content.
   2. **Confirm the restore** with `git status --short` and `git diff` — the file must show only the
      slice's intended changes, with no trace of the revert.
   3. **Re-run** the affected tests and show them green again.
   4. **Report** the revert, the red count, and the confirmed restore in the evidence.

   If any of the four cannot be completed, say so plainly and treat verification as **failed**
   (§9) — never leave a reverted guard in the working tree.
2. **Proof by comparison (AP-4).** A resource the caller may not reach and one that does not exist
   must return the **same status and the same body** — assert them against each other, not
   separately.
3. **Raw-JSON assertions.** Sweep responses for `passwordHash`, `storagePath` and any staff field on
   a portal payload by searching the **value actually stored in the database**, not only the property
   name, and over the raw response text rather than a parsed field.
4. **Row-level assertions.** After a call that must be refused, **re-read both rows** and assert
   nothing was written. A status code is not evidence that no write happened.

## 5. Slice boundary and regression

Every slice records these, **as applicable** — the fourth is a backend-slice check and does not apply
to a front-end slice:

- **Boundary:** routes belonging to later slices return `404`; assert the `openapi/v1.json` path
  count and that new request schemas publish exactly their mapped fields.
- **Regression:** every pre-existing endpoint answers as before, including the deliberate `405`s.
- **Restore:** any seeded row mutated during verification is patched back and **re-read**.
- **Front end untouched** (backend slices): its build and specs pass unchanged — that they pass
  without edits is itself the proof the server conformed rather than the client being bent to fit.

## 6. Front-end slices, additionally

Drive the real screens through the Chrome DevTools Protocol against the running `web` container:
each role signs in and lands correctly, guards deep-link to `/403`, RTL **mirrors** rather than
re-aligns, and there is no horizontal body scroll at 390 px (T3-F, ui-design §10).

**If no browser-driving capability is available in this session, do not fabricate the result.** Say
the check could not be run and why, list the specific screens and assertions that still need manual
verification, and mark the slice's UI evidence **outstanding** — a front-end slice is not verified
until they are covered by someone.

## 7. Findings, not inventions

If the approved documents do not settle a question the slice ran into, record a numbered `I-*`
finding stating: what was done, **why it is not a product decision**, and what the user must decide
if the choice is wrong (PROJECT-PROGRESS §6.8 sets the form). Classify each as **blocks the slice**,
**needs a user decision**, or **informational**. Never resolve a gap by inventing behaviour.

**Never answer an open question yourself.** An `OQ-*`, or any question the approved documents leave
open, is recorded for the user's decision — *"ambiguity gets recorded, not resolved silently"*
(sdd-workflow §6). Assumptions A-1…A-n are fixed; disagreeing with one is a product-scope edit the
user makes, not an implementation choice.

## 8. Output — the evidence rows

Produce one PROJECT-PROGRESS §8-shaped row per check:

```
| <check name> | ✅ Passed <date> | <command run> → <what it returned>; <counts and delta>; <what was proven> |
```

**No ✅ without a named command and its result.** State skips as skips. Hand these rows to
`record-progress`; do not write them yourself.

### Evidence provenance — non-negotiable

**Every claim must come from a command actually executed in this task.** PROJECT-PROGRESS §7 states
the rule the project already runs on: *"Every claim below was re-verified by running the commands in
§8 … **not inferred from a plan**."*

- **Never reuse a previous slice's §8 evidence**, in whole or in part, however similar the check.
- **Never infer a result** from the story plan, the changelog, an existing PROJECT-PROGRESS row, or
  from the code looking correct.
- If a command was not run in this task, it has **no** evidence row — say it was not run.

A well-formed row is not evidence. A row describing a command this task actually executed is.

## 9. If verification fails

If any required check fails — a red test, a failed build, a lint error, a plan step that does not
produce its stated result, or a guard-revert that cannot be restored and re-proved green (§4):

1. **Stop.** Do not continue to the remaining checks as though the slice were sound.
2. **Do not hand off to `record-progress`**, and do not let anything be marked Implemented or
   Verified.
3. **Do not report the slice as done or verified.**
4. **Report the failure with the actual output** — the command, its exit state, and the relevant
   lines — plus what it implies about the slice.

A partial pass is a fail. The slice returns to implementation, and verification restarts from §1.

## Not this skill

Does not edit PROJECT-PROGRESS.md, CHANGELOG-IMPLEMENTATION.md or any plan file. Does not commit or
push. Does not begin the next slice — that waits for explicit user approval (CLAUDE.md §4).

The **only** source edit it may make is the temporary guard revert of §4, which must be restored and
the restore confirmed before it finishes.
