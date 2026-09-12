[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

2. **Story 05 is complete — implemented and verified 2026-08-31.** Story 05 was delivered **whole, not in slices**, so it has no slice map; the plan's
   thirteen numbered tasks were the unit of work, and all thirteen are done. Evidence is in §8, from
   commands run in the tracking task of 2026-08-31.

   **What landed:** `Ticket`, `TicketActivity` and the enums in the Domain with `SlaClock` beside
   them; the `Tickets` migration and `TicketSeeder`; **`TicketScope`, the one department-scoping
   helper every later ticket query composes** (AD-5); `TicketService` with A-14 auto-assignment;
   `TicketsController`'s **seven** operations, taking `openapi/v1.json` to **22 paths**; and the
   workspace ticket list, detail header, assign control and customer panel with their shared
   `StatusChip`, `PriorityChip` and `TicketFilterBar`.

   **Two audit findings are closed by this story.** **S9-2** — the ticket half of story 04's
   attachment acceptance criterion — is discharged by `GET`/`POST /tickets/{id}/attachments`.
   **S9-5** — both SLA due timestamps required at creation — is implemented and proven live.

   **A-20 is implemented as code rather than merely satisfied.** `SlaClock.OnPriorityChanged` is a
   deliberate, commented no-op that `PatchAsync` still calls, so stories 06 and 09 find exactly one
   home for the rule instead of three copies of "we don't touch the dates". Proven live —
   `High → Urgent` moved neither timestamp — and pinned by the two tests of finding **I-15**.

   **Seven findings came out of story 05 (I-14…I-20), all in §6.8, none resolved by invention.**
   **One needs a user decision — I-16:** no approved endpoint lets an Agent list the staff in their
   own department, because all five `/users` routes are Administrator-only (api-design §5.3), so the
   `Assign ▾` picker cannot be populated for the role that needs it most. **The current behaviour was
   deliberately kept** — everyone gets *Assign to me*, which needs no directory and is always legal;
   an Administrator also gets a real picker — after checking the intake, plan task 13 and ui-design
   §5.3 and finding that **none explicitly requires an agent-readable staff directory**. If the user
   reads the intake's *"an agent can assign and reassign a ticket within their department"* as
   requiring an Agent to pick a colleague, that is a **§5 contract addition for the user to take**.
   **I-20 is recorded as a verification-plan wording issue, not a code defect** — the plan's step 3
   greps for `branch` in the ticket Domain folder and matches `Ticket.cs`'s own guard-rail comment;
   the code was not changed to satisfy it, and the intent is proven three stronger ways in §8.
   **The remaining five (I-14, I-15, I-17, I-18, I-19) are implementation consequences with no
   product content.**

   **Four findings now stand as the user's call across the project** — **I-1**, **I-2** and
   **I-12** from story 04, and **I-16** from story 05. None blocks anything built.

   **The demo database was left clean.** The one ticket created for the plan's step 5 was removed
   with its activity rows and the counts re-read (`total tickets: 6`, the seeded number), so story 05
   discloses **no** leftover verification row — unlike story 04's audit-linked customer, which stays
   for the AD-10 reason recorded below.

