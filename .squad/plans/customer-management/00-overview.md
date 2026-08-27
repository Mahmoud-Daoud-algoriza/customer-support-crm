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
| 2 — customer service and endpoints | 3, 8 (customer routes) — `CustomerService`, including the **A-19** propagation and its one `UserEmailChanged` audit entry | Not started |
| 3 — notes, timeline, attachments | 4, 5, 6, 8 (remaining routes), 9 | Not started |
| 4 — registration | 7, plus `User.CreateCustomerUser` | Not started |
| 5 — front end | 11, 12, 13, 14 | Not started |

**Slice 1 published no endpoint**, which is checked rather than assumed: `/customers`,
`/attachments/{id}/content` and `/auth/register` all return `404` against the running API. Its
tests are `tests/SupportCrm.Tests/Customers/CustomerDataLayerTests.cs` and
`AttachmentStorageTests.cs` — **22 tests**. Plan task 10's `CustomerAccessTests.cs` belongs to the
slices that publish the endpoints it exercises.
