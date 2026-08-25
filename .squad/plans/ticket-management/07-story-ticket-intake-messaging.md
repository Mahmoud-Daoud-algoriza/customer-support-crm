# Story 07 — Web form intake and in-portal messaging

> **Source of truth:** `docs/requirements.md` §3.5, §3.3 (partial) · `docs/product-scope.md` T2-B, T3-A, T3-B, A-5, A-9, A-13, A-14, A-17, R-13, R-14 · `docs/architecture.md` §5.2, §2.5 · `docs/data-model.md` §2.8, §2.6 invariant 2b, §5 constraints 8, 9a, 16, 17, §6 · `docs/api-design.md` §5.6, §5.7, §6.4, §7, §8.2, AP-5, AP-10, AP-11 · `docs/ui-design.md` §5.3, §7.2, §7.3
> **Intake:** `.squad/stories/ticket-management/ticket-intake-messaging/intake.md` · **Tier:** T2 — *"if cut, the portal and AI reply stories lose their thread; cut this last among T2 items"*
> **Phase:** 4 — Channels and workspace.

## Prerequisites

- **Story 05 completed:** `Ticket`, `TicketScope`, `LoadScopedAsync`, `TicketActivityRecorder`.
- **Story 06 completed:** `TicketLifecycleService`, including
  `ApplyAutomaticCustomerReplyTransitionAsync` — **this story supplies its trigger**, and
  `INotificationPublisher`.
- **Story 04 completed:** `Customer`, and `POST /auth/register` so a customer can exist to submit.
- **Story 16 Part A completed:** the category list and the **category → department map** (A-14),
  which a portal submission needs before it can be routed.

---

## Story Goal

Deliver the one real communication channel for this assessment, and the message model every other
channel would plug into.

1. **Web form** (§3.5) — `POST /portal/tickets` creates a ticket for the **authenticated**
   submitting customer. **No anonymous submission** (A-9).
2. **In-portal messaging** (§3.3, partial) — customer and agent exchange replies over ordinary
   request/response. **This is not real-time chat** (T3-B).
3. **One channel-agnostic message model** carrying the channel it arrived on — the field that makes
   T3-A a seam rather than a rewrite.
4. **The one status side effect in this API**: a customer reply on a `Pending` ticket returns it to
   `Open`, in the same transaction, attributed to the replying customer (R-13, R-14).

**Not in this story:** the portal *screens* (Story 13 builds them; this story delivers their two
endpoints and the staff-side thread UI), attachments beyond Story 04's, and any real email,
WhatsApp or SMS transport (Story 18 delivers the adapter seam only).

---

## Context — Read These Files First

1. `docs/data-model.md` §2.8 `TicketMessage` — the five fields, **"Immutable once posted"**, and the
   full invariant paragraph including the automatic transition and *"generates no notification of
   its own"*. Then §2.6 invariant **2b** and §5 constraints **8, 9a, 17**.
2. `docs/api-design.md` §5.7 — `POST /portal/tickets` (**`customerId`, `departmentId` and
   `priority` are not accepted**), and **"the one status side effect in this API"**. Then §6.4 for
   `Message`, the **`POST /portal/tickets/{id}/messages` response envelope**
   (`{ message, ticketStatus, statusChanged }`), and §7 (`direction` and `channel` are
   server-derived, **PF-7**).
3. `docs/architecture.md` §5.2 — the channel seam: one normalized message model, one outbound
   adapter interface, **inbound arrives through the same ingestion service the web form uses**, and
   **"in-app notifications are a different thing and must not be confused with this seam"**.
4. `docs/api-design.md` **AP-11** — there is **no inbound-channel HTTP endpoint**, and why:
   publishing one would force the undecided system-actor question (**PF-2**) into the contract.
5. `docs/ui-design.md` §5.3 (staff thread and reply composer) and §7.3 (portal request detail — the
   *"reopened"* cue, and *"the UI must not offer a manual reopen for a `Pending` request"*).
6. `.squad/stories/ticket-management/ticket-intake-messaging/intake.md` — eleven acceptance
   criteria, including *"No polling implementation is presented as, or described as, real-time
   chat."*

---

## Product rules (from story)

- **Every message records its channel of origin, author and timestamp.** `channel` is
  `WebForm` | `Portal` **today**; a new value arrives with the adapter that implements it — that is
  the seam (architecture §5.2).
