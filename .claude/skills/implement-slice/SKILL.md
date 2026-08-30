---
name: implement-slice
description: Implement one approved slice of a story plan — confirm the entry gate, read the plan and only the sections it cites, build exactly the slice's numbered tasks under the project's fixed conventions, record gaps as findings instead of inventing behaviour, then hand off to verify-slice. Use when the user has approved a specific slice and asks for it to be built. Implementation only — it verifies nothing, writes no tracking document, and never commits or pushes.
---

# Implement a slice

## 0. Entry gate — before any code

All five must hold. If any fails, **stop and report**; do not proceed under an assumption.

1. **The user has explicitly approved this slice.** Approval of the previous slice is not approval
   of this one (CLAUDE.md §4).
2. **Prerequisites are implemented, not merely planned** — the per-story half of gate 9 → 10
   (sdd-workflow §4). Check the plan's `## Prerequisites` against what actually exists.
3. **No open question or blocked decision gates this slice** — PROJECT-PROGRESS §6.1's *Blocks*
   column and any `⚠ Blocked decision` box in the plan. A gated slice stops here.
4. **Sequencing holds** — phase order, the *Must be sequential* list, **one migration at a time**,
   seeder `Order` 10/20/30/40/50 (00-implementation-plan §3, §5).
5. **The task contract is stated and confirmed** — see §1.

## 1. The task contract

**The slice is a set of numbered plan tasks, and nothing else.** Slice boundaries are not in the
plan — *"the plan's numbered tasks are"* — so the mapping comes from the feature's
`00-overview.md` *Implementation progress* table plus the user's explicit confirmation.

Open by restating: **story, slice, the exact task numbers, and the files those tasks name.** That
list is the scope for the whole session. *(Slice 6 of story 04 is tasks **11, 12, 13, 14** —
confirmed by the user 2026-08-30.)*

**The user's confirmed contract is authoritative.** The repository's slice map is known to lag: if
it disagrees with what the user confirmed, **report the disagreement and proceed on the user's
contract** — do not reconcile it, do not edit the map, and do not infer a different boundary from a
stale status cell.

**Never invent or re-cut a slice boundary.** Narrowing or widening is proposed and approved first
(precedent: slice 2 narrowed to task 3 alone; `User.CreateCustomerUser` moved from slice 5 to 3).

## 2. Read, in this order

