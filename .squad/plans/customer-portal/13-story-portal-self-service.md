# Story 13 — Customer portal self-service and feedback

> **Source of truth:** `docs/requirements.md` §8 · `docs/product-scope.md` T2-F, T3-F, A-5, **A-16**, A-17, **A-18**, R-13 · `docs/architecture.md` §1 (`customer-portal` is a front-end area), §2.2, §4.3, §4.4 · `docs/data-model.md` §2.15, **DM-7**, §5 constraint 21 · `docs/api-design.md` §5.7, §6.4, **AP-5**, **AP-16**, AP-4 · `docs/ui-design.md` §1, §4.2, §7, §11, **UI-11**
> **Intake:** `.squad/stories/customer-portal/portal-self-service/intake.md` · **Tier:** T2 — *"the portal is the second actor surface and carries much of the permission demonstration — cut it late among T2 items"*
> **Phase:** 6 — Knowledge and portal.

## Prerequisites

- **Story 02 completed:** the `Customer` role, guards, the portal shell stub.
- **Story 04 completed:** `Customer`, `POST /auth/register`, `AttachmentService`.
- **Story 05, 06 completed:** `Ticket`, `TicketScope`, `TicketLifecycleService` and the A-16
  authority matrix.
- **Story 07 completed:** `POST /portal/tickets`, the portal message endpoints and the
  `{ message, ticketStatus, statusChanged }` envelope. **This story builds the screens around
  them.**
- **Story 12 completed:** `GET /portal/kb/articles` for §8.4.

> ### ⚠ Blocked decision — **OQ-1** must be answered before task 4 and task 9 are implemented
>
> T2-F specifies *"a one-question satisfaction rating with an optional comment"* and **fixes no
> scale**. `data-model.md` §2.15 stores an ordinal and **encodes no range** — no minimum, maximum
> or step — and states that **none may be inferred into a validation rule, a check constraint, or a
> UI control**.
>
> The contract is already scale-agnostic: `GET /config` publishes `feedback.ratingScale`
> (`min`, `max`) from the **approved** `Feedback rating scale` configuration key
> (architecture §6.3), and the server validates against it, returning `400` outside the range.
> **The key's values are deliberately undecided.**
>
> **Consequences for this plan:** the server side (task 4) can be written now against the
> configured range. **The UI control cannot** — `ui-design.md` §11 records that *"an ordinal range
> renders as a rating scale; a binary scale renders as two buttons"*, and **"the plan must not
> hardcode a star widget until OQ-1 is answered"**. Task 9 therefore renders **from the configured
> range**, and a binary decision would require a different control. **Obtain the answer before
> task 9.**
>
> A related recorded finding, **N-5**, notes that `data-model.md` types `rating` as *"an ordinal
> value"* while OQ-1's candidate list includes a binary thumbs up/down. It stays **recorded, not
> fixed** — resolving it would pre-empt OQ-1.

---

## Story Goal

All five lines of requirements §8, as a **separate, simpler surface for the Customer role over the
same backend**. It is not a second application with its own data.

1. **Submit** (§8.1) — the portal surface around Story 07's web form.
2. **Track** (§8.2) — status of their own requests, in the A-5 vocabulary.
3. **View history** (§8.3) — their own past tickets and the message thread on each.
4. **Access FAQs** (§8.4) — browse and search **public** knowledge-base articles only.
5. **Submit feedback** (§8.5) — a one-question rating with an optional comment, offered when a
   ticket reaches `Resolved`. **The sole CSAT input in the system.**

---

## Context — Read These Files First

1. `docs/api-design.md` §5.7 — every portal endpoint, **`AP-5`** (a separate path space: different
   scoping, different DTOs, different authority), **`AP-16`** (the customer's ticket DTO **omits
   assignee identity**), the `POST .../feedback` preconditions, and the OQ-1 box. Then §6.4
   **`Ticket (portal)`** — **no assignee, no department, no priority, no SLA or breach fields, no
   internal anything**.
2. `docs/ui-design.md` **§7 in full** — the four portal screens; note §7.3's four rules: the
   *"reopened"* cue, **"the UI must not offer a manual reopen for a `Pending` request"**, cancel
   offered **only while `New`**, and *"declining is normal: the UI never nags or blocks"*. Then
   **UI-11** and §4.2 (the portal shell has **no notification bell**).
3. `docs/data-model.md` §2.15 `CustomerFeedback` — **unique per ticket, write-once, offered only
   after `Resolved`**, and *"declining is a normal outcome, so the absence of a row is meaningful
   and reporting must treat it as 'no response', not as a zero"*. Then **DM-7**: the entity belongs
   to the **`Tickets`** module; **there is no `Portal` backend module**.
