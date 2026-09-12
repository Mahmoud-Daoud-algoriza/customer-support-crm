[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 11 is complete — all seven plan tasks, implemented and verified 2026-08-31 — with one Done
   Criterion left open by S9-4 (see below). All T1 scope is now delivered.**

   **What landed.** `TicketAiAssistService` and the three endpoints of api-design §5.8. **Scoping runs
   before any provider call** — an out-of-department ticket is `404` first, so customer content never
   leaves the process for a ticket the caller may not read, and a **recording stub proves the seam was
   never invoked**. On the front end: the `AiAssistPanel` with **an always-visible AI-generated label**
   (a rendered element, not a tooltip — A-8, UI-6), *Insert into reply* routed to the **same composer
   Story 08's quick replies use** (UI-7, so there is one draft and one Send), and
   **`/workspace/tickets/new`** — the agent creation screen T1-B requires and Story 11 task 6
   specifies — where a blur asks for a category and priority suggestion that **pre-selects both
   selectors without touching the agent's words**.

   **Verified live against real SQL Server with no AI credentials**: all three assists answer through
   the fake, the same ticket **summarizes identically twice**, a Customer gets **`403`** on every
   assist, and the ticket row is **unchanged** afterwards.

   **T1-F proved by hand, not only by test.** A second API host was started with
   `Provider=Provider` against an unreachable endpoint: all three assists returned
   **`503 ai-unavailable`** while, in the same run, **creation, a reply and a transition all
   succeeded**. That pairing is the requirement; either half alone proves much less.

   **Suite 373 passing, 1 skipped by design** (was 364/1); front end **51 specs** (was 46).

   ⛔ **S9-4 leaves one Done Criterion open, and no field was invented to close it.** *"The suggested
   values, and whether the agent accepted or overrode them, are written to ticket history"* has **no
   contract path**: `POST /tickets` accepts six fields and none is this. The capture and a **marked
   call site** are in place; the server would reject an undocumented member with a `400` (AP-10), so
   sending one would break creation. **This needs a Stage 7 decision** — a request field on
   `POST /tickets`, or a small recording endpoint.

