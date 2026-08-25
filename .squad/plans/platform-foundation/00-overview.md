# platform-foundation — plan overview

Entry point for the **platform-foundation** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-solution-skeleton.md](01-story-solution-skeleton.md) | Solution skeleton, local run and API documentation | — | — |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/platform-foundation/`](../../stories/platform-foundation/).

## Dependency notes

**Phase 0.** The first story in the sequence; every other story depends on it.

- Fixes the project layout, the four-project dependency rule (AD-2), the Compose stack, the
  configuration mechanism, the Problem Details handler and the Angular folder skeleton.
- **Also delivers Story 17 Part A** — the i18n/RTL scaffolding — by the split-in-time exception in
  `docs/story-backlog.md`. See [17-story-i18n-responsive-branding.md](../platform-experience/17-story-i18n-responsive-branding.md).
- Delivers `/health` and the anonymous `/config/bootstrap` only. The two **authenticated**
  configuration tiers (`/config`, `/config/staff`) belong to
  [16 Part A](../administration/16-story-audit-configuration.md) — finding **S9-13**.
- **Cannot be cut.**
