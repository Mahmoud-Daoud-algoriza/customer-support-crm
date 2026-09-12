[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 15 (`management-dashboard`) is implemented — all six plan tasks, delivered whole — and
   its backend, data-layer and live-stack verification passed 2026-09-06. It is NOT verified in
   full.** One thing is outstanding, and it is verification debt rather than a decision.

   ✅ **PF-4 / S9-9 was closed before a line was written — R-18.** The approved documents genuinely
   did not decide *"tickets assigned"*: requirements §9.3 and T2-G word it unqualified, api-design
   §5.11 says *"this contract does not decide it"*, §6.8 calls it *"deliberately unqualified"*, and
   §9 item 2's requirement to pin it before planning was never met. **The product owner chose
   *currently assigned*.** One correction to how the plan scoped this: it called the decision
   *"isolated to one method"*, which is true of **where it lives** but not of **what it blocked** —
   `assignedCount` is composed into the single §6.8 response, so the plan's *"let the method throw"*
   would have made **every** dashboard call a `500`. PF-4 gated the endpoint, which is why the
   decision was taken up front rather than the story part-delivered.

   **What landed.** Four aggregate queries behind **one** `RequireManager` endpoint. **§9 introduces
   no entity** (data-model §7), so there is **no migration**. The two filters apply **once**, with
   `branchId` resolved **through the customer** — proven live, where the API's per-branch totals
   equal the SQL count joined via `Customers` (**6** and **5**) and **differ** from the count joined
   via the assigned agent (**4** and **3**). **`TicketScope` is deliberately not composed**, with the
   reason at the code, and a test asserts a Manager's unfiltered dashboard spans both departments
   (AD-5).

   **"Empty is not zero" is implemented three times and asserted three times**: no ratings →
   `averageRating: null`; no resolutions → `averageResolutionHours: null`; a priority with no
   tickets → `attainmentPercent: null`, **never `100`**. On a scratch database with every rating
   deleted the raw JSON reads `"averageRating":null` — exactly as §6.8 writes it, which it did not
   before finding **I-40**. Suite **451 passing, 0 skipped** (was 430), **21 this story's**. Front
   end **66 specs**, unchanged. **`openapi/v1.json` now publishes 66 operations — the complete
   api-design contract**, story 15 having closed the last gap.

   ⚠ **The front-end screen checks are outstanding.** Plan verification step 8 — all four regions
   rendering, filters combining and surviving a reload, tables scrolling internally at phone width —
   **could not be run**: no browser-driving capability was available, the same limitation story 14
   hit. **No result was fabricated.** The four assertions are enumerated in §8. **Until they are
   covered, story 15 does not count toward §1.1**, which is why the figure stays at 71.1%.

   **Three findings, all informational — I-38, I-39, I-40.** The resolution average is computed over
   a two-column projection because no date-arithmetic expression translates on both providers;
   AP-15's unknown-parameter refusal is enforced **on this endpoint only**, the wider gap reported
   rather than fixed; and three meaningful nulls opt back into serialization under §2's own
   exception.

   **Story 15 is where this task stops.** Stories **17 Part B** and **18** are what remain of the
   approved scope, and neither is started. 17 Part B is next in phase order — and its own
   precondition, *"after every T1/T2 screen exists"*, is **now met** for the first time.

