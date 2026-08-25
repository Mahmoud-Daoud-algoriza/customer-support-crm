# Story 05 — Ticket creation, listing and assignment

> **Source of truth:** `docs/requirements.md` §2.1–2.3 · `docs/product-scope.md` T1-B, A-2, A-6, A-14, A-17, A-18 · `docs/architecture.md` §2.1, §3, §4.3, AD-5 · `docs/data-model.md` §2.6, §2.7, §5 constraints 5, 10, 11, 11a, 12, §6 · `docs/api-design.md` §4.3, §5.6, §6.4, §7, AP-1, AP-4, AP-10, AP-15 · `docs/ui-design.md` §5.2, §5.3
> **Intake:** `.squad/stories/ticket-management/ticket-core/intake.md` · **Tier:** T1 — **cannot be cut. If anything in the assessment must work, it is this.**
> **Phase:** 3 — Ticket core loop.

## Prerequisites

- **Story 01–04 completed.** In particular: `ICurrentUser` (Story 02), `Department` (Story 03),
  `Customer` and `AttachmentService` (Story 04).
- **Story 16 Part A completed.** `Ticket` requires a validated **category list**, the
  **category → department map** (A-14) and the **per-priority SLA targets** (A-3) before a single
  ticket can be created. See
  [16-story-audit-configuration.md](../administration/16-story-audit-configuration.md) §"Part A".

> ### ⚠ Blocked decision — **OQ-2** must be answered before task 5's priority branch is implemented
>
> `PATCH /tickets/{id}` changes `priority`. `data-model.md` §2.6 invariant 6 states that **what
> happens to `firstResponseDueAt` and `resolutionDueAt` when priority changes is not decided** —
> **(a) recompute** from `createdAt` with the new priority's hours, or **(b) freeze** at their
> creation values. Both are consistent with A-3; they produce materially different §9.2 numbers.
>
> `PROJECT-PROGRESS.md` §6.1 records OQ-2 as blocking **Story 09**. It blocks this story too,
> because **the first code path that changes a priority is this story's `PATCH`, not Story 09's
> escalation.** That widened blast radius is recorded as **S9-3** in `00-implementation-plan.md`.
>
> **Do not pick one.** Implement `PATCH` so it updates `priority` and writes the
> `PriorityChanged` activity row — behaviour every source already fixes — and leave the due-date
> consequence behind a single explicitly-named method,
> `SlaClock.OnPriorityChanged(ticket, newPriority)`, whose body is a `throw new
> NotImplementedException("OQ-2")` until the decision is recorded. **Obtain the answer before
> implementing this story.**

---

## Story Goal

The first half of requirements §2 — creating, finding and owning tickets.

1. Create a ticket: by an **agent on behalf of a customer**, and (Story 07) by a customer through
   the portal. Exactly one customer and exactly one department.
2. List and filter tickets, **department-scoped for Agents**, unrestricted for Manager and
   Administrator, own-tickets-only for Customers.
3. Category and priority as **fixed configuration enumerations** (A-6), with `categoryCode`
   validated against the configured list.
4. **Manual** assignment and reassignment, validated against the ticket's department. **Assignment
   does not change status** (A-18).
5. **The one department-scoping helper** every later ticket query composes (architecture §4.3
   point 2, AD-5). This is the highest-risk artefact in the whole project.

**Not in this story:** status transitions, escalation and the `/activity` read surface (Story 06);
automatic round-robin assignment, the SLA monitor and notifications (Story 09); messages (Story 07).

---

## Context — Read These Files First

1. `docs/architecture.md` **§4.3 in full** — the visibility table, the five enforcement points, and
   the **rejected alternative** (EF global query filters fail open). Then §3 (the eleven-step
   request flow) and §2.1 (what belongs in each layer).
