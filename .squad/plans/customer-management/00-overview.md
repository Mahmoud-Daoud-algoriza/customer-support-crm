# customer-management — plan overview

Entry point for the **customer-management** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 04 | [04-story-customer-records.md](04-story-customer-records.md) | Customer profiles, notes, attachments and interaction history | — | Stories 01–03, **16 Part A** |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/customer-management/`](../../stories/customer-management/).

## Dependency notes

**Phase 2.** Runs after [16 Part A](../administration/16-story-audit-configuration.md) supplies the
default branch (A-15) and the attachment size cap.

- Also delivers **`POST /auth/register`**, deferred from Story 02 (finding **S9-7**).
- The interaction timeline is built **against an empty ticket set** and completed by
  [Story 06](../ticket-management/06-story-ticket-lifecycle.md); the intake authorizes exactly this.
- The shared attachment service and the AP-19 download land here; the two **ticket**-scoped
  attachment endpoints are published by [Story 05](../ticket-management/05-story-ticket-core.md),
  so this story's attachment AC completes there (finding **S9-2**).
- ✅ **OQ-5 is closed (2026-08-27) by A-19** — changing `Customer.email` **does** change a linked
  portal login's sign-in email, in the same unit of work, with `User.email`'s existing uniqueness
  rule applied to the new value. Implemented by **task 3** (`CustomerService.UpdateAsync`), not
  task 5, which the story plan's decision box now specifies case by case.
- Attachments (T2-A) are cut before profiles, notes and timeline (T1-A).

## Implementation progress

Story 04 is being delivered in **slices**, at the user's instruction, with approval taken between
each. The slice boundaries are not in the plan; the plan's numbered tasks are.

| Slice | Plan tasks | Status |
|---|---|---|
| **1 — domain and data layer** | **1, 2** — the three `Customers` entities, their EF configurations, the `Customers` migration, the `User.CustomerId` FK that completes DM-1, and the `IAttachmentStorage` seam with its local-disk implementation | ✅ **Done and verified 2026-08-27** |
| **2 — customer service** | **3** — `CustomerService`, including the **A-19** propagation and its one `UserEmailChanged` audit entry | ✅ **Done and verified 2026-08-27** |
| **3 — notes, timeline, attachments, seed** | **4, 5, 6, 9** — `CustomerNoteService`, `CustomerTimelineService` (against the empty ticket set), `AttachmentService` (both owners, AP-19 download), and `CustomerSeeder` | ✅ **Done and verified 2026-08-28** |
| 4 — controllers | 8 — the nine customer routes and the AP-19 download. **Plan task 10's `CustomerAccessTests` lands here**, because its `403` and `404`-not-`403` assertions need a route to call | Not started |
| 5 — registration | 7 — `AuthService.RegisterAsync`, `POST /auth/register`, the three A-15 outcomes | Not started |
| 6 — front end | 11, 12, 13, 14 | Not started |

> **Slice 2 was narrowed to task 3 alone** at the user's instruction. The earlier map paired task 3
> with task 8's customer routes; task 8 now has its own slice.
>
> **`User.CreateCustomerUser` moved from slice 5 to slice 3**, because task 9 cannot seed *"at least
> two with a linked portal `User`"* without it. The factory alone moved — `RegisterAsync`, the
> endpoint and the three A-15 outcomes are all still slice 5's. See finding **I-5**.

**No slice has published an endpoint**, which is checked rather than assumed: `/customers`,
`/customers/{id}/notes`, `/customers/{id}/timeline`, `/customers/{id}/attachments`,
`/attachments/{id}/content` and `/auth/register` all return `404` against the running API, and
`openapi/v1.json` still lists **11 paths** with none of them among it.

**Tests so far:** `CustomerDataLayerTests` (14) and `AttachmentStorageTests` (8) from slice 1,
`CustomerServiceTests` (24) from slice 2, and `CustomerNotesAndTimelineTests` (9),
`AttachmentServiceTests` (11) and `CustomerSeederTests` (6) from slice 3 — **72 tests** under
[`tests/SupportCrm.Tests/Customers/`](../../../backend/tests/SupportCrm.Tests/Customers/).

**One Done Criterion is met early; three are met but not yet provable end to end.** The
empty-timeline criterion is satisfied now and stays satisfied when Story 06 fills the projection in.
The `storagePath`-in-no-response criterion is proven at the DTO and serialization level, the `413`
cap at the service level, and the note-immutability criterion structurally — but each of their
HTTP-level forms, and *"a Customer cannot browse the customer directory"*, wait on slice 4's routes.
