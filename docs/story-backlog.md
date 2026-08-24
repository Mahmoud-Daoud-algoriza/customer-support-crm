# Story Backlog — Customer Support CRM

Stage 4 output of [sdd-workflow.md](sdd-workflow.md): the 18 story intakes, their execution order,
and their traceability back to [requirements.md](requirements.md) and [product-scope.md](product-scope.md).

Intakes live under [.squad/stories/](../.squad/stories/). Plan files do not exist yet — they are
generated at stage 9, one per story, into `.squad/plans/<feature>/NN-story-<slug>.md`.

**Sequence numbers below are the intended execution order.** squad-kit assigns the real `NN`
prefix when each plan is generated (`naming.globalSequence: true`), so the two will agree only if
plans are generated in this order.

---

## Backlog

| Seq | Feature | Story id | Tier | Requirements | Scope refs |
|----|---------|----------|------|--------------|------------|
| 01 | platform-foundation | [solution-skeleton](../.squad/stories/platform-foundation/solution-skeleton/intake.md) | T2 | §11.1 | T2-L, A-12 |
| 02 | identity-access | [auth-and-roles](../.squad/stories/identity-access/auth-and-roles/intake.md) | **T1** | §10.1–10.3 | T1-D, A-4, A-9 |
| 03 | organization | [departments-branches](../.squad/stories/organization/departments-branches/intake.md) | **T1** + T2 | §12 | T1-E, T2-K, A-2 |
| 04 | customer-management | [customer-records](../.squad/stories/customer-management/customer-records/intake.md) | **T1** + T2 | §1 | T1-A, T2-A |
| 05 | ticket-management | [ticket-core](../.squad/stories/ticket-management/ticket-core/intake.md) | **T1** | §2.1–2.3 | T1-B, A-6 |
| 06 | ticket-management | [ticket-lifecycle](../.squad/stories/ticket-management/ticket-lifecycle/intake.md) | **T1** | §2.4–2.5 | T1-B, A-5 |
| 07 | ticket-management | [ticket-intake-messaging](../.squad/stories/ticket-management/ticket-intake-messaging/intake.md) | T2 | §3.3, §3.5 | T2-B |
| 08 | agent-workspace | [agent-dashboard](../.squad/stories/agent-workspace/agent-dashboard/intake.md) | **T1** | §4.1, §4.2, §4.4 | T1-C |
| 09 | sla-automation | [sla-routing-escalation](../.squad/stories/sla-automation/sla-routing-escalation/intake.md) | T2 | §5 | T2-D, A-3, A-13 |
| 10 | ai-assist | [ai-service-seam](../.squad/stories/ai-assist/ai-service-seam/intake.md) | **T1** | §7 | T1-F, A-7, A-8 |
| 11 | ai-assist | [ai-ticket-assists](../.squad/stories/ai-assist/ai-ticket-assists/intake.md) | **T1** | §7.1–7.3 | T1-F, A-8 |
| 12 | knowledge-base | [kb-articles-search](../.squad/stories/knowledge-base/kb-articles-search/intake.md) | T2 | §6, §7.4 | T2-E |
| 13 | customer-portal | [portal-self-service](../.squad/stories/customer-portal/portal-self-service/intake.md) | T2 | §8 | T2-F |
| 14 | agent-workspace | [tasks-internal-notes](../.squad/stories/agent-workspace/tasks-internal-notes/intake.md) | T2 | §4.3, §4.5 | T2-C |
| 15 | reporting | [management-dashboard](../.squad/stories/reporting/management-dashboard/intake.md) | T2 | §9 | T2-G |
| 16 | administration | [audit-configuration](../.squad/stories/administration/audit-configuration/intake.md) | T2 | §10.4–10.5 | T2-H, T2-I |
| 17 | platform-experience | [i18n-responsive-branding](../.squad/stories/platform-experience/i18n-responsive-branding/intake.md) | T2 + T3 | §12 | T2-J, T3-E, T3-F, A-11 |
| 18 | integration-seams | [channel-erp-adapters](../.squad/stories/integration-seams/channel-erp-adapters/intake.md) | T3 | §3.1, §3.2, §3.4, §11.2–11.4 | T3-A, T3-D, A-7 |

**Split-in-time exceptions to the sequence.** Two stories cannot be executed as one contiguous
block, and their intakes say so:

- **17 · i18n** — the i18n/RTL *scaffolding* must be wired with story 01, because retrofitting
  every Angular component later costs more than doing it once. Only the translation pass belongs
  at position 17.
- **16 · configuration** — the configuration half is consumed by stories 05, 09, 08 and 17. It is
  defined early; only the audit-log UI belongs at position 16.

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

---

## Cut order

From product-scope §10: *T2 items are cut before T1 items, and T3 fakes are cut before T2 items.*

1. **Cut first** — 18 (integration seams, T3). Nothing depends on it.
2. Then — 14 (tasks; keep internal notes, drop tasks), 15 (reporting), 16 (audit-log UI),
   17 (translation pass and branding; the RTL scaffolding stays).
3. Then — 12 (drop suggested solutions before article CRUD), 09 (dashboard falls back to
   priority-then-age ordering), 13, 07.
4. **Never cut** — 01–06, 08, 10, 11. These are T1 plus the skeleton they stand on.

Any cut is recorded in [product-scope.md](product-scope.md), not left silent.

---

## Story state

| State | Count |
|---|---|
| Intake written (stage 4 complete) | 18 |
| Plan generated (stage 9) | 0 |
| Implemented (stage 10) | 0 |

Run `squad list` for the live view.
