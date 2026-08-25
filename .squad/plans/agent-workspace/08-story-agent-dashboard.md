# Story 08 — Agent dashboard queue, customer context and quick replies

> **Source of truth:** `docs/requirements.md` §4.1, §4.2, §4.4 · `docs/product-scope.md` T1-C, T3-F, A-1, A-3 · `docs/architecture.md` §2.2, §2.3, §6.3 · `docs/data-model.md` §2.6, §6 · `docs/api-design.md` §5.1, §5.6, §5.5, §6.4, §6.9, AP-15, AP-17 · `docs/ui-design.md` §4.1, §5.1, §5.3, §8, §9, §10.3, UI-2, UI-4, UI-7, UI-10
> **Intake:** `.squad/stories/agent-workspace/agent-dashboard/intake.md` · **Tier:** T1 — cannot be cut. **This is the primary demo surface for the agent actor.**
> **Phase:** 4 — Channels and workspace.

## Prerequisites

- **Story 05 completed:** `GET /tickets` with the filter set, the **sort whitelist** and the
  **SLA-urgency default sort**.
- **Story 06 completed:** transition menu, escalate, activity region.
- **Story 07 completed:** the thread and the shared `reply-composer`.
- **Story 04 completed:** `GET /customers/{id}` and `GET /customers/{id}/timeline` for the customer
  panel.
- **Story 16 Part A completed:** `GET /config/staff` publishes the **quick-reply library** (T1-C,
  architecture §6.3, AP-17).

> **This story does *not* hard-depend on Story 09.** `PROJECT-PROGRESS.md` §3 lists Story 09 among
> this story's dependencies and the intake offers a *"order by priority then age and swap in the
> SLA ordering when it lands"* fallback. **That fallback is not needed.** `data-model.md` §2.6 makes
> `firstResponseDueAt` and `resolutionDueAt` **required at creation**, and Story 05 computes them —
> so the real SLA-urgency ordering is available now. What Story 09 adds is the *population* of the
> latching breach flags, which default to `false` and are already returned by `TicketListItem`.
> The queue is therefore built once, correctly, with no swap to record. Recorded as **S9-8** in
> `00-implementation-plan.md`.

> ### ⚠ Contradiction found in audit — **S9-1** — the dashboard task region has no endpoint
>
> `ui-design.md` §5.1 places *"Open tasks and overdue tasks for this agent"* as a secondary region
> on **My queue**, §13 lists a *"queue task list"* under Story 14, and `data-model.md` §6 provides
> the index `TicketTask(assignedUserId, isDone, dueAt)` *"Open and overdue tasks on the dashboard"*.
> **`api-design.md` publishes no endpoint that lists a user's tasks across tickets** — §5.6 has only
> `GET`/`POST /tickets/{id}/tasks`.
>
> **No endpoint is invented here.** This story builds the queue **without** the task region and
> leaves the slot with a `<!-- Story 14 / S9-1 -->` marker. The decision — publish a
> cross-ticket task endpoint (a Stage 7 change) or drop the region from the dashboard (a Stage 8
> change) — must be taken before [Story 14](14-story-tasks-internal-notes.md) is implemented, and
> is recorded in that plan's blocked-decision box.

---

## Story Goal

The agent's daily surface, at T1 depth.

1. **My queue** — the logged-in agent's own tickets, **ordered by SLA urgency with breached tickets
   first**, as the agent's landing screen (UI-2).
2. **Customer information in context** — the customer panel reachable **from inside a ticket without
   losing place**, and specifically without losing an in-progress reply draft.
3. **Quick replies** — a configured canned-response library that inserts **editable** text into the
   draft and **never sends on its own**.
4. **Phone-width usability** for the queue and the ticket view (T3-F, A-1).

**Backend work is small by design.** Every endpoint this screen needs already exists; this story is
overwhelmingly front-end.

---

## Context — Read These Files First

1. `docs/ui-design.md` **§5.1 in full** (My queue: purpose, API, roles, rows, states, responsive
   behaviour), **§5.3** (ticket detail regions and the customer panel), **§4.1** (staff shell),
   **§8** (the shared component table — `SlaIndicator`, `PagedTable`, `CustomerPanel`,
   `ReplyComposer`), **§9** (states), **§10.3** (phone-width behaviour for the three T3-F surfaces).
2. `docs/api-design.md` §5.6 — `GET /tickets` filters, the **sort whitelist**, `assigneeId=me`, and
   *"Default sort is SLA urgency — `resolutionDueAt:asc` with breached tickets first"*. Then §6.4
   `TicketListItem` — **note it carries `resolutionDueAt` but not `firstResponseDueAt`**.
