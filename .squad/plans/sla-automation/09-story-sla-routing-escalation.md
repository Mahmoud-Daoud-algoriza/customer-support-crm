# Story 09 — SLA targets, auto-assignment, escalation and notifications

> **Source of truth:** `docs/requirements.md` §5 · `docs/product-scope.md` T2-D, **A-3**, A-13, A-18, §9 question 5 · `docs/architecture.md` §2.1, §3, §6.3, **AD-6** · `docs/data-model.md` §2.2, §2.6 invariants 5–7, §2.12, §5 constraints 12–14, §6 · `docs/api-design.md` §5.10, §6.6, §7 · `docs/ui-design.md` §4.1, §5.8, §8, §11
> **Intake:** `.squad/stories/sla-automation/sla-routing-escalation/intake.md` · **Tier:** T2 — *if cut, Story 08 keeps its ordering (see below) and Story 15 loses its SLA tile*
> **Phase:** 5 — Automation and AI.

## Prerequisites

- **Story 05 completed:** `Ticket` with both due timestamps and both latching breach flags, the two
  **filtered due-date indexes**, and the `IAutoAssignmentPolicy` extension point left as a no-op.
- **Story 06 completed:** `TicketLifecycleService.EscalateAsync(ticketId, actorKind)` — **this story
  reuses that method and must not duplicate escalation logic** (intake).
- **Story 03 completed:** `Department.ManagerUserId` — the escalation recipient.
- **Story 16 Part A completed:** the per-priority **SLA targets** in configuration (A-3).

> ### ⚠ Blocked decision — **OQ-2** must be answered before task 4 is implemented
>
> On breach, priority rises one level. **What happens to the two due timestamps when priority
> changes is undecided** (`data-model.md` §2.6 invariant 6, §8):
>
> - **(a) Recompute** — `createdAt` + the new priority's hours. An escalation tightens the deadline,
>   and **a ticket can become breached as a direct consequence of the escalation that the breach
>   itself triggered.**
> - **(b) Freeze** — the deadlines stay as computed at creation.
>
> Both are consistent with A-3; they produce **materially different §9.2 attainment numbers**.
> Story 05 left `SlaClock.OnPriorityChanged` as an explicit `NotImplementedException("OQ-2")`.
> **Obtain the decision and implement it there — in that one method — before writing task 4.**
> Do not encode a behaviour anywhere else. Product-scope §9 question 5 (real SLA policy) is this
> question's parent and stays open regardless.

> ### ⚠ Open question with a **bounded** effect — **OQ-3**
>
> T2-D's rule is *"flag breached -> raise priority one level -> **notify the department manager**"*.
> `data-model.md` §2.2 records that a department may have **no** manager and **no fallback recipient
> is invented** — not all Managers, not Administrators, not a silent drop.
>
> **The breach flag and the priority raise are unaffected and must still occur.** Only the recipient
> is undetermined. Implement exactly that: publish when `ManagerUserId` is set; when it is null, log
> at `Warning` with the ticket and department ids and take no substitute action.

> ### ⚠ Open finding — **PF-5**
>
> `firstRespondedAt` is set **only** by the first outbound message, so **a ticket resolved without a
> reply is permanently first-response-breached**. `PROJECT-PROGRESS.md` §6.5 assigns PF-5 to this
> story and `api-design.md` §9 states *"No contract change; the behaviour question stays open."*
>
> **This plan does not close it.** Implement the sweep exactly as A-3 words it — a ticket whose
> `firstResponseDueAt` has passed with `firstRespondedAt` still null **is** first-response
> breached — and record the consequence in the plan's report so the decision is visible rather than
> discovered later. Changing it would be a change to A-3.

---

## Story Goal

All four lines of requirements §5, at the simplest defensible depth of A-3 and T2-D.

1. **Targets** — first-response and resolution, per priority, from configuration, on a **24/7
   wall-clock** starting at creation. **`Pending` does not pause it.**
2. **Automatic assignment** — **round-robin across active agents in the ticket's department**, at
   creation. **It does not move the ticket out of `New`** (A-18).
3. **Escalation** — exactly one rule, on breach: **flag breached -> raise priority one level ->
   notify the department manager**, reusing Story 06's escalation path.
4. **Alerts** — **in-app only**: a notification list and an unread badge, for exactly the four
   events of A-13.
5. Breach evaluation as a **periodic in-process check at coarse granularity** (AD-6). **No job
   queue, no broker, no external scheduler.**

---

## Context — Read These Files First

1. `docs/product-scope.md` **A-3 in full** — two clocks, per-priority targets in hours, 24/7,
   **no pause on `Pending`**, no business hours, no holidays, no per-branch timezones, one breach
   action, and *"timing precision is explicitly not a goal"*.
