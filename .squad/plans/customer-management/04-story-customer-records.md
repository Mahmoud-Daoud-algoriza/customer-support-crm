# Story 04 — Customer profiles, notes, attachments and interaction history

> **Source of truth:** `docs/requirements.md` §1 · `docs/product-scope.md` T1-A, T2-A, A-10, A-15 · `docs/architecture.md` §2.5, §4.4, §5.3, §6.3 · `docs/data-model.md` §2.4, §2.5, §2.11, DM-1, DM-6, §5 constraints 1, 16, 20, §6 · `docs/api-design.md` §5.2, §5.5, §6.3, §6.7, AP-13, AP-19 · `docs/ui-design.md` §5.4, §5.5
> **Intake:** `.squad/stories/customer-management/customer-records/intake.md` · **Tier:** T1 (profiles, notes, timeline) + T2 (attachments)
> **Phase:** 2 — Configuration and Customers.

## Prerequisites

- **Story 01 completed:** skeleton, seeder pipeline, Problem Details, attachment-free file layout.
- **Story 02 completed:** `ICurrentUser`, role policies, `IAuditRecorder`, `User` entity.
- **Story 03 completed:** `Branch` — `Customer.branchId` is **required** (data-model §2.4).
- **Story 16 Part A completed** for the `Registration:DefaultBranchId` key that A-15 requires and
  the attachment size cap. See
  [16-story-audit-configuration.md](../administration/16-story-audit-configuration.md) §"Part A".

> ### ⚠ Blocked decision — **OQ-5** must be answered before task 5 is implemented
>
> `PATCH /customers/{id}` may change `Customer.email`. A customer profile may have a **linked
> portal login** whose `User.email` is also unique and is the sign-in identifier (DM-1, A-15).
> **No approved source says whether changing one changes the other** (api-design §5.5, OQ-5).
>
> **Do not invent either behaviour.** Task 5 delivers the patch of the *customer profile* and its
> `409 customer-email-in-use` validation, which **are** supported by the model. The consequence for
> a linked `User.email` is the open decision: obtain the answer, then implement it as a one-line
> branch in `CustomerService.UpdateAsync`. Until then the linked login is left untouched and the UI
> **makes no claim either way** (ui-design §11).

---

## Story Goal

Deliver requirements §1 in full, at the depth of T1-A and T2-A.

1. Create, read, update, list and search customer profiles with name, email, phone and branch.
2. **Interaction timeline** — a per-customer chronological **read projection** over that customer's
   tickets and ticket activity. Not a stored log (architecture §2.5).
3. **Notes** on a customer — authored, timestamped, and **immutable once written**.
4. **Attachments** — single-file upload to local disk, size-capped, owned by a customer **or** a
   ticket, downloadable through an authorized endpoint that never exposes a path.
5. **`POST /auth/register`** — deferred here from Story 02, because it is the first point at which
   a `Customer`, a `Branch` and the configured default branch all exist (A-15).

---

## Context — Read These Files First

1. `docs/data-model.md` §2.4 `Customer` (note `externalReference` — the ERP seam field, DM-6,
   **read-only and never settable**), §2.5 `CustomerNote` (**immutable once written**), §2.11
   `Attachment` (the **owner XOR** rule and the size cap), §5 constraints 1, 16, 20, §6 indexes
   `Customer(email)` unique and `Customer(branchId)`.
2. `docs/api-design.md` §5.5 — all ten endpoints, the timeline's exclusion rule, and the **`email`
   is patchable** correction with the OQ-5 box. Then §6.3 payloads (`Customer`,
   `CustomerListItem` with `openTicketCount`, `CustomerNote`, `TimelineEntry`), §6.7
   `AttachmentMetadata`, AP-13 and **AP-19** (one download endpoint for every role).
3. `docs/api-design.md` §5.2 — the **three** A-15 outcomes of `POST /auth/register` and the
   `409 user-already-exists` rule (PF-6).
