# Story 06 — Ticket status lifecycle, escalation and history

> **Source of truth:** `docs/requirements.md` §2.4–2.5 · `docs/product-scope.md` T1-B, **A-5**, **A-16**, A-18, R-13, R-14 · `docs/architecture.md` §2.1, §2.5, §4.2, AD-10 · `docs/data-model.md` §2.6 invariants 1–2b, §2.7, §5 constraints 7–9a, 15, 17–18 · `docs/api-design.md` §5.6, §6.4, §6.12, AP-1, AP-6, AP-7 · `docs/ui-design.md` §5.3, §11, UI-3, UI-8, UI-12
> **Intake:** `.squad/stories/ticket-management/ticket-lifecycle/intake.md` · **Tier:** T1 — cannot be cut
> **Phase:** 3 — Ticket core loop.

## Prerequisites

- **Story 05 completed:** `Ticket`, `TicketActivity`, `TicketActivityRecorder`, `TicketScope` and
  `LoadScopedAsync`, the tickets controller.
- **Story 02 completed:** `IAuditRecorder` — ticket lifecycle actions are auditable actions
  (`audit-configuration` intake, architecture §2.4).

> ### ✅ Decision taken — **OQ-3 is closed by A-21** (2026-08-31). No longer open.
>
> Escalation *"notifies the department manager"* (A-5, T2-D, intake AC), but
> `data-model.md` §2.2 makes `Department.ManagerUserId` **optional**. The recipient in that case is
> now defined ([product-scope.md](../../../docs/product-scope.md) §7, **A-21**) as a cascade:
>
> 1. **The department's own manager**, when set and that user is still **active** and still holds
>    `Manager` or `Administrator` — the eligibility §2.2's invariant already requires.
> 2. Otherwise **every active `Manager`**.
> 3. Otherwise **every active `Administrator`**.
> 4. Otherwise **nobody** — and the escalation still happens.
>
> **The priority raise, the status non-change and the `Escalated` activity row always occur**, on
> every rung. A missing manager suppresses a notification, never an escalation.
>
> **Do not re-express this rule here.** It is implemented once, in
> `Application/Modules/Sla/EscalationRecipientPolicy.cs` behind **`IEscalationRecipientPolicy`**,
> which **already exists** — it was built with the decision, ahead of both callers. Resolve
> recipients through it and publish to the ids it returns. **The fallback is logged at `Warning`
> inside the policy**, so this story and Story 09 cannot report the same condition differently.
>
> **No contract change comes with this decision** — no endpoint, payload, response field or column.
> The cascade only ever notifies `Manager` and `Administrator`, both of which already hold
> cross-department authority (A-4, A-16), so a `Notification`'s `ticketId` is always readable by its
> recipient.
>
> OQ-3 reached **this** story as well as Story 09 because the **manual** escalation action lives
> here — recorded as **S9-3** in `00-implementation-plan.md`, and why the answer was needed at this
> point rather than at Story 09.

---

## Story Goal

The second half of requirements §2: the state machine, escalation, and the ticket's own audit trail.

1. **One fixed status set with enforced transitions** (A-5), expressed **once**, in the Domain, so
   no caller can bypass it. An illegal transition is **refused by the server**, not hidden in the UI.
2. **The A-16 authority matrix** enforced in the Application layer — *which role may invoke which
   legal transition*, distinct from *which transitions are legal at all*.
3. **Escalation as an action, not a status** (AP-7): priority up exactly one level, `Urgent` stays
   `Urgent`, **status unchanged**, an `Escalated` activity row, and a manager notification.
4. **Ticket history** — the append-only activity trail, readable at
   `GET /tickets/{id}/activity`, with actor, timestamp and before/after values.
5. Complete the **customer interaction timeline** Story 04 left against an empty ticket set.

**Not in this story:** the *automatic* `Pending -> Open` on a customer reply — the **rule** is
implemented here in the Domain and the Application transition path, but its **trigger** is the
portal message endpoint of [Story 07](07-story-ticket-intake-messaging.md), which calls into it.

---

## Context — Read These Files First

