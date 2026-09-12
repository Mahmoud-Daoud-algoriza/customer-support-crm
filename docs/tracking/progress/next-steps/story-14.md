[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 14 (`tasks-internal-notes`) is implemented — plan tasks 1–7, 9 and 10, delivered whole —
   and its backend, data-layer and live-stack verification passed 2026-09-06. It is NOT verified in
   full.** Two things are open on it, and they are different in kind.

   **What landed.** T2-C's two halves, and the higher-risk one made **structural rather than
   filtered**. `TicketInternalNote` is a separate table that **no customer-facing query names** — so
   `TicketInternalNoteService` contains no exclusion logic, because there is nothing to exclude, and
   the three customer-facing reads were each re-read and needed **no change** (the portal thread
   queries `TicketMessage`; the timeline excludes `Internal` **and** does not join the note table;
   A-13 has four notification types and none is a note). `TicketActivity.InternalNotePosted` accepts
   **neither a type nor a visibility**, so §2.7's *"set if and only if"* and *"always `Internal`"*
   cannot be spelled wrongly at a call site. `TicketTask.SetDone` is the **only** writer of
   `completedAt` (§5 constraint 23). Five `RequireAgent` endpoints; **no portal counterpart, and
   none may be added** (AP-5) — *the path space is the visibility rule*. The ticket detail gains the
   internal-notes region with its **persistent** "Not visible to the customer" marker (UI-5) and the
   tasks region with overdue rows distinguished.

   **The rule was proven, not asserted.** A canary note posted live against real SQL Server returns
   **0** hits on the portal thread, the portal detail, the portal list and the customer timeline —
   against a **populated** 3-message thread. Reverting the one line that carries the rule turned
   **exactly 2** tests red and 428 stayed green; the line was restored and re-proved. Backend suite
   **430 passing, 0 skipped** (was 418/1) — **the skip reached zero because story 14 implemented
   `InternalNotesAreUnreachableTests` rather than deleting it**, which is what story 07's stub
   demanded.

   ⛔ **1. S9-1 is due, and it is the user's decision.** Plan task 8 — the dashboard task region — is
   **not built**. Four approved documents assume a cross-ticket task list; api-design §5.6 publishes
   none. The plan says *"do not invent an endpoint"*, so `TicketTaskService.ListForUserAsync` was
   written, marked, and **left referenced by no controller**, and story 08's marker at
   `agent-queue.component.ts:175` is untouched. **Story 14's AC 2 stays open until Option A (publish
   `GET /tasks?assignedUserId=me&isDone=false` — a Stage 7 change) or Option B (drop the region — a
   Stage 8 change recorded per product-scope §10) is taken.** Nothing was invented.

   ⚠ **2. The front-end screen checks are outstanding**, and this is verification debt rather than a
   decision. Plan verification step 7 and the ui-design §10 browser pass **could not be run** — no
   browser-driving capability was available and installing one was outside the slice. **No result
   was fabricated.** The four specific assertions still needing coverage are enumerated in §8.
   **Until they are covered, story 14 does not count toward §1.1's delivery track**, which is why
   the figure stays at 71.1% rather than moving to 74.7%.

   **One new finding, I-37 (informational)** — no document fixes a default sort for
   `GET /tickets/{id}/tasks`; soonest-due-first was implemented and stated at the code. **I-16 was
   reused rather than re-raised** for the task assignee picker. **Two deliberate deviations are
   recorded in §6.8** — **D-1**, the `TicketAssigneePolicy` extraction the plan's *"reuse the check"*
   required, and **D-2**, the `CustomerNotesAndTimelineTests` guard re-expressed exactly as its own
   remarks instructed once an internal-note set appeared.

   **Story 14 is where this task stops.** Story 15 (`management-dashboard`) is next in phase order
   and is **not started**.

