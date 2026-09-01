# Implementation Change Log — Customer Support CRM

> **This file reports history. It does not define state.**
> It holds the change log that was [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md) §9 until
> 2026-08-30, moved here unchanged so the dashboard stays small. The authority order is
> unchanged and is stated at the top of [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md): nothing here
> is a source of truth, and on any conflict the approved documents and the repository win.
>
> **Current** status, findings, blockers and next steps are **not** here — they are in
> [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md) §1, §3, §6, §7, §8 and §10.
>
> **Read a dated entry, not this file.** Find the entry you need by its date or story heading
> and read that range only.

---

Newest first. Every meaningful project change gets an entry.

### 2026-09-01 (latest) — story 13: customer portal self-service and feedback

**Phase 6 closes.** Story 13 delivered **whole rather than in slices**, like stories 05, 06, 07 and
12: all twelve plan tasks, requirements **§8** in full — submit, track, view history, access FAQs and
submit feedback — as *a separate, simpler surface for the Customer role over the same backend*, not a
second application with its own data.

**What changed, and where.**

- **Domain** — `Modules/Tickets/CustomerFeedback.cs` (data-model **§2.15**). It sits under
  **`Tickets`**, which is **DM-7**: `customer-portal` is an Angular area and a planning slug, not a
  backend module, and **no eleventh module was created**. **Write-once by construction** — private
  setters, **no mutator at all**, no `Update`, no `Withdraw` — so §2.15's *"not editable, not
  resubmittable"* is a property of the type rather than a rule a service remembers. The factory
  refuses only what is structurally impossible (no ticket) and **range-checks nothing**: **there is no
  rating constant anywhere in the Domain** (**OQ-1**).
- **Infrastructure** — `Persistence/Configurations/CustomerFeedbackConfiguration.cs` and migration
  `20260901154739_CustomerFeedback`. **Unique index on `TicketId`** (data-model §6, §5 constraint 21),
  FK to `Ticket` as **`Restrict`** rather than cascade — a rating is a reporting fact feeding the §9.4
  average, and because the *absence* of a row is meaningful, deleting one with its ticket would
  silently change a reported number. **No check constraint on `Rating`**, deliberately: a range here
  would encode **OQ-1** into the schema, which §2.15 forbids.
- **Application** — `Modules/Tickets/CustomerFeedbackService.cs`, whose four preconditions run in a
  fixed order and the order *is* the contract: **scope first** (`404`, **AP-4**) so a customer never
  learns from a `409` that someone else's ticket exists; then *has reached `Resolved`*; then
  one-per-ticket (`409 feedback-already-submitted`); then **the configured range** (`400`). *"Has
  reached `Resolved`"* is read from **`ResolvedAt`, not from the current status** — that stamp is the
  only record the ticket ever got there, so a `Closed` ticket qualifies (it is reached *from*
  `Resolved`) and so does one the customer reopened, both of which a status check would get wrong.
  `PortalTicketService` gained `ListAsync`, `GetAsync` and `TransitionAsync`; the transition
  **delegates to story 06's `TicketLifecycleService`** and restates **no part of A-16** — the matrix
  has one home in `TransitionAuthority`, and a second copy could drift in the quiet direction. It then
  re-reads the ticket as `PortalTicketDto`, which costs one query and makes it **impossible** for the
  staff DTO the lifecycle service returns to escape through a portal route. `hasFeedback` is an
  **`EXISTS` projection**, never a column (api-design §7, finding **N-4**).
- **Api** — `PortalTicketsController` gained the remaining **six** endpoints of api-design **§5.7**:
  the own list, the own detail, the transition, both attachment actions and feedback. With story 07's
  three and story 12's two, **the portal path space is complete at eleven** and matches §5.7 endpoint
  for endpoint. Story 07's `Location` header was retargeted from the thread to the detail action, as
  its own comment asked once that action existed. **There is no portal download route** — `GET
  /attachments/{id}/content` serves every role, **AP-19**'s single named exception to AP-5's split.
- **Infrastructure (seed)** — `TicketSeeder` gained three `Resolved` requests and one rating, so every
  branch of ui-design §7.3 is demonstrable in one sign-in: an unrated `Resolved` request **for each of
  the two portal customers**, one already rated (so story 15's §9.4 tile is non-trivial and the
  "already rated" branch is visible), with the existing `New` and `Pending` rows carrying cancel and
  the automatic reopen. Both new transitions go through `Ticket.TransitionTo`, so `ResolvedAt` is
  stamped by the entity exactly as an endpoint stamps it — which matters, because that is the field
  the feedback precondition reads.
- **Front end** — `layout/portal-shell/` is now **its own shell** rather than the staff `AppLayout`:
  **two destinations, no sidebar, no notification bell** (ui-design **§4.2**). The Customer branch was
  removed from `layout/component/app.menu.ts`, because a customer no longer mounts that component.
  `features/portal/portal-requests.component.ts` is the **card** list of §7.1 with its *"response
  needed from you"* cue and a URL-bound status filter (**UI-9**); `portal-submit-request.component.ts`
  keeps its **exactly four** inputs (§7.2); `portal-request-detail.component.ts` replaces the Story 07
  stub with the five regions of §7.3. `shared/components/rating-input/` takes `{ min, max }`.
  `core/api/portal.client.ts` gained five methods and `PortalTransitionTarget`, a union of the **two**
  targets A-16 allows.

**Why, with the ids.**

- **DM-7** — feedback is domain behaviour attached to a ticket, so it lives in `Tickets`. **No
  eleventh backend module.**
- **AP-5 / AP-16 / UI-11** — the portal returns a **distinct DTO**, not the staff one with fields
  hidden. `PortalIsolationTests` asserts the absence of `assignee`, `departmentId`, `priority`,
  `firstResponseDueAt`, `resolutionDueAt`, `firstResponseBreached` and `resolutionBreached` **on the
  raw JSON** of the detail, a list row *and* the transition response — a type is what the server
  promises; the payload is what a customer receives.
- **A-16 / A-18** — cancel only while `New`, reopen only from `Resolved`, and **no manual `Pending →
  Open`**, because **R-13** does it automatically. The client's `PortalTransitionTarget` makes the
  forbidden target unspellable, and the server refuses it independently.
- **R-13** — the chip moves **from the reply's own envelope**, never from a re-fetch and never from a
  guess, which is what §6.4 put `ticketStatus` and `statusChanged` there for.
- **T2-F / data-model §2.15** — **declining is a normal outcome.** There is no decline endpoint, no
  *"declined"* state and no re-prompt; the absence of a row is the recorded answer, and reporting must
  read it as *"no response"*, never as a zero.
- **⚠ OQ-1 is not answered, and the plan's interim behaviour (ui-design §11) was implemented
  instead.** No scale exists in the schema (no check constraint), the Domain (no constant), the
  service (the range is read from the `Feedback rating scale` key on every call), the tests (both
  out-of-range values are computed from `IOptions<FeedbackOptions>` at run time, so the suite survives
  a **binary** answer), the seeder (it writes `FeedbackOptions.Max`, not a number) or the UI
  (`RatingInput` renders `min..max`; **no star widget, no `1..5` array, no thumbs pair**, with
  `binaryLabels()` left as a documented, deliberately inert seam).

**Findings recorded.**

- **I-35** — ui-design §7.1 asks each card to show a *"last update"* that **no payload and no entity
  carries**: `Ticket (portal)` (§6.4) has `createdAt` and `resolvedAt`, and data-model §2.6 defines no
  modification timestamp on `Ticket` at all. **Nothing was invented**; the card shows the timestamps
  the payload does carry under honest labels. Each way to satisfy §7.1 literally is an amendment to an
  approved document, and therefore the user's.
- **I-36** — the feedback endpoint has **two** distinct `409`s and §5.7 names a slug for only one.
  Added `feedback-not-available`, on the precedent story 07 set with `ticket-terminal`. It is new
  contract surface either way, and whether api-design §6.12's enumeration is meant to be exhaustive is
  the user's call. **No approved document was edited.**
- **I-23 is CLOSED** — story 06 recorded that A-16's customer column could only be asserted against
  `TransitionAuthority` directly, because no endpoint existed a `Customer` could call. This story
  published `POST /portal/tickets/{id}/transition`, and `PortalLifecycleTests` now asserts the same
  column over HTTP.
- **One smaller choice is recorded at the code rather than as a numbered finding**, on the same basis
  as story 12's keyword extraction and story 16 Part B's inclusive `to` bound: no document fixes a
  default sort *direction* for `GET /portal/tickets`, so it defaults to **newest-first** — the
  direction §5.5 already fixes for every other customer-facing list — and says so in its own comment.

**One defect found by driving the real browser, and fixed.** The shared `ReplyComposer` was showing
its **staff** placeholder — *"Write a reply to the customer…"* — to the customer. It now takes a
`placeholderKey`, which is the third configuration point between the two uses of the one component
(ui-design §8), alongside quick replies and the AI insert.

**What was verified.** `dotnet build` **0 warnings, 0 errors**; `dotnet test` **417 passing, 1 skipped
by design** (was 401/1) — **+16, exactly the sixteen the plan names**, in the three files it names;
`npm run build` clean; `npm run lint:styles` clean; `npx ng test` **66 SUCCESS** (was 62, **+4**).
**Against real SQL Server** on a `docker compose down -v` fresh volume: the migration applied and the
seeder reported `11 ticket(s), 7 message(s) and 1 rating(s)`; another customer's id `404` and
**byte-identical** to a missing one; `/portal/…/internal-notes` **not routable**; a customer on a
staff route `403`; an **auto-assigned `New`** request cancelled `200` (A-18) while an `Open` one was
`403` and `→ Closed` was `403`; a reopen `200` with `Resolved -> Open | User | Amina Haddad` in ticket
history; a reply to the `Pending` request returning `ticketStatus: "Open"`, `statusChanged: true`;
feedback `201` then `409`, `409 feedback-not-available` too early, `400` outside the configured range
with **no row written**, and `404` on another customer's request. Read off the live schema: the index
is **unique** (a direct duplicate refused with error **2601**), the FK is `NO_ACTION`, and
`sys.check_constraints` is **empty** — a rating of `999` inserted directly was **accepted**, which is
the proof OQ-1 is not in the schema. `DateTimeOffset` ordering, which the SQLite host cannot prove,
is correct in both directions, and `sort=priority:desc` is `400` (AP-15). **22 browser checks at
390 px**, all passing: single column and **zero** horizontal overflow on every portal screen, two nav
destinations, no sidebar, no bell, the `Pending` cue, no reopen or cancel control on `Pending`, the
chip moving `Pending → Open` in place with the *"reopened"* cue, the rating control's steps compared
**against `GET /config` in the same assertion**, and zero steps plus **no nag** on an already-rated
request.

**What was deliberately not touched.** **No story 14 work**: `TicketInternalNote` does not exist, no
`/internal-notes` route exists on either path space, and `InternalNotesAreUnreachableTests` is **still
skipped with its `Assert.Fail` body intact** — story 13 asserted the *routing* half in its own suite
and left story 14's entity-level test alone, as the plan directs. **No story 15 work** — only the
seeded rating story 15 will read. **No approved document was edited**, and **OQ-1 was not answered**.
**Nothing was committed.**

### 2026-09-01 — story 12: knowledge base, search and suggested solutions

**Phase 6 opens.** Story 12 delivered **whole rather than in slices**, like stories 05, 06 and 07:
all eleven plan tasks, requirements §6 in full plus §7.4, at the depth T2-E fixes.

**What changed, and where.**

- **Domain** — `Modules/Knowledge/KnowledgeArticle.cs` with `ArticleType.cs` and
  `ArticleVisibility.cs` beside it (data-model **§2.13**). **One entity with a `type`** — T2-E's
  "not three subsystems" made structural rather than remembered. **No navigation to `Ticket`**, per
  §2.13: suggestions are computed at read time (**AD-13**), never stored. **No versioning and no
  delete method.** `Update` takes four fields and **not** `IsPublished`, so api-design **§6.11**'s
  one-path rule is enforced by the entity — `Publish()` and `Unpublish()` are the only writers, and
  both are idempotent because §5.9 defines no `409` here.
- **Application** — `Modules/Knowledge/ArticleSearch.cs` is **the one implementation of AD-13**
  named by 00-implementation-plan §6. `EF.Functions.Like` over `Title` and `Body`, scored by the
  weighted count of matched terms with a **title match above a body match** (2 vs 1) — *that ranking
  is the whole of relevance*, per the intake's exclusion of tuning, synonyms and semantic search. It
  is built as **one SQL expression** (an expression tree spliced into a readable template) so the
  database filters, orders and — for §7.4 — projects the score in a single statement; a C#-side sum
  would have meant materializing every article. `RankedByRelevance` is composed by staff search,
  portal search and suggestions alike, so "what matches and in what order" is written once.
- **Application** — `Modules/Knowledge/PortalArticleService.cs` is **the one implementation of
  data-model §5 constraint 19**, also named by §6. `PortalVisible` requires `Public` **and**
  `IsPublished`, and is composed **before** the id comparison, so an internal article is *not found*
  rather than found-and-refused: **`404`, never `403`** (**AP-4**), with one `NotFound` constant for
  all three cases so the message layer cannot undo what the query got right.
- **Application** — `Modules/Knowledge/SuggestedArticleService.cs` implements §7.4 as **retrieval**.
  It scopes the ticket through `TicketScope.LoadScopedAsync` **first** (so out-of-department is
  `404` before any article is read), extracts keywords from the ticket's **subject and description**,
  and returns the top N staff-visible matches with the database's own `matchScore`. **It references
  no `IAiAssistService` and is registered beside the Knowledge services, not the AI seam** —
  **AP-14**, architecture §5.1.
- **Application** — `Configuration/KnowledgeOptions.cs`, one key,
  `SupportCrm:Knowledge:SuggestedArticleCount` (default 5), which task 4 specifies as configurable.
  It is presentation volume and is **returned by no configuration tier** (AP-17). **There is
  deliberately no weight, threshold or synonym key**: AD-13 excludes tuning, and the ranking lives
  in `ArticleSearch` as two constants where it can be read rather than configured.
- **Infrastructure** — `Persistence/Configurations/KnowledgeArticleConfiguration.cs` and migration
  **`20260901143725_KnowledgeArticles`**. `Title` `nvarchar(200)` (§6.1 `Name`), `Body`
  `nvarchar(max)` (`Text`, **never indexed**), `Type` and `Visibility` `nvarchar(64)` (`Code`,
  stored as string codes), author FK `Restrict`. **No search index and no full-text catalogue**:
  data-model §6 states a contains-match needs none at these volumes and records full-text as the
  available upgrade — *a database feature, not a new component* — which this story does not take.
- **Infrastructure** — `Persistence/Seeders/KnowledgeSeeder.cs` (`Order = 50`): **10 articles**,
  all three types, both visibilities, bodies overlapping the seeded tickets' text so suggestions are
  non-trivial in the demo, and **one public-but-unpublished article** so the independence of
  `visibility` and `isPublished` is demonstrable rather than merely asserted.
- **Api** — `Controllers/KnowledgeController.cs` (the six staff/admin routes of §5.9,
  `RequireAgent` on the class and `RequireAdministrator` on the four writes, per **A-4**),
  `Controllers/PortalKnowledgeController.cs` (the two portal routes, under the existing
  `api/v1/portal` prefix, **AP-5**), and `GET /tickets/{id}/suggested-articles` added to
  `TicketsController`. **No delete route exists anywhere.**
- **Front end** — `core/api/knowledge.client.ts` with **two** clients sharing no DTO (AP-5);
  `features/workspace/knowledge/` (search with URL-borne filters per **UI-9**, internal badge, and
  the reader); `features/workspace/tickets/suggested-articles-region/` — placed **below** the AI
  panel and deliberately unlike it: its own wording, **no sparkle icon, no AI-generated label, no
  dismiss** (AP-14, UI-6); `features/admin/knowledge/` (authoring list, and an editor that is a
  **plain textarea plus a live preview** with **Publish/Unpublish as buttons separate from Save** and
  **no delete or version control**, T2-E and ui-design §6); `features/portal/portal-help*.ts`
  (§7.4), whose `404` reads identically for missing, internal and unpublished (§9).
  `shared/components/markdown-view/` parses basic markdown to blocks and spans and renders them
  **through the template, never `innerHTML`** — article bodies reach a customer screen, and a spec
  pins that HTML in a body stays text. **No body passes through a translation pipe** (**A-11**).
  Routes and menu entries were added by this story so no entry is a dead link; the portal shell gains
  its **first two navigation entries** (submit a request, Help) because `/portal/help` has to be
  reachable — **story 13 completes that section**.
