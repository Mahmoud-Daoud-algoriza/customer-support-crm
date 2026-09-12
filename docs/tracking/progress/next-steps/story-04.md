[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

3. **Story 04 is complete — all six slices done and verified.** Story 04 was delivered in slices at the user's instruction; the map lives in
   [.squad/plans/customer-management/00-overview.md](../.squad/plans/customer-management/00-overview.md).

   | Slice | Plan tasks | State |
   |---|---|---|
   | **1 — domain and data layer** | 1, 2 | ✅ **Done and verified 2026-08-27** |
   | **2 — `CustomerService`, carrying A-19** | 3 | ✅ **Done and verified 2026-08-27** |
   | **3 — notes, timeline, attachments, seed** | 4, 5, 6, 9 | ✅ **Done and verified 2026-08-28** |
   | **4 — controllers** | 8, 10 | ✅ **Done and verified 2026-08-28** |
   | **— cross-cutting: AP-10 / finding I-9** | none — a contract fix, not a plan task | ✅ **Done and verified 2026-08-28** |
   | **— cross-cutting: AP-2 / finding I-10** | none — a contract fix, not a plan task | ✅ **Done and verified 2026-08-28** |
   | **5 — registration** | 7 — `RegisterAsync`, the route, the three A-15 outcomes | ✅ **Done and verified 2026-08-28** |
   | **6 — front end** | 11, 12, 13, 14 | ✅ **Done and verified 2026-08-30** |

   **Thirteen findings came out of story 04's six slices and the two contract fixes, and none is
   resolved by invention** — all are in §6.8, alongside story 05's seven. **Three of story 04's
   remain the user's call:** the plan requires a
   "configured root" for attachment storage that no approved document supplies (**I-1**); data-model
   §6 declares no index for `CustomerNote` where it declares one for the analogous
   `TicketInternalNote` (**I-2**); and **no §6.9 payload publishes the attachment size cap** that
   ui-design §8 asks the uploader to show, so the cap line is absent while `413` still reads as a
   translated sentence (**I-12**, new in slice 6). **Three are closed:** AP-10 is enforced globally
   (**I-9**), every model-state `400` now carries the AP-2 slug and §6.12 envelope (**I-10**), and
   **I-11 is withdrawn** — the `Msg 8624` on a `Customers` delete was `sqlcmd`'s
   `QUOTED_IDENTIFIER OFF` meeting a filtered index, not a schema defect, proven on 2026-08-30 by
   varying only that setting on the same statement. **I-5 was closed by slice 5.** The rest are
   implementation consequences with no product content (**I-3, I-4, I-6, I-7, I-13**) plus one real
   deployment defect found by running the stack and fixed (**I-8**).

   **One dev row is disclosed rather than cleaned up.** Driving the real registration form for the
   plan's verification step 5 created the customer `slice6.verify.1788107299323@example.com`, and it
   cannot be removed: deleting its login would mean deleting an append-only `UserCreated` audit entry
   (AD-10). The dev database therefore holds **10 customers, not the seeded 9** — the same situation
   slice 5's three verification rows left, and harmless for the same reason.