- **`direction` is derived from the author's role** — customer -> `Inbound`, agent -> `Outbound`
  (**PF-7**). It is **absent from every request model**; a request containing it is a `400`
  (AP-10), never accepted-and-ignored.
- **`channel` is derived from the endpoint used** — `WebForm` for portal creation, `Portal` for
  portal replies. Also never client-set.
- **A message is immutable once posted.** No edit, no delete; a correction is a new message.
- **A message on a `Closed` or `Cancelled` ticket is refused** -> `409` (A-5).
- **The first `Outbound` message sets `Ticket.firstRespondedAt`** if it is still null. It is set
  once and never again.
- **Every message has exactly one `MessagePosted` activity row** (DM-4), written in the same
  transaction. The **body lives on the message, never duplicated onto the activity row**.
- **R-13 / R-14 — the automatic transition.** An `Inbound` message posted while the ticket is
  `Pending` moves it to `Open` **in the same transaction**, writing a `StatusChanged` row
  **attributed to the replying customer** with `actorKind = User`. It fires **from `Pending`
  only**: a reply on `New` leaves it `New`, and a reply on `Resolved` does **not** reopen it.
- **An agent reply never transitions the ticket.**
- **The automatic transition generates no notification** — A-13 defines exactly four notification
  types and none of them is a status change. A **customer reply on an assigned ticket** does raise
  a `CustomerReplied` notification, which is a different rule.
- **A customer submission never carries a priority or a department.** Category chooses the
  department (A-14); `isUrgent` is the boolean the customer may send (A-17) and **it does not set
  priority**.

---

## Backend Tasks

### 1 — Domain: `TicketMessage`

**Create file: `src/SupportCrm.Domain/Modules/Tickets/MessageChannel.cs`**

```csharp
// architecture §5.2 — a new value arrives with the adapter that implements it. That is the seam.
public enum MessageChannel { WebForm, Portal }
```

**Create file: `src/SupportCrm.Domain/Modules/Tickets/MessageDirection.cs`**

```csharp
public enum MessageDirection { Inbound, Outbound }
```

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketMessage.cs`** — `TicketId`,
`AuthorUserId`, `Direction`, `Channel`, `Body`, `PostedAt`. **Private setters throughout and no
mutator method** — immutability is structural, exactly like `CustomerNote`.

### 2 — Infrastructure: EF configuration and migration

**Create file: `Persistence/Configurations/TicketMessageConfiguration.cs`** — index
`TicketMessage(TicketId, PostedAt)` for thread rendering (data-model §6); enum conversions to
string; `TicketActivity.MessageId` relationship completed (**set if and only if
`activityType = MessagePosted`**).

```bash
dotnet ef migrations add TicketMessages -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Application: the one ingestion service

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketMessageService.cs`**

`PostAsync(ticketId, body, MessageChannel channel)` — **one method serving the staff endpoint, the
portal endpoint, and (later) Story 18's inbound fake adapter**, so channel origin is *data*, not a
separate code path (architecture §5.2, §3).

Order of operations, all inside **one unit of work**:

1. `LoadScopedAsync(ticketId)` — out of scope or missing -> `404`.
2. Terminal-status guard: `Closed` or `Cancelled` -> `ConflictException("ticket-terminal")` -> `409`.
3. **Derive `direction`** from `ICurrentUser.Role`: `Customer` -> `Inbound`, staff -> `Outbound`.
   **Never read it from the request.**
4. Persist the message.
5. Write the `MessagePosted` activity row, `visibility = CustomerVisible`, linking `messageId`.
6. If `direction == Outbound` **and** `ticket.FirstRespondedAt is null`, set it to `PostedAt`.
7. If `direction == Inbound`, call
   `TicketLifecycleService.ApplyAutomaticCustomerReplyTransitionAsync(ticket, caller)` — which
   itself does nothing unless the status is `Pending` (Story 06 owns that guard; **do not
   re-implement the condition here**).
8. If `direction == Inbound` **and** `ticket.AssignedUserId is not null`, publish a
   **`CustomerReplied`** notification to the assignee (A-13). This is the notification rule; the
   status change has none.
9. Return the message **plus the ticket's current status and whether it changed**.

**Create file: `src/SupportCrm.Application/Modules/Tickets/PortalTicketService.cs`**

`SubmitAsync(subject, description, categoryCode, isUrgent)`:

- `customerId` = **the caller's own profile**, from `ICurrentUser.CustomerId`. **Not accepted from
  the body.**
- `departmentId` = **derived from `categoryCode` through the configured map** (A-14). **Not
  accepted from the body.** An unmapped or unknown category -> `400`.
- `priority` — **not accepted**. It is set by an agent or the AI suggestion (A-6).
- `isUrgent` — accepted, stored, **and does not affect priority** (A-17).
- Reuses `TicketService.CreateAsync`'s internals for SLA due dates and the `Created` activity row;
  do not duplicate them.
- Writes the originating text as `Ticket.description` — **not as a first `TicketMessage`**
  (data-model §2.6: *"Replies are `TicketMessage` rows, not copies of this"*).

### 4 — Api: endpoints

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add, `RequireAgent`:

```
GET   /api/v1/tickets/{id}/messages        -> paged Message
POST  /api/v1/tickets/{id}/messages        { "body": "..." }  -> 201 Message   (channel = Portal)
```

**Create file: `src/SupportCrm.Api/Controllers/PortalTicketsController.cs`** — `RequireCustomer`,
route prefix `api/v1/portal/tickets`. **AP-5: the portal is a separate path space** because it has
different scoping, different DTOs and different authority. This story publishes three of its
endpoints; [Story 13](../customer-portal/13-story-portal-self-service.md) publishes the rest into
this same controller.

```
POST  /api/v1/portal/tickets                    { subject, description, categoryCode, isUrgent }
GET   /api/v1/portal/tickets/{id}/messages      -> paged portal Message
POST  /api/v1/portal/tickets/{id}/messages      { "body": "..." }
```

**The `POST` reply response is the envelope of api-design §6.4, not a bare message:**

```json
{ "message": { "id": "...", "author": {...}, "direction": "Inbound", "body": "...", "postedAt": "..." },
  "ticketStatus": "Open",
  "statusChanged": true }