2. `docs/data-model.md` §2.6 `Ticket` — every field, and **invariants 2a, 3, 6 and 11a**. Note that
   `firstResponseDueAt` and `resolutionDueAt` are **required (non-null)**, so they are computed at
   creation. §2.7 `TicketActivity` — the activity-type set and the `messageId`/`internalNoteId`
   iff-rules. §5 constraints 10, 11, 11a. §6 indexes.
3. `docs/api-design.md` §5.6 — the seventeen staff ticket endpoints, the filter and **sort
   whitelist**, the `POST /tickets` body (**`isUrgent` is not accepted here**), and
   `POST /tickets/{id}/assignment` -> `422 assignee-out-of-department`. Then §6.4 (`Ticket (staff)`,
   `TicketListItem`), §7 (server-derived fields), AP-4 (**out of scope returns `404`, not `403`**).
4. `docs/product-scope.md` **A-14** (a ticket's department comes from its category),
   **A-17** (`isUrgent` is customer input only and does **not** set priority), **A-18**
   (assignment is not the start of work).
5. `docs/ui-design.md` §5.2 (ticket list; the agent's department filter is **fixed and disabled**)
   and §5.3 (ticket detail header, assign control, **the status chip stays `New` after assignment**).
6. `.squad/stories/ticket-management/ticket-core/intake.md` — acceptance criteria and Out of scope.

---

## Product rules (from story)

- **A ticket has exactly one customer and exactly one department.** `Ticket.departmentId` is the
  authorization boundary (A-2).
- **`Ticket` has no `branchId` column.** A ticket's branch is derived `Ticket -> Customer -> Branch`
  (data-model §2.3). Adding one would put a branch value in arm's reach of the scoping helper,
  where A-2 forbids its use.
- **Categories and priorities come from configuration**, not a table (A-6). An unknown
  `categoryCode` or priority is a `400`.
- **Priority is four levels**: `Low`, `Medium`, `High`, `Urgent`.
- **`departmentId` on `POST /tickets` is optional**: omitted, it is derived from the category map
  (A-14); supplied by an agent, it overrides, because A-14 makes the mapping a default and **not a
  cage for agents**.
- **`isUrgent` is not accepted on the staff create endpoint** — it is customer input only (A-17).
  The column exists and is returned; the portal sets it in Story 07.
- **Assignment does not change status** (A-18). A ticket may be assigned while still `New`.
  **Nothing may infer status from the presence of an assignee, or an assignee from the status.**
- `assignedUserId`, when set, must be an **active staff user in the ticket's department** — so a
  ticket can never be assigned to someone who could not then see it. Violation -> **`422
  assignee-out-of-department`**.
- **Managers may reassign across departments; agents may not.**

---

## Backend Tasks

### 1 — Domain: `Ticket`, the enums, and the SLA clock

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketStatus.cs`**

```csharp
public enum TicketStatus { New, Open, Pending, Resolved, Closed, Cancelled }   // A-5: six, and no others
```

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketPriority.cs`**

```csharp
public enum TicketPriority { Low, Medium, High, Urgent }                       // A-6
```

Both persist as **stable string codes** (api-design §2).

**Create file: `src/SupportCrm.Domain/Modules/Tickets/Ticket.cs`** — the eighteen fields of
data-model §2.6, with private setters. Behaviour on the entity:

- `Assign(Guid userId)` — sets `AssignedUserId`. **It does not touch `Status`.** Put the A-18
  sentence in a comment directly above it.
- `ChangePriority(TicketPriority)`, `ChangeCategory(string categoryCode)`.
- **No `SetStatus` method exists in this story** — Story 06 adds the guarded transition method, so
  no caller can bypass the state machine before it exists.

**Create file: `src/SupportCrm.Domain/Modules/Sla/SlaClock.cs`** — the 24/7 wall-clock arithmetic of
A-3, in the Domain because architecture §2.1 puts SLA target calculation there:

```csharp
public static (DateTimeOffset FirstResponseDueAt, DateTimeOffset ResolutionDueAt)
    ComputeAtCreation(DateTimeOffset createdAt, TicketPriority priority, SlaTargets targets);

// OQ-2 — undecided. See the blocked-decision box at the top of this plan.
public static void OnPriorityChanged(Ticket ticket, TicketPriority newPriority) =>
    throw new NotImplementedException("OQ-2: recompute vs freeze is an open business rule.");
```

`SlaTargets` is a Domain-side record of per-priority `FirstResponseHours` / `ResolutionHours`,
populated from configuration. **No business hours, no holiday calendar, no timezone arithmetic, no
pause on `Pending`** (A-3).

> **Planning note.** `data-model.md` §7 maps the SLA fields to Story 09, but §2.6 makes both due
> timestamps **required at creation**, so this story must compute them. Story 09 keeps breach
> detection, the latching flags, round-robin assignment and notifications. Recorded as **S9-5** in
> `00-implementation-plan.md`.

### 2 — Domain: `TicketActivity` — the history spine

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketActivity.cs`** and
`TicketActivityType.cs` with the twelve types of data-model §2.7: `Created`, `StatusChanged`,
`Assigned`, `PriorityChanged`, `CategoryChanged`, `Escalated`, `SlaBreached`, `MessagePosted`,
`InternalNotePosted`, `AiSuggestionOffered`, `AiSuggestionResolved`, `FeedbackSubmitted`.

Fields per data-model §2.7, including `ActorKind` (`User` | `System`) and `Visibility`
(`CustomerVisible` | `Internal`). **All setters private, no mutator, no delete** — append-only by
construction (architecture §2.5).

> **Planning note.** `data-model.md` §7 maps `TicketActivity` to Story 06, but **this story's own
> acceptance criterion** requires every assignment change to be recorded, and the `Created`,
> `Assigned`, `PriorityChanged` and `CategoryChanged` types are all this story's events. The entity
> and the single recorder are therefore introduced here; **Story 06 owns the state machine, the
> lifecycle types, the `/activity` read endpoint and the append-only tests.** Same placement rule
> as Story 02's `AuditEntry`; recorded as **S9-5**.

### 3 — Infrastructure: EF configuration and migration

**Create files:** `Persistence/Configurations/TicketConfiguration.cs` and
`TicketActivityConfiguration.cs`.

- Indexes from data-model §6, exactly: `Ticket(DepartmentId, Status)`,
  `Ticket(AssignedUserId, Status)`, `Ticket(CustomerId, CreatedAt)`, and the **two filtered
  due-date indexes** restricted to non-terminal, non-breached rows — without them the Story 09
  sweep scans every ticket on every tick. `TicketActivity(TicketId, OccurredAt)`.
- **`Ticket` has no branch column and no branch navigation.** Assert it in a comment.
- Enum conversions via `.HasConversion<string>()`.
- Complete the `Attachment.TicketId` relationship Story 04 left configured but unrelated.
- **Do not** index `Ticket.Priority` or `Ticket.CategoryCode` — data-model §6 excludes both on
  purpose; they are low-cardinality and always queried alongside a covered column.

```bash
dotnet ef migrations add Tickets -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 4 — Application: the department scoping helper (AD-5)

**This is the single most important file in the codebase. One implementation, one test suite.**

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketScope.cs`**

```csharp
public static class TicketScope
{
    // architecture §4.3: Customer -> own tickets; Agent -> own department;
    // Manager and Administrator -> all departments. Branch appears nowhere.
    public static IQueryable<Ticket> ForCaller(this IQueryable<Ticket> query, ICurrentUser caller) =>
        caller.Role switch
        {
            UserRole.Customer => query.Where(t => t.CustomerId == caller.CustomerId),
            UserRole.Agent    => query.Where(t => t.DepartmentId == caller.DepartmentId),
            _                 => query   // Manager, Administrator — unrestricted
        };
}
```

Rules, each stated as a comment in the file:

1. **Every** ticket query composes this — list, detail, dashboard, report, portal, AI assist.
2. **Write paths re-check on load.** Loading a ticket for modification goes through
   `LoadScopedAsync(id)`, which composes `ForCaller` and throws `NotFoundException` when the row is
   absent **or out of scope** — **fetch-then-authorize, never authorize-then-fetch-by-id**.
3. **No caller-supplied department id, customer id or role is ever trusted.** A `departmentId`
   *filter* narrows within what the caller may already see and can never widen it; another
   department's id is **not an error, it simply matches nothing** (api-design §4.3).
4. **`Branch` appears in no predicate in this file, and must never be added to it.**
5. **No EF global query filter is used** (AD-5) — a filter that is accidentally absent fails open,
   Managers and Administrators must bypass it, and reporting aggregates must not be silently
   narrowed.

### 5 — Application: `TicketService`

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketService.cs`**

| Method | Endpoint | Notes |
|---|---|---|
| `ListAsync` | `GET /tickets` | Filters `status`, `priority`, `categoryCode`, `assigneeId` (accepts the literal `me`), `departmentId`, `breached`, `q`. **Sort whitelist:** `resolutionDueAt`, `firstResponseDueAt`, `createdAt`, `priority`; anything else -> `400` (AP-15). **Default sort is SLA urgency:** `resolutionDueAt:asc` **with breached tickets first** |
| `CreateAsync` | `POST /tickets` | See below |
| `GetAsync` | `GET /tickets/{id}` | Through `LoadScopedAsync`; out of scope -> `404` |
| `PatchAsync` | `PATCH /tickets/{id}` | **`categoryCode` and `priority` only.** `status` is not a patchable field (AP-1) |
| `AssignAsync` | `POST /tickets/{id}/assignment` | See below |

**`CreateAsync` order of operations, which A-14 fixes and which must not be reordered:**

1. Validate `categoryCode` against the configured list -> unknown is `400`.
2. Resolve `departmentId`: **use the supplied value if the caller is staff and supplied one**;
   otherwise take it from the **category → department map** (A-14).
3. Validate `customerId` exists.
4. Compute `firstResponseDueAt` and `resolutionDueAt` via `SlaClock.ComputeAtCreation`.
5. Persist with `status = New`, `isUrgent = false` (staff create never accepts it, A-17).
6. Write a `Created` activity row.
7. **Automatic assignment does not happen here** — it is Story 09's, and it runs *after* creation
   and *before* the response, at the same seam. Leave a named extension point,
   `IAutoAssignmentPolicy`, with a **no-op implementation** registered now.

**`AssignAsync`:**

- Load through `LoadScopedAsync` (an out-of-department ticket is `404` before any assignment logic).
- The assignee must be an **active staff user whose `departmentId` equals the ticket's** ->
  otherwise `UnprocessableException("assignee-out-of-department")` -> **`422`**.
- An **Agent** may assign only within their own department; **Manager and Administrator may
  reassign across departments** (intake).
- **Status is not touched.** Write an `Assigned` activity row with old and new assignee display
  names in `oldValue` / `newValue`.

### 6 — Application: the activity recorder, and the Story 04 hand-back

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketActivityRecorder.cs`** — the
**one** writer of `TicketActivity` (architecture §2.5). It exposes `RecordAsync(...)` and
**no update or delete method**. Every Application service that changes a ticket calls it **on the
same path** that performs the change, inside the same unit of work.

**File: `src/SupportCrm.Application/Modules/Customers/CustomerService.cs`** — replace the
`openTicketCount` placeholder Story 04 left with the real aggregate: a **single grouped subquery**
over non-terminal tickets, not one query per row (api-design §6.3).

### 7 — Application: ticket attachments (completing Story 04's AC)

**File: `src/SupportCrm.Application/Modules/Customers/AttachmentService.cs`** — wire
`UploadForTicketAsync` / `ListForTicketAsync` to `LoadScopedAsync`, so a ticket attachment inherits
the ticket's department scope, and the AP-19 download endpoint can now resolve a ticket-owned file.

### 8 — Api: controller

**Create file: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — policy `RequireAgent`:

```
GET    /api/v1/tickets
POST   /api/v1/tickets
GET    /api/v1/tickets/{id}
PATCH  /api/v1/tickets/{id}
POST   /api/v1/tickets/{id}/assignment
GET    /api/v1/tickets/{id}/attachments
POST   /api/v1/tickets/{id}/attachments        (multipart/form-data, AP-13)
```

Response shapes exactly api-design §6.4. **`TicketListItem` carries only what the queue renders**
(ui-design §5.1) — do not return the full `Ticket` from the list endpoint.

The remaining ten endpoints under `/tickets/{id}` are added by Stories 06, 07, 11, 12 and 14 to
**this same controller or a sibling controller in the same folder**; do not create a second route
prefix.

### 9 — Seed data

**Create file: `Persistence/Seeders/TicketSeeder.cs` (`Order = 40`)** — enough tickets across
**both departments**, **all four priorities**, several categories and a mix of assigned and
unassigned, so filtering is demonstrable (intake AC). Include at least one ticket whose customer is
in a **different branch from the assigned agent** — that row is what Story 03's branch test needs.

### 10 — Tests

**Create file: `tests/SupportCrm.Tests/Tickets/TicketScopingTests.cs`** — the suite product-scope
T1-D demands, **run through the API as each role, bypassing the UI**:

1. An `Agent` `GET /tickets` returns **only** their own department's tickets, with no filter applied.
2. An `Agent` `GET /tickets/{id}` for an out-of-department ticket -> **`404`, not `403`** (AP-4).
3. The same for `PATCH` and `POST /assignment` — **the write path re-checks on load**, so a guessed
   id is refused.
4. A `Manager` sees tickets from both departments.
5. An `Agent` supplying `departmentId=<other department>` as a **filter** gets an **empty page**,
   not an error and not another department's rows.
6. A `Customer` calling any `/tickets` endpoint -> `403` (the staff path space is role-gated).

**File: `tests/SupportCrm.Tests/Organization/BranchIsNotABoundaryTests.cs`** — **enable the test
Story 03 left skipped**: an agent sees an in-department ticket whose customer belongs to a
different branch. Add the source-level assertion that `typeof(Ticket)` exposes no branch member.

**Create file: `tests/SupportCrm.Tests/Tickets/TicketCreationTests.cs`**

7. Omitting `departmentId` derives it from the **category map** (A-14).
8. An agent supplying `departmentId` **overrides** the map.
9. An unknown `categoryCode` -> `400`.
10. `isUrgent` in a staff create body -> `400` (**not** accepted-and-ignored, AP-10).
11. Both due timestamps are **non-null** on the created ticket and match the configured hours.
12. **Assigning a `New` ticket leaves `status = New`** (A-18) — assert the status explicitly.
13. Assigning an out-of-department agent -> `422 assignee-out-of-department`.

**Create file: `tests/SupportCrm.Tests/Domain/SlaClockTests.cs`** — a pure Domain unit test (no
database) that `ComputeAtCreation` adds the configured hours on a **24/7 clock**, across a weekend
and across midnight.

---

## Frontend Tasks

### 11 — Typed client

`core/api/tickets.client.ts` — `list(filters)`, `get(id)`, `create(body)`, `patch(id, body)`,
`assign(id, assignedUserId)`, plus the attachment calls. Filter and sort parameter **names mirror
the API exactly** (UI-9), so a screen never translates them.

### 12 — Ticket list — `/workspace/tickets` (ui-design §5.2)

- `PagedTable` on desktop, **stacked cards below the table breakpoint** (UI-10).
- `TicketFilterBar` bound to **URL query parameters** (UI-9), so a filtered list is shareable and
  survives a reload.
- **The department filter is fixed to the agent's own department and disabled**, with a hint
  explaining why; **enabled across all departments for Manager+**. Reuse
  `shared/components/department-filter/` from Story 03 — do not re-implement the rule.
- **No branch filter here** (A-2, T2-K).
- `StatusChip` and `PriorityChip` in `shared/` — one colour per A-5 status, four priority levels.
  `PriorityChip` is **staff-only and must never be imported by a portal component**.

### 13 — Ticket detail header and assign — `/workspace/tickets/:id` (ui-design §5.3)

Build the **header region and the customer panel** only; the thread, notes, activity, tasks and AI
regions are added by Stories 06, 07, 11, 12 and 14 into the region slots left here.

- Header: id, subject, `StatusChip`, priority · category · department, and the SLA line.
- **Assign** control (`Assign ▾`). **After assigning an unassigned `New` ticket the status chip
  must still read `New`** — assignee and status are rendered as **two independent facts** (A-18).
  Add a UI test or a reviewer note; this is the detail most likely to be got wrong.
- `422 assignee-out-of-department` renders inline: *"That agent is not in this ticket's
  department"* (ui-design §9).
- **Customer panel** — a side region on desktop, a **drawer at phone width**, reachable **without
  navigating away and without losing an unsent draft** (T1-C, UI-4). It renders
  `GET /customers/{id}` plus recent tickets derived from `GET /customers/{id}/timeline`; `GET
  /tickets` has **no `customerId` filter**, so do not invent one (**S9-6**).
- **`Transition ▾` and `Escalate` controls are not rendered in this story.** Story 06 adds them
  with their authority rules; a disabled-looking control with no behaviour is worse than none.
- Regions load **independently** so one slow call never blanks the screen (ui-design §5.3).

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~Tickets|FullyQualifiedName~BranchIsNotABoundary|FullyQualifiedName~SlaClock"`
   — every scoping test green.
3. **Negative model check:** `grep -ri "branch" backend/src/SupportCrm.Domain/Modules/Tickets/`
   returns nothing.
4. **Scoping by hand:** sign in as the Billing agent, note a Technical ticket id from the database,
   and `curl` `GET /api/v1/tickets/{thatId}` -> **`404`**.
5. **Creation:** `POST /api/v1/tickets` without `departmentId` for a `billing` category lands in the
   Billing department; both due timestamps are set.
6. **Regression:** `GET /customers` now returns a real `openTicketCount`; the customer timeline
   still returns an empty page (Story 06 fills it).
7. **Frontend:** `cd frontend && npm run build`; the list filters combine and survive a reload; the
   agent's department filter is disabled; assigning leaves the status chip on `New`.

---

## Done Criteria

- [ ] An agent can create a ticket for an existing customer with subject, description, category,
      priority and department.
- [ ] A ticket always references exactly one customer and one department.
- [ ] The ticket list filters by status, priority, category, assignee and department, and the
      filters combine.
- [ ] An Agent's list shows only their own department's tickets; a Manager's shows all.
- [ ] Category and priority come from **configuration**, and an unknown value is rejected.
- [ ] Priority offers exactly `Low`, `Medium`, `High`, `Urgent`.
- [ ] An agent can assign and reassign within their department; an out-of-department assignee is
      **refused server-side with `422`**.
- [ ] **Assignment does not change status** (A-18), asserted by a test.
- [ ] Every assignment change is recorded as a `TicketActivity` row.
- [ ] Out-of-scope tickets return **`404`, not `403`**, on read **and** on write.
- [ ] **`Ticket` has no branch column**, and an agent sees in-department tickets regardless of the
      customer's branch.
- [ ] Seed data demonstrates filtering across departments and priorities.
- [ ] **OQ-2 is not answered here.** `SlaClock.OnPriorityChanged` remains explicitly unimplemented
      until the decision is recorded.
- [ ] Story 04's ticket-attachment acceptance criterion is now met.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 06.**
