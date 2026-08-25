# platform-experience — plan overview

Entry point for the **platform-experience** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 17 | [17-story-i18n-responsive-branding.md](17-story-i18n-responsive-branding.md) | Arabic/English i18n, RTL, responsive layout and branding | — | **Part A:** Story 01 · **Part B:** every T1/T2 screen |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/platform-experience/`](../../stories/platform-experience/).

## Dependency notes

**SPLIT IN TIME — Phase 0 and Phase 8.** `docs/story-backlog.md` records the exception.

| Part | Phase | Delivers |
|---|---|---|
| **A — Scaffolding** | **0**, inside [Story 01](../platform-foundation/01-story-solution-skeleton.md) task 12 | Transloco, `en`/`ar` dictionaries, direction service, language switcher, **logical-property stylelint rule**, breakpoints, runtime branding loader |
| **B — Translation pass** | **8**, after every T1/T2 screen exists | String extraction, RTL audit against real screens, PrimeNG verification in Arabic, Gregorian date pipe, phone-width regression check, branding proof |

- **Part A is uncuttable in practice** — retrofitting direction handling into every component costs
  more than doing it once. The **full translation pass is cuttable**; the branding seam is T3 and is
  cut before that.
- **Almost no backend work**, and that is the design: the API returns **codes, not display prose**
  (architecture §2.3), so translation lives entirely in the front end.
- Introduces **no endpoint** — a conclusion recorded in `api-design.md` §10.1, not an omission.
