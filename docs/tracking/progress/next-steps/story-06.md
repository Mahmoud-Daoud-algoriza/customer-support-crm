[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0a. **Story 06 is complete — implemented and verified 2026-08-31. Phase 3 and the assessment's core
   loop are closed. Awaiting explicit approval before story 07.** Delivered **whole, not in slices**,
   like story 05; the plan's eleven numbered tasks were the unit of work. Evidence in §8, from
   commands run in this task.

   **What landed.** `TicketLifecycle` holds A-5's graph **once, as data**, and `Ticket.TransitionTo`
   is the **only writer of `Status`** in the codebase — so an illegal edge is refused however the
   ticket is reached. `TransitionAuthority` holds A-16 **separately**, which is what lets the API
   fail two different ways: **`403 transition-not-permitted`** for authority, **`409
   illegal-transition`** for legality, the latter carrying `allowedTransitions`. `Escalation` is the
   AP-7 action — priority up one level, `Urgent` stays `Urgent`, **status untouched**.
   `TicketLifecycleService` enforces the §5.6 order (scope → authority → legality) in one unit of
   work with its activity row and audit entry. `TicketActivityQueryService` publishes the
   append-only history, and **`CustomerTimelineService` — written by story 04 against an empty
   ticket set — now projects real rows**. Front end: the transition menu, the separate escalate
   control, the activity region, and a populated timeline. Three endpoints, `openapi/v1.json` at
   **25 paths**.

   **A-21 is applied, not re-implemented**, and verified on **both** rungs against real SQL Server:
   with the seeded manager the recipient is the department's own; with the manager unseated the
   `Warning` fired naming `AllManagers` and **the escalation still succeeded**. The cascade appears
   nowhere in story 06's code.

   **Story 04's last outstanding Done Criterion is discharged.** The interaction timeline is
   populated — the criterion story 04's plan deferred here by name.

   **Three findings, all about the plan rather than the product, none needing a decision.**
   **I-22** — a system-initiated escalation has no auditable actor and no approved source says what
   it should record, so the audit entry is written on the **user** path only and the system path is
   left explicitly unaudited for **story 09** to settle; the *activity* row is written either way,
   so ticket history is complete. **I-23** — the plan's customer-facing transition tests cannot run
   here, because no endpoint in this story admits a `Customer` (the staff controller is
   `RequireAgent` by task 7's own instruction, and the portal route is **story 13's**); A-16's
   customer column is proven against `TransitionAuthority` over the full 6×6 matrix plus the `403`
   a customer actually gets, and pinned on the client too. **I-24** — two plan lines written before
   A-21 contradicted it (*"notifies the department manager when one is set"* and *"wording that
   makes no notification claim"*); both were corrected in place, each saying what it previously said
   and why it changed.

   **F-1 remains open by design.** The client's A-5 and A-16 copies live in **exactly one** file,
   `shared/lifecycle/transition-matrix.ts`, labelled F-1 — and are now pinned by **9 specs**
   transcribed from product-scope, so the duplicate cannot drift unnoticed. Closing F-1 later still
   deletes exactly one file plus its spec.

   **Four findings stand as the user's call across the project** — **I-1**, **I-2**, **I-12** and
   **I-16**. None blocks anything built.

   **The demo database was left clean.** The verification ticket, its activity and audit rows and the
   inserted `Internal` row were all removed and the counts re-read (`total tickets: 6`). Story 06
   discloses **no** leftover verification row.

1. **OQ-3 is closed, and its shared foundation was built before story 06 began.**

   **The decision (2026-08-31, A-21, R-17).** The escalation notification recipient is a cascade:
   the department's own manager when `managerUserId` is set **and that user is still active and
   still holds `Manager` or `Administrator`**; otherwise every active `Manager`; otherwise every
   active `Administrator`; otherwise nobody. **Escalation is never blocked** — the breach flag and
   the priority raise occur on every rung. Taken by the product owner from four options with their
   API, authorization and front-end consequences set out.

   **What was built, and deliberately nothing more.** `IEscalationRecipientPolicy` and its one
   implementation in `Application/Modules/Sla/`, registered in the composition root, with **8 tests
   covering every rung**. **It has no caller yet** — story 06's manual escalate and story 09's
   automatic sweep are its two callers and neither exists. This follows A-20's shape on purpose: a
   rule with more than one caller gets **one named seam**, so the cascade is never re-expressed at a
   call site.

   **No contract surface moved.** No endpoint, payload, response field, error slug, entity, column
   or migration. `openapi/v1.json` still publishes **22 paths**. The decision is observable only in
   whose recipient-scoped notification list gains a row once story 09 writes them.

   **The `Warning` log level is standardised**, in the policy rather than at the call sites — the
   story 06 and story 09 plans had specified `Information` and `Warning` for the same condition, and
   putting the line inside the policy is what makes the two paths unable to diverge at all.

   **One finding — I-21, and it wants the user's eye.** A-21's rung 1 says *"when `managerUserId` is
   set"* and does not say what happens when the user it points at is no longer usable. It is
   implemented as **"set *and still eligible*"**, falling through to rung 2 for a manager who was
   deactivated or demoted after appointment — because data-model §2.2's invariant already requires
   the reference to be an active `Manager`/`Administrator`, AD-15 makes the user row authoritative
   over a stale reference, and the user's own wording for rungs 2 and 3 says *"every **active**"*.
   **If rung 1 was meant literally, it is a one-line change** and two `[InlineData]` rows are what
   would go red.

   **Documents amended, consistently:** product-scope §7 (**new A-21**), data-model §2.2 and §8,
   api-design §5.6 and §6.2, ui-design §5.3 and §11 (**the dialog wording constraint is lifted** —
   it may now say *a manager* will be notified, one sentence true on every rung, with no new payload
   field), the story 06 and story 09 plans (**both now carry the same recipient rule and the same
   log level**), three feature overviews, `00-implementation-plan.md` §7.3 and `00-index.md`.