1. `docs/product-scope.md` **A-5 in full** — the transition graph, the per-status paragraphs, the
   `Pending` bullet, the escalation bullet, and the **actor-attribution paragraph (R-14)**. Then
   **A-16 in full** — the eight-row authority table and its three consequences.
2. `docs/data-model.md` §2.6 invariants **1, 2, 2a, 2b, 4, 5**, §2.7 (activity types, `actorKind`,
   `visibility`, the `messageId`/`internalNoteId` iff-rules), §5 constraints **7, 8, 9, 9a, 11b,
   15, 17, 18**.
3. `docs/api-design.md` §5.6 — `POST /tickets/{id}/transition` and its **enforcement order**:
   role gate (`403`) -> scope (`404`) -> **A-16 authority** (`403 transition-not-permitted`) ->
   **A-5 legality** (`409 illegal-transition`, carrying `allowedTransitions` in the problem
   detail). Then the legality/authority table, `POST /escalate`, and §6.4 `Activity entry`.
4. `docs/architecture.md` §2.5 — ticket history is **separate from the audit log by decision**
   (AD-10), **append-only**, one recorder, and **visibility is a property of the entry**.
5. `docs/ui-design.md` §5.3 (transition menu, escalate as a separate control, terminal statuses
   disabling the composer), **UI-3** (transitions computed client-side; see F-1 below), **UI-8**
   (no optimistic UI for lifecycle changes), **UI-12** (confirm dialogs name the effect), and
   §11 (the ~~OQ-3~~ row — **the wording constraint is lifted by A-21**; the dialog may now say
   *a manager* will be notified).
6. `.squad/stories/ticket-management/ticket-lifecycle/intake.md` — thirteen acceptance criteria;
   every one is a Done Criterion below.

---

## Product rules (from story)

**A-5 — legal transitions (what the Domain enforces):**

```
New ──▶ Open ──▶ Pending ──▶ Resolved ──▶ Closed
         ▲         │             │
         │         │             └── reopen ──────────┐  (Resolved → Open, explicit)
         │         └── customer reply ────────────────┤  (Pending → Open, AUTOMATIC)
         └──────────────────────────────────────────────┘

Any non-terminal status ──▶ Cancelled
```

**A-16 — who may invoke each (what the Application enforces):**

| Transition | Customer | Agent / Manager / Administrator |
|---|---|---|
| `New → Open` | ✖ directly — a **reply on a `Pending` ticket** triggers it automatically | ✔ |
| `Open → Pending` | ✖ | ✔ |
| `Pending → Open` | ✖ directly; **automatic on customer reply** (R-13) | ✔ |
| `Open → Resolved`, `Pending → Resolved` | ✖ | ✔ |
| `Resolved → Closed` | ✖ | ✔ **manual only — no timer, no scheduled job** |
| `Resolved → Open` (reopen) | ✔ own | ✔ |
| any non-terminal `→ Cancelled` | ✔ own, **only while `New`** | ✔ |
| **Escalate** (an action, not a transition) | ✖ | ✔ |

- **`Closed` and `Cancelled` are terminal.** No further transitions, messages or notes -> `409`.
- **Escalation raises priority exactly one level; `Urgent` stays `Urgent`; status is unchanged.**
  There are no escalation tiers and no L1/L2/L3 levels.
- **Every transition is recorded with actor, timestamp and before/after values.**
- **R-14 — the automatic `Pending -> Open` is attributed to the replying customer**, with
  `actorKind = User`. **It is not a `System` actor**: the SLA monitor is the only system actor in
  this design, and attributing a customer-caused change to the system would make ticket history
  less truthful.
- **History is append-only.** No UI and no API path edits or deletes an entry.
- **Ticket history is not the audit log** (AD-10). Both stay independently queryable.

---

## Backend Tasks

### 1 — Domain: the transition state machine

**Create file: `src/SupportCrm.Domain/Modules/Tickets/TicketLifecycle.cs`** — the A-5 graph, in one
place, as data:

```csharp
public static class TicketLifecycle
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Legal =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.New]      = [TicketStatus.Open,     TicketStatus.Cancelled],
            [TicketStatus.Open]     = [TicketStatus.Pending,  TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.Pending]  = [TicketStatus.Open,     TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.Resolved] = [TicketStatus.Open,     TicketStatus.Closed,   TicketStatus.Cancelled],
            [TicketStatus.Closed]   = [],      // terminal
            [TicketStatus.Cancelled]= [],      // terminal
        };

    public static bool IsLegal(TicketStatus from, TicketStatus to) => Legal[from].Contains(to);
    public static IReadOnlyList<TicketStatus> LegalFrom(TicketStatus from) => Legal[from];
    public static bool IsTerminal(TicketStatus s) => Legal[s].Length == 0;
}
```

`LegalFrom` exists because api-design §5.6 requires `allowedTransitions` **inside the `409` problem
detail** — that is the only place the contract publishes it today (see F-1 below).

**Create file: `src/SupportCrm.Domain/Modules/Tickets/Escalation.cs`**

```csharp
// A-5: raise priority exactly one level; Urgent stays Urgent.
public static TicketPriority RaiseOneLevel(TicketPriority current) =>
    current == TicketPriority.Urgent ? TicketPriority.Urgent : current + 1;
```

**File: `src/SupportCrm.Domain/Modules/Tickets/Ticket.cs`** — add the guarded mutator Story 05
deliberately withheld:

```csharp
public void TransitionTo(TicketStatus target, DateTimeOffset now)
{
    if (!TicketLifecycle.IsLegal(Status, target))
        throw new IllegalTransitionException(Status, target, TicketLifecycle.LegalFrom(Status));
    Status = target;
    if (target is TicketStatus.Resolved)                     ResolvedAt = now;
    if (target is TicketStatus.Closed or TicketStatus.Cancelled) ClosedAt = now;
}
```

`ResolvedAt` / `ClosedAt` are lifecycle side effects and are **never accepted from a client**
(api-design §7). **There is no path that writes `Status` without passing this method.**

### 2 — Application: the A-16 authority matrix

**Create file: `src/SupportCrm.Application/Modules/Tickets/TransitionAuthority.cs`** — the second
of the two rules, kept **separate from legality on purpose** so the two failure modes stay
distinguishable (`403` vs `409`):

```csharp
public static bool MayInvoke(ICurrentUser caller, Ticket ticket, TicketStatus target) =>
    caller.Role switch
    {
        UserRole.Customer =>
            (target is TicketStatus.Cancelled && ticket.Status is TicketStatus.New) ||   // A-16 + A-18
            (target is TicketStatus.Open      && ticket.Status is TicketStatus.Resolved),// reopen own
        _ => true    // Agent, Manager, Administrator — the full matrix; scope is enforced separately
    };
```

**Customers cannot close** a ticket — they can only reopen a `Resolved` one. Put that sentence in
the file; A-16 calls it out as a deliberate consequence.

