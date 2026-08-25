# Story Backlog — Customer Support CRM

Stage 4 output of [sdd-workflow.md](sdd-workflow.md): the 18 story intakes, their execution order,
and their traceability back to [requirements.md](requirements.md) and [product-scope.md](product-scope.md).

Intakes live under [.squad/stories/](../.squad/stories/). **Stage 9 is complete:** all eighteen plan
files exist at `.squad/plans/<feature>/NN-story-<slug>.md`, indexed by
[.squad/plans/00-index.md](../.squad/plans/00-index.md), with the programme-level view — phases,
dependency graph, parallelization and audit findings — in
[.squad/plans/00-implementation-plan.md](../.squad/plans/00-implementation-plan.md).

**Sequence numbers below are the intended execution order,** and the generated `NN` prefixes match
them exactly (`naming.globalSequence: true`), because plans were generated in this order.

**Execution order differs from `NN` order in three places**, for dependencies the approved documents
already imply. All three are recorded in the implementation plan §3 and none changes a business
decision:

1. **Story 03's data layer runs before Story 02** — `POST /users` requires a `departmentId` for
   every staff role. Both intakes already say these stories are *"planned together or in immediate
   sequence"*.
2. **Story 16 Part A runs before Stories 04 and 05** — the split-in-time exception below.
3. **`POST /auth/register` is implemented in Story 04, not Story 02** — A-15 requires a `Customer`,
   a `Branch` and a configured default branch, none of which exist at Story 02.

---

## Backlog

