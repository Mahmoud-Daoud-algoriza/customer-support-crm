[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

### 6.4 Decision → document → story traceability

Which documents and which story intakes each resolved decision actually changed. "—" means nothing
needed amending, because nothing in that artifact contradicted the decision.

| Decision | Documents changed | Story intakes changed |
|---|---|---|
| **R-1 / AD-15** token asserts identity only | `architecture.md` §4.1, **new §4.1.1**, §3 flow, §4.3, AD-7, **new AD-15** | — |
| **R-17 / A-21** escalation climbs to the next authority level when a department has no manager | `product-scope.md` **new A-21**; `data-model.md` §2.2 (the OQ-3 block replaced by the answer), §8 (row struck, resolution recorded); `api-design.md` §5.6 (`/escalate`), §6.2 (`Department`); `ui-design.md` §5.3, §11 (the wording constraint lifted). **No contract shape changed in any of them** | — |
| **R-2** branch derived, not stored | `data-model.md` §2.3, §4, §8 | — |
| **R-3 / DM-5** no AI entity | `data-model.md` §1 | — |
| **R-4 / DM-1** `User` and `Customer` separate | `data-model.md` §1, §2.1, §2.4 | — |
| **R-5** CSAT scale assumption withdrawn | `data-model.md` §2.15, §8 (reopened as OQ-1) | — |
| **R-6 / A-14** category → department routing | `product-scope.md` A-6, **new A-14**; `architecture.md` §6.3 (new config key); `data-model.md` invariant 11a | — |
| **R-7 / A-15** self-registration branch + linking | `product-scope.md` A-9, **new A-15**; `architecture.md` §6.3 (new config key); `data-model.md` DM-1, §2.4 | — |
| **R-8 / A-16** transition authority matrix | `product-scope.md` A-5, **new A-16**; `data-model.md` invariant 11b | `ticket-lifecycle`, `portal-self-service` |
| **R-9 / A-17** `isUrgent` customer input | `product-scope.md` A-6, **new A-17**; `data-model.md` §2.6 (**new field**) | — |
| **R-10** agents and managers may cancel | `product-scope.md` A-16 (cancel row + consequences) | `ticket-lifecycle`, `portal-self-service` |
| **Stage 8 UI design** | `ui-design.md` (new); `sdd-workflow.md` | All 18 — §13 maps every story to its screens (10 and 18 have none) |
| **B-1** feedback rating-scale config key | `architecture.md` §6.3 | — (story 13 consumes it) |
| **B-2 / AP-17** configuration split by audience | `api-design.md` §5.1, §3 | — (stories 01, 08, 13, 16 consume it) |
| **N-2 / OQ-5** customer email is patchable | `api-design.md` §5.5 | — (story 04; **no longer blocked** — see R-15) |
| **R-15 / A-19** a customer's email and their portal sign-in are one address, **and the login change is audited** | `product-scope.md` **new A-19** (incl. the audit bullet); `api-design.md` §5.5 (box rewritten + audit note), §9 item 3, §10.3; `data-model.md` §2.4 invariant, **new §5 constraints 1a and 1b**, §2.14 (`UserEmailChanged` note + action list), §8 resolved list; `ui-design.md` §5.5, §11 (row retired); `sdd-workflow.md` + 4 source-of-truth lines | `customer-records` (task 3); `audit-configuration` (task 6 inventory) |
| **R-14 / A-5** automatic transition attributed to the replying customer | `product-scope.md` A-5 (new attribution rule); `data-model.md` §2.6 invariant 2b, §2.7 `actorKind` note, §2.8 invariants, §5 constraint 9a | `ticket-lifecycle` |
| **R-13 / A-5** customer reply reopens a `Pending` ticket | `product-scope.md` A-5 (transition graph + `Pending` bullet), A-16 (`-> Open` row); `data-model.md` §2.6 invariant 2b, §2.8 invariants, §5 constraint 9a | `ticket-lifecycle`, `ticket-intake-messaging`, `portal-self-service` |
| **R-12 / DM-7** `CustomerFeedback` owned by `Tickets` | `data-model.md` **new DM-7**, §1 preamble, §2.15 ownership, §3 entity list; `architecture.md` §1 (`customer-portal` row) | — (no intake asserted an owning module) |
| **R-11 / A-18** assignment is not the start of work | `product-scope.md` A-5, **new A-18**, §9 (question 8 closed); `data-model.md` §2.6 field, **new invariant 2a**, 11b, §8 | `ticket-lifecycle`, `ticket-core`, `sla-routing-escalation`, `portal-self-service` |

`sdd-workflow.md` was touched by the A-14…A-18 set only to widen the assumption range it cites.
No decision so far has changed [requirements.md](requirements.md), which is never edited.