4. `docs/product-scope.md` **A-16** and **A-18** — a customer may cancel their own ticket **only
   while `New`**, and A-18 makes that window real because an auto-assigned ticket is still `New`.
   Customers **cannot close**; they can only reopen a `Resolved` one.
5. `docs/architecture.md` §1 — `customer-portal` is an **Angular area**, and its server-side
   behaviour, **including customer feedback, lives in `Tickets`**.
6. `.squad/stories/customer-portal/portal-self-service/intake.md` — twelve acceptance criteria.

---

## Product rules (from story)

- **A customer sees only their own tickets**, enforced server-side, verified by a test that
  bypasses the UI. Another customer's id -> **`404`** (AP-4).
- **The portal never shows assignee, department, priority or SLA data** (UI-11, AP-16) — *"the UI
  cannot show what the contract does not return."*
- **Internal notes are unreachable by path.** The portal never calls that endpoint; there is no
  merged list to filter (AP-5, T2-C).
- **Cancel is offered only while the status is `New`** (A-16). Once `Open`, it disappears with a
  line explaining that work has started.
- **Reopen is offered only on a `Resolved` request.** **No manual reopen is offered for a
  `Pending` request** — no such transition is available to a customer.
- **Replying to a `Pending` request returns it to `Open` automatically** (R-13). The reply response
  carries the new status, so the UI reflects it immediately rather than guessing.
- **Feedback is offered once a ticket reaches `Resolved`, once per ticket, write-once.** A second
  submission -> `409 feedback-already-submitted`. **Declining is simply never calling it** — there
  is no "declined" state to record.
- **The status vocabulary is the same as staff's.** `StatusChip` is shared; **no separate customer
  wording was authorized** (ui-design §8).

---

## Backend Tasks

### 1 — Domain: `CustomerFeedback`

**Create file: `src/SupportCrm.Domain/Modules/Tickets/CustomerFeedback.cs`** — `TicketId` (unique),
`Rating`, `Comment?`, `SubmittedAt`. **Private setters and no mutator at all** — write-once, not
editable, not resubmittable (data-model §2.15).

> **It lives under `Modules/Tickets`, not a portal module** (**DM-7**). Feedback is domain behaviour
> attached to a ticket; `customer-portal` is a front-end and planning concern. **Do not create an
> eleventh backend module.**

`Rating` is an `int` with **no range constant anywhere in the Domain** — the permitted set comes
from configuration (OQ-1).

### 2 — Infrastructure: EF configuration and migration

