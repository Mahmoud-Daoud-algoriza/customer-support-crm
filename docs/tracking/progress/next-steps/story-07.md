[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 07 is complete — implemented and verified 2026-08-31. Phase 4 has begun. Awaiting
   explicit approval before story 08.** Delivered **whole, not in slices**, like stories 05 and 06;
   the plan's ten numbered tasks were the unit of work. Evidence in §8, from commands run in this
   task.

   **What landed.** `TicketMessage` is the **one normalized message model**, immutable by
   construction and carrying the channel it arrived on — the field that makes T3-A a seam rather
   than a rewrite. `TicketMessageService.PostAsync` is the **one ingestion method** the staff
   endpoint, the portal endpoint and story 18's in-process adapter all call, and **it does not
   branch on channel**: the value is a parameter that is stored, which is the whole claim
   architecture §5.2 makes and the one a reviewer should test. `PortalTicketService` publishes the
   **authenticated** web form (A-9) and **creates nothing itself** — it calls
   `TicketService.CreateCoreAsync`, extracted so the A-14 department derivation, the A-3 clock and
   the `Created` row have **one home** shared with the staff create rather than a second path that
   could drift. Three endpoints, `openapi/v1.json` at **28 paths**.

   **The one status side effect is delivered, and story 06's rule was not re-implemented.**
   `ApplyAutomaticCustomerReplyTransitionAsync` — built by story 06 and left with no caller — now
   has its one caller, and the *"only from `Pending`"* guard stays where story 06 put it. Proven
   **live against real SQL Server**: the response carried `"statusChanged": true,
   "ticketStatus": "Open"`, and the activity trail showed the `MessagePosted` and the
   `StatusChanged Pending→Open` rows **at the same timestamp**, the latter with `actorKind: User`
   and the **replying customer** as actor — **R-14 observed rather than asserted**.

   **Two absences are load-bearing and were checked as absences.** **AP-11**: no inbound HTTP
   endpoint exists, `grep` finds no `channels/inbound` or `webhook`, and the published path list
   confirms it — so **PF-2 stays untouched and open for story 18**. **T3-B**: a source sweep of
   `backend/src`, `frontend/src` and `README.md` for *real-time*, *websocket*, *polling*,
   *setInterval* and *chat* returns three hits, **all of them negations**. Nothing polls, and
   nothing is described as chat.

   **One defect was found by running the stack, and it is the kind the suite cannot see.** The
   seeded demo thread was written `now + minutes`, so the R-13 reply a demo posts seconds later
   sorted **above** the question it answered and the conversation read backwards. Fixed to seconds,
   re-verified from an empty database. Recorded as **I-26** — the same class as **I-8** and
   **I-17**, and invisible to the hermetic suite because the SQLite host removes every seeder.

   **One finding needs the user's decision, and it is the only one.** **I-25** — no approved
   document states the **priority** a customer-submitted ticket is created with. `Ticket.priority`
   is required and both SLA timestamps are computed from it at creation (A-3), while A-6 says
   priority is *"set by the agent or by the AI suggestion"*, A-17 forbids `isUrgent` from setting
   it, and api-design §5.7 refuses it in the body. **`Medium` was implemented** — the neutral middle
   of A-6's scale, the value that claims least before an agent or the AI has decided — as a named
   constant carrying the finding. **A-17 holds either way**, and is asserted: `isUrgent = true` sits
   on the same row as `Priority = Medium`. **If the user reads it differently, the change is one
   line plus its assertion.** **I-27** is informational: `MessageChannel.WebForm` is written by no
   path in this story, because api-design §7 maps it to *"portal creation"* while data-model §2.6
   forbids a submission from writing a first message — the authority order settles it, the member
   stands as declared seam surface, and story 18's adapter is its first writer.

   **Four findings stand as the user's call across the project** — **I-1**, **I-2**, **I-12** and
   **I-16** — plus **I-25** from this story. None blocks anything built.

   **The demo database was left as the seeders wrote it**, plus the rows the by-hand verification
   deliberately created: one customer reply on the seeded `Pending` ticket, one submitted web-form
   ticket, and one ticket moved to `Cancelled` to prove the terminal `409`. **The stack was then
   stopped with `docker compose down`, so the next `up` re-seeds from the same volume** — those
   three rows are demo-visible and are disclosed here rather than quietly left.