2. `docs/architecture.md` **AD-6** and §3 *"Two flows do not start in the browser"* — the hosted
   service ticks on a timer, resolves a **scoped** Application service, and **runs the same
   escalation path a manual escalation uses**.
3. `docs/data-model.md` §2.12 `Notification` — the **four** types and no others, recipient-scoped,
   `readAt` transitions **only** null -> timestamp; §2.6 invariants **5** (breach flags **latch**)
   and **7**; §6 — the two **filtered** due-date indexes exist for this sweep, *"without these it
   scans every ticket on every tick"*.
4. `docs/api-design.md` §5.10 — two endpoints, **no create endpoint**, recipient-scoped, and
   **`POST /notifications/read-all` was removed as unrequested surface (AP-18)**. Then §6.6 — the
   `unreadCount` at the top level of the envelope, which is what the badge renders.
5. `docs/ui-design.md` §5.8 (notification screen — **no mark-all-read control**), §4.1 (the bell in
   the staff shell), §11 (the OQ-2, OQ-3 and PF-5 rows).
6. `.squad/stories/sla-automation/sla-routing-escalation/intake.md` — thirteen acceptance criteria
   and the Out of scope list.

---

## Product rules (from story)

- **Two clocks per ticket:** first response and resolution. Targets per priority, in hours, from
  configuration.
- **The clock is 24/7 wall-clock from `createdAt`.** It does **not** pause on `Pending` and it
  ignores business hours, holidays and timezones. *"The 24/7 no-pause clock is a known
  simplification. Do not 'fix' it here."*
- **Breach flags latch.** Once true they never return to false, so history and SLA reporting stay
  honest even if priority later changes.
- **Round-robin across active agents in the ticket's department.** No skills, no load balancing, no
  capacity rules. **Never selects an agent outside the department or a deactivated user.**
- **Manual assignment always overrides** and is recorded in ticket history.
- **Auto-assignment does not change status.** The ticket stays `New` until an agent starts work
  (A-18) — so the customer's cancellation window is real (A-16).
- **Escalation raises priority exactly one level; `Urgent` stays `Urgent`.**
- **Notifications are in-app only**, for exactly `TicketAssigned`, `SlaBreached`,
  `TicketEscalated`, `CustomerReplied`. **No per-user preferences. No email, SMS or push** — that
  is T3-A.
- **There is no notification create endpoint.** Notifications are raised by the server only.

---

## Backend Tasks

### 1 — Domain: `Notification`

**Create file: `src/SupportCrm.Domain/Modules/Sla/NotificationType.cs`**

```csharp
// A-13 — exactly these four. Adding a fifth is a scope change, not a code change.
public enum NotificationType { TicketAssigned, SlaBreached, TicketEscalated, CustomerReplied }
```

**Create file: `src/SupportCrm.Domain/Modules/Sla/Notification.cs`** — `RecipientUserId`, `Type`,
`TicketId`, `CreatedAt`, `ReadAt?`. The **only** mutator is `MarkRead(DateTimeOffset now)`, which
**no-ops when `ReadAt` is already set** — data-model §5 constraint 22 allows null -> timestamp and
nothing else.

### 2 — Infrastructure: EF configuration and migration

**Create file: `Persistence/Configurations/NotificationConfiguration.cs`** — index
`Notification(RecipientUserId, ReadAt)` for the list and the unread badge (data-model §6); enum to
string; `Restrict` FKs to `User` and `Ticket`.

