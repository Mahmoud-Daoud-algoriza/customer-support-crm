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
| 3 — notes, timeline, attachments | 4, 5, 6, 9 | Not started |
| 4 — controllers | 8 — the nine customer routes, the AP-19 download, and `POST /auth/register`'s route | Not started |
| 5 — registration | 7, plus `User.CreateCustomerUser` | Not started |
| 6 — front end | 11, 12, 13, 14 | Not started |

> **Slice 2 was narrowed to task 3 alone** at the user's instruction. The earlier map paired task 3
> with task 8's customer routes; task 8 now has its own slice, so **no endpoint is published yet**.

**Neither slice has published an endpoint**, which is checked rather than assumed: `/customers`,
`/attachments/{id}/content` and `/auth/register` all return `404` against the running API, and
`openapi/v1.json` still lists **11 paths** with no customer or attachment route among them.

**Tests so far:** `CustomerDataLayerTests` (14) and `AttachmentStorageTests` (8) from slice 1,
`CustomerServiceTests` (24) from slice 2 — **46 tests** under
[`tests/SupportCrm.Tests/Customers/`](../../../backend/tests/SupportCrm.Tests/Customers/). Plan task
10's `CustomerAccessTests.cs` belongs to slice 4, which publishes the endpoints it exercises: its
`403`, `404`-not-`403` and Problem Details assertions all need a route to call.
