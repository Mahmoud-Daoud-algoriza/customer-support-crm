# Plans index

One row per feature folder under `.squad/plans/`. `NN` is a **global execution sequence** across all
features (`naming.globalSequence: true` in [config.yaml](../config.yaml)) and matches the intended
order in [docs/story-backlog.md](../../docs/story-backlog.md).

**Start here:** [00-implementation-plan.md](00-implementation-plan.md) — the programme-level view:
workstreams, phases, the dependency graph, what can run in parallel, shared conventions, the Stage 9
audit findings, and full traceability to stories, endpoints, entities and screens.

| Feature | Overview | NN range | Phase |
|---------|----------|----------|-------|
| `platform-foundation` | [00-overview.md](platform-foundation/00-overview.md) | 01 | 0 |
| `identity-access` | [00-overview.md](identity-access/00-overview.md) | 02 | 1 |
| `organization` | [00-overview.md](organization/00-overview.md) | 03 | 1 |
| `customer-management` | [00-overview.md](customer-management/00-overview.md) | 04 | 2 |
| `ticket-management` | [00-overview.md](ticket-management/00-overview.md) | 05–07 | 3–4 |
| `agent-workspace` | [00-overview.md](agent-workspace/00-overview.md) | 08, 14 | 4, 7 |
| `sla-automation` | [00-overview.md](sla-automation/00-overview.md) | 09 | 5 |
| `ai-assist` | [00-overview.md](ai-assist/00-overview.md) | 10–11 | 5 (10 parallel) |
| `knowledge-base` | [00-overview.md](knowledge-base/00-overview.md) | 12 | 6 (partly parallel) |
| `customer-portal` | [00-overview.md](customer-portal/00-overview.md) | 13 | 6 |
| `reporting` | [00-overview.md](reporting/00-overview.md) | 15 | 7 |
| `administration` | [00-overview.md](administration/00-overview.md) | 16 | **2 (Part A) + 7 (Part B)** |
| `platform-experience` | [00-overview.md](platform-experience/00-overview.md) | 17 | **0 (Part A) + 8 (Part B)** |
| `integration-seams` | [00-overview.md](integration-seams/00-overview.md) | 18 | 8 |

**18 plans across 14 feature folders.** Two stories are **split in time** — 16 and 17 — exactly as
[docs/story-backlog.md](../../docs/story-backlog.md) records; each of their overviews carries both
execution points.

## Blocked decisions before Stage 10 completes

Four acceptance criteria cannot be met until a decision is recorded. **None blocks Story 01, and
none blocks Phase 0 or Phase 1.** Full detail in
[00-implementation-plan.md](00-implementation-plan.md) §7.

| ID | What is undecided | Blocks |
|---|---|---|
| **S9-1** | No endpoint lists a user's tasks across tickets | Story 14 AC 2, Story 08's dashboard region |
| **S9-4** | No contract path records AI suggestion acceptance or override | Story 11 AC 4 |
| **PF-4 / S9-9** | *"Tickets assigned"* — currently or ever? | Story 15 `agentPerformance.assignedCount` |
| **PF-2 / S9-10** | The inbound channel adapter has no actor | Story 18 AC 3 |

**Open questions carried unchanged:** OQ-1 (CSAT scale — Stories 13, 15), OQ-2 (SLA due dates on a
priority change — Stories **05** and 09), OQ-3 (breach recipient with no department manager —
Stories 06 and 09, non-blocking), and **F-1**,
which stays open by design: `ui-design.md` UI-3 is approved, the server remains the authority, and
the gate 9 → 10 requires no decision.

**Closed since:** **OQ-5** (customer email vs linked login — Story 04), answered 2026-08-27 by
**A-19**: they are one address, updated together in one unit of work.