| Seq | Feature | Story id | Tier | Requirements | Scope refs | Plan (stage 9) | Phase |
|----|---------|----------|------|--------------|------------|----------------|-------|
| 01 | platform-foundation | [solution-skeleton](../.squad/stories/platform-foundation/solution-skeleton/intake.md) | T2 | §11.1 | T2-L, A-12 | [01-story-solution-skeleton.md](../.squad/plans/platform-foundation/01-story-solution-skeleton.md) | 0 |
| 02 | identity-access | [auth-and-roles](../.squad/stories/identity-access/auth-and-roles/intake.md) | **T1** | §10.1–10.3 | T1-D, A-4, A-9 | [02-story-auth-and-roles.md](../.squad/plans/identity-access/02-story-auth-and-roles.md) | 1 |
| 03 | organization | [departments-branches](../.squad/stories/organization/departments-branches/intake.md) | **T1** + T2 | §12 | T1-E, T2-K, A-2 | [03-story-departments-branches.md](../.squad/plans/organization/03-story-departments-branches.md) | 1 |
| 04 | customer-management | [customer-records](../.squad/stories/customer-management/customer-records/intake.md) | **T1** + T2 | §1 | T1-A, T2-A | [04-story-customer-records.md](../.squad/plans/customer-management/04-story-customer-records.md) | 2 |
| 05 | ticket-management | [ticket-core](../.squad/stories/ticket-management/ticket-core/intake.md) | **T1** | §2.1–2.3 | T1-B, A-6 | [05-story-ticket-core.md](../.squad/plans/ticket-management/05-story-ticket-core.md) | 3 |
| 06 | ticket-management | [ticket-lifecycle](../.squad/stories/ticket-management/ticket-lifecycle/intake.md) | **T1** | §2.4–2.5 | T1-B, A-5 | [06-story-ticket-lifecycle.md](../.squad/plans/ticket-management/06-story-ticket-lifecycle.md) | 3 |
| 07 | ticket-management | [ticket-intake-messaging](../.squad/stories/ticket-management/ticket-intake-messaging/intake.md) | T2 | §3.3, §3.5 | T2-B | [07-story-ticket-intake-messaging.md](../.squad/plans/ticket-management/07-story-ticket-intake-messaging.md) | 4 |
| 08 | agent-workspace | [agent-dashboard](../.squad/stories/agent-workspace/agent-dashboard/intake.md) | **T1** | §4.1, §4.2, §4.4 | T1-C | [08-story-agent-dashboard.md](../.squad/plans/agent-workspace/08-story-agent-dashboard.md) | 4 |
| 09 | sla-automation | [sla-routing-escalation](../.squad/stories/sla-automation/sla-routing-escalation/intake.md) | T2 | §5 | T2-D, A-3, A-13 | [09-story-sla-routing-escalation.md](../.squad/plans/sla-automation/09-story-sla-routing-escalation.md) | 5 |
| 10 | ai-assist | [ai-service-seam](../.squad/stories/ai-assist/ai-service-seam/intake.md) | **T1** | §7 | T1-F, A-7, A-8 | [10-story-ai-service-seam.md](../.squad/plans/ai-assist/10-story-ai-service-seam.md) | 5 ∥ |
| 11 | ai-assist | [ai-ticket-assists](../.squad/stories/ai-assist/ai-ticket-assists/intake.md) | **T1** | §7.1–7.3 | T1-F, A-8 | [11-story-ai-ticket-assists.md](../.squad/plans/ai-assist/11-story-ai-ticket-assists.md) | 5 |
| 12 | knowledge-base | [kb-articles-search](../.squad/stories/knowledge-base/kb-articles-search/intake.md) | T2 | §6, §7.4 | T2-E | [12-story-kb-articles-search.md](../.squad/plans/knowledge-base/12-story-kb-articles-search.md) | 6 ∥ |
| 13 | customer-portal | [portal-self-service](../.squad/stories/customer-portal/portal-self-service/intake.md) | T2 | §8 | T2-F | [13-story-portal-self-service.md](../.squad/plans/customer-portal/13-story-portal-self-service.md) | 6 |
| 14 | agent-workspace | [tasks-internal-notes](../.squad/stories/agent-workspace/tasks-internal-notes/intake.md) | T2 | §4.3, §4.5 | T2-C | [14-story-tasks-internal-notes.md](../.squad/plans/agent-workspace/14-story-tasks-internal-notes.md) | 7 |
| 15 | reporting | [management-dashboard](../.squad/stories/reporting/management-dashboard/intake.md) | T2 | §9 | T2-G | [15-story-management-dashboard.md](../.squad/plans/reporting/15-story-management-dashboard.md) | 7 |
| 16 | administration | [audit-configuration](../.squad/stories/administration/audit-configuration/intake.md) | T2 | §10.4–10.5 | T2-H, T2-I | [16-story-audit-configuration.md](../.squad/plans/administration/16-story-audit-configuration.md) | **2 + 7** |
| 17 | platform-experience | [i18n-responsive-branding](../.squad/stories/platform-experience/i18n-responsive-branding/intake.md) | T2 + T3 | §12 | T2-J, T3-E, T3-F, A-11 | [17-story-i18n-responsive-branding.md](../.squad/plans/platform-experience/17-story-i18n-responsive-branding.md) | **0 + 8** |
| 18 | integration-seams | [channel-erp-adapters](../.squad/stories/integration-seams/channel-erp-adapters/intake.md) | T3 | §3.1, §3.2, §3.4, §11.2–11.4 | T3-A, T3-D, A-7 | [18-story-channel-erp-adapters.md](../.squad/plans/integration-seams/18-story-channel-erp-adapters.md) | 8 |

**Split-in-time exceptions to the sequence.** Two stories cannot be executed as one contiguous
block, and their intakes say so:

- **17 · i18n** — the i18n/RTL *scaffolding* must be wired with story 01, because retrofitting
  every Angular component later costs more than doing it once. Only the translation pass belongs
  at position 17.
- **16 · configuration** — the configuration half is consumed by stories 05, 09, 08 and 17. It is
  defined early; only the audit-log UI belongs at position 16.

  > **Widened by the stage 9 audit.** The consumer list is larger than recorded here: **story 04**
  > also needs it (the `Registration:DefaultBranchId` of A-15 and the attachment size cap) and
  > **story 13** needs the feedback rating-scale key. Configuration therefore executes as
  > **Part A in phase 2, before story 04**, not merely before story 05. See
  > [16-story-audit-configuration.md](../.squad/plans/administration/16-story-audit-configuration.md).

