# identity-access — plan overview

Entry point for the **identity-access** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 02 | [02-story-auth-and-roles.md](02-story-auth-and-roles.md) | Authentication, roles and permission enforcement | — | Story 01; **Story 03 data layer first** |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/identity-access/`](../../stories/identity-access/).

## Dependency notes

**Phase 1.** Executes as an **adjacent interleaved pair** with
[03-story-departments-branches.md](../organization/03-story-departments-branches.md): Story 03's
entities and the initial migration, then this story, then Story 03's endpoints and filters.
`POST /users` requires a `departmentId` for every staff role (finding **S9-12**).

- Introduces `AuditEntry` and the single `IAuditRecorder`, because this story's own acceptance
  criterion requires sign-in recording (finding **S9-5**). The audit **read** surface is
  [16 Part B](../administration/16-story-audit-configuration.md).
- **`POST /auth/register` is deferred to [Story 04](../customer-management/04-story-customer-records.md)** —
  A-15 needs a `Customer`, a `Branch` and a configured default branch (finding **S9-7**).
- Ticket scoping is [Story 05](../ticket-management/05-story-ticket-core.md)'s, because
  architecture §4.3 puts the helper in the Tickets module.
- **T1 — cannot be cut.**