**Create file: `Persistence/Configurations/CustomerFeedbackConfiguration.cs`** —
**unique index on `TicketId`** (data-model §6: *"enforces one rating per ticket; feeds the §9.4
average"*), `Restrict` FK to `Ticket`. **No check constraint on `Rating`** — a range here would
encode OQ-1 into the schema, which data-model §2.15 forbids.

```bash
dotnet ef migrations add CustomerFeedback -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Application: the portal read services

**File: `src/SupportCrm.Application/Modules/Tickets/PortalTicketService.cs`** (Story 07) — add:

- `ListAsync(status?)` — the caller's **own** tickets, via `TicketScope.ForCaller`, filter `status`,
  sort `createdAt`.
- `GetAsync(id)` — through `LoadScopedAsync`; another customer's id -> `404`.
- `TransitionAsync(id, targetStatus)` — delegates to **Story 06's**
  `TicketLifecycleService.TransitionAsync`, which already enforces the A-16 matrix. **Do not
  re-implement the authority rules here** — a customer's permitted set is `Cancelled` (while `New`)
  and `Open` (from `Resolved`), and Story 06 owns that table.

**The portal DTO is a distinct type**, not the staff DTO with fields hidden:

```csharp
// api-design §6.4 — Ticket (portal). AP-16: no assignee. UI-11: no department, priority or SLA.
public sealed record PortalTicketDto(Guid Id, string Subject, string Description, string CategoryCode,
                                     string Status, bool IsUrgent, DateTimeOffset CreatedAt,
                                     DateTimeOffset? ResolvedAt, bool HasFeedback);
```

**`hasFeedback` is computed from the existence of a feedback row** — a response projection, not a
stored field (api-design §7, **N-4**).

### 4 — Application: feedback

**Create file: `src/SupportCrm.Application/Modules/Tickets/CustomerFeedbackService.cs`**

`SubmitAsync(ticketId, rating, comment?)`:

1. `LoadScopedAsync(ticketId)` — another customer's ticket -> `404`.
2. **Precondition: the ticket has reached `Resolved`.** A ticket that never reached it ->
   `409`. (`Closed` is reached *from* `Resolved`, so a closed ticket has reached it.)
3. **One per ticket:** an existing row -> `ConflictException("feedback-already-submitted")` -> `409`.
4. **Validate `rating` against `feedback.ratingScale` from configuration** — outside the range is
   `400`. **The range comes from the config key, never from a constant in this file** (OQ-1).
5. Persist, and write a **`FeedbackSubmitted`** activity row (data-model §2.7).

**There is no update and no delete method.** Declining is the absence of a call.

### 5 — Api: the remaining portal endpoints

**File: `src/SupportCrm.Api/Controllers/PortalTicketsController.cs`** (Story 07) — add, all
`RequireCustomer`:

```
GET   /api/v1/portal/tickets                      ?status=   sort=createdAt
GET   /api/v1/portal/tickets/{id}
POST  /api/v1/portal/tickets/{id}/transition      { "targetStatus": "Cancelled" | "Open" }
GET   /api/v1/portal/tickets/{id}/attachments
POST  /api/v1/portal/tickets/{id}/attachments     (multipart/form-data)
POST  /api/v1/portal/tickets/{id}/feedback        { rating, comment? }
```

With Story 07's three and Story 12's two, the portal path space is complete at **eleven** endpoints,
matching api-design §5.7.

**No portal endpoint returns an assignee, a department, a priority, an SLA field or an internal
note.** Add a test that asserts it on the serialized payload, not merely on the DTO type.

### 6 — Seed data

**File: `Persistence/Seeders/TicketSeeder.cs`** — ensure at least one **`Resolved`** ticket per
seeded portal customer **without** feedback (so the control is demonstrable), at least one **with**
feedback (so Story 15's satisfaction tile is non-trivial), one left **`New`** (so cancel is
demonstrable), and one left **`Pending`** (so the automatic reopen is demonstrable).

### 7 — Tests

**Create file: `tests/SupportCrm.Tests/Portal/PortalIsolationTests.cs`** — the permission
demonstration this story carries:

1. A customer's `GET /portal/tickets` returns **only** their own.
2. `GET /portal/tickets/{id}` for **another customer's** ticket -> **`404`** (AP-4).
3. The same for `POST .../messages`, `.../transition`, `.../attachments` and `.../feedback`.
4. A customer calling any **staff** `/tickets` endpoint -> `403`.
5. **The serialized portal ticket payload contains none of**
   `assignee`, `departmentId`, `priority`, `firstResponseDueAt`, `resolutionDueAt`,
   `firstResponseBreached`, `resolutionBreached` (AP-16, UI-11) — assert on the raw JSON.
6. **`/portal/tickets/{id}/internal-notes` is not routable** (AP-5).

**File: `tests/SupportCrm.Tests/Tickets/InternalNotesAreUnreachableTests.cs`** — the stub Story 07
left; it is completed in Story 14, which owns the entity.

**Create file: `tests/SupportCrm.Tests/Portal/PortalLifecycleTests.cs`**

7. A customer cancels their own **`New`** ticket -> `200`, **including when it has already been
   auto-assigned** (A-18) — assert the ticket had an assignee.
8. A customer cancelling their own **`Open`** ticket -> **`403 transition-not-permitted`** (A-16).
9. A customer reopens their own `Resolved` ticket -> `200`, and the reopen appears in ticket history.
10. A customer attempting `-> Closed` -> `403`.
11. Replying to a **`Pending`** request returns `"statusChanged": true` and `"ticketStatus": "Open"`.

**Create file: `tests/SupportCrm.Tests/Portal/FeedbackTests.cs`**

12. Feedback on a ticket that has **not** reached `Resolved` -> `409`.
13. Feedback on a `Resolved` ticket -> `201`, and `hasFeedback` on the ticket becomes `true`.
14. A **second** submission -> **`409 feedback-already-submitted`**.
15. A rating outside the **configured** range -> `400`. **Write this test against the configured
    range, not against literal numbers**, so it survives whatever OQ-1 decides.
16. **Not submitting is not an error**: the ticket stays valid and reporting sees no row.

---

## Frontend Tasks

### 8 — Portal shell — `layout/portal-shell/` (ui-design §4.2)

Two destinations — **My requests** and **Help** — plus the language switcher and the avatar menu.
**No sidebar and no notification bell**: A-13's four events are staff-facing and no requirement
gives a customer an in-app feed.

**No staff vocabulary anywhere in this area** — no department, no priority, no assignee, no SLA
(UI-11). Add a lint-style review note: `features/portal/**` must not import `PriorityChip`,
`SlaIndicator` or `CustomerPanel`.

### 9 — My requests — `/portal/requests` (ui-design §7.1)

**Cards, not a dense table.** Each shows subject, status, last update, and a **"response needed from
you"** cue when the status is `Pending`. Filter by status. Empty state: *"You haven't submitted any
requests yet"*, with the submit action. **Single column throughout, at every width** (ui-design
§10.3).

### 10 — Submit a request — `/portal/requests/new` (ui-design §7.2)

**Exactly four inputs, and that is a direct consequence of the contract:**

| Field | Rule |
|---|---|
| Subject | Required |
| Description | Required |
| **Category** | Required, from `GET /config`. **The customer chooses a category, never a department** (A-14) — the department is derived server-side and **never appears in this form** |
| **"This is urgent"** checkbox | The `isUrgent` boolean of A-17, **labelled as an indication, not as a priority** |

**No priority selector. No department selector. No assignee.** Attachment upload is offered **after
submission**, on the request detail screen.

### 11 — Request detail — `/portal/requests/:id` (ui-design §7.3)

Replaces Story 07's stub. Regions: header, thread, reply composer, attachments, and the feedback
block.

- **Thread** — the portal configuration of `MessageThread` (no `channel`, no `authorRole`).
  **Internal notes are unreachable: a different endpoint, never requested here.**
- **Reply composer** — the portal configuration: **no quick replies, no AI insert**.
  On a `Pending` request, the reply response carries `statusChanged: true`; **update the status chip
  in place with a short "reopened" cue.** Do not re-fetch to discover it, and do not guess.
- **Cancel** — rendered **only while `status === 'New'`** (A-16). A `ConfirmDialog` names the effect
  (UI-12). Once `Open` the control **disappears with a line explaining that work has started**.
- **Reopen** — rendered **only on `Resolved`**. **Never offer a manual reopen on `Pending`.**
- **Feedback** — appears when the request reaches `Resolved` and `hasFeedback` is false.

  > **OQ-1.** Render the control **from `feedback.ratingScale` in `GET /config`**. **Do not hardcode
  > a star widget**, a 1–5 array, or a thumbs pair. Build
  > `shared/components/rating-input/` taking `{ min, max }` and rendering an ordinal scale; leave a
  > documented seam for a binary control should OQ-1 decide that way. **Declining is normal: the UI
  > never nags, never blocks and never re-prompts.**

- `Closed` and `Cancelled` requests are **read-only, with a line saying so**.
- **The status chip uses the shared `StatusChip`** and the same A-5 vocabulary as staff.

### 12 — Help

`/portal/help` and `/portal/help/:id` were built in Story 12; confirm they sit inside this shell and
that a `404` reads identically for a missing and for an internal article.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:** `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Portal` —
   all sixteen green, including the raw-JSON assertion that no staff field leaks.
3. **Isolation by hand:** with customer A's token, `GET /api/v1/portal/tickets/{B's ticket}` ->
   **`404`**.
4. **Cancel window:** cancel a seeded auto-assigned **`New`** request -> succeeds; move another to
   `Open` and try -> **`403`**, and the control is gone from the UI.
5. **Automatic reopen:** reply to the seeded `Pending` request — the chip changes to `Open` in place
   with the "reopened" cue, **without a page refresh and without a second request**.
6. **Feedback:** submit on a `Resolved` request -> accepted; submit again -> `409` surfaced as a
   readable message; a rating outside the configured range -> `400` inline.
7. **Phone width:** at 390 px every portal screen is single column, the submit form is full width,
   and the body does not scroll sideways.
8. **Regression:** Stories 05–12 suites still pass.

---

## Done Criteria

- [ ] A customer signs in and reaches a portal surface **distinct from the agent workspace**.
- [ ] A customer can submit a ticket and immediately see it in their list.
- [ ] A customer sees **only their own tickets**, enforced server-side and verified by a test that
      bypasses the UI.
- [ ] Ticket status is shown using the A-5 vocabulary and updates as agents work the ticket.
- [ ] A customer can read the thread and reply, and **cannot see internal notes**.
- [ ] A customer can browse and search **public** KB articles; internal articles never appear.
- [ ] When a ticket becomes `Resolved` the customer is offered a **one-question rating with an
      optional comment**, and submitting records it against that ticket.
- [ ] A rating can be submitted **once per ticket**, and **declining is a normal outcome, not an
      error**.
- [ ] A customer can reopen a `Resolved` ticket, and the reopen is reflected in ticket history.
- [ ] Replying to a `Pending` request returns it to `Open` **without any agent action**, and the
      customer sees the updated status.
- [ ] A customer can cancel their own ticket **while it is `New` — including when already
      auto-assigned** — and is **refused once it is `Open`**.
- [ ] The portal is usable at **phone width**.
- [ ] **No portal payload or screen exposes an assignee, department, priority or SLA field.**
- [ ] **OQ-1 is not answered here.** The rating control is rendered from the configured range and
      **no scale is hardcoded** in the schema, the service, the tests or the UI.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 14.**