3. `docs/api-design.md` §5.1 and §6.9 `StaffConfig` — quick replies come from `GET /config/staff`,
   which is **Agent and above**; a Customer calling it gets `403` (AP-17).
4. `docs/architecture.md` §2.2 (front-end structure; **feature components never call `HttpClient`
   directly**) and §2.3 (RTL and responsive rules).
5. `.squad/stories/agent-workspace/agent-dashboard/intake.md` — nine acceptance criteria, and the
   Out of scope list (no bulk actions, no saved views, no @mentions, no presence).
6. Read [05-story-ticket-core.md](../ticket-management/05-story-ticket-core.md) task 13 — this story
   fills the region slots that plan deliberately left empty.

---

## Product rules (from story)

- **The queue ordering is the API's default sort, not a client re-sort** (ui-design §5.1). The
  client must not fetch and reorder — the server owns urgency.
- **`assigneeId=me`** produces the agent's own queue. The client sends the literal `me`; it never
  reads its own user id and passes it as a filter, because a caller-supplied identity is never
  trusted (architecture §4.3 point 1).
- **Quick replies come from configuration** (T2-I: no configuration UI). There is no quick-reply
  entity and no CRUD (data-model §2.16).
- **Inserting a quick reply never sends the message by itself** (A-8's discipline applied to canned
  text too, and the intake's own AC).
- **The customer panel must not navigate away.** A side region on desktop, a **drawer** at phone
  width — never a route change, because an unsent draft would be lost (UI-4).
- **An agent sees no ticket from another department anywhere on this surface** — enforced by
  Story 05's `TicketScope`, not by this screen.

---

## Backend Tasks

**Almost none.** Every endpoint exists. Two small additions only:

### 1 — Quick replies in `StaffConfig`

**File: `src/SupportCrm.Application/Configuration/QuickReplyOptions.cs`** — created by Story 16
Part A; confirm it is bound, validated at startup and surfaced by `GET /config/staff` as
`quickReplies: [ { id, title, body } ]` (api-design §6.9). **If Story 16 Part A left it as a stub,
complete it here** and note the hand-back in that plan.

### 2 — Confirm the default sort, and test it

**File: `src/SupportCrm.Application/Modules/Tickets/TicketService.cs`** — assert in code and in a
test that, with **no `sort` parameter**, `ListAsync` orders by
`resolutionBreached DESC, resolutionDueAt ASC`. *Breached first, then soonest due.*

**Create file: `tests/SupportCrm.Tests/Tickets/QueueOrderingTests.cs`**

1. With no `sort`, a breached ticket with a **later** due date sorts **above** an unbreached ticket
   with an earlier one.
2. `assigneeId=me` returns only the caller's assigned tickets, and no other agent's.
3. `sort=subject:asc` -> **`400`** (not on the whitelist, AP-15).
4. `GET /config/staff` as `Customer` -> **`403`**; as `Agent` -> `200` with a non-empty
   `quickReplies` array.

**No new endpoint is added by this story.**

---

## Frontend Tasks

### 3 — `SlaIndicator` — `shared/components/sla-indicator/`

Renders remaining or overdue time from `resolutionDueAt`, with **breached visually distinct**.
**Staff only — it is never imported by a `features/portal/` component** (UI-11).

- The queue uses `resolutionDueAt` and the two breach flags; `TicketListItem` does **not** carry
  `firstResponseDueAt`, so the queue indicator is resolution-based. The ticket **detail** SLA region
  shows both clocks, from the full `Ticket` payload.
- **`firstRespondedAt` may be `null` on a resolved ticket** (**PF-5**). Render **"—"**, and
  **not "breached" and not "0"** (ui-design §11). PF-5 stays open for Story 09; this is the display
  rule the design already fixes, not an answer to it.
- Numerals and countdowns stay **LTR-embedded inside RTL text** (ui-design §10.2).

### 4 — My queue — `/workspace/queue` (ui-design §5.1)

**The agent's landing screen** (UI-2) — `/workspace` redirects here, not to `/workspace/tickets`.

- `GET /tickets?assigneeId=me` with **no `sort` parameter**, so the server's SLA-urgency default
  applies.
- Rows: **subject, customer, status chip, priority chip, SLA indicator, age, category**.
- Quick filters: `status`, `priority`, `breached` — bound to URL query parameters (UI-9).
- **States** (ui-design §5.1, §9): loading = **skeleton rows** matching the final layout, not a
  spinner over blank space; empty = *"No tickets assigned to you"* with a link to all tickets, and
  it is **an expected state, not an error**; error = inline retry with navigation still usable.
- **Task region:** leave the slot with the `<!-- Story 14 / S9-1 -->` marker described above. Do
  not render a placeholder that implies data is coming.

### 5 — Responsive: table to cards (UI-10, T3-F)

`shared/components/paged-table/` gains a **card mode below the table breakpoint**. Each card leads
with **subject, status and SLA**. Filters collapse into a filter sheet at phone width.

**Wide content scrolls inside its own container so the page body never scrolls sideways**
(architecture §2.3). Verify on the queue and on the activity region.

### 6 — Customer panel — `shared/components/customer-panel/` (ui-design §5.3)

- Desktop: a **side region**. Phone: a **drawer** whose slide direction mirrors under RTL.
- Content: contact details from `GET /customers/{id}`, plus **recent interaction history** from
  `GET /customers/{id}/timeline` (the §1.3 projection), and an *Open profile* link.

  > **`GET /tickets` has no `customerId` filter** (api-design §5.6). The panel's "recent tickets"
  > list is therefore derived from **distinct `ticketId` values in the timeline response**, which
  > already carries `ticketId` and `ticketSubject`. **Do not invent a `customerId` filter.**
  > Recorded as **S9-6** in `00-implementation-plan.md`.

- **Opening and closing the panel must not touch the router and must not remount the composer.**
  Add a manual test step: type a partial reply, open the panel, close it, and confirm the draft
  survives. This is the T1-C acceptance criterion and it is easy to break.

### 7 — Quick replies in the composer (UI-7)

`shared/components/reply-composer/` — the **same** component Story 07 built, now with a
`[Quick replies ▾]` control fed by `GET /config/staff`.

- Selecting one **inserts editable text at the cursor** in the draft.
- **It never sends.** There is one send action, and it is the agent pressing Send. Story 11's
  *Insert into reply* uses the **same** insertion point, which is what keeps A-8's "never
  auto-sent" true by construction rather than by discipline.
- The AI panel slot in the composer stays empty until Story 11.

### 8 — Staff shell landing and navigation

`layout/staff-shell/` — set *My queue* as the first navigation item and the post-sign-in
destination for `Agent` and `Manager`. The notification bell slot stays empty until Story 09.

---

## Verification Steps

1. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~QueueOrdering`.
2. **Frontend builds:** `cd frontend && npm run build`.
3. **Landing:** sign in as a seeded Agent — the destination is `/workspace/queue` and it shows
   **only** that agent's tickets.
4. **Ordering:** with a manually breach-flagged ticket in the database, the queue shows it **first**
   even though its due date is later.
5. **Draft survival:** open a ticket, type a partial reply, open the customer panel, close it — the
   draft is intact. Repeat at phone width with the drawer.
6. **Quick reply:** insert one — the text lands in the draft **and nothing is sent**; the thread is
   unchanged until Send is pressed.
7. **Phone width:** at 390 px the queue renders as cards, the ticket detail collapses to a
   single-column accordion, the composer docks to the bottom, and **the page body does not scroll
   sideways**.
8. **Cross-department:** the Billing agent sees no Technical ticket anywhere on this surface —
   queue, ticket list, or customer panel.
9. **Regression:** Stories 05–07 test suites still pass.

---

## Done Criteria

- [ ] Signing in as an agent lands on a dashboard showing **that agent's assigned tickets only**.
- [ ] The queue is ordered by SLA urgency with **breached tickets visually distinct and first**, and
      the ordering comes from the API's default sort rather than a client re-sort.
- [ ] Opening a ticket from the queue shows the thread, and the customer panel is reachable from
      within the ticket **without navigating away or losing an in-progress reply draft**.
- [ ] The customer panel shows contact details and that customer's recent interaction history.
- [ ] A quick-reply library from **configuration** is available in the composer, and selecting one
      inserts **editable** text.
- [ ] **Inserting a quick reply never sends the message by itself.**
- [ ] An agent sees no ticket from another department anywhere on this surface.
- [ ] The dashboard renders a sensible empty state when the agent has no assigned tickets.
- [ ] The dashboard and ticket view are usable at **phone width**, with no horizontal body scroll.
- [ ] **`firstRespondedAt: null` renders "—"**, never "breached" and never "0" (PF-5 unresolved).
- [ ] **No cross-ticket task endpoint was invented** (S9-1); the region slot is marked and empty.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09.**