### 3 — Application: `TicketLifecycleService`

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketLifecycleService.cs`**

`TransitionAsync(ticketId, targetStatus)` — **the enforcement order is fixed by api-design §5.6 and
must not be rearranged**:

1. Role gate — the controller policy. `403`.
2. **Scope** — `LoadScopedAsync(ticketId)`. Out of scope or missing -> **`404`** (AP-4).
3. **A-16 authority** — `TransitionAuthority.MayInvoke` -> `ForbiddenException("transition-not-permitted")` -> **`403`**.
4. **A-5 legality** — `ticket.TransitionTo(...)` -> `ConflictException("illegal-transition")` -> **`409`**,
   with **`allowedTransitions` added to the problem-details extensions** (api-design §6.12).
5. Write a `StatusChanged` activity row: `oldValue` = previous status, `newValue` = target,
   `actorKind = User`, actor = `ICurrentUser`.
6. Write an audit entry `TicketStatusChanged` through `IAuditRecorder` (architecture §2.4).
7. Return `200` with the **updated ticket** (api-design §5.6).

All of it in **one unit of work, committed once** (architecture §3).

**`ApplyAutomaticCustomerReplyTransitionAsync(ticket, replyingCustomerUser)`** — the R-13/R-14 rule,
written here because it is a lifecycle rule, and **called** by Story 07's portal message endpoint:

- Fires **only** when `ticket.Status == Pending`. A reply on `New` leaves it `New`; a reply on
  `Resolved` does **not** reopen it — reopening `Resolved` stays the explicit transition of A-16.
- Runs **in the same transaction as the message** (data-model §2.6 invariant 2b).
- Writes a `StatusChanged` row **attributed to the replying customer**, `actorKind = User`
  (**R-14**). Add the R-14 justification as a comment: *not a `System` actor — the SLA monitor is
  the only system actor in this design.*
- **Generates no notification of its own**, because A-13 defines exactly four notification types and
  none of them is a status change (data-model §2.8).

`EscalateAsync(ticketId)` — AP-7, **its own endpoint, never part of `/transition`**:

- Scope check, then `Escalation.RaiseOneLevel`. **Status is not touched.**
- Write an `Escalated` activity row with the old and new priority, and an audit entry
  `TicketEscalated`.
- Raise a `TicketEscalated` notification to **the recipients `IEscalationRecipientPolicy` returns**
  (**A-21** — see the decision box above). One call, no branching here: the cascade and its
  `Warning` log live in the policy. Publish one notification per returned id; an empty list is a
  valid result and **must not** stop the escalation.
- Customers -> `403` (A-16). Returns `200` with the ticket.
- **Story 09 calls this same method from the breach path** — the intake requires the automatic
  trigger to *reuse this escalation path rather than duplicating it*. Keep the signature usable by
  a system caller: `EscalateAsync(ticketId, ActorKind actorKind = ActorKind.User)`.

### 4 — Application: the notification seam

**Create file: `src/SupportCrm.Application/Modules/Sla/INotificationPublisher.cs`**

```csharp
public interface INotificationPublisher
{
    Task PublishAsync(Guid recipientUserId, NotificationType type, Guid ticketId, CancellationToken ct);
}
```

`NotificationType` is the **four values of A-13 and no others**: `TicketAssigned`, `SlaBreached`,
`TicketEscalated`, `CustomerReplied` (data-model §2.12).

**Create file: `src/SupportCrm.Infrastructure/Notifications/LoggingNotificationPublisher.cs`** — a
temporary implementation that logs. **Story 09 replaces the registration with the persistent
implementation that writes `Notification` rows**; the interface, the call sites and the type set do
not change.

> This is exactly what the `ticket-lifecycle` intake prescribes: *"Notification delivery is defined
> by `sla-routing-escalation` (in-app only, A-13); if that story is not yet planned, raise the
> manager notification through the same abstraction it will own."* **Record the swap in Story 09's
> plan** — it is recorded there, task 5.

### 5 — Application: ticket history read

**Create file: `src/SupportCrm.Application/Modules/Tickets/TicketActivityQueryService.cs`** —
`GET /tickets/{id}/activity`, paged, ordered by `occurredAt`, using the
`TicketActivity(TicketId, OccurredAt)` index.

**Staff read: internal entries are included** (api-design §5.6, *"Full history, internal entries
included"*). The **customer-facing** filter lives in Story 04's timeline projection and Story 13's
portal reads, not here.

Response shape exactly api-design §6.4 `Activity entry`. **`actor` is null exactly when `actorKind`
is `System`** — assert that invariant in the projection.

### 6 — Application: complete the customer timeline

**File: `src/SupportCrm.Application/Modules/Customers/CustomerTimelineService.cs`** — replace the
`// Story 06: join TicketActivity here` marker Story 04 left with the real projection over the
customer's tickets and their activity, newest first.

**Re-assert the two exclusions:** no entry whose `visibility` is `Internal`, and
`TicketInternalNote` is not joined at all.

### 7 — Api: endpoints

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add, all `RequireAgent`:

```
POST  /api/v1/tickets/{id}/transition       { "targetStatus": "Resolved" }  -> 200 Ticket
POST  /api/v1/tickets/{id}/escalate         (no body)                       -> 200 Ticket
GET   /api/v1/tickets/{id}/activity                                          -> paged Activity entry
```