```bash
dotnet ef migrations add Notifications -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Replace the temporary notification publisher

**Create file: `src/SupportCrm.Infrastructure/Notifications/PersistentNotificationPublisher.cs`**
implementing the `INotificationPublisher` **Story 06 introduced**. Swap the DI registration in the
composition root from `LoggingNotificationPublisher` to this one.

> **This is the swap Story 06's plan promised to record.** The interface, its four types and every
> call site are unchanged — only the implementation. That is the whole point of having declared the
> abstraction in Application (AD-11): the escalation code written in Story 06 needs **no edit**.
> Delete `LoggingNotificationPublisher` in the same commit so there is one implementation, not two.

### 4 — Application: the SLA evaluator

**Create file: `src/SupportCrm.Application/Modules/Sla/SlaEvaluationService.cs`**

`EvaluateDueTicketsAsync(CancellationToken)` — one pass:

1. Query tickets where **status is non-terminal** and either
   (`firstResponseDueAt <= now && !firstResponseBreached && firstRespondedAt is null`) or
   (`resolutionDueAt <= now && !resolutionBreached && resolvedAt is null`).
   **This is the query the two filtered indexes exist for** — confirm the plan uses them
   (`EXPLAIN`/execution plan check in verification).
2. For each: set the relevant **latching** flag, write an `SlaBreached` activity row with
   **`actorKind = System` and `actorUserId = null`** — the SLA monitor is the **only** system actor
   in this design (data-model §2.7).
3. Call **`TicketLifecycleService.EscalateAsync(ticketId, ActorKind.System)`** — Story 06's method,
   which raises priority one level, leaves status unchanged, writes the `Escalated` row and
   publishes the manager notification. **Do not re-implement any of it.**
4. Publish an `SlaBreached` notification to the department manager **when `ManagerUserId` is set**
   (OQ-3 box above).
5. Apply the **OQ-2** decision through `SlaClock.OnPriorityChanged` — **the single method that
   encodes it**. Until OQ-2 is recorded, this call throws and the story cannot be completed. That
   is deliberate.
6. Commit once.

**Idempotence comes from the latching flags**, not from external state: a re-run finds nothing
because the flags are already set. No lock, no queue, no dedupe table.

### 5 — Api: the hosted service (AD-6)

**Create file: `src/SupportCrm.Api/BackgroundServices/SlaMonitorHostedService.cs`** — a
`BackgroundService` on a **periodic timer** whose interval comes from configuration
(`Sla:SweepIntervalSeconds`, default **60**). Each tick creates a **scope**, resolves
`SlaEvaluationService`, and calls it inside a `try`/`catch` that logs and continues — **a failed
tick must never stop the host**.

**It contains no business logic itself** (architecture §2.1, AD-6). Coarse granularity is what A-3
asks for: *minutes, not seconds.* No Hangfire, no Quartz, no broker.

### 6 — Application: round-robin auto-assignment

**Create file: `src/SupportCrm.Application/Modules/Sla/RoundRobinAssignmentPolicy.cs`**
implementing the `IAutoAssignmentPolicy` **Story 05 registered as a no-op**. Swap the registration.

- Candidates: `User` where `Role >= Agent`, `IsActive`, `DepartmentId == ticket.DepartmentId` —
  served by the `User(departmentId, isActive)` index.
- Rotation: pick the candidate with the **oldest** `Ticket(assignedUserId, createdAt)` most-recent
  assignment, or keep a per-department cursor. Either satisfies *"successive tickets go to different
  agents"*; pick one and comment why. **No skills, no load balancing, no capacity.**
- **No eligible candidate leaves the ticket unassigned.** That is a normal outcome, not an error —
  an agent assigns manually later.
- Called from `TicketService.CreateAsync` **after** creation and **before** the response, at the
  extension point Story 05 left.
- **It sets `assignedUserId` and nothing else. `status` stays `New`** (A-18). Put that sentence in
  the file.
- Writes an `Assigned` activity row and publishes a **`TicketAssigned`** notification to the
  assignee.

### 7 — Application: notification read service

**Create file: `src/SupportCrm.Application/Modules/Sla/NotificationService.cs`**

- `ListAsync(unreadOnly, page, pageSize)` — **recipient-scoped to `ICurrentUser.Id`**; another
  user's notification is `404`. Returns the standard paged envelope **plus `unreadCount` at the top
  level** (api-design §6.6). `ticketSubject` is projected so a row is readable without a call per
  notification.
- `MarkReadAsync(id)` -> `204`.
- **There is no create method and no bulk method.** `POST /notifications/read-all` was removed as
  unrequested surface (AP-18) — **do not add it back as a convenience.**

### 8 — Api: controller

**Create file: `src/SupportCrm.Api/Controllers/NotificationsController.cs`** — `[Authorize]`, no
role policy (any authenticated user reads their own):

```
GET   /api/v1/notifications          ?unreadOnly=true
POST  /api/v1/notifications/{id}/read     -> 204
```

### 9 — Seed data

**File: `Persistence/Seeders/TicketSeeder.cs`** — add tickets whose due dates are already in the
past, so the **first sweep after startup produces real breached rows** and Story 15's SLA tile has
non-trivial values. Comment the intent, because a seeded breach otherwise looks like a bug.

### 10 — Tests

**Create file: `tests/SupportCrm.Tests/Sla/SlaEvaluationTests.cs`**

1. A ticket past `resolutionDueAt` is flagged breached, its priority rises **one** level, its status
   is **unchanged**, and an `SlaBreached` activity row exists with **`actorKind: "System"` and a
   null actor**.
2. Running the sweep **twice** changes nothing the second time (latching).
3. A breached ticket whose priority is later lowered **keeps** its breach flag (flags latch).
4. An `Urgent` breached ticket stays `Urgent`.
5. **A ticket moved to `Pending` still breaches on schedule** — the A-3 no-pause rule, asserted
   explicitly.
6. A department with **no** manager: the flag and the priority raise still happen and **no
   notification row is created** — no substitute recipient (OQ-3).

**Create file: `tests/SupportCrm.Tests/Sla/AutoAssignmentTests.cs`**

7. A new ticket is auto-assigned to an active agent **in its own department**.
8. Successive tickets in one department go to **different** agents.
9. A **deactivated** agent and an agent from **another department** are never selected.
10. **The auto-assigned ticket's status is `New`** (A-18) — assert the status, not just the
    assignee.
11. A manual reassignment **overrides** the automatic one and writes an `Assigned` activity row.

**Create file: `tests/SupportCrm.Tests/Sla/NotificationTests.cs`**

12. All four A-13 events produce exactly one notification each, to the right recipient.
13. `GET /notifications` returns only the caller's own; another user's id -> `404`.
14. `unreadCount` matches the unread rows; `POST /{id}/read` -> `204`; a **second** read leaves
    `readAt` unchanged.
15. **No route exists at `/notifications/read-all`** — assert a `404`/`405` from the router
    (AP-18).

---

## Frontend Tasks

### 11 — Typed client and the badge

`core/api/notifications.client.ts` — `list(unreadOnly)`, `markRead(id)`.
`core/notifications/notification.store.ts` — a signal store holding `unreadCount`, refreshed on
navigation and after a mark-read. **Polling interval, if any, is coarse; nothing here is described
as real-time** (T3-B).

### 12 — Notification bell and screen

- `layout/staff-shell/` — fill the bell slot Story 08 left: the unread count from `unreadCount`,
  opening a panel of recent notifications. **The portal shell gets no bell** — A-13's four events
  are staff-facing and no requirement gives a customer an in-app feed (ui-design §4.2).
- `features/workspace/notifications/` — `/workspace/notifications`, the four types, each row linking
  to its ticket. **No mark-all-read control anywhere** (AP-18, ui-design §5.8).

### 13 — SLA indicator now shows real breaches

No component change. Confirm `SlaIndicator` (Story 08) renders the breached style once the sweep
sets the flags, and that the queue's default ordering surfaces breached tickets first.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Sla` — all fifteen green.
3. **Index usage:** capture the execution plan for the sweep query and confirm it uses
   `IX_Tickets_FirstResponseDueAt` / `IX_Tickets_ResolutionDueAt` rather than a table scan.