4. `docs/architecture.md` §2.5 (the timeline is a read projection assembled on read, **not** a
   second store; internal entries excluded in the Application layer, once), §4.4 (download passes
   the owner's authorization path; **no guessable file URL**), §5.3 (ERP seam).
5. `docs/ui-design.md` §5.4 (customer directory — branch **is** a filter here) and §5.5 (customer
   detail regions; **notes offer no edit control**; the email field with `409` surfaced inline).
6. `.squad/stories/customer-management/customer-records/intake.md` — acceptance criteria and the
   Out of scope list (no virus scanning, no cloud storage, no previews, no versioning, no merge or
   dedupe).

---

## Product rules (from story)

- **A customer is identified by an email address unique within the organization** (A-10). A
  duplicate is **rejected**, never reconciled — there is no merge or dedupe tooling.
- `Customer.branchId` is **required**. A self-registering customer receives the **configured
  default branch** and is never asked to choose one (A-15).
- **A note is immutable once written.** No edit path, no delete path, no UI control — the intake
  requires notes to be "not silently editable by other users" and immutability is the cheapest
  guarantee.
- **An attachment has exactly one owner** — a ticket **or** a customer, never both, never neither.
- **`storagePath` is never returned by any endpoint**, ever (api-design §6). The bytes come only
  from `GET /attachments/{attachmentId}/content`.
- **Deleting a customer is not an application operation** (data-model §2.4).

---

## Backend Tasks

### 1 — Domain entities

**Create file: `src/SupportCrm.Domain/Modules/Customers/Customer.cs`** — fields exactly
data-model §2.4: `FullName`, `Email`, `Phone?`, `BranchId`, `ExternalReference?`, `CreatedAt`.

`ExternalReference` has a **private setter and no public mutator**: DM-6 makes it the ERP seam's
single persisted field, unused by default and **not settable through any endpoint**
(api-design §8.3).

**Create file: `src/SupportCrm.Domain/Modules/Customers/CustomerNote.cs`** — `CustomerId`,
`AuthorUserId`, `Body`, `CreatedAt`. **All properties have private setters and there is no mutator
method at all** — immutability is structural.

**Create file: `src/SupportCrm.Domain/Modules/Customers/Attachment.cs`** — `TicketId?`,
`CustomerId?`, `FileName`, `ContentType`, `SizeBytes`, `StoragePath`, `UploadedByUserId`,
`UploadedAt`. Two factories, `ForCustomer(...)` and `ForTicket(...)`, each setting exactly one owner
— **the XOR rule cannot be violated because no constructor allows it**.

> `Attachment` lives in the `Customers` domain module but is used by both `Customers` and `Tickets`
> (data-model §3 lists its module as "Customers/Tickets"). Keep the type in `Modules/Customers` and
> let the `Tickets` Application service call the shared attachment service; do not create a second
> attachment type.

### 2 — Infrastructure: EF configuration, storage, migration

**Create files:** `Persistence/Configurations/CustomerConfiguration.cs`,
`CustomerNoteConfiguration.cs`, `AttachmentConfiguration.cs`.

- `Customer.Email` — unique index with a case-insensitive collation (data-model §5 constraint 1).
- `Customer.BranchId` — index (branch filtering in reports, T2-K/T2-G) and a `Restrict` FK.
- `Attachment` — a **check constraint** asserting exactly one of `TicketId` / `CustomerId` is
  non-null. The `TicketId` FK is added by Story 05's migration; configure the column now and the
  relationship there.
- `User.CustomerId` FK to `Customer` — completes the DM-1 link Story 02 left open.

**Create file: `src/SupportCrm.Application/Abstractions/IAttachmentStorage.cs`**

```csharp
public interface IAttachmentStorage
{
    Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct);  // returns storagePath
    Task<Stream> OpenAsync(string storagePath, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Storage/LocalDiskAttachmentStorage.cs`** — writes
under a configured root with a **server-generated** file name (never the client's), so a crafted
`fileName` cannot escape the root. The original name is kept in the `FileName` column for download.
**No cloud object storage, no virus scanning, no previews, no versioning** (T2-A).

```bash
dotnet ef migrations add Customers -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Application: customer service

**Create file: `src/SupportCrm.Application/Modules/Customers/CustomerService.cs`**

| Method | Endpoint | Notes |
|---|---|---|
| `ListAsync` | `GET /customers` | Paged. Filters `q` (name/email), `branchId`. Sort whitelist: `fullName`, `createdAt`; anything else -> `400` (AP-15) |
| `CreateAsync` | `POST /customers` | `{ fullName, email, phone?, branchId }`. Duplicate email -> `ConflictException("customer-email-in-use")` |
| `GetAsync` | `GET /customers/{id}` | |
| `UpdateAsync` | `PATCH /customers/{id}` | `fullName`, `phone`, `branchId`, **and `email`** — see the OQ-5 box above |

**`CustomerListItem.openTicketCount`** is an aggregate over that customer's **non-terminal** tickets
(api-design §6.3). `Ticket` does not exist yet: implement the projection with a
`// Story 05: replace the constant 0 with the ticket subquery` marker returning `0`, and
[05-story-ticket-core.md](../ticket-management/05-story-ticket-core.md) task 6 replaces it. **Compute
it in one grouped subquery, not one query per row.**

All four methods are `RequireAgent`. **A Customer cannot browse the customer directory** — the role
gate is the whole of that rule, and it is tested.

### 4 — Application: notes

**Create file: `src/SupportCrm.Application/Modules/Customers/CustomerNoteService.cs`** — serving
`GET /customers/{id}/notes` (paged, newest first) and
`POST /customers/{id}/notes` (`{ body }`), both `RequireAgent`.

**Author and timestamp are server-set from `ICurrentUser`** and are never accepted from the client
(api-design §7). There is **no update and no delete method** — not merely no endpoint.

Response shape `{ id, author: { id, displayName }, body, createdAt }`. **No `updatedAt` exists**,
because the entity is immutable (api-design §6.3).

### 5 — Application: the interaction timeline

**Create file: `src/SupportCrm.Application/Modules/Customers/CustomerTimelineService.cs`**

A **read projection** over the customer's tickets and their `TicketActivity`, newest first
(architecture §2.5, requirements §1.3). It is assembled on read and **never stored**.

```csharp
// TimelineEntry — api-design §6.3
{ occurredAt, ticketId, ticketSubject, activityType, actorKind, actor?, oldValue?, newValue? }
```

**Two exclusions are applied here, in the Application layer, once:**

1. **No entry whose `visibility` is `Internal`** is ever returned.
2. **`TicketInternalNote` is never touched by this query at all** — it is a different table, and
   the projection does not join it (data-model §2.9). A rendering bug therefore cannot leak one.

**Customer notes do not appear in the timeline** — they are a separate collection (api-design §5.5).

`Ticket` and `TicketActivity` do not exist yet. Write the service **against the empty set**: return
an empty page and a clean empty state, exactly as the intake authorizes
(*"build it against an empty ticket set and enrich when tickets land"*), with a
`// Story 06: join TicketActivity here` marker.
[06-story-ticket-lifecycle.md](../ticket-management/06-story-ticket-lifecycle.md) task 9 completes it
and enables the test.

### 6 — Application: attachments

**Create file: `src/SupportCrm.Application/Modules/Customers/AttachmentService.cs`**

- `UploadForCustomerAsync(customerId, IFormFile)` — validates against the configured cap
  (`Attachments:MaxSizeBytes`, Story 16 Part A); over the cap ->
  `PayloadTooLargeException("attachment-too-large")` -> **`413`**. Returns `AttachmentMetadata`.
- `ListForCustomerAsync(customerId)` -> `AttachmentMetadata` rows.
- `OpenForDownloadAsync(attachmentId)` — **AP-19, one endpoint for every role.** Loads the
  attachment, then **authorizes through its owner**: a customer-owned file requires
  `RequireAgent`; a ticket-owned file requires the ticket to be reachable by the caller through the
  Story 05 scoping helper. Unreachable **or** missing -> `NotFoundException` -> **`404`** (AP-4), so
  the endpoint never reveals which of the two it was.
- `UploadForTicketAsync` / `ListForTicketAsync` — **the method bodies are written here; the two
  `/tickets/{id}/attachments` endpoints are published by Story 05**, because `Ticket` and its
  scoping helper arrive there. See the note below.

> **Planning note — where ticket attachments live.** This story's acceptance criterion says *"A file
> can be attached to a customer **and to a ticket**, and downloaded again."* `api-design.md` §10.1
> maps `/customers/*` and `/attachments/{id}/content` to this story but **assigns
> `GET`/`POST /tickets/{id}/attachments` to no story at all** — a traceability omission recorded as
> **S9-2** in `00-implementation-plan.md`. Resolution, taken from the intakes rather than invented:
> the shared service and the download endpoint are built here; the two ticket-scoped endpoints are
> published in Story 05, where the ticket and its scoping helper exist. This story's AC is
> therefore fully met at the end of Story 05, and its Done Criteria say so.

### 7 — `POST /auth/register` (deferred from Story 02)

**File: `src/SupportCrm.Application/Modules/Identity/AuthService.cs`** — add
`RegisterAsync(email, password, fullName, phone?)`. Request body carries **exactly those four
fields**; a branch, a role or a customer id in the body is a `400` (api-design §5.2, §7).

The **three A-15 outcomes, and only these three**:

| Situation | Result |
|---|---|
| No `Customer` and no `User` for the email | `201` — create the `Customer` with the **configured default branch** and its `User` |
| A `Customer` profile exists (agent-created), no `User` | `201` — create the `User` and **link it to the existing profile**. No duplicate customer |
| A `User` already exists for the email | `409` `type: user-already-exists` (**PF-6**) |

Role is **always** `Customer`; `departmentId` is **always** null; `branchId` is **always** the
configured default (A-15, DM-1). Add `User.CreateCustomerUser(...)` to the domain factory now.

Both success paths run in **one transaction** and write an audit entry.

### 8 — Api: controllers

**Create file: `src/SupportCrm.Api/Controllers/CustomersController.cs`** — the nine customer-scoped
endpoints of api-design §5.5, all `RequireAgent`.

**Create file: `src/SupportCrm.Api/Controllers/AttachmentsController.cs`** —
`GET /attachments/{attachmentId}/content`, **`[Authorize]` with no role policy** (AP-19: one
endpoint for every role; authorization is by owner reachability, not by role). Returns the file
stream with `Content-Type` and a `Content-Disposition` filename. **There is no JSON body and no
`storagePath` anywhere in the response.**

**File: `src/SupportCrm.Api/Controllers/AuthController.cs`** — add `POST /auth/register`
(**Anonymous**).

Uploads are `multipart/form-data` (AP-13); every other endpoint is `application/json`.

### 9 — Seed data

**Create file: `Persistence/Seeders/CustomerSeeder.cs` (`Order = 30`)** — at least four customers
spread **across both seeded branches**, at least two with a linked portal `User`, at least one with
a note, and one with a small attachment. The cross-branch spread is what makes Story 05's
"branch is not a boundary" test meaningful.

### 10 — Tests

**Create file: `tests/SupportCrm.Tests/Customers/CustomerAccessTests.cs`**

1. `GET /customers` as `Customer` -> `403`; as `Agent` -> `200`.
2. Duplicate email on `POST` and on `PATCH` -> `409 customer-email-in-use`.
3. `GET /customers/{id}/timeline` for a customer with no tickets -> `200` with an **empty page**,
   not an error (intake AC).
4. An oversized upload -> `413 attachment-too-large`.
5. `GET /attachments/{id}/content` for a customer-owned file as `Customer` -> `404` (**not `403`**,
   AP-4).
6. **No response body anywhere in this suite contains the string `storagePath`.**

**Create file: `tests/SupportCrm.Tests/Identity/RegistrationTests.cs`** — the three A-15 outcomes,
each asserted separately, including that the second creates **no second `Customer` row**.

---

## Frontend Tasks

### 11 — Typed clients

`core/api/customers.client.ts` — list, get, create, patch, timeline, notes, attachments;
`core/api/attachments.client.ts` — `downloadUrl(attachmentId)` returning the API path (the auth
interceptor supplies the bearer token; **never build a URL from a storage path**).

### 12 — Customer directory — `/workspace/customers` (ui-design §5.4)

`PagedTable` with name, email, phone, branch, **open-ticket count**. Filters `q` and **`branchId`**
— branch's legitimate reporting use. **No department filter here.** URL query parameters (UI-9).

### 13 — Customer detail — `/workspace/customers/:id` (ui-design §5.5)

Four regions, each with its own loading, empty and error state (ui-design §9):

- **Profile** — editable form (`fullName`, `phone`, `branchId`, `email`).
  `409 customer-email-in-use` renders **inline on the email field**. **The UI makes no claim about
  a linked portal login** — OQ-5 (ui-design §11). Do not add a warning, a tooltip or a confirmation
  that implies either behaviour.
- **Interaction timeline** — newest first; empty state *"No activity yet"*. It fills as Stories 05
  and 06 land.
- **Notes** — list plus an add form. **No edit and no delete control is rendered anywhere**, because
  none exists server-side.
- **Attachments** — `AttachmentList` + uploader from `shared/`; the configured cap is shown, and
  `413` surfaces inline on the uploader.

### 14 — Registration screen

`features/auth/register/` — enable the form Story 02 scaffolded. Fields: email, password, full name,
optional phone. **No branch selector and no role selector** — A-15 fixes both server-side.
`409 user-already-exists` renders as a translated message inviting the user to sign in.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Migration applies:** `docker compose up --build`; `Customers`, `CustomerNotes` and
   `Attachments` tables exist with the XOR check constraint.
3. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~Customers|FullyQualifiedName~Registration"`.
4. **Upload and download round-trip:** `POST /api/v1/customers/{id}/attachments` with a small file,
   then `GET /api/v1/attachments/{id}/content` returns the same bytes and the original file name.
5. **Registration:** register a brand-new email -> `201`; register an email that already has an
   agent-created `Customer` -> `201` with **the same `customerId`**; register it again -> `409`.
6. **Regression:** `GET /auth/me`, `GET /users` and `GET /departments` are unaffected.
7. **Frontend:** the directory filters by branch; the detail screen saves a profile, adds a note,
   uploads and downloads a file, and renders an empty timeline without an error.

---

## Done Criteria

- [ ] An agent can create, view, edit and list customers, and search and filter the list.
- [ ] Name, email, phone and branch are captured; **email uniqueness is enforced with a clear
      error** on both create and update.
- [ ] Opening a customer shows a chronological interaction timeline built from that customer's
      tickets and ticket activity, newest first *(renders empty until Story 06 lands; completed
      there)*.
- [ ] An agent can add a note showing author and timestamp; **no path edits or deletes one**.
- [ ] A file can be attached to a **customer** and downloaded again *(the **ticket** half is
      published by Story 05, task 8 — this AC completes there)*.
- [ ] Attachment size is capped and an oversized upload is rejected with a clear message (`413`).
- [ ] A Customer cannot browse the customer directory; an Agent can — proven by a server-side test.
- [ ] The timeline for a customer with no tickets renders an empty state rather than an error.
- [ ] `POST /auth/register` implements **exactly** the three A-15 outcomes.
- [ ] `storagePath` and `passwordHash` appear in no response.
- [ ] `externalReference` is returned read-only and is settable through no endpoint.
- [ ] **OQ-5 is not answered here.** No behaviour for a linked login's email was invented, and the
      UI claims nothing about it.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 05.**