**`status` is not, and never becomes, a field on `PATCH /tickets/{id}`** (AP-1). A single
`/transition` endpoint keeps the matrix in one place and returns one `409` shape for every illegal
transition (AP-6).

The **portal** transition endpoint (`POST /portal/tickets/{id}/transition`) is published by
[Story 13](../customer-portal/13-story-portal-self-service.md) and calls **this same service**.

### 8 — Tests

**Create file: `tests/SupportCrm.Tests/Domain/TicketLifecycleTests.cs`** — pure Domain unit tests,
no database:

1. Every legal edge in the A-5 graph succeeds.
2. **Every illegal edge throws** — enumerate the full 6×6 matrix and assert exactly the legal set.
3. `Closed` and `Cancelled` accept **no** outgoing transition.
4. `RaiseOneLevel`: `Low->Medium`, `Medium->High`, `High->Urgent`, **`Urgent->Urgent`**.

**Create file: `tests/SupportCrm.Tests/Tickets/TransitionAuthorityTests.cs`** — through the API,
**as each role, bypassing the UI**:

5. A customer cancelling their own **`New`** ticket -> `200`.
6. A customer cancelling their own **`Open`** ticket -> **`403 transition-not-permitted`**
   (A-16 + A-18) — *not* `409`; the transition is legal, the caller is not permitted.
7. A customer attempting `-> Closed` -> `403`.
8. A customer reopening their own `Resolved` ticket -> `200`.
9. An agent transitioning `New -> Resolved` -> **`409 illegal-transition`**, with
   `allowedTransitions` present in the body.
10. A customer cancelling **another customer's** ticket -> **`404`** (scope beats authority; AP-4).
11. An agent escalating a `High` ticket: priority becomes `Urgent`, **`status` is unchanged**, an
    `Escalated` row exists, and the department manager received a notification.
12. Escalating an `Urgent` ticket leaves it `Urgent`.
13. A customer calling `/escalate` -> `403`.

**Create file: `tests/SupportCrm.Tests/Tickets/TicketHistoryTests.cs`**

14. `GET /tickets/{id}/activity` shows status, assignment, priority and category changes with
    actor, timestamp and before/after values.
15. **Append-only, proven by reflection:** `TicketActivityRecorder` exposes **no** method whose name
    contains `Update` or `Delete`, and `SupportCrmDbContext` has no code path that removes a
    `TicketActivity`. Assert it as a test, not as a comment.
16. The customer's interaction timeline now reflects these entries **and contains no `Internal`
    entry**.
17. `TicketActivity` and `AuditEntry` are **independently queryable** and neither is derived from
    the other (AD-10): a `UserRoleChanged` audit entry has no ticket, and a `MessagePosted`
    activity row has no audit entry.

---

## Frontend Tasks

### 9 — Transition menu and escalate control (ui-design §5.3)

- **`TransitionMenu`** in `shared/components/` — offers only transitions **legal from the current
  status** *and* **permitted for the caller's role**, computed client-side per **UI-3**.

  > **F-1 is open and stays open.** The ticket payload does not expose `allowedTransitions`
  > (api-design §6.4), so this component reimplements the A-5 legality set and the A-16 authority
  > matrix. **The server remains the authority** and a wrong offer gets `403`/`409`. Put the A-5
  > and A-16 tables in **one** TypeScript constant file, `shared/lifecycle/transition-matrix.ts`,
  > with a header comment naming F-1, so that if the API later returns `allowedTransitions` exactly
  > one file is deleted. **Do not add the field to the API in this story** — that is a Stage 7
  > decision to be taken explicitly.

- **`Escalate` is a separate control, never inside the transition menu** — escalation is an action,
  not a status change (AP-7, A-5).
- **Confirmation dialogs name the effect** (UI-12). The escalate dialog says *priority rises one
  level, status is unchanged*, and — since **A-21** closed OQ-3 — **may now also say a manager will
  be notified**. Word it as *"a manager"*, not *"the department manager"*: one unconditional
  sentence is true on every rung of the cascade, so the screen needs no conditional and **no new
  payload field telling it which rung fired** (ui-design §11).