4. **The sweep runs:** start the stack, wait one interval, and confirm the log line and the newly
   breached seeded tickets. `GET /api/v1/tickets?breached=true` returns them.
5. **Auto-assignment:** create three tickets in one department through `POST /tickets` and confirm
   they land on different agents, **all still `New`**.
6. **No excluded infrastructure:** `grep -rniE "hangfire|quartz|rabbit|kafka|redis" backend/` returns
   nothing (product-scope §8, architecture §8).
7. **Regression:** Stories 05–08 suites still pass; the manual `POST /escalate` still works and now
   produces a **persisted** notification instead of a log line.
8. **Frontend:** the bell shows an unread count; opening a notification navigates to its ticket;
   there is no mark-all-read control.

---

## Done Criteria

- [ ] First-response and resolution targets are read from configuration **per priority**.
- [ ] Each ticket exposes its target times and remaining/overdue state on a **24/7 clock** from
      creation.
- [ ] **Moving a ticket to `Pending` demonstrably does not pause the clock.**
- [ ] A newly created ticket is auto-assigned round-robin to an active agent in its department, and
      successive tickets go to different agents.
- [ ] **Auto-assignment does not change status** — the ticket stays `New` (A-18).
- [ ] Auto-assignment never selects an agent outside the department or a deactivated user.
- [ ] A manual reassignment overrides the automatic one and is recorded in ticket history.
- [ ] On breach: flagged breached, priority up exactly one level (**`Urgent` stays `Urgent`**), and
      the department manager receives an in-app notification **when one is set**.
- [ ] The breach event is written to ticket history with **`actorKind = System`**.
- [ ] Notifications appear in an in-app list with an unread badge for the **four** A-13 events.
- [ ] Breach detection runs **periodically in-process** — no queue, broker or external scheduler.
- [ ] Story 08's queue ordering consumes this SLA data (it already did; breach flags now populate).
- [ ] SLA attainment data is queryable for Story 15.
- [ ] **OQ-2 is answered before this story ships**, and its answer lives in exactly one method.
- [ ] **OQ-3 is not answered here** — no fallback recipient invented.
- [ ] **PF-5 remains open and its consequence is reported**, not silently changed.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 10.**