---

## Coverage against product scope

| Scope item | Story |
|---|---|
| T1-A Customer management | 04 |
| T1-B Ticket management | 05, 06 |
| T1-C Agent dashboard | 08 |
| T1-D Users, roles, permissions | 02 |
| T1-E Multi-department routing | 03 |
| T1-F Ticket-facing AI | 10, 11 |
| T2-A Attachments | 04 |
| T2-B Web form + portal messaging | 07 |
| T2-C Tasks & internal notes | 14 |
| T2-D SLA & automation | 09 |
| T2-E Knowledge base | 12 |
| T2-F Customer portal | 13 |
| T2-G Reports & dashboards | 15 |
| T2-H Audit logs | 16 |
| T2-I System configuration | 16 |
| T2-J Arabic & English | 17 |
| T2-K Multi-branch | 03 |
| T2-L Public API | 01 |
| T3-A External channels | 18 |
| T3-B Live chat | 18 (documented, not built) |
| T3-C AI chatbot | 10 (seam), 18 (documented) |
| T3-D ERP & external systems | 18 |
| T3-E Custom branding | 17 |
| T3-F Mobile / responsive | 17 (asserted in 08, 13) |
| T3-G Multi-tenancy | 03 (boundary noted, not built) |

Every T1, T2 and T3 item in [product-scope.md](product-scope.md) maps to at least one story.
T4 items appear only in the **Out of scope** section of the stories nearest to them, so the
exclusion is visible where someone might otherwise implement it.

**Two rows are split across stories once planned** (stage 9 finding **S9-5**; the rule is *the entity
lands with the story that first writes it, the read surface with the story that owns it*):

- **T2-H Audit logs** — the `AuditEntry` entity, the single `IAuditRecorder` and the write call
  sites land in **story 02**, whose own acceptance criterion requires sign-in recording. Story 16
  Part B owns `GET /audit` and the admin screen.
- **T1-B Ticket management** — story 05 introduces `TicketActivity` (its AC requires assignment
  recording) and computes both SLA due timestamps ([data-model.md](data-model.md) §2.6 makes them
  required at creation); story 06 owns the lifecycle types and the `/activity` read.

---

## Cut order

From product-scope §10: *T2 items are cut before T1 items, and T3 fakes are cut before T2 items.*

1. **Cut first** — 18 (integration seams, T3). Nothing depends on it.
2. Then — 14 (tasks; keep internal notes, drop tasks), 15 (reporting), 16 (audit-log UI),
   17 (translation pass and branding; the RTL scaffolding stays).
3. Then — 12 (drop suggested solutions before article CRUD), 09, 13, 07.

   > **Correction from the stage 9 audit (S9-8).** The consequence recorded for cutting 09 —
   > *"dashboard falls back to priority-then-age ordering"* — **does not hold.**
   > [data-model.md](data-model.md) §2.6 makes both SLA due timestamps **required at creation**, so
   > story 05 computes them and the agent queue keeps its real SLA-urgency ordering with story 09
   > cut. What is lost is the *population* of the latching breach flags, so no ticket ever appears
   > as breached, and story 15 loses its SLA tile. The ordering itself survives.
4. **Never cut** — 01–06, 08, 10, 11. These are T1 plus the skeleton they stand on.

Any cut is recorded in [product-scope.md](product-scope.md), not left silent.

---

## Story state

| State | Count |
|---|---|
| Intake written (stage 4 complete) | 18 |
| **Plan generated (stage 9 complete)** | **18** |
| Implemented (stage 10) | 0 |

`squad status` reports 18 stories, 18 plan files, next `NN` **19**; `squad doctor` is
6 ok · 0 warn · 0 fail · 7 skip. Run `squad list` for the live view.

**Four acceptance criteria across stories 11, 14, 15 and 18 are blocked on decisions the approved
documents do not contain** — S9-1, S9-4, PF-4 and PF-2. None blocks stories 01–04. They are listed
in [.squad/plans/00-index.md](../.squad/plans/00-index.md) and detailed in
[00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §7.