```

`statusChanged` is true **only** when the automatic `Pending -> Open` fired. The client never has to
guess and never has to re-fetch.

**The portal `Message` variant omits `channel` and `authorRole`**, keeping `direction` so the thread
can distinguish the two sides (api-design §6.4).

**No inbound-channel endpoint is created** (**AP-11**). Story 18's fake adapter calls
`TicketMessageService.PostAsync` **in-process**; it does not get an HTTP route.

### 5 — Seed data

**File: `Persistence/Seeders/TicketSeeder.cs`** — extend so at least one seeded ticket carries a
short two-way thread, and **at least one ticket is left in `Pending`** so the automatic transition
is demonstrable in one click during the demo.

### 6 — Tests

**Create file: `tests/SupportCrm.Tests/Tickets/MessagingTests.cs`**

1. An authenticated customer submits through `POST /portal/tickets`; the ticket is created, linked
   to **that** customer, with the department **derived from the category**.
2. An **unauthenticated** submission -> `401`.
3. A `priority` or `departmentId` in the portal body -> **`400`** (AP-10).
4. Customer and agent exchange replies; both read the thread in order.
5. `direction` is `Inbound` for the customer's message and `Outbound` for the agent's, **with no
   client input**; a `direction` in the request body -> `400`.
6. Every message has **exactly one** `MessagePosted` activity row.
7. A reply on a `Closed` ticket -> `409`; on a `Cancelled` ticket -> `409`.
8. The **first** outbound message sets `firstRespondedAt`; the second does **not** change it.
9. A **customer reply on a `Pending` ticket** -> the response carries `"statusChanged": true` and
   `"ticketStatus": "Open"`; the activity trail contains **both** a `MessagePosted` **and** a
   `StatusChanged` row, and the `StatusChanged` row's actor is **the replying customer** with
   `actorKind: "User"` (**R-14**).
10. A customer reply on a **`New`** ticket leaves it `New`; on a **`Resolved`** ticket leaves it
    `Resolved`. Assert both.
11. An **agent** reply on a `Pending` ticket does **not** transition it.
12. A customer replying to **another customer's** ticket -> `404` (AP-4).
13. A customer reply on an **assigned** ticket raises exactly one `CustomerReplied` notification;
    the automatic status change raises **none**.

**Create file: `tests/SupportCrm.Tests/Tickets/InternalNotesAreUnreachableTests.cs`** — a stub with
one skipped test referencing Story 14, so the portal-thread isolation check has a home the moment
internal notes exist.

---

## Frontend Tasks

### 7 — Typed clients

- `core/api/tickets.client.ts` — `listMessages(ticketId)`, `postMessage(ticketId, body)`.
- **Create file: `core/api/portal.client.ts`** — `submit(body)`, `listMessages(id)`,
  `postMessage(id, body)`. Typed against the **portal** payloads, which are a different shape from
  the staff ones (AP-5, UI-11). Do not share a DTO type between the two path spaces.

### 8 — Shared thread and composer components

- **`shared/components/message-thread/`** — **two configurations of one component** (ui-design §8):
  the staff configuration shows `channel` and `authorRole`; the portal configuration does not.
  Inbound and outbound render on opposite sides, **mirrored under RTL** via logical properties.
- **`shared/components/reply-composer/`** — **one component** (UI-7). This story delivers plain
  text; Story 08 adds the quick-reply insert and Story 11 the AI *Insert into reply*, into the
  **same** draft. One insertion point is what keeps *"never auto-sent"* true by construction.
- **Nothing in either component polls, and nothing in the UI, the code comments or the README
  describes the portal thread as chat or as real-time** (T3-B, intake AC).

### 9 — Staff ticket detail: the thread region

`features/workspace/ticket-detail/thread-region/` — fills the slot Story 05 left. Loads
independently of the other regions. Empty state: *"No replies yet."*

### 10 — Portal wiring (screens land in Story 13)

Add `features/portal/` route stubs for `/portal/requests/new` and `/portal/requests/:id` that call
`portal.client.ts`, so the two endpoints are exercised end to end now. **Story 13 replaces these
stubs with the designed screens** of ui-design §7.2 and §7.3, including the *"reopened"* cue driven
by `statusChanged`.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Messaging` — all thirteen green.
3. **By hand — the R-13 path:** as an agent, move a ticket to `Pending`; as its customer,
   `POST /api/v1/portal/tickets/{id}/messages`. The response body has
   `"statusChanged": true, "ticketStatus": "Open"`, and `GET /api/v1/tickets/{id}/activity` shows
   the `StatusChanged` row with the **customer** as actor.
