# Story 15 — Management dashboard and operational reports

> **Source of truth:** `docs/requirements.md` §9 · `docs/product-scope.md` **T2-G**, T2-K, A-2, A-4 · `docs/architecture.md` §2.1, §4.2, §4.3 (aggregates must not be silently narrowed), AD-5 · `docs/data-model.md` §2.15 (absence means "no response"), §7 (§9 has no entity — aggregates), §6 · `docs/api-design.md` §4.4, §5.11, §6.8 · `docs/ui-design.md` §5.7, §9, §11
> **Intake:** `.squad/stories/reporting/management-dashboard/intake.md` · **Tier:** T2 — *"late in the sequence because every metric depends on data other stories produce"*
> **Phase:** 7 — Collaboration, reporting and administration.

## Prerequisites

- **Story 05, 06 completed:** tickets, statuses, resolution timestamps.
- **Story 09 completed:** SLA breach flags — **the §9.2 tile has no data without them.**
- **Story 13 completed:** `CustomerFeedback` — **the §9.4 tile has no data without it.**
- **Story 03 completed:** departments and branches — the two filters.

> ### ⚠ Blocked decision — **PF-4** — `api-design.md` §9 requires it **before this story is planned**
>
> T2-G says *"tickets assigned"* without saying whether that means **currently assigned** or **ever
> assigned**. Both are computable; **the response shape is identical either way** (api-design §6.8).
>
> `api-design.md` §9 item 2 states: *"PF-4's metric semantics must be pinned before story 15 is
> **planned**."* It was not pinned. This plan is therefore written **with the decision isolated to
> one method**, and the box below is a Done Criterion rather than a silent choice:
>
> | Reading | Query | What it tells a manager |
> |---|---|---|
> | **Currently assigned** | `COUNT(*) WHERE assignedUserId = agent AND status NOT IN (Closed, Cancelled)` | Present workload |
> | **Ever assigned** | `COUNT(DISTINCT ticketId)` over `TicketActivity` rows of type `Assigned` | Historical throughput |
>
> **Do not pick one.** `AgentPerformanceQuery.AssignedCount` is the single place it is encoded; it
> throws `NotImplementedException("PF-4")` until the decision is recorded. The label stays exactly
> as T2-G words it — **"tickets assigned"** — with **no clarifying tooltip that would assert a
> meaning** (ui-design §11). Recorded as **S9-9** in `00-implementation-plan.md`.

> ### ⚠ Open question — **OQ-1** affects what the satisfaction tile *means*
>
> The §9.4 average is an average of `CustomerFeedback.rating`, whose **scale is undecided**
> (`data-model.md` §2.15, ui-design §11). The **query and the response shape are unaffected**; the
> **interpretation** is not. Render the average with the configured `min`–`max` beside it
> (for example *"4.2 of 5"*), taken from `feedback.ratingScale`, so the number is never presented
> without its scale. **Do not hardcode a denominator.**

---

## Story Goal

All five lines of requirements §9, delivered as **one management dashboard with a small fixed metric
set**. **No report builder, no scheduling, no export.**

1. **Ticket reports** (§9.1) — counts by status, priority, category and department.
2. **SLA performance** (§9.2) — attainment: percentage met vs breached, **per priority**.
3. **Agent performance** (§9.3) — per agent: tickets assigned, tickets resolved, average resolution
   time.
4. **Customer satisfaction** (§9.4) — average rating and response count, from the single portal
   feedback question.
5. **The dashboard** (§9.5) — the four groups on one screen, **Manager and Administrator only**,
   filterable by **department** and **branch**.

---

## Context — Read These Files First

1. `docs/api-design.md` §5.11 — **one** endpoint, the four response groups, *"Agents and Customers
   get `403`"*, and *"Each group returns an explicit empty shape rather than a misleading zero"*.
   Then **§6.8 in full** — the exact JSON, and the **"Empty is not zero"** paragraph.
