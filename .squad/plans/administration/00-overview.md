# administration — plan overview

Entry point for the **administration** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 16 | [16-story-audit-configuration.md](16-story-audit-configuration.md) | Audit log and system configuration | — | **Part A:** Stories 01, 03 · **Part B:** Stories 02, 06 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/administration/`](../../stories/administration/).

## Dependency notes

**SPLIT IN TIME — Phase 2 and Phase 7.** `docs/story-backlog.md` records the exception.

| Part | Phase | Delivers |
|---|---|---|
| **A — Configuration** | **2**, before Stories 04 and 05 | Option types, **startup validation**, `GET /config`, `GET /config/staff` |
| **B — Audit surface** | **7** | `GET /audit`, the audit screen, the read-only configuration screen |

- **Do not execute Part B early and do not defer Part A.** Story 05 cannot create a ticket without
  Part A's category list, category→department map (A-14) and SLA targets (A-3).
- The `AuditEntry` entity and the single `IAuditRecorder` land in
  [Story 02](../identity-access/02-story-auth-and-roles.md), whose AC requires sign-in recording
  (finding **S9-5**). Part B adds only the read surface.
- **AP-17's three audience tiers must not be merged back into one** — that was blocking defect B-2.
- The rating-scale key holds a **placeholder commented with OQ-1**; no `min`/`max` constant may
  appear anywhere else in the codebase.
- T2. If cut in part, keep the audit write path and drop the filtering UI.

## Delivery log

### 16 Part A — Configuration — **complete** (2026-08-27)

Tasks 1–5 only. **Part B is untouched** — no `GET /audit`, no audit screen, no configuration view,
and no front-end file of any kind. Part B stays at Phase 7, where its prerequisites (Story 06's
lifecycle actions) are met.

| Task | Delivered |
|---|---|
| 1 — Option types | Seven types in `Application/Configuration/`: `CategoryOptions` (the list **and** the A-14 map, in one place because A-14 makes them inseparable), `PriorityOptions`, `SlaTargetOptions`, `QuickReplyOptions`, `RegistrationOptions`, `AttachmentOptions`, `FeedbackOptions`. All bound, data-annotation validated and `ValidateOnStart`, following Story 01's pattern. **No configuration key beyond architecture §6.3's table and the attachment cap was introduced** |
| 2 — Startup validation | `ConfigurationValidator.cs` — six `IValidateOptions` implementations for the structural checks, plus `ValidateAgainstDatabaseAsync` for the two that read rows. Wired into `DatabaseInitializer` **after** migrations and seeding, exactly as the plan requires. Every message names the offending value |
| 3 — `GET /config` | Customer-safe tier, `[Authorize]` — all authenticated roles. Categories carry `code` and `name` only |
| 4 — `GET /config/staff` | Staff-only tier, policy `RequireAgent`. The four groups of api-design §6.9 |
| 5 — Tests | `ConfigurationTierTests` (8), `ConfigurationValidationTests` (10), `NoConfigurationEntityTests` (18) — **36 passing** |

**One contradiction in the plan, resolved the way the project already resolves this class of
problem.** Task 1 and task 2 check 4 both say `Priorities` is validated against *"the
`TicketPriority` enum"* — but that enum does not exist: `05-story-ticket-core.md` creates
`Domain/Modules/Tickets/TicketPriority.cs`, and Part A runs **before** Story 05 by design. Both
spellings encode the same authority (Story 05's plan annotates the enum `// A-6`), so
`PriorityOptions.ApprovedLevels` holds the four A-6 names and the validator compares against it.
**Story 05 must replace that array with `Enum.GetNames<TicketPriority>()` and delete it**, so the
names live in one place. Marked at the code, in the same style as Story 04's `openTicketCount`
placeholder. **No product rule was invented — A-6 fixes the four levels and always has.**

**Verified.** Build 0 warnings / 0 errors; the three Part A suites **36 passing**; the whole suite
**68 passing, 1 skipped by design**. Against real SQL Server: configuration validation runs after
seeding and the API comes up. **Fail-fast proven for all six checks by starting the real API with
each value broken in turn** — a dangling category department (message names `billing` and cites
A-14), a dangling `DefaultBranchId` (cites A-15), a fifth priority level, an inverted rating scale,
a zero attachment cap, and a priority with no SLA target; every one stops the host with a non-zero
exit and a message naming the value. Live tier check with a **real Customer token**: `/config`
returns exactly `categories` and `feedback`, **`departmentId` appears nowhere in the body**, and
`/config/staff` is `403`. Stories 01–03 re-checked live and unchanged.

**OQ-1 is untouched.** The rating-scale key holds `1..5` with a block comment in `appsettings.json`
naming OQ-1 and quoting architecture §6.3's *"inventing the answer is out of scope"*. Validation
checks `Min < Max` and nothing else — a 1–10 or a 0–1 binary scale passes just as happily, which is
the point. **No `min` or `max` constant exists anywhere else in the codebase.**

**Implementation choices not fixed by the approved documents**, both recorded at the code: the
list-bearing sections bind through a single `Items` (or `Levels`) property, because
`AddOptions<T>().Bind()` binds a section to an object and a bare JSON array is not one; and the
attachment cap's default is 10 MiB, a number no document states — which is precisely why it is
configuration rather than a constant.

### 16 Part B — Audit surface and configuration view — **complete** (2026-09-01)

Tasks 6–11. **Story 16 is now complete in full** — per the plan's own words, Part B *"is the last
administrative surface"*, so nothing further is deferred.

| Task | Delivered |
|---|---|
| 6 — Coverage confirmed | Every action `AuditAction` names already had a live write call site before this task (Story 02's sign-in and user-administration writes, Story 04's `UserEmailChanged`, Story 06's `TicketStatusChanged` and `TicketEscalated`) — confirmed by reading each call site. No write path was added |
| 7 — `AuditQueryService` | `Application/Modules/Administration/AuditQueryService.cs` — the one read method, newest first, filtered by `actorUserId`, `action` and a `from`/`to` range |
| 8 — `AuditController` | `GET /api/v1/audit`, `RequireAdministrator`-gated. No write, update or delete action |
| 9 — Tests | `AuditReadTests` (9) and `AuditAndHistoryAreSeparateTests` (3) — **12 passing**, reusing `TicketApiFixture` |
| 10 — Audit screen | `/admin/audit` — URL-bound filters (UI-9), a paginator, zero row actions |
| 11 — Configuration screen | `/admin/configuration` — reads `GET /config`/`GET /config/staff` through the existing `PlatformApiService`, branding from the already-loaded `RuntimeConfigService`; zero writable elements, verified live |

**Verified.** Build 0 warnings / 0 errors; backend suite **385 passing, 1 skipped** (12 this slice's
own); `npm run build`/`lint:styles` clean; front end **51/51** unchanged. Against real SQL Server and
the real running front end: coverage-by-hand for all four audited actions, every write verb `405`
with the row count unchanged, AD-10 independence proven live, and the plan's own DOM query confirming
zero writable elements on the configuration screen and exactly four filter controls on the audit
screen. Full evidence in [PROJECT-PROGRESS.md](../../../docs/PROJECT-PROGRESS.md) §8.

**No finding raised.** The `to` date-filter's end-of-day inclusivity is a UI implementation choice
recorded at the code, not a product or contract question.