4. **By hand — the seam:** `grep -rn "MessageChannel" backend/src/` — the enum is referenced by the
   message model and the two endpoints, and **by no channel-specific branching**.
5. **No inbound route exists:** `grep -rn "channels/inbound\|webhook" backend/src/` returns nothing
   (AP-11).
6. **Regression:** Story 06's transition tests and Story 05's scoping tests still pass.
7. **Frontend:** `npm run build`; the staff thread renders in order; the portal stub submits and
   replies; switching to العربية mirrors the thread sides.

---

## Done Criteria

- [ ] An authenticated customer can submit a ticket through the web form; the ticket is created and
      linked to that customer.
- [ ] An unauthenticated submission is refused.
- [ ] Customer and agent can exchange replies; both see the thread in order.
- [ ] Every message records channel of origin, author and timestamp.
- [ ] Messages are visible to the ticket's customer; **internal notes are not** *(the exclusion is
      structural — the portal never requests that endpoint; fully verified in Story 14)*.
- [ ] Messages appear in ticket history and in the customer's interaction timeline.
- [ ] A reply on a `Closed` or `Cancelled` ticket is refused.
- [ ] A customer reply on a `Pending` ticket returns it to `Open` **in the same transaction**, with
      **both** the `MessagePosted` and `StatusChanged` entries written, the latter attributed to the
      **replying customer** with `actorKind = User`.
- [ ] An agent reply does **not** transition the ticket, and a customer reply on a `New` or
      `Resolved` ticket does not either.
- [ ] `direction` and `channel` are **server-derived** and rejected in a request body.
- [ ] The message model supports a second channel **without schema change** *(demonstrated by
      Story 18's log adapter writing an inbound message)*.
- [ ] **No polling implementation is presented as, or described as, real-time chat** — in the UI, in
      the code, or in the README.
- [ ] **No inbound HTTP endpoint exists** (AP-11); PF-2 stays untouched and open for Story 18.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 08.**