2. `docs/api-design.md` **§4.4** — `branchId` filters **through the customer**, because
   **`Ticket` has no branch field**; a ticket's branch is derived `Ticket -> Customer -> Branch`.
3. `docs/architecture.md` §4.3's rejected-alternative paragraph — one reason EF global query filters
   were rejected is that **"reporting aggregates must not be silently narrowed"**. Manager and
   Administrator see **all departments**; the `departmentId` filter is a user choice, not a scope.
4. `docs/data-model.md` §2.15 — *"declining is a normal outcome, so the absence of a row is
   meaningful and reporting must treat it as 'no response', not as a zero"*; and §7, which records
   that §9 introduces **no entity** — every metric is an aggregate.
5. `docs/ui-design.md` §5.7 (four regions, **Manager+**, each with its own empty state), §9, and
   §11's **PF-4** row.
6. `.squad/stories/reporting/management-dashboard/intake.md` — nine acceptance criteria, and
   *"Aggregate on the server; do not ship raw ticket lists to the browser to be counted there."*

---

## Product rules (from story)

- **Manager and Administrator only** (A-4). An Agent or Customer attempt is **refused server-side**
  with `403` — a capability denial they can infer from their own role, so `403` is correct here and
  AP-4's `404` rule does not apply.
- **Metrics are computed from live data.** No hardcoded or mocked figures anywhere.
- **Empty is not zero.** When no ratings exist, `satisfaction` returns
  `{ "averageRating": null, "responseCount": 0 }` — **never `0.0`, which would read as universal
  dissatisfaction.** The same rule applies to `averageResolutionHours` for an agent who has resolved
  nothing.
- **Filters combine**: `departmentId` and `branchId` together.
- **Branch resolves through the customer**, never through a ticket column.
- **No report builder, no saved reports, no scheduling, no email delivery, no PDF/Excel/CSV export,
  no trend charts beyond the fixed set, no auto-refresh infrastructure.**
- **Agents have no report access** (A-4) — there is no per-agent personal analytics surface.

---

## Backend Tasks

### 1 — Application: the four aggregate queries

**Create file: `src/SupportCrm.Application/Modules/Reporting/DashboardReportService.cs`** — one
public method, `GetAsync(Guid? departmentId, Guid? branchId)`, composing four internal queries.
**Everything aggregates in SQL**; no ticket list is materialized into memory to be counted.

**The filter is applied once, in one place:**

```csharp
private IQueryable<Ticket> Filtered(Guid? departmentId, Guid? branchId)
{
    var q = _db.Tickets.AsNoTracking();
    if (departmentId is { } d) q = q.Where(t => t.DepartmentId == d);
    // api-design §4.4 — Ticket has no branch column; a ticket's branch is its CUSTOMER's branch.
    if (branchId is { } b)     q = q.Where(t => _db.Customers.Any(c => c.Id == t.CustomerId && c.BranchId == b));
    return q;
}
```

> **`TicketScope.ForCaller` is deliberately *not* composed here.** The endpoint is Manager+ only,
> and both roles are unrestricted across departments (architecture §4.3). Composing the helper
> would be harmless today and wrong tomorrow — it would let a future role change **silently narrow
> an aggregate**, which is the exact failure AD-5 cites. Add that sentence as a comment so nobody
> "fixes" it.

**Create file: `src/SupportCrm.Application/Modules/Reporting/TicketCountsQuery.cs`** (§9.1) —
four `GROUP BY` projections: `byStatus`, `byPriority`, `byCategory`, `byDepartment`. The department
group carries `{ key, name, count }`; the other three carry `{ key, count }` (api-design §6.8).

**Create file: `src/SupportCrm.Application/Modules/Reporting/SlaAttainmentQuery.cs`** (§9.2) — per
priority: `met`, `breached`, `attainmentPercent`.

- **"Breached" is `resolutionBreached`** — the latching flag Story 09 sets.
- A priority with **no tickets** returns `met: 0, breached: 0, attainmentPercent: null` — **not
  `100.0`**, which would claim perfect attainment on no evidence. Same rule as "empty is not zero".
