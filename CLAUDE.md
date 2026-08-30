# Customer Support CRM — agent operating manual

**This file is routing and working rules only.** It defines no product behaviour, no contract and no
architecture. Everything it points at governs it. If this file and an approved document disagree,
**the document wins and this file is wrong** — say so rather than following it.

## 1. Source-of-truth authority order

On any conflict, the higher entry wins:

1. [docs/requirements.md](docs/requirements.md) — the given input. **Never edited.**
2. [docs/product-scope.md](docs/product-scope.md) — scope tiers T1–T4, assumptions A-1…A-n, exclusions, open questions.
3. The stage design documents — [architecture.md](docs/architecture.md), [data-model.md](docs/data-model.md), [api-design.md](docs/api-design.md), [ui-design.md](docs/ui-design.md), [sdd-workflow.md](docs/sdd-workflow.md).
4. [docs/story-backlog.md](docs/story-backlog.md), the intakes under [.squad/stories/](.squad/stories/), and the plans under [.squad/plans/](.squad/plans/).
5. [docs/PROJECT-PROGRESS.md](docs/PROJECT-PROGRESS.md) — **reporting and tracking only.** It reports state; it never defines it.

**Never invent behaviour the approved documents do not define.** A gap is not a design opportunity —
see §3.

## 2. Document routing map

Consult the row, not the whole file; confirm a section with a heading search before trusting a line range.

| Need | Go to |
|---|---|
| Requirement lines | [requirements.md](docs/requirements.md) (14 lines — read whole) |
| Scope tier, an assumption A-*, an open question OQ-* | [product-scope.md](docs/product-scope.md) §2–§5 (tiers), §7 (assumptions), §9 (open questions), §10 (definition of done) |
| Layering, component responsibility, security boundary, seams, decisions AD-* | [architecture.md](docs/architecture.md) §1–§2, §4, §5, §7 (AD-*), §8 (exclusions) |
| Entity, relationship, constraint, index, string length | [data-model.md](docs/data-model.md) §2 (catalogue), §4–§5 (relationships, constraints), §6 (indexing; §6.1 string lengths and collation) |
| Endpoint, payload, status code, error slug, decisions AP-* | [api-design.md](docs/api-design.md) §2 (conventions), §3 (AP-*), §4 (authz), §5 (endpoints), §6 (payloads; §6.12 error envelope), §7 (server-derived fields) |
| Screen, route, state, RTL/responsive, decisions UI-* | [ui-design.md](docs/ui-design.md) §2 (routes), §3 (UI-*), §5–§7 (screens), §9 (empty/loading/error), §10 (i18n and direction) |
| Method, stage gates, working agreements, squad commands | [sdd-workflow.md](docs/sdd-workflow.md) §2 (pipeline), §4 (gates), §6 (agreements), §7 (commands) |
| **Implementation conventions — folders, naming, migrations, seeders, shared rules** | [00-implementation-plan.md](.squad/plans/00-implementation-plan.md) **§6**; also §3 (phases), §4 (dependencies) |
| The story being implemented | `.squad/plans/<feature>/NN-story-<slug>.md` — its numbered tasks are the scope |
| Slice map and per-story delivery state | `.squad/plans/<feature>/00-overview.md` |
| Current status, blockers, findings | [PROJECT-PROGRESS.md](docs/PROJECT-PROGRESS.md) §1, §3, §6, §10 |
| Historical detail on a past slice — what changed, why, evidence | [CHANGELOG-IMPLEMENTATION.md](docs/CHANGELOG-IMPLEMENTATION.md) — find the dated entry, read that range only |
| How to build, run, reset, configure | [README.md](README.md) |

**Do not copy these documents' contents here.** This table is the only thing that belongs.

## 3. Core implementation rules

- **One story, one slice at a time.** The plan's numbered tasks are the work; the feature `00-overview.md` slice map is the current cut.
- **Never silently expand scope.** Work outside the slice — even obviously correct work — is proposed,
  not taken. A cross-cutting fix is its own approved unit, recorded as such.
- **Ambiguity becomes a numbered finding, not an invented behaviour.** Record it (findings are numbered in
  PROJECT-PROGRESS §6) and say whether it blocks the slice; product and contract decisions are the user's call.
