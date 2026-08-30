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

### 2026-08-28 (latest) — story 04 slice 5: customer self-registration

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