- **OQ-2 note:** because breach flags **latch** (data-model §2.6 invariant 5), this tile stays honest
  whichever way OQ-2 is decided. Nothing here depends on the answer.

**Create file: `src/SupportCrm.Application/Modules/Reporting/AgentPerformanceQuery.cs`** (§9.3) —
per agent: `assignedCount`, `resolvedCount`, `averageResolutionHours`.

```csharp
// PF-4 / S9-9 — "tickets assigned" is undefined: currently assigned vs ever assigned.
// api-design §5.11 and §6.8 leave it open on purpose. The response shape is identical either way.
private static IQueryable<int> AssignedCount(...) =>
    throw new NotImplementedException("PF-4: pin the semantics before implementing.");
```

- `resolvedCount` — tickets whose `resolvedAt` is set.
- `averageResolutionHours` — `AVG(resolvedAt - createdAt)` in hours over those tickets;
  **`null` when the agent has resolved nothing**, never `0.0`.

**Create file: `src/SupportCrm.Application/Modules/Reporting/SatisfactionQuery.cs`** (§9.4) —
`averageRating` and `responseCount` over `CustomerFeedback`, joined to the filtered ticket set.

- **`averageRating` is `null` when `responseCount` is 0.** The absence of a row means *"no
  response"*, not a low score (data-model §2.15).
- Publish the configured `ratingScale` alongside, so the front end can render *"4.2 of 5"* without
  hardcoding a denominator (OQ-1 box above).

### 2 — Api: the endpoint

**Create file: `src/SupportCrm.Api/Controllers/ReportsController.cs`**

```
GET /api/v1/reports/dashboard?departmentId=&branchId=      RequireManager
```

**One endpoint**, because T2-G is one dashboard with a fixed metric set. Response exactly
api-design §6.8. Unknown query parameters -> `400` (AP-15).

**No export endpoint, no second report endpoint, no `format=csv` parameter.**

### 3 — Seed data

**File: the seeders** — confirm the seeded data makes **every tile non-trivial** (intake AC):
tickets across all statuses, priorities, categories and both departments; customers in **both
branches**; at least one breached ticket (Story 09 seeds past-due rows); at least two feedback rows
with different ratings; at least one agent who has resolved nothing, so the `null`
`averageResolutionHours` path is visible in the demo rather than only in a test.

### 4 — Tests

**Create file: `tests/SupportCrm.Tests/Reporting/DashboardAccessTests.cs`**

1. `GET /reports/dashboard` as `Agent` -> **`403`**; as `Customer` -> `403`; as `Manager` -> `200`;
   as `Administrator` -> `200`.
2. A `Manager`'s unfiltered response includes tickets from **both** departments — the aggregate is
   **not** narrowed by the caller's own department.

**Create file: `tests/SupportCrm.Tests/Reporting/DashboardMetricsTests.cs`**

3. `ticketCounts.byStatus` sums to the total ticket count in scope.
4. `departmentId` and `branchId` **combine**, and `branchId` narrows **through the customer** —
   assert with a ticket whose customer's branch differs from the assigned agent's branch.
5. **With no feedback rows**, `satisfaction` is `{ averageRating: null, responseCount: 0 }` —
   assert `null`, **explicitly not `0.0`**.
6. An agent who has resolved nothing has `averageResolutionHours: null`.
7. A priority with no tickets has `attainmentPercent: null`, **not `100`**.
8. `slaAttainment` counts a latched breach even after the ticket's priority later changed.
9. **`GET /reports/dashboard?format=csv` -> `400`** — no export exists (T2-G, product-scope §8).

**Create file: `tests/SupportCrm.Tests/Reporting/AggregationHappensOnTheServerTests.cs`**

10. The response payload for a database of N tickets is **independent of N** in shape — it contains
    no ticket array. Guards the intake's *"do not ship raw ticket lists to the browser"* rule.

---

## Frontend Tasks

