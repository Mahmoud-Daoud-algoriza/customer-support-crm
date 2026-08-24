# Story intake — Arabic/English i18n, RTL, responsive layout and branding

> **Source of truth:** `docs/requirements.md` §12 · `docs/product-scope.md` T2-J, T3-E, T3-F, A-11
> **Scope tier:** T2 (i18n/RTL) + T3 (branding, responsive interpretation of "mobile")

## Feature

- **Feature name (display):** Platform Experience
- **Feature slug (folder under `plans/`):** `platform-experience`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `i18n-responsive-branding`)
- **Work item type:** Story

---

## Title

```
Arabic/English i18n, RTL, responsive layout and branding
```

---

## Description

```
The cross-cutting platform lines of requirements §12, excluding multi-department and
multi-branch (owned by `organization/departments-branches`).

Arabic & English (§12, T2-J, A-11):
- i18n wired through the front end from the first screen, with a language switcher.
- RTL layout support for Arabic — not merely translated strings in an LTR layout.
- UI STRINGS are translated for the T1 and T2 screens.
- USER-GENERATED CONTENT (tickets, notes, KB articles) is stored as authored and is NOT
  translated.
- Dates display in the Gregorian calendar in both languages.

Web and mobile friendly (§12, T3-F, A-1):
- Interpreted as RESPONSIVE WEB. Layouts work at phone width for the agent queue, the ticket
  view and the customer portal.
- Native iOS/Android apps and PWA offline support are future scope and are not stubbed.

Custom branding (§12, T3-E):
- Branding values — product name, logo, primary colour — resolved from configuration rather
  than hardcoded.
- Delivered: a single default brand loaded from config. This is a designed seam, not a
  theming engine.
```

---

## Acceptance criteria

```
- [ ] A language switcher toggles the interface between English and Arabic without a full
      reload of application state.
- [ ] Selecting Arabic switches the layout to RTL: navigation, tables, forms and icons mirror
      correctly, not just text direction.
- [ ] Every UI string on T1 and T2 screens is translated; no untranslated key or raw token is
      visible in either language.
- [ ] User-generated content is displayed exactly as authored in both languages.
- [ ] Dates render in the Gregorian calendar in both languages.
- [ ] The agent queue, ticket view and customer portal are usable at phone width, with no
      horizontal scrolling of the page body.
- [ ] Product name, logo and primary colour come from configuration; changing the configured
      values changes the running application without code edits.
- [ ] No branding value is hardcoded in a component or stylesheet.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `platform-foundation/solution-skeleton`.
- **Depends on code areas or other stories:** i18n must be wired from the FIRST front-end screen
  (T2-J) — planning this after most UI exists means retrofitting every component. Plan the
  i18n scaffolding with or immediately after the skeleton, and complete the translation pass
  once the T1/T2 screens exist. Branding values are read from the configuration owned by
  `administration/audit-configuration` (T2-I).

## Extra notes (optional)

- Cut order: the RTL/i18n scaffolding is effectively uncuttable in practice (retrofitting costs
  more than doing it). The full translation pass over T2 screens is cuttable; the branding seam
  is T3 and cut before that.
- This story is split in time, not in ownership: scaffolding early, translation pass late.

## Technical hints (optional)

- Repos/roots: `frontend` primarily; `backend` only where it returns user-facing strings —
  prefer returning codes the front end translates.
- Front end is Angular + PrimeNG; PrimeNG has RTL considerations of its own — verify components
  in Arabic rather than assuming.
- **Do not invent** endpoints here; Stage 7 (UI Design) fixes the screen inventory this story
  must cover.

## Out of scope

- Any third or subsequent language (§8).
- Translation of user-generated content, machine translation, per-article language variants
  (A-11).
- Hijri or dual calendars, per-branch timezones or locale-specific number/currency formatting
  beyond what date display requires.
- Native mobile applications, PWA, offline mode, push notifications (T3-F, §8).
- Per-tenant or per-branch themes, a branding upload UI, a CSS theming engine (T3-E).
- WCAG accessibility certification — sensible semantics only, no audit (§8).
