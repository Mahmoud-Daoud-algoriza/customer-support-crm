# SDD Workflow — Customer Support CRM

How this project moves from a requirements list to running code, which artifact exists at each
stage, where it lives, and what must be true before the next stage starts.

**Method:** spec-driven development, executed with [squad-kit](https://github.com/AzmSquad/squad-kit) v0.2.0
(workspace at [.squad/](../.squad/)).
**Status date:** 2026-08-24 (stage 5 complete)

---

## 1. Source-of-truth rule

Two documents are the source of truth for *what* is being built. Everything downstream cites them
and nothing downstream contradicts them:

| Document | Owns |
|---|---|
| [requirements.md](requirements.md) | The requirement lines themselves. **Never edited** — it is the input we were given. |
| [product-scope.md](product-scope.md) | Scope tiers (T1–T4), the 13 explicit assumptions (A-1…A-13), and the out-of-scope list. |

**Change control:** if implementation reveals that an assumption is wrong or a scope item is
undeliverable, the fix is to edit [product-scope.md](product-scope.md) and record the change —
not to quietly build something different. Product-scope §10 already states the cut rule: *"Any cut
is recorded in this document rather than left silent."*

---

## 2. The pipeline

| # | Stage | Artifact | Location | Status |
|---|---|---|---|---|
| 1 | Requirements | Requirements inventory | [docs/requirements.md](requirements.md) | ✅ Given |
| 2 | Requirements analysis | FRs, NFRs, actors, workflows, ambiguities | Conversation record; distilled into stage 3 | ✅ Done |
| 3 | Product scope | Tiered scope + assumptions + exclusions | [docs/product-scope.md](product-scope.md) | ✅ Done |
| 4 | **User stories / functional requirements** | 18 story intakes | [.squad/stories/](../.squad/stories/) + [docs/story-backlog.md](story-backlog.md) | ✅ Done |
| 5 | Architecture | Architecture decision record | [docs/architecture.md](architecture.md) | ✅ Done |
| 6 | Data model | Conceptual → logical model | `docs/data-model.md` | ⬜ **Next** |
| 7 | API design | Endpoint contracts | `docs/api-design.md` | ⬜ Pending |
| 8 | UI design | Screen inventory + flows | `docs/ui-design.md` | ⬜ Pending |
| 9 | Implementation plans | `NN-story-*.md` per story | [.squad/plans/](../.squad/plans/) | ⬜ Pending |
| 10 | Implementation | Application code | `backend/`, `frontend/` | ⬜ Not started |

Stages 5–8 are ordinary Markdown documents in `docs/`. Stage 9 is squad-kit's generated plan
format and is produced by `/squad-plan`, one story at a time.

---

## 3. How squad-kit maps onto this

squad-kit is a three-step loop — **intake → plan → implement** — that covers stages 4, 9 and 10.
Stages 5–8 are project documents squad-kit does not model; the plan generator reads them as
context because the intakes point at them.

```
docs/requirements.md ─┐
docs/product-scope.md ─┴─▶ .squad/stories/<feature>/<id>/intake.md      (stage 4 · done)
                                       │
     docs/architecture.md ┐            │
     docs/data-model.md   ├─ context ──┤                                (stages 5–8 · next)
     docs/api-design.md   │            │
     docs/ui-design.md   ─┘            ▼
                            /squad-plan <intake>
                                       │
                                       ▼
                      .squad/plans/<feature>/NN-story-<slug>.md         (stage 9)
                                       │
                                       ▼
                      scoped agent session, plan attached               (stage 10)
```

**Why stages 5–8 come before any plan is generated:** squad-kit plans are deliberately concrete —
file paths, type names, signatures, verification commands. A plan written before the data model
and API contracts exist would invent them, and eighteen stories would each invent them
differently. The intakes therefore all carry the same instruction: *"Do not invent tables or
endpoints here; Stage 5 and Stage 6 fix those first."*

---

## 4. Stage gates

A stage is not finished when its document exists — it is finished when the gate below is true.

**Gate 4 → 5 (met).** Every requirement line has a story; every story cites its requirement
section and scope tier; no story introduces a feature absent from [product-scope.md](product-scope.md).

**Gate 5 → 6 (met).** [architecture.md](architecture.md) states the layering inside the single backend, where the
T3 seams sit (AI service, channel adapter, ERP boundary), how department scoping is enforced
server-side, and where configuration is read. It must explicitly restate the technical exclusions
so the constraint survives into planning.

**Gate 6 → 7.** `docs/data-model.md` covers every entity implied by the T1/T2 stories, with the
A-5 status set and A-2 department/branch asymmetry represented. Conceptual and logical only —
migrations are written during implementation, not during design.

**Gate 7 → 8.** `docs/api-design.md` gives a contract for each capability the stories need, with
role-based access stated per endpoint (A-4), and matches the data model exactly.

**Gate 8 → 9.** `docs/ui-design.md` lists every screen for the agent workspace, the customer
portal and the admin surfaces, notes RTL implications (T2-J), and confirms phone-width behaviour
(T3-F).

**Gate 9 → 10.** A story's plan exists, cites concrete paths and verification commands, and its
prerequisites are already implemented.

---

## 5. Traceability convention

Every artifact carries, at the top, the requirement sections and scope items it derives from:

```
> **Source of truth:** docs/requirements.md §2.4–2.5 · docs/product-scope.md T1-B, A-5
```

This makes the chain auditable in both directions — from a requirement line forward to the code
that satisfies it, and from a line of code back to the requirement that justifies it. That
traceability is the SDD deliverable of this assessment (product-scope §10, item 6).

---

## 6. Working agreements

- **Plan once, execute cheap.** Planning happens in a session that has read the design documents.
  Implementation happens in a fresh, scoped session with only the plan attached.
- **One story per plan file.** Stories are sized so a plan fits an evening's slice.
- **Assumptions are not decisions to revisit.** A-1…A-13 are fixed for the assessment. Disagreeing
  with one is a product-scope edit, not an implementation choice.
- **Ambiguity gets recorded, not resolved silently.** Product-scope §9 holds seven open questions;
  they stay open.
- **Secrets never enter the repo.** `.squad/secrets.yaml` is git-ignored by squad-kit; application
  secrets follow the same rule via environment configuration.

---

## 7. Commands

| Purpose | Command |
|---|---|
| Workspace status | `squad status` |
| List stories and plan state | `squad list` |
| Health check | `squad doctor` |
| New story intake | `squad new-story <feature> --no-tracker --id <id> --title "..."` |
| Generate a plan | `/squad-plan .squad/stories/<feature>/<id>/intake.md` |
| Compose the plan prompt manually | `squad new-plan <intake-path>` |

Tracker integration is set to `none`; story ids are folder names, and plan sequence numbers
(`NN`) are global across features (`naming.globalSequence: true` in
[.squad/config.yaml](../.squad/config.yaml)).