### 5 — Typed client and route

`core/api/reports.client.ts` — `getDashboard({ departmentId, branchId })`.
Route `/workspace/reports` behind the **`Manager+`** guard. An Agent who reaches the route is
redirected, **and the endpoint returns `403` regardless** (ui-design §5.7).
*Reports* appears in the staff-shell navigation for Manager and Administrator only — hiding is
convenience, not protection.

### 6 — Four regions — `/workspace/reports` (ui-design §5.7)

Use **PrimeNG chart and table components**; **do not add a charting dependency** (intake technical
hint).

| Region | Content | Empty state |
|---|---|---|
| **Ticket counts** | Four grouped breakdowns: status, priority, category, department | *"No tickets in this selection"* |
| **SLA attainment** | Met vs breached per priority, with the percentage | *"No SLA data for this selection"* — a `null` percent renders as **"—"**, never as 100% |
| **Agent performance** | Table: agent, **"Tickets assigned"**, "Tickets resolved", "Average resolution time" | *"No agent activity in this selection"*; a `null` average renders **"—"** |
| **Satisfaction** | Average rating **with its configured scale** ("4.2 of 5") and response count | **"No ratings submitted yet"** — never *"0.0"* |

- **Filters** — department and branch, both bound to URL query parameters (UI-9). Both are enabled
  for Manager+, unlike the ticket list's department filter.
- **"Tickets assigned" is labelled exactly as T2-G words it, with no clarifying tooltip** — adding
  one would assert a meaning PF-4 has not decided (ui-design §11).
- **Report tables scroll inside their own container**, so the page body never scrolls sideways
  (architecture §2.3).
- **No export button, no print view, no scheduling control, no date-range picker** beyond the two
  approved filters.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:** `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Reporting`
   — all ten green.
3. **Role gate by hand:** `GET /api/v1/reports/dashboard` with an Agent token -> **`403`**.
4. **Branch derivation:** filter by a branch and confirm the counts match tickets whose **customer**
   is in that branch, not whose assigned agent is.
5. **Empty is not zero:** delete every `CustomerFeedback` row in a scratch database and confirm the
   response is `{"averageRating": null, "responseCount": 0}` and the tile reads *"No ratings
   submitted yet"*.
6. **Aggregation on the server:** inspect the response — it contains **no ticket array**.
7. **Regression:** Stories 05–14 suites still pass.
8. **Frontend:** `npm run build`; all four regions render with seeded data; filters combine and
   survive a reload; the tables scroll internally at phone width.

---

## Done Criteria

- [ ] One dashboard screen shows all four metric groups, reachable only by Manager and
      Administrator; an Agent or Customer attempt is **refused server-side**.
- [ ] Ticket counts break down by status, priority, category and department.
- [ ] SLA attainment shows percentage met vs breached, **per priority**, from Story 09's data.
- [ ] Agent performance shows tickets assigned, tickets resolved and average resolution time
      per agent.
- [ ] Customer satisfaction shows average rating and response count from portal feedback.
- [ ] The dashboard filters by department and by branch, **and the filters combine**, with branch
      resolved **through the customer**.
- [ ] **Metrics are computed from live data** — no hardcoded or mocked figures.
- [ ] **Each metric renders a defined empty state rather than a misleading zero.**
- [ ] Seed data is sufficient for every tile to show a non-trivial value.
- [ ] **Reporting aggregates are not narrowed by the caller's own department.**
- [ ] ⛔ **BLOCKED — PF-4 / S9-9:** `assignedCount` semantics are undecided.
      `AgentPerformanceQuery.AssignedCount` throws until the decision is recorded. **Take it before
      implementing; do not pick a reading here.**
- [ ] **OQ-1 is not answered.** The satisfaction tile renders the configured scale beside the
      average and hardcodes no denominator.
- [ ] **No export, scheduling, report-builder or extra charting dependency** was introduced.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user, **including the PF-4 blocker**, and wait for confirmation before
proceeding to Story 16 Part B.**
