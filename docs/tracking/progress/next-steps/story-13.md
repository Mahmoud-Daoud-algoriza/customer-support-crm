[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 13 (`portal-self-service`) is complete — all twelve plan tasks, implemented and verified
   2026-09-01**, delivered **whole rather than in slices**. **Phase 6 closes.**

   **What landed.** The **second actor surface**, which product-scope calls out as carrying *"much of
   the permission demonstration"* — and it is a separate, simpler surface **over the same backend**,
   not a second application with its own data. A customer signs in to a shell that is not the agent
   workspace (**two destinations, no sidebar, no notification bell** — ui-design §4.2), submits through
   the **four-input** form of §7.2, tracks their own requests as **cards** with a *"response needed
   from you"* cue on `Pending` (§7.1), reads the thread and replies (§7.3), browses **public** KB
   articles (§7.4, story 12's), and is offered a **one-question rating with an optional comment** once
   a request reaches `Resolved` (§8.5).

   **`CustomerFeedback` is in the `Tickets` module, and that is DM-7.** No eleventh backend module was
   created. It is **write-once by construction** — private setters, **no mutator at all**, and no
   update or delete method on the service or the API — so §2.15's *"not editable, not resubmittable"*
   is structural rather than a rule someone remembers. **Declining is a normal outcome**: the absence
   of a row is the recorded answer, there is no decline endpoint and no *"declined"* state, and
   reporting must read a missing row as **"no response", never as a zero**.

   **A-16's customer column is reachable for the first time, which closes I-23.** Cancel is offered
   **only while `New`** — the window **A-18** keeps genuinely open — and the live check proves the part
   that matters: a request submitted through the web form came back `New` **with an auto-assigned
   agent**, and the customer cancelled it. Once `Open`, the control **disappears with a line explaining
   that work has started** and the same call is `403 transition-not-permitted`. Reopen is offered
   **only on `Resolved`**; a customer **cannot close**; and **no manual reopen is offered on
   `Pending`**, because **R-13** does it automatically and the reply's own envelope carries
   `ticketStatus` and `statusChanged`, so the chip moves **in place, with no re-fetch and no guess**.

   **The portal path space is complete at eleven endpoints**, matching api-design §5.7 exactly, and
   **no portal payload carries a staff field** — asserted on the **raw JSON** of the detail, a list row
   and the transition response, not on the DTO type, because the type is a promise and the payload is
   what a customer receives. **Internal notes stay unreachable by path**: there is no route to filter.

   **⚠ OQ-1 is not answered, and nothing here answers it by accident.** ui-design §11's interim
   behaviour was implemented as written: the schema carries **no check constraint** (proved live — the
   database accepted a rating of `999`), the service reads the range from the `Feedback rating scale`
   key on every call, `FeedbackTests` derives **both** out-of-range values from configuration so the
   file survives a binary answer, the seeder reads `FeedbackOptions.Max` rather than writing a number,
   and `RatingInput` renders `min..max` with **no star widget, no `1..5` array and no thumbs pair** —
   with `binaryLabels()` left as a documented, deliberately inert seam. A browser check compares the
   rendered steps against `GET /config` in the same assertion, so a hardcoded widget fails a test.

   **Verified against real SQL Server** on a fresh volume: the migration applied, the seeder reporting
   `11 ticket(s), 7 message(s) and 1 rating(s)`, another customer's id **`404` and byte-identical to a
   missing one**, the unique index refusing a duplicate at the database (error 2601), and
   `DateTimeOffset` ordering — which the SQLite host cannot prove — correct in both directions. **22
   browser checks at 390 px**, all passing, including single column and **zero** horizontal body
   overflow on every portal screen. Backend suite **417 passing, 1 skipped** (16 this story's own,
   exactly the sixteen the plan names); front end **66 specs** (4 this story's).

   **Two findings need a user decision — I-35 and I-36.** §7.1 asks each card to show a *"last
   update"* that **no payload and no entity carries**, so the card shows `createdAt` and `resolvedAt`
   under honest labels instead of inventing one; and the feedback endpoint's second refusal needed a
   slug, `feedback-not-available`, that **api-design §6.12's enumeration does not list**. Both are
   recorded rather than resolved. **One smaller choice is recorded at the code rather than as a
   numbered finding**, on the same basis as story 12's keyword extraction and story 16 Part B's
   inclusive `to` bound: no document fixes a default sort *direction* for `GET /portal/tickets`, so it
   defaults to **newest-first**, which is the direction §5.5 already fixes for every other
   customer-facing list, and says so in its own comment.

   **One defect was found by driving the real browser and fixed**: the shared `ReplyComposer` was
   showing its staff placeholder — *"Write a reply to the customer…"* — to the customer. It now takes a
   `placeholderKey`, which is the third configuration point between the two uses of the one component
   (ui-design §8), alongside quick replies and the AI insert.

   **Story 13 is where this task stops.** Story 14 (`tasks-internal-notes`) is next in phase order and
   is **not started**; it also owns the deliberately-skipped `InternalNotesAreUnreachableTests`, which
   this story left skipped, with its `Assert.Fail` body intact, rather than deleting or hollowing out.

