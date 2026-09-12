[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 10 is complete — all six plan tasks, implemented and verified 2026-08-31.** Backend only:
   **no endpoint, no screen and no entity** (DM-5).

   **What landed.** One interface, `IAiAssistService`, covering summary, suggested reply and
   classification — and **A-8 is a property of its shape rather than a discipline**: every method
   returns a suggestion record, none is named for an action, and **no method takes a ticket id**, so
   there is nothing to write back to. `AiSeamShapeTests` asserts that **by reflection**, so adding an
   action method fails the build (AD-12). Two implementations behind it: a **deterministic offline
   fake** deriving every output from SHA-256 of the input — no `Random`, no ambient state, and a test
   proving byte-identical output across two instances — and a provider adapter where **every failure
   mode becomes `AiUnavailableException`**, so no vendor exception type crosses the seam.

   **The fake is the default**, which is what makes *"runs with no external accounts"* true rather
   than aspirational. Verified live: with no AI key configured, `docker compose up` starts clean and
   logs **"AI seam: deterministic offline fake implementation selected"**. Selecting a provider
   without a key **fails startup with a sentence naming the setting**, and the adapter also refuses to
   construct — a missing credential is an operator's problem at boot, not a `401` reaching the first
   agent who presses Summarize.

   **T1-F's degradation requirement is tested at the seam**: with an `IAiAssistService` that throws on
   every call, ticket creation, a staff reply, a status transition, a portal submission and a portal
   reply **all still succeed** — plus a test proving the failing double really fails, without which
   the others would pass against a working seam.

   **Suite 364 passing, 1 skipped by design** (was 350/1) — fourteen new tests. Front end unchanged at
   **46 specs**: ui-design §13 records this story as having no screen.

   **Product-scope §9 question 1 is deliberately unanswered**, and the module README records what a
   real implementation must still add — credentials, retry, token accounting, prompt versioning and a
   **data-residency decision for customer content**. It also records that the chatbot (T3-C) would be a
   *consumer* of this interface, and that **suggested solutions (§7.4) do not use this seam** — they
   are Knowledge-module keyword retrieval (AD-13, Story 12).

   **Finding I-34 added**, not blocking.

