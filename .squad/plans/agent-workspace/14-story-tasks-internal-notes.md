# Story 14 — Tasks, reminders and internal notes

> **Source of truth:** `docs/requirements.md` §4.3, §4.5 · `docs/product-scope.md` **T2-C**, A-4 · `docs/architecture.md` §2.5, **§4.4** · `docs/data-model.md` §2.9, §2.10, §2.7, §5 constraints 16, 18, 23, §6 · `docs/api-design.md` §5.6, §6.4, **AP-5** · `docs/ui-design.md` §5.1, §5.3, §8, **UI-5**
> **Intake:** `.squad/stories/agent-workspace/tasks-internal-notes/intake.md` · **Tier:** T2 — *"if cut in part, keep internal notes and drop tasks: notes carry the visibility rule that the permission model demonstrates, tasks do not"*
> **Phase:** 7 — Collaboration, reporting and administration.

## Prerequisites

- **Story 06 completed:** `TicketActivityRecorder` and the `InternalNotePosted` activity type.
- **Story 08 completed:** the agent dashboard, which is where tasks surface.
- **Story 13 completed** *(recommended)* — the customer-visibility exclusion is verified **jointly**
  with the portal, and the portal must exist to prove a customer cannot reach a note through it.

> ### ⚠ Blocked decision — **S9-1** — the dashboard task region has no endpoint
>
> `ui-design.md` §5.1 places *"Open tasks and overdue tasks for this agent"* on **My queue**, §13
> lists a *"queue task list"* under this story, this story's own AC says *"The assigned agent sees
> their open and overdue tasks on the agent dashboard"*, and `data-model.md` §6 provides the index
> `TicketTask(assignedUserId, isDone, dueAt)` **"Open and overdue tasks on the dashboard (T2-C)"**.
>
> **`api-design.md` publishes no endpoint that lists a user's tasks across tickets.** §5.6 has only
> `GET`/`POST /tickets/{id}/tasks` and `PATCH /tickets/{id}/tasks/{taskId}` — all ticket-scoped.
> Four approved documents assume a cross-ticket task list; the contract has none.
>
> **Do not invent an endpoint.** Tasks 1–5 deliver the ticket-scoped task and note surface in full.
> **Task 8 — the dashboard task region — is blocked on a decision:**
>
> | Option | What changes | Cost |
> |---|---|---|
> | **A.** Publish `GET /tasks?assignedUserId=me&isDone=false` | `api-design.md` §5.6 gains one endpoint (67 total) | A Stage 7 change; the data model and its index already support it |
> | **B.** Drop the region from My queue | `ui-design.md` §5.1 and §13 lose the task list | A Stage 8 change; the AC in this intake would need amending in `product-scope.md` per its §10 cut rule |
>
> Recorded as **S9-1** in `00-implementation-plan.md`. Take the decision before implementing.

---

## Story Goal

Requirements §4.3 and §4.5, reduced to the minimum defensible implementation (T2-C).

1. **Tasks and reminders** — a due-dated to-do attached to a ticket, assigned to an agent, markable
   done. **No calendar, no recurrence, no push reminders.**
2. **Team collaboration = internal notes only** — a note on a ticket, visible to Agent, Manager and
   Administrator, and **never to the customer, through any path**. That is the whole of
   collaboration here: **no @mentions, no presence, no chat, no shared ownership.**

**The internal/external visibility split is the highest-risk detail in this story. It is a
server-side rule, not a UI filter.**

---

## Context — Read These Files First

1. `docs/data-model.md` §2.9 `TicketInternalNote` — **"Staff only — never visible to a customer
   through any path: not the portal thread, not the interaction timeline, not a notification"**, and
   the closing invariant: *"It is a separate entity from `TicketMessage` **on purpose**: a
   customer-visible read assembles from messages and never touches this table, so the visibility
   rule is structural rather than a filter someone must remember to apply."* Then §2.10
   `TicketTask` (**`completedAt` is set if and only if `isDone`**) and §6's two indexes.
2. `docs/architecture.md` §2.5 — *"Internal notes are recorded in history and excluded from every
   customer-facing read… That exclusion is applied in the Application layer, once, not in each UI"*
   — and **§4.4**.