- **No optimistic UI** (UI-8): the status chip changes **after** the server confirms, because a
  transition can be refused.
- `409` renders contextually — *"This ticket has already been closed"* — from the problem `type`,
  never from the server's `detail` (ui-design §9).
- `Closed` and `Cancelled` **disable the composer, the note field and the transition menu, with a
  reason line** rather than silently inert controls.

### 10 — Activity region

`features/workspace/ticket-detail/activity-region/` — a chronological list with actor, timestamp and
before/after values. A `System`-actor row (Story 09's SLA breach) renders as *"System"*; **the
automatic `Pending -> Open` row renders the customer as the actor** (R-14), and no "system" label
appears on it.

### 11 — Customer timeline

`features/workspace/customer-detail/timeline-region/` now populates. Nothing to change beyond
removing the empty-state-only path.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Domain tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Domain.TicketLifecycle` — the
   full 6×6 legality matrix green.
3. **Authority and history tests pass:**
   `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~TransitionAuthority|FullyQualifiedName~TicketHistory"`.
4. **By hand:** `POST /api/v1/tickets/{id}/transition` with `{"targetStatus":"Closed"}` on a `New`
   ticket -> `409` whose body contains `allowedTransitions: ["Open","Cancelled"]`.
5. **Escalate:** escalate a `Medium` ticket -> `200`, priority `High`, **status unchanged**, one new
   `Escalated` activity row, one log line from `LoggingNotificationPublisher`.
6. **Regression:** Story 05's scoping tests still pass; `GET /customers/{id}/timeline` now returns
   entries and still excludes internal ones.
7. **Frontend:** the transition menu offers only permitted targets per role; escalate confirms with
   wording that **states the notification** — *"a manager will be notified"*, true on every rung of
   A-21's cascade; terminal tickets disable the controls with a reason. *(This step said "makes **no**
   notification claim" before **A-21** closed OQ-3 and ui-design §11 lifted the constraint — finding
   **I-24**.)*

---

## Done Criteria

- [ ] The six statuses exist exactly as listed; no others.
- [ ] Legal transitions succeed; **every illegal transition is refused server-side**, verified by a
      test that bypasses the UI.
- [ ] `Resolved` can be reopened to `Open` by either an agent or the ticket's customer.
- [ ] `Closed` and `Cancelled` are terminal: no further replies or transitions are accepted.
- [ ] A ticket in `New` may carry an assignee; status and assignee are independent (A-18).
- [ ] The **A-16 authority matrix** is enforced server-side, with `403` for authority and `409` for
      legality — two distinct failures.
- [ ] Escalation raises priority exactly one level (**`Urgent` stays `Urgent`**), leaves status
      unchanged, writes a history entry, and notifies **the recipients A-21 resolves** — the
      department's manager, else every active `Manager`, else every active `Administrator`, else
      nobody. *(This criterion said "when one is set" before **A-21** closed OQ-3 on 2026-08-31;
      that wording described the interim behaviour the decision replaced — finding **I-24**.)*
- [ ] Ticket history shows every status, assignment, priority and category change with actor,
      timestamp and before/after values.
- [ ] History is **append-only**: no UI or API path edits or deletes an entry, proven by test.
- [ ] The customer's interaction timeline reflects these entries and excludes internal ones.
- [ ] Ticket history and the audit log remain **independently queryable** (AD-10).
- [ ] The automatic `Pending -> Open` rule is implemented and attributed to the **replying
      customer** with `actorKind = User` *(its trigger and its end-to-end test land in Story 07)*.
- [ ] **A-21 is applied, not re-implemented.** Escalation resolves recipients through
      `IEscalationRecipientPolicy` — the cascade appears nowhere in this story's own code — and the
      escalate dialog may claim that *a manager* will be notified. **OQ-3 was closed on 2026-08-31
      and is not reopened here.**
- [ ] **F-1 remains open.** The client matrix lives in exactly one file, labelled with F-1.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 07.**
