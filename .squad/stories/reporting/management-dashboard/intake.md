# Story intake — Management dashboard and operational reports

> **Source of truth:** `docs/requirements.md` §9 · `docs/product-scope.md` T2-G
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** Reporting
- **Feature slug (folder under `plans/`):** `reporting`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `management-dashboard`)
- **Work item type:** Story

---

## Title

```
Management dashboard and operational reports
```

---

## Description

```
All five lines of requirements §9, delivered as ONE management dashboard with a small fixed
metric set (product-scope T2-G). There is no report builder, no scheduling and no export.

Metrics on the dashboard:
- Ticket reports (§9.1)        — ticket counts by status, priority, category and department.
- SLA performance (§9.2)       — SLA attainment: percentage met vs. breached, per priority.
- Agent performance (§9.3)     — per agent: tickets assigned, tickets resolved, average
                                 resolution time.
- Customer satisfaction (§9.4) — average rating and response count, from the single portal
                                 feedback question (T2-F).
- Management dashboard (§9.5)  — the surface itself: the four metric groups on one screen.

Access and scoping:
- Available to Manager and Administrator roles only (A-4).
- Filterable by department and by branch (T2-K makes branch a reporting attribute).
```

---

## Acceptance criteria

```
- [ ] One dashboard screen shows all four metric groups, reachable only by Manager and
      Administrator roles; an Agent or Customer attempt is refused server-side.
- [ ] Ticket counts break down by status, priority, category and department.
- [ ] SLA attainment shows percentage met vs. breached, per priority, from the SLA data owned by
      `sla-automation/sla-routing-escalation`.
- [ ] Agent performance shows tickets assigned, tickets resolved and average resolution time
      per agent.
- [ ] Customer satisfaction shows average rating and response count from portal feedback.
- [ ] The dashboard can be filtered by department and by branch, and the filters combine.
- [ ] Metrics are computed from live data — no hardcoded or mocked figures.
- [ ] Each metric renders a defined empty state when there is no data (for example, no ratings
      submitted yet) rather than an error or a misleading zero.
- [ ] Seed data is sufficient for every tile to show a non-trivial value in the demo.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-core`,
  `ticket-management/ticket-lifecycle`, `sla-automation/sla-routing-escalation` (SLA tile),
  `customer-portal/portal-self-service` (satisfaction tile),
  `organization/departments-branches` (filters).
- **Depends on code areas or other stories:** If the SLA story is cut, the SLA tile is dropped
  and that omission is recorded in `docs/product-scope.md` per its §10 cut rule.

## Extra notes (optional)

- Cut order: T2. This story is late in the sequence because every metric depends on data other
  stories produce. Planning it early risks writing queries against shapes that later change.
- Product-scope §1.1 lists visual polish as not assessed; readable PrimeNG components are enough.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG — use its chart/table
  components rather than adding a charting dependency.
- Aggregate on the server; do not ship raw ticket lists to the browser to be counted there.
- **Do not invent** endpoints here; Stage 6 (API Design) fixes the metric contracts first.

## Out of scope

- Custom report builder, saved reports, scheduled reports, email delivery of reports (§8).
- PDF/Excel/CSV export (§8).
- Historical trend charts beyond what the fixed metric set requires, cohort analysis,
  forecasting.
- Real-time streaming dashboards or auto-refresh infrastructure.
- Per-agent personal analytics surfaces (Agents do not have report access, A-4).
