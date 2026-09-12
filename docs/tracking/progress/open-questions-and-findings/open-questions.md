[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

### 6.1 Active — must be answered before the named story is implemented

| ID | Question | Impact | Blocks | Status |
|---|---|---|---|---|
| **OQ-1** | What is the CSAT rating scale? T2-F says "a one-question satisfaction rating" and fixes no scale | Determines portal control, server validation, and what the §9.4 average means. The model **encodes no range** | Story 13, and the satisfaction tile in story 15 | 🔴 Open — product decision |
| ~~**OQ-2**~~ | ~~On a priority change, do SLA due dates recompute from `createdAt` or stay frozen?~~ | Materially different §9.2 attainment numbers; recompute can breach a ticket as a consequence of the escalation the breach triggered | **Story 05** *(its `PATCH /tickets/{id}` is the **first** code path that changes a priority — widened by **S9-3**)*, story 09, and §9.2 in story 15 | **✅ Closed 2026-08-30 by A-20** — they **freeze** — see R-16 below |
| ~~**OQ-3**~~ | ~~Who is notified on breach when a department has no manager?~~ | Breach flag and priority raise were never in question; only the recipient was | ~~**Story 06** *(widened by **S9-3**)* and story 09~~ | **✅ Closed 2026-08-31 by A-21** — the notification climbs to the next authority level, and escalation is never blocked — see R-17 below |

| ~~**OQ-5**~~ | ~~When `Customer.email` is changed, does a linked portal login's sign-in email change with it?~~ | Raised 2026-08-25 by the N-2 correction | **✅ Closed 2026-08-27 by A-19** — see R-15 below | ✅ Answered |

**None of these blocked Stage 7, 8 or 9.** All are implementation-time decisions, and stage 9
scheduled each against the story and the single method that encodes it. OQ-4 was resolved on
2026-08-24 (see R-11), **OQ-5 on 2026-08-27 (see R-15)**, **OQ-2 on 2026-08-30 (see R-16)** and
**OQ-3 on 2026-08-31 (see R-17)**. **OQ-1 is the only question left in this table**, and it reaches
stories 13 and 15.

**Earliest-first order for stage 10:** ~~**OQ-2** (story 05)~~ **— closed 2026-08-30 by A-20** → **S9-4** (story 11) → **OQ-1**
(story 13) → **S9-1** (story 14) → **PF-4** (story 15) → **PF-2** (story 18). Restated in §10.
**S9-1 is now the live one (2026-09-06):** story 14 reached it and left task 8 unbuilt rather than inventing an endpoint, so it is the first of the four to have actually stopped work. See §6.7.
**OQ-5 has left this list**: it gated story 04, which was the nearest of all of them, and it is
answered.

### 6.2 Carried from product scope — non-blocking by design

[product-scope.md](product-scope.md) §9 holds seven open questions (AI provider and data
residency, ERP product, unbounded "external systems", tenancy model, real SLA policy, chatbot
handoff, anonymous submission). [architecture.md](architecture.md) §9 confirms the architecture
resolves none of them and places each behind a seam or configuration point **so they can stay
open**. They do not block the assessment. Question 5 (real SLA policy) is the parent of OQ-2 and **stays open** — A-20 closed the child, not the parent, and **A-21 does not touch it either**.

