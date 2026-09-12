[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 09 is complete — all thirteen plan tasks, implemented and verified 2026-08-31**, as one
   vertical slice: DB, backend, API and front end together.

   **What landed.** The `Notification` entity and its migration; the **persistent publisher replacing
   the logging one** — the swap Story 06's plan promised, with the interface, its four types and every
   call site unchanged and `LoggingNotificationPublisher` deleted, so there is one implementation.
   `SlaEvaluationService` sweeps both clocks, sets the **latching** flags, writes `SlaBreached` history
   with **`actorKind: System` and a null actor**, and escalates through **Story 06's
   `EscalateAsync`** rather than beside it. `SlaMonitorHostedService` is a `PeriodicTimer` holding no
   business logic (AD-6) — **no queue, no broker, no external scheduler**. Round-robin auto-assignment
   replaces Story 05's no-op at the seam `CreateAsync` already called, so **the creation path needed
   no edit**. The notification read side is recipient-scoped with `unreadCount` at the envelope's top
   level, and **no `read-all` route exists** (AP-18). The front end has the typed client, a store, the
   **bell with its unread badge** in the slot Story 08 left, and `/workspace/notifications`.

   **`NotificationType` moved from Application to Domain**, because `Notification` carries it and
   Domain ends with zero references (AD-4). One declaration, so the seam and the row cannot name
   different values.

   **Verified against real SQL Server from a clean volume**: the `Notifications` migration applied,
   **8 tickets seeded (two deliberately overdue)**, the monitor started, and the first sweep **flagged
   exactly those 2** — `High` rose to `Urgent`, `Urgent` stayed `Urgent`, **both kept status `New`**
   (A-5: escalation is an action, not a transition), and four notifications reached the manager.
   A read answered `204` and **a second read left `readAt` and the count unchanged**;
   `/notifications/read-all` answered `404`. Three consecutive tickets auto-assigned to **three
   different agents, all `New`** (A-18). **The queue now leads with the two breached tickets**, so
   Story 08 consumes this data with no change.

   **Index usage proved, not assumed** (plan step 3): at 10 rows the sweep scans, so 20 000 probe rows
   were inserted and the plan captured — the query **seeks both filtered indexes**
   (`IX_Tickets_FirstResponseDueAt` and `IX_Tickets_ResolutionDueAt`) as an index union, exactly what
   data-model §6 created them for. The probe rows were deleted and the volume rebuilt clean.

   **Suite 350 passing, 1 skipped by design** (was 330/1) — twenty new tests, stable across three
   consecutive full runs; front end **46 specs**, unchanged. **Three Story 05–07 tests asserted the
   pre-auto-assignment world and were updated**, one of which had become order-dependent — see I-32.

   **PF-5 remains open and is reported, not silently changed**: `firstRespondedAt` is set only by the
   first outbound message, so **a ticket resolved without a reply is permanently first-response
   breached**. That is A-3 as written; changing it would be a change to A-3.

   **Findings I-32 and I-33 added**, neither blocking.