1. **The story plan** — concrete and self-contained by design (sdd-workflow §6: *"a fresh, scoped
   session with only the plan attached"*).
2. **Only the sections its `## Context — Read These Files First` names.** The plan is the index into
   the design documents; it is not a substitute for the sections it cites, and it is not a licence to
   read them whole.
3. **The plan's `## Verification Steps` and `## Done Criteria` — before implementing**, not after.
   They define what this slice must be able to prove.

**Do not rediscover conventions from the code.** Folder layout, migration naming, seeder order,
string-length tiers and the one-implementation-per-shared-rule table are fixed in
00-implementation-plan §6.

## 3. Conventions that bind

- **Layering is compiler-enforced (AD-2)**; `Domain` keeps **zero** project and package references
  (AD-4); **no repository, unit-of-work, mediator or event bus (AD-3)**; one unit of work per
  request, committed once — no explicit transaction, no second `SaveChanges`.
- **Schema only through EF Core migrations** generated from the approved model, never hand-written
  SQL. Migration name = the story's subject in PascalCase.
- **No story picks a string length** — data-model §6.1's five tiers are binding, and `Text` is never
  indexed.
- **Server-derived fields are never accepted from a client** (AP-10); `storagePath` and
  `passwordHash` leave the server in no response (api-design §6, §7).
- **Front-end slices:** components never call `HttpClient` directly; filters live in the URL and
  mirror the API's names exactly; **logical CSS properties only**; guards hide but do not protect —
  every one mirrors a server rule enforced independently; error text comes from the Problem Details
  `type` slug, translated, and the server's `detail` is never rendered raw; `404` reads identically
  for missing and forbidden (AP-4).
- **Keep the build clean as you go** — `TreatWarningsAsErrors` is on and `lint:styles` bans physical
  CSS properties. Neither is a end-of-slice cleanup.

## 4. Do not invent

- **Never invent behaviour the approved documents do not define.** A gap becomes a numbered `I-*`
  finding stating **what was done, why it is not a product decision, and what the user must decide if
  the choice is wrong** (PROJECT-PROGRESS §6.8 sets the form).
- **Never answer an `OQ-*`, and never revisit an assumption.** A-1…A-n are fixed; disagreeing with
  one is a product-scope edit the user makes (sdd-workflow §6).
- **No new endpoint, entity, migration, configuration key or error slug beyond what the plan names.**
  Reuse the existing slug for an existing rule — *"do not mint a new one"*. A configuration key with
  no home in an approved document is finding I-1's situation: implement the minimum, record it, name
  what the user must decide.
- **A task that cannot be implemented as written** is not quietly reinterpreted: implement the
  smallest defensible reading, record the finding, and say what would change if the user reads it
  differently.
- **When the plan contradicts itself or a design document**, the authority order decides
  (CLAUDE.md §1) and the discrepancy is **recorded as a finding** — plans have been wrong before and
  the correction is documented in place, not applied silently.
- **A genuine judgment call is labelled as one**, with its reasoning, never presented as derived.

## 5. Deliberate incompleteness

Where a later story owns the completion, the project leaves a shaped hole rather than a guess:

- **A cross-referenced marker** naming the story and task that fills it —
  `// Story 05: replace the constant 0 with the ticket subquery`.
- **A test `Skip`ped by design**, naming the story that unskips it. Never delete it, never make it
  vacuous.
- **A half-feature fails closed** — it does not half-work.
- **Tests the plan's test task names are part of this slice**, written here, not deferred.

## 6. Cross-cutting work

A fix that is not one of the slice's tasks — a contract gap, a global setting, a shared layer — is
**never folded into the slice**. Propose it, get it approved separately, and let it be its own unit
of work with its own record (precedents: AP-10/I-9, AP-2/I-10, the front-end error layer, each *"not
a story task"*). Work outside the contract, **even obviously correct work, is proposed, not taken**.

## 7. Targeted tests are not verification

You may run targeted tests freely while building — to develop, to debug, to reach green:

```bash
dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~<Area>"
```

**That is implementation, not verification.** Such a run must never produce a PROJECT-PROGRESS §8
evidence row, and must never support the words *verified*, *done*, a percentage, a story status, or a
suite count presented as a result. Verification is `verify-slice`'s, in its own pass, from commands
it runs itself.

## 8. May modify / never modifies

| May modify | Never modifies |
|---|---|
| `backend/src`, `backend/tests`, `frontend/src` — **only for this slice's tasks** | The seven design documents: `requirements.md`, `product-scope.md`, `architecture.md`, `data-model.md`, `api-design.md`, `ui-design.md`, `sdd-workflow.md` |
| Files the tasks explicitly name | `PROJECT-PROGRESS.md`, `CHANGELOG-IMPLEMENTATION.md` — `record-progress` owns them |
| A migration, when a task calls for one | Any `.squad` plan file, **including the `00-overview.md` status cell** — `record-progress` owns that |
| `docker-compose.yml` / `appsettings.json` **only when a task names it** — and record it | `CLAUDE.md`, anything under `.claude/` |

**Never commit and never push.** You may propose the commit message —
`feat: story NN slice N — subject` — in the final report; the commit itself needs explicit user
instruction. Never rewrite, reorder or squash history.

Before finishing, review `git status --short`: every changed path must be one the contract predicted.
An unexpected path is **reported**, not explained away.

## 9. Stop and report

Stop immediately, without finishing the slice, if: a blocked decision or open question turns out to
gate the work · a task cannot be implemented as written · the plan contradicts an approved document
· the work needs a file outside the contract · a prerequisite proves not to be implemented.

Otherwise, finish the tasks and report:

- **Tasks completed**, by number, and the files touched.
- **Findings raised** (`I-*`), each classified: blocks the slice · needs a user decision ·
  informational.
- **Markers and skips left**, with the story and task that own them.
- **What was deliberately not touched**, and why — every changelog entry carries this clause.
- **The proposed commit message.**

Then **invoke `verify-slice`**. Do not self-verify, do not edit tracking, do not call
`record-progress` — it runs only after verification passes. After verification and tracking,
**stop and wait for explicit user approval** before the next slice (CLAUDE.md §4).

## Not this skill

Verification (`verify-slice`), tracking (`record-progress`), committing, and starting the next slice.
It claims no status, writes no evidence row, and edits no document outside the source tree.