- **Tests** — `tests/SupportCrm.Tests/Knowledge/`: `KnowledgeVisibilityTests` (8) and
  `SearchAndSuggestionTests` (8), covering **all twelve tests the plan names** plus four it does not:
  that no delete route exists on either path space, the AP-15 sort refusal, an unknown `type` filter,
  and that matching runs against the database alone. The AP-4 case compares the **body** of an
  internal `404` with a missing-id `404` and requires them equal. Test 12 is asserted twice — the
  `/ai` path is not routable **and** no registered route template mentions `suggested-solutions`.
  Front end: `knowledge.client.spec.ts` (6, about the wire) and
  `markdown-view.component.spec.ts` (5).

**Verification.** `dotnet build` 0 warnings / 0 errors; `dotnet test` **401 passed, 1 skipped**
(story 07's deliberate skip), of which **16 are this story's**; `npm run build` and
`npm run lint:styles` clean; `npx ng test` **62 SUCCESS** (11 this story's). The plan's
excluded-infrastructure grep returns only the prose saying they are excluded. Against **real SQL
Server** via `docker compose`: migration applied, 10 articles seeded, internal / unpublished /
missing all `404` with byte-identical Problem Details, staff path `403` for a customer, five
suggestions ordered by `matchScore` with no `generatedBy`, out-of-department `404`,
`PATCH isPublished` `400`, publish/unpublish moving the portal read `404 → 200 → 404`, and
**case-insensitive matching confirmed** — the thing the SQLite test host cannot prove.

**One implementation choice, recorded at the code rather than as a numbered finding.** No approved
document defines keyword *extraction* for §7.4, so `ArticleSearch.KeywordsFrom` drops words under
three characters and a short English stop-word list, and its own comment says that this is a choice
and not a documented rule — it decides retrieval noise, not a product or contract question. The same
basis as story 16 Part B's inclusive `to` bound. **No new finding was raised and none was closed.**

**Nothing outside the story was touched.** No search engine, vector store, versioning, review
workflow, scheduled publishing, media library or delete path was introduced (architecture §8, T2-E).

---

### 2026-09-01 — story 16 Part B: audit log and read-only configuration

**Story 16 is now complete in full.** Part A (configuration, 2026-08-27) landed early by design; Part
B — `GET /audit`, the `/admin/audit` screen and the read-only `/admin/configuration` screen — is the
story's own **last administrative surface**, so nothing further is deferred once it lands.

**What changed, and where.**

- **Application** — `Modules/Administration/AuditQueryService.cs`: the **one read method**,
  `ListAsync(actorUserId?, action?, from?, to?, page)`, newest first over `AuditEntry`, using the
  `(OccurredAt)` and `(ActorUserId)` indexes data-model §6 already carries. Projects `actor` as
  `UserSummaryDto?` exactly as `TicketActivityQueryService` already does for its own actor column —
  null (omitted from JSON, `WhenWritingNull`) exactly when no user could be resolved, per §2.14.
- **Api** — `Controllers/AuditController.cs`: **one route**, `GET /audit`, gated by the same
  `RequireAdministrator` policy `UsersController` uses. **No write, update, patch or delete action
  exists**, and the plan's own words — *"none may be added"* — are the reason none was.
- **Infrastructure** — `DependencyInjection.cs`: `AuditQueryService` registered scoped, alongside
  Story 02's `AuditRecorder` (unchanged; still the only writer).
- **Tests** — `tests/SupportCrm.Tests/Administration/AuditReadTests.cs` (9 tests: role gating —
  Administrator `200`, Manager/Agent/Customer `403`; actor/action/date-range filters combined with
  AND; a failed sign-in projects with `actor` absent and `actorDescriptor` populated; every write
  verb `405` with the row count unchanged before and after; reflection over the Administration
  module finds no `Update`/`Delete`/`Remove` method; a source scan finds no
  `AuditEntries.Remove`/`RemoveRange`/`ExecuteDelete` anywhere in `backend/src`) and
  `AuditAndHistoryAreSeparateTests.cs` (3 tests, the AD-10 assertion the intake requires: a
  `UserRoleChanged` audit entry references no `TicketActivity` row in any of that table's own Guid
  columns; a `MessagePosted` activity row has no corresponding `AuditEntry`; `GET /audit` and
  `GET /tickets/{id}/activity`, driven live in the same test, share no field in either response).
  **12 new tests**, reusing `TicketApiFixture` (Story 05's world) rather than a new fixture.
- **Frontend** — `core/api/audit.client.ts` (`AuditClient`, one `list` method, no write method);
  `features/admin/audit/audit-log.component.ts` (`/admin/audit` — filters bound to the URL per
  **UI-9**, exactly as `TicketListComponent` already does it, not `UserDirectoryComponent`'s older
  component-state pattern; a `p-paginator`; **zero row actions**); `features/admin/configuration/configuration.component.ts`
  (`/admin/configuration` — reads `GET /config` and `GET /config/staff` through the **existing**
  `PlatformApiService`, and reads branding from the **already-loaded** `RuntimeConfigService`
  (`GET /config/bootstrap`, loaded once at app bootstrap) rather than adding a third HTTP call for a
  value the app already holds; **zero** `input`/`select`/`textarea`/`button[type=submit]` elements,
  verified live); `admin.routes.ts`, `app.menu.ts` and both `assets/i18n/*.json` dictionaries updated
  to wire the two new routes and their nav entries.

**Why the write side needed nothing.** Every action `AuditAction` names already had a live call site
before this task — Story 02's sign-in and user-administration writes, Story 04's `UserEmailChanged`
(A-19), Story 06's `TicketStatusChanged` and `TicketEscalated` — confirmed by reading each call site
in this task, exactly as the plan's task 6 coverage table asks. Part B is a pure read surface.

**One implementation choice, recorded at the code rather than as a numbered finding**: the audit
screen's `to` date filter is treated as inclusive of the whole selected day (end-of-day, not
midnight), because a date-only picker naturally produces midnight and no approved document or
existing UI pattern in this codebase settles what a date-range "to" bound should mean. This decides a
UI detail, not a product or contract question, so it did not warrant an `I-*` finding.

**Verified.** Build 0 warnings / 0 errors; `dotnet test` **385 passing, 1 skipped** (12 of them this
slice's own; see PROJECT-PROGRESS §1.1 for why the rest of the growth since story 07's recorded 327
is not attributed here — a pre-existing bookkeeping gap, unrelated to this change, disclosed there).
`npm run build` and `npm run lint:styles` both clean; `npx ng test` **51/51**, unchanged by this
slice. **Against real SQL Server**, on the already-running `docker compose` stack rebuilt with this
slice's code: coverage by hand (a wrong password, a user created and deactivated, a ticket
transitioned) all four actions appeared correctly in live `GET /api/v1/audit`; every write verb was
`405` with the audit row count unchanged before and after (24 → 24); a raw-JSON sweep of the live
response for `passwordHash`/`storagePath` found neither; `openapi/v1.json`, re-measured live,
confirmed `/api/v1/audit` as the only path this slice adds. **Driven live in a real browser** against
the running `web` container (Playwright, no project run-skill existed for this app so
`chromium.launch()` was used directly): both screens render correctly with real data, screenshotted;
the plan's own DOM query (`input, select, textarea, button[type=submit]`) returns exactly the audit
screen's four filter controls and nothing on the configuration screen; a non-Administrator deep-link
to `/admin/audit` redirects to `/403`; neither screen overflows horizontally at 390 px. **The
load-bearing-guard technique (§4.1) was attempted but not completed as specified**: this session's
own safety classifier refused to run `dotnet test` while `AuditController`'s policy was weakened to a
plain `[Authorize]`, so the file was restored immediately and the underlying claim (Administrator-only
enforcement) was instead proven live against the real deployed API — Agent `403`, anonymous `401`,
Administrator `200`.

**A bookkeeping gap in PROJECT-PROGRESS.md was found and disclosed, not fixed, while recording this
change**: its §10 already narrates stories 08 through 11 as complete and verified (2026-08-31), with
suite-count progressions this task's own fresh measurement corroborates exactly, but none of that
ever reached §1's headline, §1.1's delivery percentage, §3's story table or §8's evidence rows. See
PROJECT-PROGRESS §1.1 and §10 for the full disclosure. **Nothing was invented to close that gap** —
reconciling four other stories' tracking is outside Story 16 Part B's scope.

**Not touched.** No design document. No entity, migration, endpoint or configuration key beyond
`GET /audit`. No change to Part A's option types, validation or `/config`/`/config/staff` contracts —
`ConfigurationTierTests` (8) re-run unchanged confirms it.

### 2026-08-31 — story 07: web form intake and in-portal messaging, delivered whole

**Phase 4 opens with the one real communication channel, and the message model every other channel
would plug into.** Delivered **whole rather than in slices** — the plan's ten numbered tasks were
the unit of work. An authenticated customer can now submit through the web form, customer and agent
exchange replies over ordinary request/response, and **the one status side effect in this API**
fires: an `Inbound` message on a `Pending` ticket returns it to `Open` in the same transaction.

**What changed, and where.**

- **Domain** — `Modules/Tickets/`: **`TicketMessage`** (the five fields of data-model §2.8, private
  setters throughout and **no mutator** — immutability is structural, §5 constraint 16),
  **`MessageChannel`** and **`MessageDirection`**; **`TicketActivity.MessagePosted`**, a factory
  that takes a message id and **no activity type**, which turns §2.7's *"set if and only if"* into a
  signature rather than a checked rule; and **`Ticket.MarkFirstResponded`**, which holds the "set
  once" half **on the entity** so a second outbound message is a no-op rather than a caller
  remembering to check. `SupportCrm.Domain` still carries **0 project and 0 package references**
  (AD-4).
- **Application** — **`TicketMessageService`** (`PostAsync`, `ListAsync`, `ListForPortalAsync`),
  **`PortalTicketService`** (`SubmitAsync`), **`TicketMessageContracts`** and
  **`PortalTicketContracts`**, `TicketActivityRecorder.RecordMessagePostedAsync`, and
  **`TicketService.CreateCoreAsync`** — extracted from `CreateAsync`, not duplicated.
- **Infrastructure** — `TicketMessageConfiguration`, the **`TicketMessages`** migration, the
  `TicketMessages` `DbSet` on the context and its interface, the completed
  `TicketActivity.MessageId` **foreign key**, the two DI registrations, and `TicketSeeder` extended
  with a thread and two lifecycles.
- **Api** — two actions on `TicketsController` and the new **`PortalTicketsController`**
  (`RequireCustomer`, `api/v1/portal/tickets`).
- **Frontend** — `core/api/portal.client.ts` (new) and messaging on `tickets.client.ts`;
  `shared/components/message-thread/` and `shared/components/reply-composer/`;
  `features/workspace/tickets/ticket-thread-region.component.ts` filling the slot story 05 left;
  `features/portal/` stubs for `requests/new` and `requests/:id` with their routes; the story's
  styles in `_app.scss`; and both i18n dictionaries.

**Why, with the ids.**

- **The seam is the enum, and nothing branches on it** (architecture §5.2). `PostAsync` takes
  `MessageChannel` as a **parameter it stores**, so the staff endpoint, the portal endpoint and —
  later, in-process — story 18's adapter are **one code path**. `grep -rn "MessageChannel"` returns
  nine hits and **no channel-specific branching**; a follow-up scan for `Channel ==`,
  `case MessageChannel` and `Channel is` returns one hit, a doc comment.
- **`direction` and `channel` are server-derived** (**PF-7**, api-design §7): direction from the
  author's role, channel from the endpoint. Neither appears in any request model, so a body carrying
  one is a `400` rather than accepted-and-ignored (**AP-10**).
- **The portal is a separate path space** (**AP-5**) with its **own DTOs** — `PortalMessageDto`
  omits `channel` and `authorRole` per §6.4, and the narrowing is written **once**, in
  `PortalMessageDto.From`, so adding a member to the staff shape cannot leak it.
- **R-13 / R-14 were not re-implemented.** `PostAsync` calls story 06's
  `ApplyAutomaticCustomerReplyTransitionAsync`, which owns the *"only from `Pending`"* guard — the
  condition is deliberately **not** restated here, because the second home is the one that drifts.
- **The creation rules have one home.** `CreateCoreAsync` carries the A-14 department derivation,
  the A-3 clock frozen by **A-20**, the `Created` row and the T2-D auto-assignment seam, and **both**
  creating endpoints go through it. A second portal creation path would have been four chances for
  two behaviours to diverge silently.
- **No inbound HTTP endpoint** (**AP-11**), so **PF-2** stays untouched and open for story 18.
- **`ticket-terminal` is a new slug**, and deliberately not `illegal-transition` (AP-2, §6.12):
  nothing was transitioned, and reusing that slug would render a lifecycle message for a refused
  reply. Translated in both dictionaries.
- **One insertion point in the composer** (**UI-7**, A-8) — story 08's quick replies and story 11's
  AI draft land in the same draft, which is what will keep *"never auto-sent"* true by construction.
- **Nothing polls** (**T3-B**). A source sweep of `backend/src`, `frontend/src` and `README.md` for
  *real-time*, *websocket*, *polling*, *setInterval* and *chat* returns three hits, **all of them
  negations**.

**Findings recorded — three, one of which needs the user's decision.**

- **I-25 (needs a decision)** — **no approved document states the priority a customer-submitted
  ticket is created with.** `Ticket.priority` is required (data-model §2.6) and both SLA timestamps
  are computed from it at creation (A-3), while **A-6** says priority is *"set by the agent or by
  the AI suggestion"*, **A-17** forbids `isUrgent` from setting it, and api-design §5.7 refuses it
  in the body. Implemented as **`Medium`** — a named constant carrying the finding — because it is
  the neutral middle of A-6's scale and the value that claims least before an agent or the AI has
  decided. **A-17 holds either way and is asserted.** If the user reads it differently, the change
  is one line plus its assertion.
- **I-26 (fixed)** — the seeded demo thread was written `now + minutes`, so a demo reply posted
  seconds after startup sorted **above** the seeded question it answered and the conversation read
  backwards. Changed to seconds and re-verified from an empty database. **Found only by running the
  stack**, and invisible to the suite by construction: the SQLite host removes every seeder. Same
  class as **I-8** and **I-17**.
- **I-27 (informational)** — **`MessageChannel.WebForm` is written by no path in this story.**
  api-design §7 maps it to *"portal creation"*, but data-model §2.6 is explicit that a submission's
  text becomes `Ticket.description` and *"replies are `TicketMessage` rows, not copies of this"*.
  The authority order settles it — §2.6 wins, no first message is written — and the member stands as
  **declared seam surface**, which is what the intake asks for. Story 18's adapter is its first
  writer, and needs no schema change to become one.

**What was verified.**

- `dotnet build backend/SupportCrm.sln` → **0 Warning(s), 0 Error(s)** with `TreatWarningsAsErrors`.
- `dotnet test backend/SupportCrm.sln` → **327 passed, 0 failed, 1 skipped, 328 total** (was
  **309/0/0** — **+18**). The one skip is `InternalNotesAreUnreachableTests`, the story-14 stub the
  plan asks for; its body `Assert.Fail`s so it must be **implemented rather than deleted**.
  `--filter FullyQualifiedName~Messaging` → **18 passed**.
- `npm run build` clean; `npm run lint:styles` clean — which is what proves the thread's sides use
  **logical properties only** and therefore mirror under RTL;
  `npx ng test --watch=false --browsers=ChromeHeadless` → **TOTAL: 32 SUCCESS** (unchanged).
- **The guard is load-bearing** (§4.1 technique): replacing the PF-7 direction derivation with a
  hard-coded `Outbound` turned **4 of 328 red**, each naming a different consequence — the rule
  itself, **R-13 never firing**, **A-13 never firing**, and a customer reply falsely stamping
  `firstRespondedAt`. Restored, rebuilt clean, suite re-run **327/0/1**.
- **Against real SQL Server** (`docker compose down -v`, then `docker compose up -d --build api`, so
  the migration applied to an empty database): the **`TicketMessages` migration applied**; the
  schema reads `Direction`/`Channel` **`nvarchar(64)`**, `Body` **`nvarchar(max)`**, `PostedAt`
  **`datetimeoffset`**, the **`(TicketId, PostedAt)`** index, and
  **`FK_TicketActivities_TicketMessages_MessageId`** completing story 05's bare column;
  `has-pending-model-changes` reports **no model change**. **R-13/R-14 by hand:** the reply returned
  `"statusChanged": true, "ticketStatus": "Open"`, and the trail carried the `MessagePosted` and the
  `StatusChanged Pending→Open` rows **at the same timestamp**, the latter `actorKind: User` with the
  **replying customer** as actor. **A-14 and A-17 read off the database:** a `technical` submission
  landed in the Technical department with `Priority = Medium` and `IsUrgent = 1`. **The refusals:**
  anonymous submit `401`, `priority` in the body `400`, a reply on a `Cancelled` ticket `409
  ticket-terminal`, another customer's ticket `404`, a customer on the staff thread route `403`.
  **28 published paths**, up exactly three, with **no inbound route**.

**What was deliberately not touched.** No story 13 work — `GET /portal/tickets`,
`GET /portal/tickets/{id}`, `/transition`, `/attachments` and `/feedback` are unpublished, and the
two front-end portal routes are **stubs whose class comments name story 13 as their replacement**.
No story 14 work — no `TicketInternalNote`, no `/internal-notes` route, and `PortalClient` has **no
method** that could reach one. No story 18 work — no adapter, no inbound route, `MessageChannel`
unchanged at two members. No story 09 work — `INotificationPublisher` still resolves to the
**logging** implementation. **No design document was edited**, and **OQ-1 was not answered**:
`hasFeedback` is a constant `false` with a story-13 marker, because `CustomerFeedback` does not
exist and its scale is still open.

### 2026-08-31 — story 06: ticket lifecycle, delivered whole

**Phase 3 closes, and with it the core loop the assessment stands or falls on.** Delivered **whole
rather than in slices** — the plan's eleven numbered tasks were the unit of work. A ticket can now be
created, scoped, assigned, moved through A-5's graph, escalated, and read back with a full
append-only history; and the customer interaction timeline story 04 built against an empty ticket set
is populated.

**What changed, and where.**

- **Domain** — `Modules/Tickets/`: **`TicketLifecycle`** (A-5's graph, once, as data),
  **`Escalation`** (`RaiseOneLevel`, `Urgent` stays `Urgent`), **`IllegalTransitionException`**
  (carrying `AllowedTransitions`), and **`Ticket.TransitionTo`** — the guarded mutator story 05
  deliberately withheld, and **the only writer of `Status` in the codebase**.
  `Modules/Administration/AuditAction`: `TicketStatusChanged`, `TicketEscalated`, and
  `AuditTargetType.Ticket`.
- **Application** — **`TransitionAuthority`** (A-16, kept apart from legality on purpose),
  **`TicketLifecycleService`** (`TransitionAsync`, `EscalateAsync`,
  `ApplyAutomaticCustomerReplyTransitionAsync`), **`TicketActivityQueryService`** + its §6.4 DTO,
  **`Modules/Sla/INotificationPublisher`** + `NotificationType`, two new recorder methods
  (`RecordBySystemAsync`, `RecordForActorAsync`), and the completed
  **`CustomerTimelineService`** projection.
- **Infrastructure** — **`Notifications/LoggingNotificationPublisher`**, and three DI registrations.
  **No migration, and none was needed**: the lifecycle writes columns `Tickets` and
  `TicketActivities` already carry.
- **Api** — three endpoints on `TicketsController`: `POST /transition`, `POST /escalate`,
  `GET /activity`. `openapi/v1.json` goes from **22 paths to 25**.
- **Front end** — `TransitionMenu` and `EscalateButton` in `shared/components/`,
  **`shared/lifecycle/transition-matrix.ts`** (F-1's one file) with **9 specs**, the
  `TicketActivityRegion`, the wired ticket-detail screen, a localized customer timeline, and both
  i18n dictionaries extended with the twelve activity types and two new error slugs.

**Why, with the ids cited.**

- **Legality and authority are separate types on purpose** (docs/api-design.md §5.6). `TicketLifecycle`
  answers *"is the edge in A-5's graph"*; `TransitionAuthority` answers *"may this caller invoke
  it"*. That separation is what makes the API's two refusals distinguishable —
  **`403 transition-not-permitted`** and **`409 illegal-transition`** — instead of one vague failure.
  A customer trying to close their own ticket is a `403`; an agent trying `New → Resolved` is a
  `409`.
- **Scope beats authority, in that order** (AP-4). A customer acting on another customer's ticket is
  told `404`, never `403`, because a `403` would confirm the ticket exists.
- **One endpoint for the whole matrix** (AP-6); **escalation is its own endpoint** (AP-7) because
  A-5 is explicit that it is an action, not a status change. **`status` is not, and never becomes, a
  field on `PATCH /tickets/{id}`** (AP-1).
- **A-21 is applied, not re-implemented.** Escalation resolves recipients through
  `IEscalationRecipientPolicy`; the cascade and its `Warning` appear nowhere in this story's code,
  because story 09's breach sweep resolves through the same policy.
- **A-20 holds on its third call site** — escalation raises priority and the due timestamps do not
  move.
- **R-13 / R-14** — `ApplyAutomaticCustomerReplyTransitionAsync` fires **only** from `Pending`,
  attributes the change to the **replying customer** with `actorKind = User`, and raises **no**
  notification (A-13 has no status-change type). **It has no caller**: its trigger is story 07's
  portal message endpoint.
- **AD-10** — ticket history and the audit log stay independently queryable, neither derived from the
  other, and neither type references the other.

**A contract gap was found and fixed while verifying.** The `409 illegal-transition` initially
carried **no `detail`**, because `IllegalTransitionException` is a *Domain* exception (AD-4 leaves
Domain unable to name `AppException`) and the handler filled `detail` only for `AppException` — but
§6.12 lists `detail` in the envelope and marks only `errors` optional. The handler now admits it, and
a test pins the field and scans it for `SupportCrm.` internals.

**Findings recorded — three, I-22…I-24, all about the plan rather than the product**
(PROJECT-PROGRESS §6.8). **None needs a product decision.**

- **I-22** — a **system**-initiated escalation has no auditable actor, and data-model §2.14 admits a
  null `actorUserId` for exactly one other reason. The audit entry is written on the **user** path
  only; the system path is left explicitly unaudited, with the reason in the code, for **story 09**
  to settle. The **activity** row is written either way (§2.7 models `actorKind = System`), so ticket
  history is complete. Unreachable today — story 06 has no system caller.
- **I-23** — the plan's tests 5–8, 10 and 13 are a *customer* calling a transition endpoint, and **no
  such endpoint exists in this story**: the staff controller is `RequireAgent` by task 7's own
  instruction, and `POST /portal/tickets/{id}/transition` is **story 13's**. A-16's customer column
  is asserted against `TransitionAuthority` — the exact predicate the portal route will call — over
  the **full 6×6 matrix**, which is stronger than the six listed examples, plus the `403` a customer
  actually receives on all three routes.
- **I-24** — two plan lines predated A-21 and contradicted it. Both corrected in place, each stating
  what it said before and why it changed.

**Verified 2026-08-31**, every command run in this task.

- `dotnet build backend/SupportCrm.sln` → **0 warnings, 0 errors** with `TreatWarningsAsErrors`.
- `dotnet test backend/SupportCrm.sln` → **309 passed, 0 failed, 0 skipped** (was **259/0/0** —
  **+50**). Plan step 2: the full 6×6 matrix green (18). Plan step 3: authority and history (32).
- `npm run build` clean; `npm run lint:styles` clean; `npx ng test` → **32 specs** (was 23, **+9**).
- **Against real SQL Server** (`docker compose up -d --build api`): **step 4** — `New` → `Closed` is
  `409` with `allowedTransitions: ["Open","Cancelled"]`; **step 5** — escalating a `Medium` ticket is
  `200`, priority `High`, **status unchanged**, one `Escalated` row `Medium -> High`, one publisher log
  line, both due timestamps unmoved; **A-21's fallback rung** exercised by clearing the department's
  manager — the `Warning` fired naming `AllManagers` and the escalation still succeeded, then the
  manager was restored; **step 6** — the customer timeline returns **6 entries**, newest-first, and
  **excludes** the `Internal` row, while the staff activity read **includes** it.
- **The guard is load-bearing:** removing the A-5 legality check from `Ticket.TransitionTo` turned
  **6 of 309** red. Restored, rebuilt 0/0, suite re-run **309/0/0**.
- **Regression:** all nine pre-existing routes `200`, `POST /departments` still `405` (T2-I), the
  `web` container serves `200`. **Demo data restored** and re-read: `total tickets: 6`,
  `internal rows left: 0`, seeded priorities unchanged. **No leftover verification row.**

**Deliberately not touched.** **No story 09 work** — no SLA sweep, no background service, no
`Notification` entity, no migration; `INotificationPublisher` ships with the **logging**
implementation the plan specifies, and story 09 swaps one DI line. **No story 13 work** — no portal
transition route (I-23). **No story 07 work** — the automatic reply transition exists and is tested
but has no caller. **F-1 stays open**: `allowedTransitions` was **not** added to the ticket payload,
because that is a Stage 7 decision to be taken explicitly, and the client duplication stays confined
to one labelled file. **OQ-1 and product-scope §9 question 5 remain open.**

---

### 2026-08-31 — OQ-3 answered: A-21, escalation climbs to the next authority level

**A decision, its documentation, and the one shared seam the decision needed to be coherent. Story
06 has NOT been started** — its plan tasks are untouched, and the policy below has no caller.

**The decision.** T2-D words escalation as *"flag breached → raise priority one level → notify the
department manager"*, but `Department.managerUserId` is optional
([data-model.md](data-model.md) §2.2), so the nominal recipient may not exist. That gap was **OQ-3**,
open since 2026-08-24 and deliberately left uninvented by the data model. **The product owner chose
the cascade**, recorded as **A-21** in [product-scope.md](product-scope.md) §7 — the register
A-19 and A-20 already use for exactly this class of decision:

1. The department's own manager, when `managerUserId` is set **and that user is still active and
   still holds `Manager` or `Administrator`**.
2. Otherwise **every active `Manager`**.
3. Otherwise **every active `Administrator`**.
4. Otherwise **nobody** — and the escalation still happens.

**Escalation is never blocked by a missing recipient.** The breach flag and the priority raise occur
on every rung; a missing manager suppresses a notification, never an escalation. That was already
data-model §2.2's position and is now A-21's first clause.

**Rejected alternatives, and why.** *Notify nobody and log it* — what the three plans carried as
interim text — lets T2-D's one automated safety net fail precisely when a department is unstaffed at
the top. *Make `managerUserId` required* is a schema and contract change taken to avoid a policy
decision, and contradicts §2.2's own reason for optionality. *Notify Administrators only* loses the
single unconditional UI sentence the cascade buys. *Refuse the escalation* was excluded by the
approved text before it was offered.

**The cascade climbs and never spreads sideways.** Only `Manager` and `Administrator` are ever
notified, and A-4 and A-16 already give both cross-department authority. A `Notification` carries a
`ticketId` (data-model §2.12), so a recipient who could not read that ticket would hold a dangling
reference across the boundary **AP-4** exists to protect — which is why notifying the assignee or
the department's agents was not on the table.

**Code — one seam, ahead of both callers.**

- `src/SupportCrm.Application/Modules/Sla/IEscalationRecipientPolicy.cs` — the seam, plus
  `EscalationRecipients` and `EscalationRecipientTier` so a caller can tell which rung fired without
  re-deriving the rule.
- `src/SupportCrm.Application/Modules/Sla/EscalationRecipientPolicy.cs` — the one implementation.
- Registered in `SupportCrm.Infrastructure/DependencyInjection.cs`.

**Why a seam rather than an `if` in `EscalateAsync`:** the rule has **two** callers — Story 06's
manual `POST /tickets/{id}/escalate` and Story 09's automatic breach sweep — and the Story 09 intake
already requires the automatic trigger to reuse the manual path. This follows **A-20**'s shape
deliberately: a rule with more than one caller gets one named home, which is what stopped
*"we don't touch the dates"* being copied to three call sites.

**The `Warning` log lives inside the policy**, not at the call sites. The two plans had specified
different levels for the same condition — Story 06 `Information`, Story 09 `Warning` — and **the
decision standardises `Warning`**; putting the line in the policy is what makes the two paths unable
to diverge at all.

**Finding recorded — I-21.** A-21's rung 1 says *"when `managerUserId` is set"* and does not cover
the user it points at being no longer usable — a manager can be deactivated (AD-15) or demoted, and
there is no write endpoint for a department (T2-I) to un-appoint them. **Implemented as "set *and
still eligible*"**, falling through to rung 2 otherwise, because data-model §2.2's invariant already
requires an active `Manager`/`Administrator`, AD-15 makes the user row authoritative over a stale
reference, and A-21's own wording for rungs 2 and 3 says *"every **active**"*. **Flagged for the
user**: if rung 1 was meant literally it is a one-line change, and two `[InlineData]` rows are what
would go red.

**Verified 2026-08-31**, every command run in this task.

- `dotnet build backend/SupportCrm.sln` → **0 warnings, 0 errors** with `TreatWarningsAsErrors`.
- `dotnet test backend/SupportCrm.sln` → **259 passed, 0 failed, 0 skipped** (was **251/0/0** —
  **+8, all A-21's**). Every rung asserted, including the three the seeded demo can never reach,
  plus the I-21 fall-through, the authorization guarantee (no Agent or Customer is ever a recipient,
  over both a manager-present and a manager-absent department) and determinism by repetition.
- **The guard is load-bearing:** reverting the fallback to the pre-decision behaviour turned **6 of
  the 8** red; the 2 that stayed green are rung 1 and rung 4, which must pass either way. Restored,
  rebuilt 0/0, suite re-run **259/0/0**.
- `npm run build` clean; `npm run lint:styles` clean. **The front end was not touched.**
- **The policy resolves from the real composition root** — the tests pull it out of the application's
  own container, not a double.

**Documents amended, consistently.** [product-scope.md](product-scope.md) §7 (new A-21);
[data-model.md](data-model.md) §2.2 (the OQ-3 block replaced by the answer) and §8 (row struck,
resolution recorded); [api-design.md](api-design.md) §5.6 and §6.2;
[ui-design.md](ui-design.md) §5.3 and §11 — **the dialog wording constraint is lifted**, and the
dialog may now say *a manager* will be notified, one sentence true on every rung, **with no new
payload field**; the Story 06 and Story 09 plans, which **now carry the same recipient rule and the
same log level**; the `ticket-management`, `sla-automation` and `organization` overviews;
`00-implementation-plan.md` §7.3; `00-index.md`. In `PROJECT-PROGRESS.md`: OQ-3 struck in §6.1,
**R-17** added to §6.3, **S9-3 closed** (both halves it widened are now answered), I-21 added to
§6.8, traceability row in §6.4, three evidence rows in §8.

**Deliberately not touched.** **No contract surface** — no endpoint, payload, response field, error
slug, entity, column or migration; `openapi/v1.json` still publishes **22 paths**. **No story 06
work**: no `TransitionTo`, no A-5 legality machine, no A-16 authority matrix, no `/escalate`
endpoint, no `/activity` read, no `INotificationPublisher` — that is Story 06 task 4 and is not
here. `DepartmentValidator` is unchanged and still says nothing about the null case, which stays
correct: eligibility of a manager that *is* set and choice of recipient when there is none are two
different rules. **OQ-1 and F-1 remain open**, as does product-scope §9 question 5.

---

### 2026-08-31 — story 05: ticket core, delivered whole

**The second T1 business story to land end to end, and the one the assessment's core loop rests
on.** Delivered **whole rather than in slices** — the plan's thirteen numbered tasks were the unit of
work — so there is no slice map for it. Code was committed as `a633923` with tracking deliberately
left behind; **this entry is that tracking**.

**What changed, and where.**

- **Domain** — `Modules/Tickets/`: `Ticket`, `TicketActivity`, and the `TicketStatus`,
  `TicketPriority`, `TicketActivityType`, `TicketActivityVisibility` and `TicketActorKind` enums;
  `Modules/Sla/SlaClock.cs` beside them, because [architecture.md](architecture.md) §2.1 puts SLA
  target calculation in the Domain. `SupportCrm.Domain` still has **zero project and zero package
  references** (**AD-4**).
- **Application** — `Modules/Tickets/`: **`TicketScope`**, the single department-scoping helper every
  later ticket query composes (**AD-5**); `TicketService` with category→department auto-assignment
  (**A-14**); `TicketActivityRecorder`; `TicketContracts`; `IAutoAssignmentPolicy`.
- **Infrastructure** — `TicketConfiguration`, `TicketActivityConfiguration`, the
  `20260830204837_Tickets` migration and `TicketSeeder` (6 tickets across both departments and a
  spread of priorities and statuses).
- **Api** — `TicketsController`, **seven operations**: `GET`/`POST /tickets`,
  `GET`/`PATCH /tickets/{id}`, `POST /tickets/{id}/assignment`, `GET`/`POST /tickets/{id}/attachments`.
  `openapi/v1.json` goes from **18 paths to 22**.
- **Front end** — `core/api/tickets.client.ts`; the workspace ticket list, detail header, assign
  control and customer panel; shared `StatusChip`, `PriorityChip` and `TicketFilterBar`; both i18n
  dictionaries extended.

**Why, with the ids cited.**

- **A-20 is implemented as code rather than merely satisfied.** `SlaClock.OnPriorityChanged` is a
  deliberate, commented **no-op**, and `TicketService.PatchAsync` **still calls it**. Story 06's
  manual escalation and story 09's automatic breach escalation call the same seam, so scattering
  *"we don't touch the dates"* across three call sites would have been the wrong shape. **OQ-2
  reached this story rather than only story 09** because the first code path that changes a priority
  is this `PATCH` — the widened blast radius recorded as **S9-3**.
- **`Ticket` has no branch column** and never may have one: a ticket's branch is derived
  `Ticket → Customer → Branch` ([data-model.md](data-model.md) §2.3). **A-2 / T2-K** keep a branch
  filter off the ticket list.
- **Out-of-scope tickets are `404`, not `403`**, on read *and* on write (**AP-4**).
- **Assignment does not change status** (**A-18**), rendered as two independent facts.
- Filtered-index predicates are written as two `<>` comparisons rather than `NOT IN` — a SQL Server
  requirement (**I-17**).

**Two audit findings are closed.** **S9-2** — the ticket half of story 04's attachment acceptance
criterion — is discharged by the two ticket-attachment routes. **S9-5** — both SLA due timestamps
required at creation — is implemented and proven live.

**Findings recorded — seven, I-14…I-20, none resolved by invention** (PROJECT-PROGRESS §6.8).

- **I-16 needs a user decision.** No approved endpoint lets an Agent list the staff in their own
  department: all five `/users` routes are **Administrator-only** ([api-design.md](api-design.md)
  §5.3), so the `Assign ▾` picker cannot be populated for the role that needs it most. **The current
  behaviour was deliberately kept and no endpoint was added** — everyone gets *Assign to me*, which
  needs no directory and is always legal because the ticket was loaded through the department scope;
  an Administrator additionally gets a real picker. The intake, plan task 13 and
  [ui-design.md](ui-design.md) §5.3 were each checked first, and **none explicitly requires an
  agent-readable staff directory**. If the user reads *"an agent can assign and reassign a ticket
  within their department"* as requiring an Agent to pick a colleague, that is a **§5 contract
  addition for the user to take, not a screen's to invent**.
- **I-20 is a verification-plan wording issue, not a code defect.** The plan's verification step 3
  greps for `branch` in the ticket Domain folder and expects nothing — but `Ticket.cs` carries a
  deliberate comment saying there is no branch column and there must never be one, so the literal
  grep matches **its own guard rail**, and deleting that comment to satisfy it would remove the very
  thing that stops the column being added. **The implementation was not changed.** The step's intent
  is proven three stronger ways instead: the same grep with **comments stripped** returns zero hits;
  the **type-level** `BranchIsNotABoundaryTests.Ticket_has_no_branch_member` reflects over `Ticket`'s
  members and asserts the set is empty; and the **live SQL Server schema** shows `dbo.Tickets` with
  19 columns and no branch column. **The plan text was left as approved** — rewording step 3 is a
  plan edit outside a tracking task's scope, and is offered to the user rather than taken.
- **I-14** (no approved document states how `priority` sorts — sorted by severity via the enum's own
  declaration order, the only reading under which the whitelist entry is useful), **I-15** (A-20's
  implementation is an empty method body, so two tests the plan did not name were added to make its
  reversal fail loudly), **I-17** (`NOT IN` in a filtered-index predicate took the migration down at
  startup on real SQL Server; SQLite accepts it, so only the real database could find it), **I-18**
  (`NG0950` from reading a required signal input in a constructor — found by driving the real
  screen), **I-19** (a deep link losing its filters on load, defeating **UI-9** — found by reloading
  a filtered list in a real browser).

**Verified 2026-08-31** — every command run in the tracking task, none inherited.

- `dotnet build backend/SupportCrm.sln` → **0 warnings, 0 errors** with `TreatWarningsAsErrors`.
- `dotnet test backend/SupportCrm.sln` → **251 passed, 0 failed, 0 skipped** (was **220/0/1**):
  **+31 tests, and the suite's one by-design skip is gone** — `BranchIsNotABoundaryTests` was
  skipped until story 05 created the `Ticket` type it asserts about, and now runs.
- `npm run build` clean; `npm run lint:styles` clean; `npx ng test` → **23 specs, unchanged**, which
  is the expected result for a story that added no spec.
- **Against real SQL Server**, the plan's own steps: an out-of-department ticket is **`404` on
  `GET`, on `POST /assignment` and on `PATCH`**; `POST /tickets` with no `departmentId` and
  `categoryCode: billing` lands in **Billing** with **both** due timestamps set; `GET /customers`
  returns a real **`openTicketCount`**; a Billing agent's list is **3 tickets, all in their own
  department**. **A-20 proven at the API**: `High → Urgent` moved **neither** due timestamp.
  **A-18 proven**: assigning a `New` ticket returned `status: New` with the assignee set.
- **Demo data restored:** the one verification ticket was removed with its activity rows and the
  counts re-read — `total tickets: 6`, the seeded number. Story 05 discloses **no** leftover row.
  Removing it re-confirmed **I-11** on a second, independent table: `sqlcmd`'s default
  `QUOTED_IDENTIFIER OFF` met the new filtered indexes and SQL Server named the cause outright
  (**`Msg 1934`**); with `-I` the delete succeeded.

**Deliberately not touched.** No approved document was edited — no new endpoint, no `StaffConfig`
member, no contract addition for **I-16**. The plan's verification step 3 wording was left as
approved (**I-20**). Story 06's `TransitionTo`, the A-5 legality machine, the A-16 authority matrix,
escalation and `/activity` are **not** here — story 05 withholds them by design, and `Transition ▾`
and `Escalate` are **not rendered**, because a disabled-looking control with no behaviour is worse
than none. **OQ-3** and product-scope §9 question 5 remain open and are untouched by this story.

---

### 2026-08-30 — OQ-2 answered: A-20, the SLA due timestamps freeze

**A decision record only. No source code was written, no plan file was touched, and no story was
started.** The question had gated story 05 since the stage 9 audit widened it there (**S9-3**).

- **The product owner chose freeze**, and it is recorded as **A-20** in
  [product-scope.md](product-scope.md) §7 — the register A-14…A-19 already use for exactly this
  class of decision (a business question no source answered, decided after a design stage found it).
  **`firstResponseDueAt` and `resolutionDueAt` are computed once at creation and a later priority
  change does not move them.**
- **Recompute was the rejected alternative**, and the reason is the one
  [data-model.md](data-model.md) §2.6 invariant 6 had already written down: recomputing from
  `createdAt` with the new priority's hours lets an escalation tighten a deadline retroactively, so
  a ticket can become breached *as a direct consequence of the escalation its own breach triggered*.
- **Escalation is otherwise unchanged.** It still raises priority one level (`Urgent` stays
  `Urgent`), leaves status alone, latches the breach flag, writes the activity row and notifies the
  department manager (A-5, T2-D). **Only the deadline is untouched.**
- **The model needed no change, which is why it could stay open this long.** §2.6 stores both
  timestamps and asserts no rule about them — *"compatible with either rule and asserts neither"*.
  Invariant 6 and §5 constraint 12 now cite **A-20** instead of the open question; the field table
  says frozen; the §8 register row is **struck through in place and closed**, and the question is
  added to that section's *"Resolved and closed"* list. **Nothing was deleted or relocated.**
- **`api-design.md` and `ui-design.md` needed no change at all.** Both readings write the same
  fields, so the contract is identical either way — [ui-design.md](ui-design.md) §11 had already
  established this: *"No UI dependency. Both readings write the same fields; the UI displays whatever
  the server computes."* One visible consequence worth knowing before story 05's queue is built:
  under freeze, the default `resolutionDueAt:asc` ordering **does not re-order** when a ticket is
  escalated.
- **Tracking updated:** OQ-2 struck through in place in [PROJECT-PROGRESS.md](PROJECT-PROGRESS.md)
  §6.1 with the full resolution recorded as **R-16** in §6.3 — both halves, per the OQ-5 / R-15
  precedent. Story 05's and story 09's blocker cells, the earliest-first ordering, and the §10
  decision table all updated to reflect that the earliest blocker in the sequence is now **S9-4**
  (story 11).
- **§9 question 5 is untouched and stays open.** Real SLA policy — business hours, holiday
  calendars, per-branch timezones, pause-on-customer-reply — is OQ-2's parent, and A-20 settles only
  what happens to two timestamps on a priority change. **No other `OQ-*` was answered.**
- **Deliberately not touched: the plan files.** Story 05's and story 09's ⚠ blocked-decision boxes
  and `SlaClock.OnPriorityChanged`'s `NotImplementedException("OQ-2")` still read as blocked. Plans
  are generated artefacts downstream of the design documents, and rewriting them is a separate
  approved unit of work — reported to the user rather than taken.

### 2026-08-30 — story 04 slice 6: the customer front end

Plan **tasks 11, 12, 13 and 14** — story 04's **last slice**, and the one that makes the `Customers`
module reachable by a person rather than only by `curl`. **Front end only:** no backend file changed,
and `openapi/v1.json` still lists **18 paths**.

- **Typed clients first (task 11).** `core/api/customers.client.ts` carries the nine customer calls
  and the six §6.3 / §6.7 payload types; `core/api/attachments.client.ts` carries `downloadUrl` and
  `download`. `ApiClientBase` gained one method, `getBlob` — the only binary response the contract
  has. **No component calls `HttpClient`** (architecture §2.2), and neither client has a `delete`
  method, because no endpoint does: deleting a customer is not an application operation
  (data-model §2.4) and a note is immutable (§2.5).
- **`UserSummary` landed in `identity.model.ts`**, where api-design **§6.1** defines it — *"embedded
  wherever a person is referenced"*. A note's author, an attachment's uploader and a timeline entry's
  actor are all it; the ticket payloads of stories 05–07 will be too.
- **The customer directory (task 12)** renders exactly ui-design §5.4's five columns — name, email,
  phone, branch, **open-ticket count** — and its filters are **`q` and `branchId` in the URL**
  (**UI-9**), under the API's own names with no translation step. `openTicketCount` reads `0` on every
  row today, which is the **true** aggregate until story 06 creates tickets, not a placeholder.
  **Branch is a filter here and nothing more** (T2-K, A-2), so the control is a plain select and
  deliberately **not** modelled on `app-department-filter` — that component exists to make *scoping*
  legible, and branch is in no authorization predicate anywhere. **No department filter**, because a
  customer has no department.
- **A create dialog was added to the directory, and it is a judgment call, recorded as one.** Neither
  task 12 nor ui-design §5.4 names a create control, but the plan's own Done Criteria require *"an
  agent can create … customers"* and task 11 puts `create` in the client. It is modelled on story
  02's `CreateUserDialogComponent`. **The branch is chosen here and required** — only a
  *self-registering* customer gets the configured default and is never asked (A-15).
- **The customer detail (task 13)** is four regions, each with its own loading, empty and error state
  (§9), loading **independently** so one slow call never blanks the screen. **The email field carries
  the A-19 helper line** — persistent, on the field, and **unconditional**, because the `Customer`
  payload carries no "has a login" field and this story adds none. **Both `409`s render inline on
  that field**: `customer-email-in-use` and `user-already-exists` both mean the address is taken and
  neither wrote anything. **Notes render no edit and no delete control anywhere**, because no path
  exists server-side. The timeline shows *"No activity yet"* — an empty state, never an error.
- **`AttachmentList` + uploader went into `shared/`**, as task 13 says, so the ticket and portal
  surfaces reuse it rather than re-inventing it. `413` surfaces **inline on the uploader**.
- **The registration screen (task 14)** replaces the "coming soon" placeholder story 02 left. Four
  fields, one optional, and **no branch selector and no role selector** — A-15 fixes both
  server-side, which is why `RegisterRequest` has nowhere to put either. `409 user-already-exists`
  reads as an invitation to sign in, not a failure (PF-6). A success returns an `AuthToken`, so the
  new customer is **signed in** and routed by role.
- **Two findings, neither resolved by invention.** **I-12 — no approved endpoint publishes the
  attachment size cap** that ui-design §8 asks the uploader to show: it is in none of §6.9's three
  configuration payloads, each of which enumerates its members exhaustively. The uploader takes an
  optional `maxSizeBytes`, defaulted to `null`, so the cap line is absent while `413` still reads as
  a translated sentence; publishing the cap means **adding a member to `StaffConfig`**, which is the
  user's call. **Needs a decision.** **I-13 — the plan's download rationale was wrong about a
  mechanism.** Task 11 says the auth interceptor supplies the bearer token for a bare
  `downloadUrl` path; it does not, because the interceptor only sees `HttpClient` requests and the
  token lives in `localStorage` (AD-7). `downloadUrl` exists exactly as named and is the one place
  the path is built — from the **id**, never a storage path (AP-19) — and `download()` fetches
  through `getBlob`, which makes the plan's own sentence true. **Informational.**
- **Finding I-11 is closed, and its diagnosis corrected.** Restoring demo data re-tested it: the same
  `DELETE` on the same row gives `Msg 8624` under `SET QUOTED_IDENTIFIER OFF` (`sqlcmd`'s default,
  which SQL Server refuses to combine with a **filtered index**) and **`deleted: 1`** under
  `QUOTED_IDENTIFIER ON`. **No index change is needed**, and the application never issued the failing
  form — `Microsoft.Data.SqlClient` sets the option on by default.
- **Verified against the running stack, and driven through a real browser.** The upload/download
  round-trip returns the same bytes with the original file name; **18 of 18 CDP checks** pass on the
  real screens; **no horizontal body scroll at 390 px** on all three; Arabic sets `dir="rtl"` and
  **this slice's own CSS mirrors, measured** (`0/744` → `744/0` on the uploader). **The staff shell
  sidebar does not mirror** — `layout/`, not this slice, and **story 17 Part B task 3 owns it by
  name**. **The guard was proven load-bearing:** reverting the multipart part name turned 1 of 23
  specs red, and the restore was confirmed by `grep`, `prettier --check` and a green re-run.
- **Tests:** `customers.client.spec.ts` (7, new) — front end **23 passing** (was 16). Backend
  **220 passing, 1 skipped — unchanged**, which is the evidence that no backend file moved.
- **Deliberately not touched:** no backend file, no design document, no plan content beyond the
  slice's status cell. `user-directory.component.ts` keeps its component-local filters — retrofitting
  UI-9 there is cross-cutting work, not this slice's. No shared `PagedTable` was extracted: story 02
  set the precedent of a screen-local `p-table` over the paged envelope, and extracting one now would
  mean refactoring that screen too. The directory uses a table rather than stacked cards, because
  ui-design §10.3's per-surface table names the agent queue, ticket detail and portal for card
  treatment and **not** the customer directory.

### 2026-08-28 — story 04 slice 5: customer self-registration

Plan **task 7** and its route, deferred here from Story 02 because registration is the first point
at which a `Customer`, a `Branch` and the configured default branch all exist (A-15). **Backend
only** — the registration screen is task 14, slice 6.

- **Three files changed, one added.** `RegisterRequest` (new contract, four fields),
  `AuthService.RegisterAsync`, and `POST /auth/register` on `AuthController`. No new endpoint beyond
  the one §5.2 publishes, no entity, no migration, no configuration key — `RegistrationOptions` and
  `User.CreateCustomerUser` were already there, waiting.
- **A-15's three outcomes, and only those three.** A brand-new email creates the profile and its
  login; an agent-created profile is **linked**, not duplicated, which is what keeps A-10's
  one-customer-per-email rule true; an email that already has a login is `409 user-already-exists`
  (PF-6, the same slug `POST /users` raises for the same collision).
- **Finding I-5 is closed, by reading rather than deciding.** Slice 3 left open whether A-15's
  default branch also sets `User.branchId`. It does not, and four approved documents say so —
  A-15's own wording, data-model §2.4, data-model §2.1 (`User.branchId` is a *"staff location.
  Reporting attribute only"*) and architecture §6.3 (*"assigned to self-registering **customers**"*).
  The login gets no branch; the profile gets the default. **An agent-chosen branch is never
  overwritten by it**, which the linking test asserts.
- **The response is an `AuthToken`**, so a new customer is signed in rather than bounced to the
  sign-in form. That is not a choice made here: api-design §6.1 names this endpoint alongside
  `/auth/login` for that payload.
- **`Location` is `/api/v1/auth/me`** — recorded as a judgment call, on the same reasoning the notes
  endpoint used. §2.2 pairs a `201` with a `Location`, and `/auth/me` is the only route the
  `Customer`-role token in the very same response may read the created identity through;
  `GET /users/{id}` is Administrator-only and `GET /customers/{id}` is Agent-only, so either would
  be a `Location` its recipient is forbidden to follow. A test **follows it with that token**.
- **One unit of work.** Profile, login and audit entry commit together through a single
  `SaveChanges`; no explicit transaction, because one `SaveChanges` already is one.
- **One `UserCreated` audit entry**, target and actor both the new user. `actorUserId` is passed
  explicitly for the reason `LoginAsync` passes it — the endpoint is anonymous, so `ICurrentUser` is
  empty while the actor is in fact known. The **customer profile is not audited**: creating one is
  business data, not a security event (AD-10). A refused registration writes nothing.
- **A guard for a state no approved flow produces.** A profile that already has a login under a
  different address is `409` rather than a unique-index violation surfacing as a `500`
  (data-model §5 constraint 3). A-19 keeps the two addresses in step, so it is unreachable today;
  the test builds the state directly and says so.
- **AP-10 needed no work here** — `RegisterRequest` has no `branchId`, `role` or `customerId`
  property, so the I-9 fix rejects all three with `400 validation-failed`, verified live.
- **Verified against real SQL Server**, including the case-insensitive linking SQLite cannot test:
  `SLICE5.CASE@EXAMPLE.COM` links to the `slice5.case@example.com` profile.
- **Tests:** `RegistrationTests` (22, new). Suite: **220 passing, 1 skipped** (was 198).

### 2026-08-28 — cross-cutting: AP-2 for model-state errors, finding I-10 closed

**Not a story task, and not the start of slice 5.** The fix the investigation below proposed,
approved and implemented. **No endpoint added, no status code moved, no request or response model
changed, and the front end deliberately untouched.**

- **Two registrations, one new file — `Errors/ModelStateProblemDetails.cs`**, the twin of
  `ProblemDetailsExceptionHandler`. The handler covers everything raised as an `AppException`; it
  never sees a model-state failure, because `[ApiController]` answers those itself before the action
  runs. This file is that other half.
- **`JsonOptions.AllowInputFormatterExceptionMessages = false`** — the framework's own purpose-built
  leak control. Its documentation: *"this setting controls whether clients can receive detailed
  error messages about submitted JSON data."* It substitutes a generic message and **keeps the
  key**, which is the half the contract needs: ui-design §9 renders a `400` *"inline on the offending
  field"*, so the field must survive, never the prose.
- **`InvalidModelStateResponseFactory`** — emits the §6.12 envelope through the framework's own
  `ProblemDetailsFactory`, so the response keeps whatever the pipeline adds (`traceId` today) and is
  indistinguishable in shape from a `ValidationException` `400`. `type` is `validation-failed`,
  `title` is `Invalid request`, `instance` is `METHOD /path` — the same three the handler already
  produced.
- **Keys are normalized and de-noised.** `$.email` → `email`, `DisplayName` → `displayName`,
  segment by segment so a nested path stays a path. The `request` entry — the *action's* C#
  parameter name, added when a body fails to parse — is dropped whenever a real error stands beside
  it, and otherwise re-keyed to the general `""`, so a C# identifier never reaches a client either
  way.
- **Nothing was invented.** `validation-failed` is the slug the Application layer has used since
  Story 02, `error.interceptor.spec.ts` already pinned `{ type: 'validation-failed', errors: {
  email: [...] } }`, and both dictionaries already shipped the string. **The front end passing
  unchanged is the proof** the server conformed rather than the client being bent to fit.
- **Verified live across all seven producers** against real SQL Server, and the leak scan runs over
  the **raw response text** rather than a parsed field, so a leak anywhere in the envelope fails it.
- **The guard is load-bearing:** removing the single registration turns **36 of the 43** tests in the
  two error suites red.
- **Other slugs untouched**, verified live: `customer-email-in-use`, `not-found`,
  `invalid-credentials`, and the `403` role denial.
- **One deliberate non-change: `traceId`.** `AddProblemDetails()` adds it to every error — `404` and
  `409` included — so it is a uniform RFC 9457 extension member, not an I-10 deviation. Left alone.
- **A gap recorded, not folded in:** ui-design §9 wants a `400` rendered *inline on the offending
  field*, but **no component reads `problem.errors` yet**. The server now supplies the dictionary;
  wiring it to form controls is a front-end story's work.
- **Tests:** `ModelStateProblemDetailsTests` (27, new), `UnmappedRequestMemberTests` (1 assertion
  updated from `$.{member}` to `{member}`). Suite: **198 passing, 1 skipped** (was 171).

### 2026-08-28 — investigation: I-10, the model-state `400` contract

**Investigation only. No code changed**, no endpoint contract touched, no fix implemented — the
user asked for the analysis before deciding. I-9 is approved and closed; this is the finding it
surfaced.

- **The API has two `400` shapes for the same error class.** `ValidationException` →
  `ProblemDetailsExceptionHandler` produces the §6.12 envelope (`validation-failed`, `detail`,
  `instance`). `[ApiController]`'s automatic model-state response produces the framework default:
  RFC-URL `type`, no `detail`, no `instance`. Both were captured live against the running stack
  across **six** producers — DataAnnotations, unmapped member, type mismatch, malformed JSON, query
  binding, and the two `ValidationException` paths.
- **Three concrete defects in the model-state shape**: the `type` is a URL, not a stable slug, so
  AP-2's localization contract cannot work; `errors` messages name .NET internals
  (`SupportCrm.Application.Modules.Identity.PatchUserRequest`, ``System.Nullable`1[System.Guid]``,
  byte offsets); and the `errors` **keys are inconsistent** — camelCase field names from
  DataAnnotations and query binding, JSON paths (`$.email`) plus a spurious `"request"` entry from
  the JSON reader.
- **The expected shape did not have to be decided — it is already pinned twice.**
  `error.interceptor.spec.ts` asserts a validation `400` as
  `{ type: 'validation-failed', errors: { email: ['Email is required.'] } }`, and **both** i18n
  dictionaries already ship an `errors.validation-failed` string. The server is the only component
  that does not conform.
- **It is user-visible.** Transloco's `DefaultMissingHandler` returns the key when a translation is
  missing (verified in the installed package), and `ErrorStateComponent` / the user and login forms
  render `errors.${type}` directly — so a failed `POST /users` shows the literal text
  `errors.https://tools.ietf.org/html/rfc9110#section-15.5.1`. The interceptor's generic fallback
  does not help: it runs only for `5xx` and network failures, never for `400`.
- **A separate gap, recorded not folded in:** ui-design §9 presents a `400` *"inline on the offending
  field"*, but **no component reads `problem.errors`** yet — the per-field rendering is unbuilt on
  the front end. That is a front-end story's work, not part of I-10.
- **`traceId` is on every error, not just this one.** `AddProblemDetails()` adds it to both paths
  and to `404`/`409` alike, so it is a uniform extension member rather than an I-10 deviation.
  Noted, not proposed for change.
- **Blast radius is small and measured, not estimated.** Server: 13 of 17 paths can emit a
  model-state `400`. Tests: **one** assertion reads a `400` body at all
  (`UnmappedRequestMemberTests.AssertUnmappedMemberRefusedAsync`); the other seven check status
  only. Front end: no change needed — conforming the server *fixes* it.
- **Recommendation: fix it now, before slice 5**, and the reasoning is in §6.8's I-10 row.
  ✅ **Approved and implemented the same day** — see the entry above.

### 2026-08-28 — cross-cutting: AP-10 enforced, finding I-9 closed

**Not a story task, and not the start of slice 5.** A cross-cutting fix requested between story 04
slices 4 and 5, resolving the one open contract gap in slice 4's review. **No endpoint was added,
no request or response model changed shape, and the notes `201 Location` decision is untouched.**

- **The change is one setting, in one place.** `Program.cs`'s `AddJsonOptions` block now sets
  `options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow`.
  That is the whole fix. It sits on the options every controller body is bound with, because AP-10
  is a property of the contract rather than of any one endpoint — the alternative, an attribute or
  a filter per request model, is the endpoint-specific workaround the approved design avoids.
- **The mechanism.** Omitting a property from a request model makes a field *unreachable*, which is
  the safety half of AP-10 and was always true. `System.Text.Json`'s default is to *skip* a member
  that maps to nothing, so the request was still *accepted* — the exact thing AP-10's row names as
  its rejected alternative. `Disallow` turns the skip into a `JsonException`, the MVC input
  formatter records it as a model-state error, and `[ApiController]` returns the `400`
  [api-design.md](api-design.md) §7 promises.
- **The blast radius is six endpoints, not 23.** The earlier estimate counted every route; only six
  bind a JSON body — `POST /auth/login`, `POST /customers`, `PATCH /customers/{id}`,
  `POST /customers/{id}/notes`, `POST /users` and `PATCH /users/{id}`. The other seventeen are
  `GET`s, the bodyless `POST /users/{id}/deactivate`, or the multipart upload, and none of them
  deserializes JSON at all.
- **Three things deliberately did not move**, and each has a test pinning it: query-string binding
  (model-bound, not deserialized — so AP-15's filter and sort whitelists still raise their own
  `400`s from the Application layer, and an unknown query *key* is still ignored); case-insensitive
  property matching (`JsonSerializerDefaults.Web`, unchanged — a `PascalCase` body still binds); and
  response serialization (`Disallow` is a read-side setting, so no response shape changed).
- **An empty `PATCH` body is still a `200`.** `Disallow` refuses *extra* members, never missing
  ones, which is what [api-design.md](api-design.md) §2's "PATCH with only the fields being changed"
  requires.
- **The front end needed no change.** `CreateUserRequest` and `PatchUserRequest` in
  `identity.client.ts` already mirror the server models field for field, and the two call sites
  build their bodies from explicit literals rather than spreading a form value. Build clean, 16/16
  specs pass.
- **Verified live against real SQL Server, before and after.** On the pre-fix image both named
  cases returned `200`; after `docker compose up -d --build api` both return `400` naming the
  refused member at its JSON path. The regression sweep — login, both patches, an empty patch, a
  note, two creates, a `PascalCase` create, a multipart upload, and both AP-15 query cases — is
  green, and `openapi/v1.json` still lists 17 paths with the six request schemas publishing exactly
  their mapped fields. The seeded rows the sweep touched were patched back and confirmed restored.
- **The tests fail without the setting, which is the point.** Reverting the one line turns 12 of the
  16 new tests red; the 4 that stay green are the "nothing legitimate broke" guards, which must pass
  either way.
- **Two findings came out of the fix, neither introduced by it** — **I-10** (a model-state `400`
  carries the framework's default `type`, not a stable AP-2 slug — pre-existing on every validation
  failure since Story 02) and **I-11** (`Customers` rows cannot be deleted on SQL Server, `Msg 8624`,
  because the referencing index is filtered). Both are in §6.8. **I-10 requires a decision.**
- **Tests:** `UnmappedRequestMemberTests` (16, new), `CustomerAccessTests` (15, one rewritten from
  pinning the old `200` to asserting the `400`). Suite: **171 passing, 1 skipped** (was 155).

### 2026-08-28 — story 04 slice 4: the customer endpoints

- **Plan tasks 8 and 10.** `CustomersController` (the nine customer-scoped routes of api-design
  §5.5, `RequireAgent` declared once on the class) and `AttachmentsController` (the AP-19 download,
  `[Authorize]` with **no role policy**, because authorization there is by owner reachability).
  `openapi/v1.json` goes from 11 paths to **17**.
- **`POST /auth/register` was deliberately left out.** Task 8 lists its route, but it needs task 7's
  `RegisterAsync`, which is slice 5. It still returns `404`.
- **The `IFormFile` boundary is the upload action**, mapping onto the Application-owned
  `AttachmentUpload` — the resolution of finding **I-6**. It also supplies the RFC 2046 default
  content type when a client omits the part's, so an empty string cannot reach a domain factory that
  would refuse it with a `500`.
- **`201` targets were chosen, not defaulted.** `POST /customers` points at the customer;
  `POST /customers/{id}/attachments` points at the **download**, which is the created resource;
  `POST /customers/{id}/notes` points at the collection, because §5.5 publishes no single-note
  endpoint and inventing one to have a `Location` target would be contract surface no requirement
  asks for (AP-18). Recorded as a judgment call.
- **A-19 is now proven live**, not only in tests: patching a seeded customer's email through the new
  route moved their portal sign-in with it, wrote exactly one `UserEmailChanged` entry with the
  calling agent as actor and the linked user as target, and stored no address. The seeded value was
  patched back afterwards.
- **One finding, and it is pre-existing (I-9).** AP-10 says a request carrying a server-derived field
  is a `400`; in fact unknown JSON members were silently ignored, because nothing configured
  `UnmappedMemberHandling.Disallow`. `PATCH /users/{id}` had behaved this way since Story 02. The
  field was still unreachable — `externalReference` cannot be set — but the request was *accepted*
  rather than *refused*. **Not fixed in this slice**, because it is a cross-cutting contract change
  rather than a controller one. ✅ **Closed 2026-08-28 on the user's instruction**, in the
  cross-cutting entry above.
- **Tests:** `CustomerAccessTests` (15). Suite: **155 passing, 1 skipped** (was 140).

### 2026-08-28 — cross-cutting: front-end HTTP error handling

**Not a story task.** A small cross-cutting change requested between story 04 slices 3 and 4, and
scoped to the front end's HTTP layer. **No API contract changed and no product feature was added.**

- **`errorInterceptor` now applies the cross-cutting half of [ui-design.md](ui-design.md) §9's
  status table**, and only that half. It already normalized every failure into an `ApiProblem` and
  handled `401`; it now also routes `403` to the existing `/403` screen **without clearing the
  session** — a role denial is not an expired session — and raises **one translated toast** for
  `5xx` and unreachable-server failures.
- **The `401` rule was tightened rather than rewritten.** The anonymous-auth exclusion is now a list
  (`/auth/login`, `/auth/register`) instead of a single URL, and a `401` arriving while the user is
  already inside `/auth/*` clears the session without navigating — previously that would have
  discarded a half-typed sign-in form.
- **Structured errors are explicitly not swallowed.** `400`, `409`, `413`, `422` and `404` pass
  through untouched, because §9 renders each of them *inline, in context* and only the feature knows
  where. The problem is rethrown in every case, including the ones the interceptor acts on.
- **`503` is deliberately excluded from the toast.** §9 scopes it to the AI panel — *"the rest of
  the screen stays live"* — so a global surface would contradict it (AP-12).
- **The one piece of missing infrastructure: there was no toast or notification mechanism at all**
  — no `MessageService`, no `p-toast`, nothing. PrimeNG's `MessageService` is now provided at the
  root and `<p-toast>` sits in `AppComponent`, so it survives the navigation a `401` or `403`
  causes. **It is the transport-level surface only, and is not the A-13 notification centre**, which
  is a stored per-recipient entity with an unread badge that story 09 delivers.
- **Tests:** `error.interceptor.spec.ts` — 12 specs covering each row of the table in both
  directions (what is handled, and what is left alone). Front end: **16 passing** (was 4).
  `npm run build` and `npm run lint:styles` clean. Backend untouched: **140 passing, 1 skipped**.
- **No new translation key was needed** — `errors.*` already carried `http-500`, `http-503`,
  `network-unavailable` and `internal-error`, at full en/ar parity. An unmapped `5xx` slug falls back
  to `errors.internal-error` rather than surfacing a code.

### 2026-08-28 — story 04 slice 3: notes, timeline, attachments and demo data

- **Plan tasks 4, 5, 6 and 9.** `CustomerNoteService`, `CustomerTimelineService`,
  `AttachmentService` and `CustomerSeeder`. **No endpoint is published yet** — the controllers are
  task 8, slice 4.
- **Notes are immutable by construction, and the service proves it.** Its entire public surface is
  `AddAsync` and `ListAsync`; there is no update and no delete *method*, which is what
  docs/data-model.md §5 constraint 16 asks for rather than merely no route.
- **The timeline is a read projection written against the empty ticket set**, exactly as the intake
  authorizes. It returns a well-formed empty page today — the intake's *"renders an empty state
  rather than an error"* criterion is **met now** — with a `// Story 06:` marker carrying the query
  that replaces it, including the note that `TicketInternalNote` is absent from it **on purpose**.
- **`AttachmentService` covers both owners** (S9-2). The customer half is complete; the ticket half's
  bodies are written and **fail closed** through a single `EnsureTicketReachableAsync` seam that
  Story 05 fills in, so a forgotten scoping check surfaces as a visible `404` rather than a hole.
- **`CustomerSeeder` (`Order = 30`)** seeds 4 customers across both branches, 2 portal logins and 2
  deliberately-unlinked profiles, a note and an attachment — both DM-1 shapes present, so **A-19 is
  demonstrable by hand** against demo data.
- **Four findings, none resolved by invention** (§6.8): **I-5** task 9 needs
  `User.CreateCustomerUser`, which the plan assigns to task 7 — the factory moved, registration did
  not; **I-6** `IFormFile` cannot cross into the Application layer, so the upload takes an
  Application-owned `AttachmentUpload` following the `Stream`-based seam slice 1 already built;
  **I-7** SQLite cannot `ORDER BY` a `DateTimeOffset`, which blocks the *mandated* newest-first
  ordering of two endpoints, resolved with a SQLite-only value converter under a provider guard
  mirroring the existing SQL Server collation guard; **I-8** a real deployment defect — the
  attachment volume was root-owned and the non-root container could not write to it.
- **Tests:** `CustomerNotesAndTimelineTests` (9), `AttachmentServiceTests` (11),
  `CustomerSeederTests` (6). Suite: **140 passing, 1 skipped** (was 114).

### 2026-08-27 — story 04 slice 2: `CustomerService` and the A-19 propagation

- **Plan task 3 only.** The user narrowed slice 2 to the service; the controllers (task 8) moved to
  their own slice, so the slice map in
  [.squad/plans/customer-management/00-overview.md](../.squad/plans/customer-management/00-overview.md)
  is now **six** slices, not five. **No endpoint is published yet.**
- **`CustomerService`** — the four profile operations of api-design §5.5: `ListAsync` (paged, `q`
  and `branchId` filters, the `fullName`/`createdAt` sort whitelist §5.5 itself enumerates),
  `GetAsync`, `CreateAsync` and `UpdateAsync`.
- **A-19 is implemented, and it is the substance of this slice.** `UpdateAsync` handles the six
  cases exactly as the story plan tabulates them, and the propagation writes **one**
  `UserEmailChanged` entry against the **linked user** — actor resolved from `ICurrentUser`, never
  passed explicitly. **One `SaveChangesAsync`, no explicit transaction**: architecture §3's existing
  unit-of-work rule is what makes it atomic, and two commits are the divergence A-19 exists to
  prevent.
- **`AuditAction.UserEmailChanged`** added beside `UserRoleChanged` and `UserDepartmentChanged` —
  **the entire schema change**, exactly as the plan says. `AuditEntry`, `AuditTargetType`,
  `IAuditRecorder` and the migration are untouched, and the entry records **no address**.
- **`User.ChangeEmail`** added, because Story 02 deliberately left `User` without an email mutator.
  This does **not** make email patchable through `PATCH /users/{id}`: `PatchUserRequest` still has
  no `email` property (AP-10), which is asserted by a test. See the deviations note below.
- **`openTicketCount` is a literal `0`** with the `// Story 05:` marker the plan requires, and the
  marker carries the plan's instruction to compute it in **one grouped subquery**, not per row.
- **Deliberately not built:** `CustomerNoteService`, `CustomerTimelineService`, `AttachmentService`,
  every controller, `POST /auth/register`, `User.CreateCustomerUser`, `CustomerSeeder` and all
  front-end files — each verified absent.
- **Tests:** `CustomerServiceTests` (24). Suite: **114 passing, 1 skipped** (was 90).

### 2026-08-27 — story 04 slice 1: customers domain and data layer

- **Plan tasks 1 and 2 only**, at the user's instruction to deliver story 04 in slices with approval
  between each. The slice map is in
  [.squad/plans/customer-management/00-overview.md](../.squad/plans/customer-management/00-overview.md).
- **Three domain entities** in `SupportCrm.Domain/Modules/Customers/`. Each invariant the data model
  calls structural is structural, and each is asserted by a test:
  - `Customer` — `ExternalReference` has a private setter and **no mutator of any kind**, so the ERP
    seam field (DM-6, api-design §8.3) cannot reach a request model.
  - `CustomerNote` — **no public setter and no instance method at all**, which is §5 constraint 16
    ("immutable once written") rather than merely an absent endpoint.
  - `Attachment` — two factories, `ForCustomer` and `ForTicket`, and no public constructor, so the
    owner-XOR rule of §5 constraint 20 is unconstructible to violate.
- **`Customers` migration** applied to real SQL Server: `Customers`, `CustomerNotes`, `Attachments`,
  the `CK_Attachments_OwnerXor` check constraint, `IX_Customers_Email` (unique), `IX_Customers_BranchId`,
  and **`FK_Users_Customers_CustomerId`** — which closes the DM-1 link story 02 deliberately left open.
- **`Customer.Email` takes the same collation and width as `User.Email`** — `nvarchar(256)`,
  `SQL_Latin1_General_CP1_CI_AS`. §6.1 exists precisely so these two, being the same address compared
  across two tables (A-19), cannot drift apart.
- **`IAttachmentStorage` seam**, declared in Application and implemented in Infrastructure as
  `LocalDiskAttachmentStorage` — the same shape as the `ITokenIssuer` seam (AD-11). The name on disk
  is **server-generated**; seven crafted file names (`../../appsettings.json`, an absolute Windows
  path, `/etc/passwd`, `....//`) are each proven to land inside the configured root.
- **One new configuration key: `SupportCrm:Attachments:StorageRoot`.** Story 04's plan task 2
  requires a "configured root" and no earlier document provides one — see the deviations noted below.
  A second Compose volume, `supportcrm-attachments`, backs it.
- **Deliberately not built:** `CustomerService`, `CustomerNoteService`, `CustomerTimelineService`,
  `AttachmentService`, any controller, `POST /auth/register`, `User.CreateCustomerUser`,
  `CustomerSeeder`, and every front-end file. **The `UserEmailChanged` audit constant was not added**
  either: it belongs to task 3, and adding a constant nothing writes would be dead code that looks
  like an implemented decision. **A-19 is untouched and preserved exactly as documented.**
- **Story 16 Part A is still uncommitted.** It was reported as committed before this slice began; `git log` shows head `634bad2`, a docs commit, and `HEAD` contains none of Part A's files. Both stories are therefore stacked in the working tree, with disjoint file sets.
- **Story 02's `AddCustomerRoleUserAsync` test helper was updated**, not worked around. It used to
  insert a `Users` row with an arbitrary `CustomerId`, which was only ever valid because the column
  had no foreign key. It now creates a real `Customer` through the domain factory and links to it —
  which is what a portal login has always meant (DM-1). Five tests failed on the new FK before this
  change and pass after it.
- **Tests:** `CustomerDataLayerTests` (14) and `AttachmentStorageTests` (8). Suite: **90 passing,
  1 skipped** (was 68).

### 2026-08-27 — story 16 Part A implemented

- **Tasks 1–5 only.** Seven option types, `ConfigurationValidator`, `GET /config`,
  `GET /config/staff`, and three test files — **36 tests**. **Part B is untouched**: no
  `GET /audit`, no audit screen, no configuration view, and no front-end file changed.
- **Startup validation is the acceptance criterion, and it is proven by starting the real API.**
  The intake requires that *"invalid configuration fails fast at startup with a clear message"*.
  All six checks were run against real SQL Server with the value broken in turn; every one stops
  the host with a non-zero exit and names the offending value — the dangling-category message
  reads *"category 'billing' maps to departmentId '…', which is not an existing department. Every
  category must map to a department that exists (A-14)."*
- **The two referential checks run in `DatabaseInitializer`, after migrations and seeding**, as
  the plan requires — they read rows that seeding creates, so they cannot run during binding.
- **AP-17's split is now enforced by test and proven live.** With a real Customer token,
  `/config` returns exactly `categories` and `feedback`, `departmentId` appears **nowhere** in the
  body, and `/config/staff` is `403`. This is the **B-2** regression: the first contract returned
  quick replies and SLA targets to every authenticated caller.
- **One contradiction found in the plan and resolved without inventing anything.** Task 1 and
  task 2 check 4 validate `Priorities` against *"the `TicketPriority` enum"* — which does not
  exist, because `05-story-ticket-core.md` creates it and Part A runs **before** story 05 by
  design. `PriorityOptions.ApprovedLevels` holds the four **A-6** names instead — the same
  authority the enum's own plan cites (`// A-6`). **Story 05 must replace it with
  `Enum.GetNames<TicketPriority>()` and delete it**; marked at the code, in the same style as
  story 04's `openTicketCount` placeholder.
- **OQ-1 is untouched and stays open.** The rating-scale key holds `1..5` behind a block comment
  in `appsettings.json` naming OQ-1 and quoting architecture §6.3's *"inventing the answer is out
  of scope"*. Validation checks `Min < Max` and **nothing else**, so a 1–10 or a 0–1 binary scale
  passes just as happily. **No `min`/`max` constant exists anywhere else in the codebase.**
- **`NoConfigurationEntityTests` is the guard that keeps T2-I true as later stories add tables** —
  no `DbSet` for a category, priority, SLA policy, quick reply, branding, setting, tenant,
  organization or role, asserted by reflection over the context.
- **Two implementation choices no document fixes**, both recorded at the code: list-bearing
  sections bind through an `Items` (or `Levels`) property, because `AddOptions<T>().Bind()` binds
  a section to an object and a bare JSON array is not one; and the attachment cap defaults to
  10 MiB, a number no document states — which is why it is configuration and not a constant.
- **Regression:** 68 backend tests pass, 1 skipped by design. Stories 01–03 re-checked live —
  `/health` and `/config/bootstrap` still anonymous, `/users` still `401`/`403`/`403`/`200`,
  `/departments` and `/branches` still `403` for a Customer and `200` for Agent and above.

### 2026-08-27 — OQ-5 answered: A-19, and the login change is audited

- **The product owner chose Option A**, and it is recorded as **A-19** in
  [product-scope.md](product-scope.md) §7 — the register A-14…A-18 already use for exactly this
  class of decision (a business question no source answered, decided after a design stage found it).
  **When `Customer.email` changes, the linked portal `User.email` is set to the same value.**
- **Atomic by the existing rule, not a new one.** [architecture.md](architecture.md) §3 already says
  *"one unit of work per request, owned by the Application service, committed once"* — both rows
  change in one commit, so no committed state has them differing. `architecture.md` needed no
  change beyond its assumption-range citation.
- **`User.email` uniqueness is untouched and applies to the propagated value** across all users,
  staff included. A collision returns **`409 user-already-exists`** — **PF-6's existing slug for
  PF-6's existing rule** — and writes **neither** row. No new problem type was minted.
- **Closing it removed a latent gap in `api-design.md` §5.2.** Had divergence been allowed, a
  profile could hold an address matching a registration attempt while already carrying a login under
  a different one — a state none of A-15's three outcomes covered, and one §5 constraint 3 forbids
  resolving by linking a second login. A-19 makes that state unreachable, so the three outcomes stay
  exhaustive.
- **`ui-design.md` §11 released the warning it was holding.** That row said *"add a warning only once
  OQ-5 is answered"*; §5.5 now specifies a persistent helper line on the email field, stated **before**
  the save because A-9 excludes account recovery. It is unconditional: the `Customer` payload carries
  no "has a login" field and **none was added** — inventing contract surface to vary one sentence was
  rejected.
- **Two pre-existing contradictions were found by the consistency check and fixed**, neither related
  to OQ-5: `api-design.md` §10.3 still traced A-10 to *"email immutability"* eight weeks after N-2
  removed that rule, and `ui-design.md`'s source-of-truth line still cited *"65 endpoints,
  AP-1…AP-18"* after AP-19 took it to 66.
- **Amended the same day, on the product owner's instruction: the propagated login-email change is
  audited**, as part of A-19 and inside the same unit of work.
  - **Action code `UserEmailChanged`**, following `UserRoleChanged` / `UserDepartmentChanged`
    exactly. **One new constant in `AuditAction`** — `AuditEntry`, `AuditTargetType`,
    `IAuditRecorder` and the migration are untouched, and no endpoint is added. `AuditAction`'s
    own comment already declared the set open, and data-model §2.14 already gave its actions as
    examples.
  - **Actor is the agent who issued the `PATCH`**, resolved from `ICurrentUser` the ordinary way.
    The `actorUserId` override is explicitly **not** used — it exists for the anonymous
    successful-sign-in case alone.
  - **Target is the linked `User`, not the customer.** The audited fact is that a sign-in
    identifier changed; the profile edit beside it is business data, not a security event (AD-10),
    and is not audited.
  - **The entry records no email address, old or new** — `AuditEntry` has no value columns, exactly
    as `UserRoleChanged` records that a role changed without recording which. Adding them would be
    a schema change *and* would copy a personal identifier into a log that is never deleted, so it
    was rejected. Recorded in data-model §2.14 so nobody re-opens it as an oversight.
  - **Written only on a real change**, and never on a rejection: no entry for an absent `email`, an
    unchanged address, a customer with no linked login, or either `409`. **No `Failure` row exists
    for this action**, matching every user-administration call site in Story 02 — only a failed
    *sign-in* is recorded as `Failure`.
  - **Atomic without new machinery:** `RecordAsync` adds to the change tracker and does not commit,
    so the single `SaveChangesAsync` commits both rows and the entry together.
  - **Story 16's audit-coverage inventory (task 6) gained the row**, so its completeness check does
    not later flag `UserEmailChanged` as an unaccounted action.
- **Nothing was implemented.** Story 04 remains Not Started.

### 2026-08-27 — story 03 completed

- **Story 03 tasks 4–8 implemented**, closing the story and phase 1. `OrganizationQueryService` and
  `DepartmentValidator` in Application; `DepartmentsController` and `BranchesController` in Api, one
  `GET` each behind `RequireAgent`, **with no write verb on either** (T2-I). Front end:
  `organization.client.ts` with both lists `shareReplay`-cached for the session, and
  `shared/components/department-filter/` carrying the `disabledForOwnDepartment` rule so the story-05
  ticket list cannot re-implement it.
- **Both placeholders the previous entry flagged are gone.** The create-user dialog and the
  user-detail form now bind a selector populated from `GET /departments` instead of taking a
  department id as free text, and the avatar menu shows the department **name** rather than its id.
- **`IdentitySeeder`'s manager second pass now calls `DepartmentValidator`** rather than restating the
  eligibility rule inline — what story 03 task 4 always intended, and what the seeder's own comment
  forward-referenced. The throw is caught, so an ineligible demo manager leaves the department
  without one (a legal state) instead of taking the API down.
- **One defect found and fixed, inherited from story 02.**
  `CreateUserDialogComponent.departmentMissing` was a `computed` over a **plain** field, so it cached
  against `touched` alone: after one failed submit, the warning and the disabled submit button could
  never clear whichever department the administrator then picked. The field is now a signal. It was
  reached by replacing the input it guarded.
- **Verification step 6 could not be run as written.** It names the `/workspace/tickets` department
  filter, and the ticket list is **story 05** — an ordering fact, not a gap. The rule it checks was
  verified instead by `department-filter.component.spec.ts`. **Story 05 must run the step against the
  real screen**, and remove the `Skip` on `BranchIsNotABoundaryTests.Ticket_has_no_branch_member`.
- **OQ-3 remains open and remains unanswered.** `DepartmentValidator` constrains only a manager that
  *is* set and says nothing about a department without one; the seeded managers are still a demo
  convenience, commented as such at both ends.
- **Two implementation choices not fixed by the approved documents**, both following the `/users`
  precedent: the sort whitelist for the two organization endpoints is `name` only (api-design §2.1
  requires *a* whitelist and names none for them), and the front-end client requests `pageSize=100`,
  the contract's documented maximum — so more than 100 departments would be truncated, which is a
  question for api-design and ui-design rather than a client-side loop.
- **Verified against real SQL Server on a wiped volume:** `InitialSchema` applies, `OrganizationSeeder`
  writes 4 rows, the manager second pass assigns 2, and `GET /api/v1/departments` returns `Billing`
  and `Technical`. Live role matrix on both endpoints — anonymous `401`, Customer `403`, Agent,
  Manager and Administrator `200`; `POST`/`PATCH`/`PUT`/`DELETE` all `405`; an unknown sort field
  `400`. A department with a null `ManagerUserId` serializes with **no `managerUserId` key at all**.
  `grep -ri "branchid"` over `Domain/Modules/Tickets/` returns nothing, and **no `Branch` reference
  anywhere in the backend sits in an authorization predicate**. Stories 01 and 02 re-checked live.

### 2026-08-26 — story 02 implemented

- **Story 02 (`auth-and-roles`) complete and verified.** Email + password sign-in, the four fixed
  hierarchical roles as policies, per-request identity resolution, Administrator user management, and
  the single audit recorder. Nine endpoints now exist.
- **AD-15 is enforced by shape, not by discipline.** `ITokenIssuer.Issue(Guid userId)` has no
  parameter through which a role, department or active flag could reach the token, and
  `CurrentUserMiddleware` re-reads all three from the authoritative row on every authenticated
  request — refusing a missing or deactivated user with **`401` before authorization runs**, and
  replacing the principal's role claim so the endpoint gate and the row scoping read the same
  vintage. Verified: the issued token carries `sub`, `jti`, `iat`, `exp`, `iss`, `aud` and nothing
  else, and a user deactivated after their token was minted is `401` on the very next request.
- **The single `InitialSchema` migration was created here**, containing all four tables
  (`Branches`, `Departments`, `Users`, `AuditEntries`), exactly as both story plans and
  `00-implementation-plan.md` §6 require. Applied to real SQL Server; `Users.Email` carries the
  case-insensitive collation and a mixed-case sign-in was exercised against it.
- **Story 03 task 3 (`OrganizationSeeder`) was executed early, with the user's approval**, because
  Story 02 task 10 cannot seed a staff user without a department — the ordering Story 03's own
  prerequisites describe. Story 03 is otherwise untouched: no query service, no controllers, no
  endpoints.
- **Two defects found by this story's own verification, both contract violations, both fixed:**
  - **Enums were serializing as integers**, which api-design §2 forbids outright. It surfaced as
    `POST /users` rejecting `role: "Agent"`, so the contract breach and a functional break were one
    bug. `JsonStringEnumConverter` added.
  - **A successful sign-in recorded `actorUserId = null`.** data-model §2.14 allows null for exactly
    one reason — "no user could be resolved, a failed sign-in" — so a success must be attributed.
    `IAuditRecorder` gained an explicit actor override for the one case where the caller knows the
    actor but the request has no identity yet, `POST /auth/login` being anonymous. Tests now lock
    both directions.
- **Two front-end bugs found and fixed in the browser, not in review:** an `inject()` after an
  `await` in the app initializer threw **NG0203** and left the page blank; and a deep link to a
  guarded route bounced a signed-in user to sign-in, because
  `withEnabledBlockingInitialNavigation()` runs the router's initial navigation before the bootstrap
  `loadMe()` resolves. The guards now resolve identity on demand, which removes the provider-ordering
  dependency instead of papering over it.
- **Deviations from the plan's letter, all deliberate and reported:** `UnauthorizedException` added
  to the Story 01 exception family (the plan names it; 401 had no member yet); `IApplicationDbContext`
  introduced so Application can orchestrate persistence without naming `SupportCrmDbContext` — AD-3
  still holds, it wraps nothing and adds no method; `ICurrentUser` gained `IsAuthenticated` because
  the audit recorder must handle the actorless failed sign-in; `IAuditRecorder` takes an
  `AuditOutcome` enum rather than a `string`, so an invalid outcome cannot reach the column.
- **Three values no approved document fixes**, all placed in configuration rather than in code and
  all flagged: JWT `AccessTokenMinutes` (60), `Issuer`/`Audience` (`SupportCrm`), and the seeded demo
  password (development default in `appsettings.Development.json`; any other environment must supply
  it or startup fails). Plus the `GET /users` sort whitelist, which api-design §2.1 requires to exist
  but does not enumerate for this endpoint.
- **`POST /auth/register` remains deferred to Story 04** (S9-7). Its route and component are
  scaffolded with submit disabled and a note, rather than calling an endpoint that does not exist.
- **Nothing was committed** — the working tree is left for review.

### 2026-08-26 — string-length convention closed

- **[data-model.md](data-model.md) §6.1 added — string column length, collation and index
  eligibility.** Closes the finding raised by story 03: no approved document stated a string length
  anywhere, yet the model declares four *unique* indexes on string columns, and SQL Server cannot
  build one over `nvarchar(max)`. Every story would have invented its own widths, and nothing kept
  `User.email` and `Customer.email` — the same address in two tables — the same size.
- **Five tiers, and a tier for every one of the 39 string fields in §2.** `Code` 64 · `Name` 200 ·
  `Email` 256 · `Line` 512 · `Text` max. Implementers pick a tier, never a number. Verified
  mechanically: 39 string fields declared in §2, 39 assigned, **no field unassigned and no stale
  entry**.
- **The index-key rule is the point of the section.** A column in an index key must be `Code`,
  `Name` or `Email`; `Text` can never be indexed. Checked against every index the model requires —
  §6's list plus the four §2 unique constraints — the widest key is `User(email)`/`Customer(email)`
  at **512 bytes, 30% of SQL Server's 1700-byte limit**, and `Ticket.status` is the only string
  inside a composite key.
- **This amends the document's own scope, and the amendment is recorded rather than slipped in.**
  The header previously said physical lengths were deliberately left to implementation, and the §8
  gate row asserted "conceptual and logical only". Both now state the single exception and why it
  was unavoidable. §2 is untouched; no DDL was added.
- **Collation decided:** `User.email` and `Customer.email` declare
  `SQL_Latin1_General_CP1_CI_AS` explicitly. The SQL Server 2022 image already defaults to
  case-insensitive, so this changes no behaviour — it is declared because "two addresses differing
  only in case are the same address" is a **product rule** (A-9, A-10) and must not depend on a
  server default a different deployment could change.
- **Three tier choices carry a stated reason** where the obvious pick was wrong:
  `Attachment.contentType` is `Name` not `Code`, because real MIME types exceed 64 characters;
  `AuditEntry.actorDescriptor` is `Email` but the recorder **truncates rather than throws**, since a
  failed sign-in with an absurd identifier must still be recorded; `User.passwordHash` is `Line` for
  headroom against a future hashing algorithm.
- **[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §6 now points at §6.1**
  so no plan re-invents lengths, and records the pinned `dotnet-ef` local tool.
- **Two comments in story 03's EF configurations corrected.** They said the length was an
  implementation choice not fixed by the data model — no longer true. They now cite the `Name` tier
  and say *"do not pick a length here; pick a tier."* No behaviour changed: 200 was already the
  value, which is why the `Name` tier was set to 200.
- **No migration was created and story 02 was not started.**

### 2026-08-26 (later) — story 03 data layer

- **Story 03 tasks 1–2 implemented** — `Department` and `Branch` domain entities plus their EF
  configuration and `DbSet`s. This is the slice S9-12 names as Story 02's prerequisite: `POST /users`
  needs a `departmentId` that already exists. Nothing else of story 03 was built, and no story 02
  code was written.
- **The `InitialSchema` migration was deliberately *not* generated, and story 03's
  `OrganizationSeeder` was deliberately *not* written.** Both plans place the migration after story
  02 task 3 so that one migration creates all four tables (`Departments`, `Branches`, `Users`,
  `AuditEntries`), and
  [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §6 lists no separate
  Organization or Identity migration. The seeder follows the migration because it queries
  `Departments`: registering it against a database with no tables would crash the API at startup and
  regress story 01. **This is a sequencing consequence of the approved plans, not a deviation** —
  see §10 item 1.
- **Schema verified without committing a migration.** A throwaway migration was generated, its
  `Up()` read — `Departments` and `Branches`, `nvarchar(200)` unique names, nullable
  `ManagerUserId` **with no foreign key** — and then reverted, leaving the tree migration-free.
- **`Department.ManagerUserId` carries no foreign key**, per data-model §2.2: the real rule (exists,
  active, role `Manager` or `Administrator`) is cross-row and conditional, which an FK cannot
  express, and a second FK would create a create-order cycle with `User.DepartmentId`. Reasoning is
  in `DepartmentConfiguration`, at the point a reader would ask.
- **Implementation choice, flagged because no document fixes it:** entity name columns are
  `nvarchar(200)`. **No approved document states a string length anywhere**, and SQL Server cannot
  build a unique index over `nvarchar(max)`, so a bound was unavoidable. If a length convention is
  wanted, it belongs in [data-model.md](data-model.md) §6 and should be applied consistently before
  story 04 adds `Customer`.
- **Added `backend/dotnet-tools.json`** pinning `dotnet-ef` **10.0.11** as a local tool. The tool was
  not installed, so the plans' own `dotnet ef migrations add` command had nothing to run. A pinned
  local manifest keeps it reproducible for a fresh clone.
- **OQ-3 remains open and unanswered.** No fallback escalation recipient exists in the code. The
  `Department.ManagerUserId` doc comment states the gap and names story 09 as the deadline.

### 2026-08-26

- **Stage 10 begins — story 01 (`solution-skeleton`) implemented and verified.** First code in the
  repository. The five-project backend solution, the Angular + PrimeNG front end, and the
  three-service Compose stack all exist and run. Two endpoints only — `GET /api/v1/health` and
  `GET /api/v1/config/bootstrap` — and **no business entity, screen or endpoint beyond them**.
  Evidence for every claim is in §8.
- **Front end is built on the PrimeNG Sakai template, tag `20.0.0` (MIT), not scaffolded from
  scratch.** This **supersedes task 8 of the story-01 plan**, which said `ng new frontend`. Taken on
  explicit user instruction. The template was stripped of its demo pages and services; its layout
  chrome (topbar, sidebar, footer, configurator) was kept and re-pointed at runtime branding, and
  the folder tree of [architecture.md](architecture.md) §2.2 was added alongside it.
- **Angular and PrimeNG are pinned to Sakai 20.0.0's own lockfile resolutions** — Angular
  **20.1.2**, PrimeNG **20.0.0**, `@primeuix/themes` **1.2.1** — rather than the looser
  "Angular 20 / PrimeNG 20" of
  [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §6. Exact pins, so
  `npm ci` is reproducible and no dependency drifts within `^20` on a later install. Sakai master
  (21.0.0, Angular 21) was **considered and rejected**: it would have contradicted the approved
  plan, and the user chose the tag that matches it.
- **Story 17 Part A delivered here**, per the split-in-time exception in
  [story-backlog.md](story-backlog.md): Transloco (AD-9), `en`/`ar` dictionaries with a
  programmatically checked-identical key set, `DirectionService` (document `dir`/`lang`, PrimeNG
  locale, choice persisted to browser storage), the language switcher in all three shells, the
  `property-disallowed-list` stylelint rule, and the three breakpoint mixins.
- **Contract correction — lowercase OpenAPI paths.** The default `[controller]` token published
  `/api/v1/Health` and `/api/v1/Config/bootstrap`, which do not match
  [api-design.md](api-design.md) §5.1. Fixed with a route-token transformer
  (`Api/Routing/SlugifyParameterTransformer.cs`) rather than `RouteOptions.LowercaseUrls`, because
  that setting only affects generated links — the route template, and therefore the published
  document, would have kept the class name's casing. Every future controller inherits the
  behaviour.
- **Bug found and fixed during UI verification.** The `en`/`ar` dictionaries used `health.database`
  as both a label and a namespace prefix, so `health.database.reachable` rendered as a raw key. A
  key cannot be a string and an object. Split into `health.databaseLabel` and `health.db.*`; a
  key-parity check across both dictionaries now runs whenever they are edited.
- **Four further deviations from the plan, all deliberate and recorded** in
  [.squad/plans/platform-foundation/00-overview.md](../.squad/plans/platform-foundation/00-overview.md):
  the stylelint logical-property rule is scoped to project-authored stylesheets with the vendored
  Sakai stylesheets ignored (they are upstream code this project does not edit);
  `frontend/proxy.conf.json` was added so `ng serve` shares an origin with the API, with CORS kept
  as the plan specified; the temporary health screen sits in `features/platform/`, to be removed
  with story 02's role redirect; and `.dockerignore` was added because host `bin/`/`obj/` were
  overwriting each image's own restore.
- **No migration was generated, by design.** Story 01 introduces no entity, so there is nothing to
  migrate; the first migration belongs to story 03. The schema will only ever be created and
  changed by **EF Core migrations** generated from [data-model.md](data-model.md) — never by
  hand-written SQL.
- **Recalculated §1.1**: delivery moves from 0 of 18 to **1 of 18**, so overall progress goes
  **35% → 38.6%**. One enabling story that carries no business behaviour is worth exactly one
  eighteenth of the delivery track and no more.
- **Stopped at the story-01 boundary.** The plan's own closing instruction and the user's standing
  constraint both require explicit approval before story 02.

### 2026-08-25

- **Stage 9 complete — the implementation plans exist.** Eighteen `NN-story-*.md` plan files
  generated into `.squad/plans/<feature>/`, one per story, in the backlog's execution order, so the
  generated `NN` prefixes match the intended sequence exactly. Each plan follows squad-kit's story
  document pattern — `Prerequisites`, `Story Goal`, `Context — Read These Files First`,
  `Product rules (from story)`, `Backend Tasks` / `Frontend Tasks`, `Verification Steps`,
  `Done Criteria` — with concrete file paths, type names, signatures and runnable commands.
  `squad status`: **18 stories, 18 plan files, next `NN` 19**; `squad doctor`:
  **6 ok · 0 warn · 0 fail · 7 skip**.
- **Added `.squad/plans/00-implementation-plan.md`** — the programme-level artifact squad-kit does
  not model: 14 workstreams, 9 phases, the dependency graph, what may run in parallel and what may
  not (**EF Core migrations are the serialization constraint** — one `DbContext`, one migration
  chain), the conventions all eighteen plans share, the audit findings, and full traceability from
  each plan back to stories, endpoints, entities and screens.
- **Filled all 14 `00-overview.md` stubs** and rewrote `.squad/plans/00-index.md`.
- **Ran a full traceability and consistency audit** across every approved document and all 18
  intakes. **13 findings, S9-1…S9-13** (§6.7). **Four block a named acceptance criterion:**
  - **S9-1** — the dashboard task region assumed by `ui-design.md` §5.1/§13, story 14's AC and
    `data-model.md` §6's index has **no endpoint** in `api-design.md`. **Contradiction.**
  - **S9-4** — story 11's AC requires AI suggestion acceptance/override to reach ticket history,
    and **no request field or endpoint exists to carry it**. **Contradiction.**
  - **S9-9 / PF-4** — `api-design.md` §9 required PF-4 to be pinned *before* story 15 was planned;
    it was not.
  - **S9-10 / PF-2** — the inbound channel adapter has **no actor**; the finding comes due in
    story 18.
  **None was resolved by invention.** Each is isolated to a single named method or region, which
  throws or stays empty until the decision is recorded.
- **Nine non-blocking findings resolved from the approved documents and recorded** — S9-2, S9-3,
  S9-5, S9-6, S9-7, S9-8, S9-11, S9-12, S9-13 (§6.7). Three of them corrected statements elsewhere
  in this tracker and in `story-backlog.md`: OQ-2 and OQ-3 reach one story earlier than recorded
  (S9-3); story 08 does **not** depend on story 09 (S9-8); story 16's configuration half is
  consumed by stories 04 and 13 as well (recorded in `story-backlog.md`).
- **No open question was answered and no business decision was changed.** OQ-1, OQ-2, OQ-3, OQ-5,
  F-1, PF-2, PF-4, PF-5 and N-5 all stand exactly as they did before stage 9.
- **`sdd-workflow.md` updated** — stage 9 marked complete, gate 9 → 10 restated as a per-story gate
  and its programme-plan artifact recorded. **`story-backlog.md` updated** — plan and phase columns,
  the execution-order refinements, the S9-8 correction to the cut order, and the story-state table.
- **Overall progress 31% → 35%.** The design-and-planning track is now fully consumed; every
  remaining point is delivery.
- **Closed N-1 — the API payload catalogue.** A focused Stage 7 refinement, not Stage 9.
  `api-design.md` §6 grew from 5 payload shapes to **29 across 12 subsections**: identity and
  access, organization, customers, tickets, knowledge base, notifications, attachments, reporting,
  administration and configuration, AI assists, the three previously unstated request bodies
  (`POST /auth/login`, `POST /kb/articles`, `PATCH /kb/articles/{id}`), and Problem Details.
  Every field traces to `data-model.md`; display-name projections are labelled as projections.
  Three fields are stated as never appearing in any response: `passwordHash`, `storagePath`, and
  raw actor ids where a display name suffices.
  **A genuine contradiction surfaced and was fixed (F-2):** AP-13 promised an authorized attachment
  download endpoint that the catalogue never defined, while story 04 requires "downloaded again".
  `GET /attachments/{attachmentId}/content` was added as **AP-19** — one endpoint for every role,
  the single deliberate exception to AP-5's portal split, because a byte stream has no DTO to vary
  and the authorization question is identical. Endpoint count **65 → 66**.
  **No open question was resolved:** the `ratingScale` values in the config example are labelled
  illustrative placeholders (OQ-1), `assignedCount` stays unqualified (PF-4), `firstRespondedAt` is
  documented as possibly null on a resolved ticket (PF-5).
  **F-1 re-checked and deliberately left open** — see §6.6.
  *Files:* `docs/api-design.md`, `docs/PROJECT-PROGRESS.md`.
- **Completed Stage 8 — UI Design.** `docs/ui-design.md`: **24 screens** across four surfaces —
  2 auth, 8 workspace, 7 admin, 4 portal, 3 status — with a route tree, three shells, a shared
  component inventory, and empty/loading/error conventions. Every screen carries its route, roles,
  responsibilities and **the API endpoints it consumes**; all of them verified to exist in
  `api-design.md` (the two endpoints removed by AP-18 appear nowhere). Twelve UI decisions
  (UI-1…UI-12) recorded with rationale.
  **Lifecycle rules honoured visibly:** the transition menu offers only what A-5 and A-16 allow;
  assignment does not change the status chip (A-18); replying to a `Pending` request reopens it
  automatically from the response (R-13) and the activity region shows the **customer** as the
  actor (R-14); portal cancel is offered only while `New`, which A-18 makes a real window.
  RTL (§10.2) and phone-width behaviour for the three T3-F surfaces (§10.3) are specified.
  **Open questions were not resolved:** §11 marks seven dependencies — OQ-1 (the feedback control's
  shape), OQ-3 (what the escalate dialog may claim), OQ-5 (the email field), PF-4, PF-5, N-1, and
  OQ-2 which turns out to have no UI dependency at all.
  **One finding, F-1 (non-blocking):** the ticket payload exposes no `allowedTransitions`, so the
  menu duplicates the authority matrix client-side. Reported, not applied.
  Gate 8 -> 9 met.
  *Files:* `docs/ui-design.md` (new), `docs/sdd-workflow.md`, `docs/PROJECT-PROGRESS.md`.
- **Stage 7 post-flight review, and the corrections it required.** Reviewed `api-design.md` against
  every authoritative source. **Two blocking defects found and closed:**
  **B-1** — the feedback contract depended on a `Feedback:RatingScale` configuration key that
  architecture §6.3 did not have, so the approved contract was not implementable from the approved
  documents. The key is now an approved entry **with its values deliberately undecided**, because
  OQ-1 is still open and the scale must not be invented.
  **B-2** — `GET /config` handed **quick replies and SLA targets to every authenticated caller,
  including Customers**. Configuration is now split into three audience tiers — public,
  customer-safe, staff-only — recorded as AP-17, with `403` for a customer reaching the staff tier.
  **Three non-blocking items also closed:** the unsupported email-immutability rule removed (N-2,
  replaced with the uniqueness validation the model actually supports), two endpoints that traced to
  no requirement removed (N-3, AP-18), and `hasFeedback` declared as a derived field (N-4).
  **One new open question:** **OQ-5** — whether changing a customer's email also changes a linked
  portal login's sign-in email. Raised rather than invented; blocks story 04 only.
  **Deferred by decision:** N-1, the eleven undefined response shapes, tracked for completion
  **before Stage 9**; N-5 recorded without change because fixing it would pre-empt OQ-1.
  Endpoint count 66 → 65. No new contradiction introduced.
  *Files:* `docs/architecture.md` §6.3, `docs/api-design.md`, `docs/PROJECT-PROGRESS.md`.

### 2026-08-24

- **Completed Stage 7 — API Design.** `docs/api-design.md`: 66 endpoints across 12 modules,
  covering every T1/T2 story. Role gate stated on every endpoint; department scoping expressed as
  `404` rather than `403` so the boundary does not leak existence (AP-4); a separate `/portal` path
  space for the Customer role (AP-5); lifecycle changes as an action endpoint carrying the target
  status, with A-5 legality and A-16 authority producing distinct `409` and `403` (AP-6); the
  automatic `Pending -> Open` on customer reply, with R-14 attribution, as the one status side
  effect in the API (§5.7); thirteen classes of server-derived field excluded from request models
  (§7). Sixteen technical decisions recorded as AP-1…AP-16 with rationale and rejected alternative.
  **No business rule was invented.** Gate 7 -> 8 met.
  *Notable:* PF-2 is **avoided** rather than solved — AP-11 publishes no inbound-channel endpoint,
  so no contract needs a system actor the model cannot express; the gap remains for story 18.
  PF-6 and PF-7 are **closed** by the contract. PF-3 is handled but **requires a
  `Feedback:RatingScale` key that architecture §6.3 does not have — raised for approval, not
  applied**. PF-4's metric semantics remain undecided by design.
  *Files:* `docs/api-design.md` (new), `docs/sdd-workflow.md`, `docs/PROJECT-PROGRESS.md`.
- **Approved the actor attribution for the automatic `Pending -> Open` transition.** When PF-1 was
  propagated, the transition had no invoking user while A-5 requires every transition to carry an
  actor; the customer was chosen and **flagged as a derived detail rather than applied silently**.
  Now approved and recorded as a rule: the **replying customer** is the actor, `actorKind = User`,
  never `System`. The SLA monitor remains the only system actor. The wording in the data model was
  changed from reasoning ("since their action caused it") to an approved rule citing A-5, and the
  `actorKind` field note now states the boundary where the enum is defined.
  **No new entity or field** — `TicketActivity` already carries `actorUserId` and `actorKind`.
  *Files:* `docs/product-scope.md` (A-5), `docs/data-model.md` (§2.6, §2.7, §2.8, §5),
  `.squad/stories/ticket-management/ticket-lifecycle/intake.md` (new acceptance criterion),
  `docs/PROJECT-PROGRESS.md`.
- **Resolved PF-1 — a customer reply reopens a `Pending` ticket.** The Stage 7 pre-flight found
  that `Pending -> Open` appeared in no source, while `Pending` means "awaiting customer input",
  A-13 defines a `CustomerReplied` notification, and A-3 keeps the SLA clock running — so a replied-to
  ticket could not leave `Pending`. **Decision: the transition is legal and the customer's reply
  triggers it automatically**, in the same transaction as the message, from `Pending` only.
  Two contracts were blocked and are now designable: the ticket transition endpoint's legal-transition
  table, and the post-message endpoint's status side effect.
  *Consistency re-checked after propagation:* transition graph, A-16 authority matrix (the customer
  still cannot invoke `-> Open` directly), notification behaviour (no new type — A-13's four stand,
  and the transition raises none of its own), SLA behaviour (unchanged; `Pending` never paused the
  clock), and a search for the old three-arrow transition set, which no longer appears anywhere.
  *Derived detail, flagged not invented:* A-5 requires every transition to carry an actor, so the
  automatic `StatusChanged` row is attributed to the **replying customer**. **Approved as R-14 the
  same day** and recorded as a rule.
  *Files:* `docs/product-scope.md` (A-5 graph and `Pending` bullet, A-16 `-> Open` row),
  `docs/data-model.md` (§2.6 invariant 2b, §2.8 invariants, §5 constraint 9a), three story intakes
  (`ticket-lifecycle`, `ticket-intake-messaging`, `portal-self-service`), `docs/PROJECT-PROGRESS.md`.
- **Ran the Stage 7 pre-flight audit.** Re-read every authoritative source and checked traceability
  end to end, all 18 stories against the architecture and data model, and the fifteen named subject
  areas. Verdict at the time: 🔴 NOT READY on one blocking finding (PF-1, above). Six non-blocking
  findings recorded in §6.5; three cosmetic ones noted. All numbering, stage numbering, module
  ownership, active-vs-resolved question state and story blockers verified consistent.
  *Files:* none — the audit modified nothing.
- **Resolved the `CustomerFeedback` module-ownership mismatch.** The data model labelled the entity
  as owned by a "Portal" module, which does not exist — [architecture.md](architecture.md) §1
  defines ten backend modules and `customer-portal` is a front-end area. **Decision: the existing
  `Tickets` module owns it**, because feedback is domain behaviour attached to a ticket and is
  offered when the ticket reaches `Resolved`. **No new module was created and the ten-module
  architecture is unchanged**; the entity's shape, fields, relationships and invariants are
  untouched. Recorded in the data model as **DM-7** so the reasoning travels with the label.
  While there, one adjacent looseness was fixed for consistency: `Notification` was labelled "SLA"
  and "SLA/notifications module" and now reads `Sla`, matching the module list.
  *Why:* found during the §1 wording correction and reported as needing a decision before the
  Stage 7 pre-flight, since the feedback contract depends on it.
  *Files:* `docs/data-model.md` (**new DM-7**, §1 preamble, §2.12, §2.15, §3),
  `docs/architecture.md` §1 (`customer-portal` row), `docs/PROJECT-PROGRESS.md`.
  *Not changed:* `sdd-workflow.md` — its inventory counts the A-\* assumptions, and this is a
  DM-level modelling decision, so the range A-1…A-18 is still correct.
- **Corrected loose wording in `architecture.md` §1** about the relationship between feature slugs
  and backend modules. The text claimed the ten modules matched "the feature slugs already in
  story-backlog.md"; there are **fourteen** slugs. The section now maps the ten slugs that do have
  a backend module, names the four that do not — `platform-foundation`, `agent-workspace`,
  `customer-portal`, `platform-experience` — as front-end areas or cross-cutting platform
  concerns, and states that a feature slug is a unit of planning while a module is a unit of code
  organization.
  *Why:* found by the tracker audit and reported as inaccurate but unfixed; corrected now on
  request. **Documentation wording only — no architecture, module boundary, business decision or
  data-model change.**
  *Files:* `docs/architecture.md` §1, `docs/PROJECT-PROGRESS.md`.
- **Audited this tracker against every project document** ahead of the Stage 7 pre-flight.
  Checked the twelve things it must track, then cross-read `product-scope.md`,
  `architecture.md`, `data-model.md`, `sdd-workflow.md`, all 18 intakes and the repository state.
  **Six discrepancies found, all in this file, all fixed:** the plan-overview stub count was 13 and
  is 14 (14 feature slugs, not the 11 previously reported); line counts cited for
  `architecture.md` and `data-model.md` had gone stale and were removed rather than re-pinned;
  the working-tree row still claimed 5 commits, clean, nothing pushed, when there are 6 commits,
  all pushed, with 9 files uncommitted; a historical change-log entry repeated the feature
  miscount; §5 read as though it were the complete decision inventory when the A-* business
  decisions live in §6.3; and the decision → document → story traceability required of this
  tracker did not exist, so §6.4 was added.
  **No stale status, missing decision, missing open question, mis-stated blocker, or unsupported
  claim was found** — the stage numbering matches `sdd-workflow.md`, the three active open
  questions match `data-model.md` §8, and every story blocked by an open decision is marked.
  *Why:* the tracker is the single progress source of truth and had drifted on facts about itself.
  *Files:* `docs/PROJECT-PROGRESS.md` only. No project document was modified.
- **Corrected the cancel authority and resolved OQ-4.** Agents and Managers may now cancel
  (Administrators were already unrestricted); customers may cancel their own ticket only while it
  is `New`. OQ-4 answered by **A-18**: automatic assignment does not mean work has started, a
  ticket may be assigned while still `New`, and `New → Open` is the agent starting work.
  *Why:* the first version of A-16 withheld cancel from agents and managers, and the cancellation
  window it described could have been zero because auto-assignment runs at creation.
  *Contradiction found and fixed:* A-5 and the `ticket-lifecycle` intake both defined `New` as
  "created, **unassigned**" and `Open` as "assigned and being worked" — directly incompatible with
  A-18. Both were rewritten, and the data model's `assignedUserId` note ("Null while `New`") with
  them; status and assignee are now explicitly independent.
  *Files:* `docs/product-scope.md` (A-5, A-16, **A-18**, §9), `docs/data-model.md` (§2.6 field and
  invariants 2a/11b, OQ register), `docs/architecture.md` and `docs/sdd-workflow.md` (assumption
  range), four story intakes (`ticket-lifecycle`, `ticket-core`, `sla-routing-escalation`,
  `portal-self-service`), `docs/PROJECT-PROGRESS.md`.
- **Answered four blocking business decisions ahead of Stage 7** and recorded them as assumptions
  **A-14…A-17** in the product scope: category→department routing, self-registration branch and
  profile-linking, the ticket-transition authority matrix with manual-only closure, and the
  `isUrgent` customer input. Consequences propagated: two configuration keys added to the
  architecture (category→department map, default branch), `Ticket.isUrgent` added to the data
  model, and `Customer`/DM-1 amended for the linking rule.
  *Why:* a pre-Stage-7 review found four questions that no source answered and that each changed
  an API contract; none was in the open-question register.
  *Files:* `docs/product-scope.md`, `docs/architecture.md`, `docs/data-model.md`,
  `docs/sdd-workflow.md`, `docs/PROJECT-PROGRESS.md`.
- **Opened OQ-4** — the boundary of "before work begins" for customer cancellation. Auto-assignment
  at creation may leave a zero-length cancellation window, so A-16's rule needs one more
  clarification before the portal cancellation contract can be written.
  *Files:* `docs/product-scope.md` (§9 question 8), `docs/data-model.md` (§8 register).
- **Reviewed all design documents for contract-blocking ambiguities** before starting Stage 7.
  Found four blockers (recorded above), one partial (OQ-1, deferrable through configuration) and
  eight non-blocking items with the technique for designing around each. Established that OQ-2 and
  OQ-3 do not affect any contract shape.
  *Files:* none — analysis only.
- **Created this progress tracker.** No other file modified.
  *Files:* `docs/PROJECT-PROGRESS.md` (new).
- **Clarified four points in the data model** after review, without changing the model.
  Branch derivation `Ticket → Customer → Branch` stated explicitly with a full audit of the
  sources confirming no requirement asks for a ticket-level branch; the CSAT 1–5 assumption
  **withdrawn** so no range is encoded (reopened as OQ-1); SLA due-date recomputation
  **un-decided**, with both readings documented (OQ-2); missing-department-manager behaviour
  documented as unresolved with **no fallback invented** (OQ-3). Open items became a register
  naming the story each one blocks.
  *Why:* three assumptions were hardening into implementation constraints the requirements do not
  support. *Files:* `docs/data-model.md`. *Commit:* `a149b01`.
- **Completed Stage 6 — Data Model.** 15 entities, six ownership decisions (DM-1…DM-6) resolved
  before modelling, 14 candidate entities explicitly not modelled. Gate 6 → 7 met.
  *Files:* `docs/data-model.md` (new), `docs/sdd-workflow.md`. *Commit:* `a149b01`.
- **Corrected the JWT authorization design.** The token had carried role and department, which go
  stale when an Administrator moves, demotes or deactivates a user — a silent, fail-open
  confidentiality defect. The token now asserts identity only; role, department and active status
  are resolved per request from the authoritative user record. AD-7 amended, AD-15 added,
  §4.1.1 written. Gate 5 → 6 re-verified.
  *Why:* raised in review before Stage 6 began. *Files:* `docs/architecture.md`. *Commit:* `e51e1aa`.
- **Completed Stage 5 — Architecture.** Layered modular monolith, front-end structure, three
  enforcement points, department scoping mechanism, three integration seams, configuration
  strategy, Compose runtime, 14 decisions. Also corrected an off-by-one in the stage references
  inside all 18 intakes.
  *Files:* `docs/architecture.md` (new), `docs/sdd-workflow.md`, 18 intakes. *Commit:* `bbcff22`.
- **Completed Stages 2–4 — Product Scope, Story Backlog, Squad Kit.** Tiered scope with 13
  assumptions and 7 open questions; squad-kit v0.2.0 initialized; 18 story intakes across 14
  feature slugs; SDD workflow and backlog documents written.
  *Files:* `docs/product-scope.md`, `docs/sdd-workflow.md`, `docs/story-backlog.md`, `.squad/**`.
  *Commit:* `ba89afd`.
- **Stage 1 — Requirements received** and analysed (functional and non-functional requirements,
  actors, workflows, ambiguities, assumptions). Analysis delivered in conversation and distilled
  into the product scope. *Files:* `docs/requirements.md`. *Commit:* `c08899c`.
