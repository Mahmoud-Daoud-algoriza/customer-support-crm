[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 08 is complete — all eight plan tasks, implemented and verified 2026-08-31**, across
   three slices (08-1 My queue, 08-2 customer panel, 08-3 quick replies).** **Execution model changed here: story 08 is delivered
   in small full-stack slices**, not whole like stories 05–07, because two days remain and My queue
   is the one part of the agent demo that cannot be cut. The slice map is in
   [.squad/plans/agent-workspace/00-overview.md](../.squad/plans/agent-workspace/00-overview.md).

   **What landed.** `/workspace/queue` is the staff landing screen (UI-2) and the first sidebar
   entry: `GET /tickets?assigneeId=me` with **no `sort`**, so the server's SLA-urgency default is
   the ordering and the screen never re-sorts (ui-design §5.1). The new shared `SlaIndicator`
   renders resolution time with breached visually distinct, and **a null due date as "—", never
   "breached" and never "0"** (PF-5's display rule, which stays open for story 09). The three quick
   filters live in the URL (UI-9); `assigneeId` deliberately does **not**, because *my* queue is
   whose queue it is.

   **No production backend change was needed and none was made.** Story 05 already implements both
   `assigneeId=me` and the `resolutionBreached DESC, resolutionDueAt ASC` default; **neither was
   covered by a test**, so `QueueOrderingTests` adds the three that pin the queue's whole promise.
   Suite **330 passing, 1 skipped by design** (was 327/1); front end **41 specs** (was 32).

   **Verified against real SQL Server**, which is the only place the ordering could be proved: a
   manually breach-flagged ticket due **2026-09-01** sorted **above** an unbreached one due
   **2026-08-31**, and the Technical agent's queue contained no Billing ticket. The flag was
   reverted afterwards, so seeded demo state is unchanged.

   **Findings raised: I-28, I-29** — see §6. Neither blocks the slice.

   **08-2 and 08-3 added:** the customer panel becomes a **drawer at phone width** whose slide edge
   mirrors under RTL, rendered as drawer *or* side region and never both, so opening it touches no
   route and cannot remount the composer — the T1-C draft-survival rule holds by structure. The
   composer docks to the bottom at phone width and the queue's filters collapse into the §10.3 filter
   sheet. **Quick replies needed no backend at all**: Story 16 Part A already bound, validated and
   published them, and three are configured. The control feeds the **one** `insert` point the AI draft
   will also use (UI-7), so *"never auto-sent"* holds by construction — and it is **opt-in**, because
   the same component renders in the portal and `GET /config/staff` answers a Customer `403`,
   **verified live**. Front end **46 specs** (was 41).

   **S9-1 still blocks the dashboard task region** and no cross-ticket task endpoint was invented.
   **Findings I-30 and I-31 added**, neither blocking.