- **Cite the id** for any non-obvious decision — in a code comment where it helps a reader, and always in the
  change-log entry: `A-*`, `AD-*`, `AP-*`, data-model constraint numbers, `UI-*`, `S9-*`, `PF-*`, `OQ-*`, `I-*`.
- **Linear git history.** One commit per slice, `feat: story NN slice N — subject` (or `fix:` / `docs:`).
  Never rewrite, reorder, squash or drop a commit. Never delete a historical decision or finding —
  resolved items move to PROJECT-PROGRESS §6.3. Commit and push only when asked.

## 4. Approval gate

Per slice, in order:

1. **Implement** the slice's plan tasks — those and nothing else.
2. **Verify** with the commands in §5 and the plan's own verification steps. Evidence names the command
   and its result; no ✅ without one.
3. **Update tracking** — PROJECT-PROGRESS, in the same task (§7).
4. **Stop and wait for explicit user approval** before starting the next slice. Do not begin the next
   slice because the current one went well.

## 5. Verification commands

Canonical, from [README.md](README.md) § *Checks* — do not substitute alternatives:

```bash
dotnet build backend/SupportCrm.sln            # must be warning-free: TreatWarningsAsErrors is on
dotnet test  backend/SupportCrm.sln
cd frontend && npm run build && npm run lint:styles
```

`npm run lint:styles` fails on physical CSS properties — logical properties only (ui-design §10.2).
Front-end specs run as `npx ng test --watch=false --browsers=ChromeHeadless` (the karma target story 01
configured; recorded in PROJECT-PROGRESS §8).

**Against real SQL Server**, per [README.md](README.md) § *Run it*: `docker compose up --build`, or
`docker compose up -d --build api` for the API alone; `docker compose down -v` for a clean volume. Migrations
and demo seeding run at startup (AD-8). What the SQLite test host cannot prove — case-insensitive collation,
`DateTimeOffset` ordering, filtered indexes — is verified here, not in the unit suite.

## 6. Architecture constraints

Canonical: [architecture.md](docs/architecture.md) §7 and [00-implementation-plan.md §6](.squad/plans/00-implementation-plan.md).
The critical few, already decided — do not relitigate:

- **Layering is compiler-enforced, not conventional (AD-2).** `Domain` references nothing; `Application`
  → `Domain`; `Infrastructure` → both; `Api` → `Application` + `Infrastructure`.
- **`SupportCrm.Domain` ends with zero project and zero package references (AD-4)** — no EF attribute,
  no framework type.
- **No repository, no unit-of-work, no mediator, no domain-event bus (AD-3).**
- One unit of work per request, committed once. Schema changes only ever via EF Core migrations
  generated from the approved data model — never hand-written SQL.
- Everything else — module folders, migration naming, seeder order, string-length tiers, the
  one-implementation-per-shared-rule table, front-end structure — is in **00-implementation-plan §6**.

## 7. Progress tracking

[docs/PROJECT-PROGRESS.md](docs/PROJECT-PROGRESS.md) is updated **in the same task** as the change it reports,
per its own Maintenance section, which governs. Its accuracy rules override convenience: never record progress
that did not happen, never mark Verified without evidence named in its §8, never delete history.

**No status, percentage, finding, blocker or slice state is duplicated here** — it would be stale within
one slice. Read it from that file.

## 8. Efficiency rules

- **Never read a large document in full when a section will do.** `CHANGELOG-IMPLEMENTATION.md`,
  `data-model.md`, `api-design.md`, `architecture.md` and `ui-design.md` are all section-addressable.
- **Search headings first, then read the range**: `grep -n "^## " <file>`, then a line-range read.
- **Read the slice's plan file first.** It is concrete and self-contained; consult design documents only for what it cites.
- **PROJECT-PROGRESS is current status only** — the history is in `CHANGELOG-IMPLEMENTATION.md`, which is
  read one dated entry at a time and never wholesale to establish what is true now.
- **Do not rediscover conventions.** If it looks like a project convention, it is in 00-implementation-plan §6.

## 9. Scope boundary

This file is guidance and routing. It is **not** a specification, not a substitute for the approved
documents, and not a place to record product decisions. New product or contract content goes in the
document that owns it — per §1 — and this file gains at most a pointer.