3. `docs/api-design.md` §5.6 (`/internal-notes` is **"Staff only, by path"**, AP-5) and §6.4
   (`InternalNote` — *"Returned **only** by `/tickets/{id}/internal-notes`, which no portal path
   reaches"*; `Task`).
4. `docs/ui-design.md` **UI-5** (*"Internal notes live in a visually distinct region with a
   persistent 'not visible to the customer' marker… the UI states it continuously rather than
   relying on memory"*, and the **rejected** alternative — a merged thread with per-item badges),
   §5.3 and §8.
5. `.squad/stories/agent-workspace/tasks-internal-notes/intake.md` — seven acceptance criteria and
   the Out of scope list.

---

## Product rules (from story)

- **A task is always attached to a ticket.** Standalone personal to-dos are out of scope.
- A task carries a **due date** (required), an assignee (required), and `isDone`.
  **`completedAt` is set exactly when `isDone` is true** — and cleared if it is un-done.
- **An internal note is visible to Agent, Manager and Administrator and never to a Customer.**
- **Internal notes appear in ticket history for internal roles**, with `visibility = Internal`.
- **Tasks and notes are recorded with actor and timestamp and are not silently editable by other
  users.** A note is **immutable once written**, like `CustomerNote` and `TicketMessage`.
- **No @mentions, no notification on mention, no presence, no agent-to-agent chat, no shared or
  co-owned tickets. No calendar integration, no recurring tasks, no push or email reminders.**

---

## Backend Tasks

### 1 — Domain: `TicketInternalNote` and `TicketTask`

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketInternalNote.cs`** — `TicketId`,
`AuthorUserId`, `Body`, `CreatedAt`. **Private setters, no mutator method** — immutable by
construction.

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketTask.cs`** — `TicketId`, `Title`,
`DueAt`, `AssignedUserId`, `IsDone`, `CompletedAt?`, `CreatedByUserId`, `CreatedAt`. One mutator:

```csharp
public void SetDone(bool done, DateTimeOffset now)
{
    IsDone = done;
    CompletedAt = done ? now : null;   // data-model §5 constraint 23 — set iff IsDone
}
```

### 2 — Infrastructure: EF configuration and migration

**Create files:** `Persistence/Configurations/TicketInternalNoteConfiguration.cs` and
`TicketTaskConfiguration.cs`.

- `TicketInternalNote(TicketId, CreatedAt)` — staff thread rendering.
- `TicketTask(AssignedUserId, IsDone, DueAt)` — **the index data-model §6 provides for "open and
  overdue tasks on the dashboard"**. Create it regardless of how **S9-1** is decided; it also serves
  the per-ticket read.
- Complete `TicketActivity.InternalNoteId` — **set if and only if
  `activityType = InternalNotePosted`** (data-model §2.7).

```bash
dotnet ef migrations add TasksAndInternalNotes -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Application: internal notes

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketInternalNoteService.cs`**

- `ListAsync(ticketId)` — through `LoadScopedAsync`; paged, oldest first.
- `CreateAsync(ticketId, body)` — author and timestamp from `ICurrentUser`; terminal-status guard
  (`Closed`/`Cancelled` -> `409`, A-5); writes an `InternalNotePosted` activity row with
  **`visibility = Internal`** — data-model §2.7: *"`InternalNotePosted` is always `Internal`
  visibility."* Set it in the recorder call, not as a caller-supplied value.
- **No update and no delete method.**

**Then verify the three customer-facing reads still cannot reach it**, which is the point of this
story:

| Read | Why a note cannot appear |
|---|---|
| Portal thread — `GET /portal/tickets/{id}/messages` | Queries `TicketMessage`. **It does not join `TicketInternalNote`** (Story 07) |
| Customer interaction timeline — `GET /customers/{id}/timeline` | Excludes `Internal` visibility **and does not join the note table** (Story 04 task 5, Story 06 task 6) |
| Notifications | Four types, none of which is a note (A-13, Story 09) |

**Re-read all three call sites now and confirm the exclusions are still in place.** They were
written before the entity existed; this is the story that makes them load-bearing.

### 4 — Application: tasks

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketTaskService.cs`**

- `ListForTicketAsync(ticketId)` — through `LoadScopedAsync`.
- `CreateAsync(ticketId, title, dueAt, assignedUserId)` — `dueAt` **required**; the assignee must be
  an **active staff user** (reuse the check Story 05 wrote for assignment).
- `SetDoneAsync(ticketId, taskId, isDone)` — **`completedAt` is server-set** (api-design §5.6),
  never accepted from the client.
- **`ListForUserAsync(...)` — blocked, see S9-1.** Write the method, leave it unreferenced by any
  controller, and mark it:
  `// S9-1: no endpoint publishes this. Do not add a route until the decision is recorded.`

### 5 — Api: endpoints

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add, all `RequireAgent`:

```
GET   /api/v1/tickets/{id}/internal-notes      -> paged InternalNote
POST  /api/v1/tickets/{id}/internal-notes      { "body": "..." }
GET   /api/v1/tickets/{id}/tasks               -> paged Task
POST  /api/v1/tickets/{id}/tasks               { title, dueAt, assignedUserId }
PATCH /api/v1/tickets/{id}/tasks/{taskId}      { isDone }
```

**No portal counterpart exists for any of these, and none may be added** (AP-5). The path space
*is* the visibility rule.

### 6 — Seed data

**File: `Persistence/Seeders/TicketSeeder.cs`** — add at least one internal note and one open task
**on a ticket that also has a customer thread**, so the demo can show, on one screen, that the
customer's view and the agent's view differ.

### 7 — Tests

**File: `tests/SupportCrm.Tests/Tickets/InternalNotesAreUnreachableTests.cs`** — **complete the stub
Story 07 created.** This is the suite the intake demands be *"performed as a customer, not by
checking the UI"*:

1. `GET /tickets/{id}/internal-notes` as `Agent`, `Manager`, `Administrator` -> `200`.
2. As `Customer` -> **`403`** (the staff path space is role-gated).
3. **`/portal/tickets/{id}/internal-notes` is not routable** — assert `404`/`405` from the router.
4. **A ticket with an internal note: the portal thread response contains none of the note's text**
   — assert on the **raw JSON**, not on a DTO shape.
5. **The customer interaction timeline contains no entry whose `visibility` is `Internal`**, and
   none of the note's text — again on the raw JSON.
6. **No notification is raised** by posting an internal note.
7. Internal notes **do** appear in `GET /tickets/{id}/activity` for staff, as `InternalNotePosted`
   with `visibility: "Internal"` and a linked `internalNoteId`.
8. **Immutability by reflection:** `TicketInternalNoteService` exposes no method whose name contains
   `Update` or `Delete`.

**Create file: `tests/SupportCrm.Tests/Tickets/TicketTaskTests.cs`**

9. Create a task with a due date and an assignee; it appears on the ticket.
10. `PATCH { isDone: true }` sets **`completedAt`**; `PATCH { isDone: false }` **clears it**
    (constraint 23).
11. `completedAt` supplied in a request body -> **`400`** (server-derived, AP-10).
12. A task on an **out-of-department** ticket -> `404` for an Agent.
13. An assignee who is deactivated or in another department -> `422`.

---

## Frontend Tasks

### 8 — Dashboard task region — **blocked, see S9-1**

`features/workspace/queue/task-region/` — **do not implement until the S9-1 decision is recorded.**
The slot Story 08 marked stays marked. If **Option A** is chosen, this region lists the agent's open
and **overdue** tasks with the ticket each belongs to; if **Option B**, delete the marker and record
the cut in `docs/product-scope.md` per its §10 rule.

### 9 — Internal notes region — `features/workspace/ticket-detail/internal-notes-region/` (UI-5)

- **A visually distinct block, a different colour from the thread**, with a **persistent
  "Not visible to the customer" marker** — always shown, not a hover state, not a per-item badge.
  T2-C's visibility rule is the highest-risk detail in the product and **the UI states it
  continuously rather than relying on memory**.
- **Fetched from its own endpoint.** There is **no merged list and no client-side filter**, so a
  rendering bug cannot leak one (ui-design §5.3).
- **No edit and no delete control** — neither exists server-side.
- Empty state: *"No internal notes."*

### 10 — Tasks region — `features/workspace/ticket-detail/tasks-region/`

Table of title, due date, assignee and a done checkbox. **Overdue rows are visually distinct.**
Add via a small dialog: title, due date (PrimeNG date picker, **Gregorian calendar in both
languages**, A-11), assignee.

**No calendar view, no recurrence control, no reminder settings** — none exists server-side and
rendering one would promise behaviour the product does not have.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~InternalNotesAreUnreachable|FullyQualifiedName~TicketTask"`
   — all thirteen green, including the two raw-JSON leak checks.
3. **Leak check by hand:** post an internal note containing a unique token such as
   `INTERNAL-CANARY-9137`, then, with the customer's token, fetch
   `GET /api/v1/portal/tickets/{id}/messages`, `GET /api/v1/portal/tickets/{id}` and (as an agent,
   for that customer) `GET /api/v1/customers/{id}/timeline`, and
   `grep -c "INTERNAL-CANARY-9137"` each response — **all zero**.
4. **Path check:** `grep -rn "internal-notes" backend/src/SupportCrm.Api/Controllers/` matches the
   **staff** controller only.
5. **Tasks:** create a task, mark it done, confirm `completedAt` is set; un-do it and confirm it is
   cleared.
6. **Regression:** Stories 05–13 suites still pass; the portal thread and timeline are unchanged.
7. **Frontend:** the internal-notes region is visually distinct with a persistent marker; the portal
   request detail shows no note and no marker.

---

## Done Criteria

- [ ] An agent can add a task to a ticket with a due date and an assignee, and mark it done.
- [ ] ⛔ **BLOCKED — S9-1:** *"The assigned agent sees their open and overdue tasks on the agent
      dashboard."* No cross-ticket task endpoint exists. **Take Option A or Option B before closing
      this box; do not invent an endpoint.**
- [ ] An agent can add an internal note showing author and timestamp.
- [ ] Internal notes are visible to Agent, Manager and Administrator.
- [ ] **A Customer cannot see internal notes anywhere** — not in the portal thread, not in the
      interaction timeline, not through the API — **verified by a server-side test performed as a
      customer, not by checking the UI**.
- [ ] Internal notes appear in ticket history for internal roles, with `visibility = Internal`.
- [ ] Tasks and notes record actor and timestamp and are **not silently editable by other users**.
- [ ] `completedAt` is set **exactly when** `isDone` is true.
- [ ] **No @mention, presence, chat, shared-ownership, calendar, recurrence or reminder feature was
      introduced.**
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user, **including the S9-1 blocker**, and wait for confirmation before
proceeding to Story 15.**
