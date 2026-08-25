# Story 17 — Arabic/English i18n, RTL, responsive layout and branding

> **Source of truth:** `docs/requirements.md` §12 · `docs/product-scope.md` **T2-J**, **T3-E**, **T3-F**, **A-1**, **A-11** · `docs/architecture.md` **§2.3**, §6.3, **AD-9**, AD-14 · `docs/api-design.md` §5.1, §6.9 `BootstrapConfig`, §6.12 (the front end owns display text) · `docs/ui-design.md` **§10**, §9, §13
> **Intake:** `.squad/stories/platform-experience/i18n-responsive-branding/intake.md` · **Tier:** T2 (i18n/RTL) + T3 (branding, responsive)
> **Phases: this story is SPLIT IN TIME.**

## ⚠ This story executes in two parts, at two different points in the sequence

`docs/story-backlog.md` records the split as an explicit exception: *"the i18n/RTL scaffolding must
be wired with story 01, because retrofitting every Angular component later costs more than doing it
once. Only the translation pass belongs at position 17."* The intake agrees: *"This story is split
in time, not in ownership: scaffolding early, translation pass late."*

| | **Part A — Scaffolding** | **Part B — Translation pass, RTL audit, branding proof** |
|---|---|---|
| **Executes in** | **Phase 0**, inside [01-story-solution-skeleton.md](../platform-foundation/01-story-solution-skeleton.md) task 12 | **Phase 8**, at its backlog position, **after every T1/T2 screen exists** |
| **Delivers** | Transloco, `en`/`ar` dictionaries, the direction service, the language switcher, logical-property lint rule, breakpoint variables, the runtime branding loader | Tasks 1–8 below |

**Part A is already done when this plan is executed.** This file is the record of what it contained
and the plan for everything that remains.

## Prerequisites

- **Part A completed** (with Story 01).
- **Every T1 and T2 screen exists** — Stories 02–16. A translation pass over screens that do not yet
  exist would have to be repeated, and an RTL audit of PrimeNG components cannot be done before the
  components are on screen.
- **Story 16 Part A completed** — branding values live in the configuration this story reads
  (T2-I, architecture §6.3).

---

## Story Goal

The cross-cutting platform lines of requirements §12, **excluding** multi-department and
multi-branch (Story 03 owns those).

1. **Arabic and English** — a language switcher toggling the interface **without a full reload of
   application state**; **RTL layout for Arabic — mirrored, not merely re-aligned**.
2. **Every UI string on T1 and T2 screens translated**; **user-generated content stored as authored
   and never translated**; dates in the **Gregorian calendar in both languages**.
3. **Responsive web** (A-1) — the agent queue, the ticket view and the customer portal usable at
   phone width, with **no horizontal scrolling of the page body**.
4. **Branding from configuration** — product name, logo and primary colour; **no branding value
   hardcoded in a component or stylesheet**.

---

## Context — Read These Files First

1. `docs/ui-design.md` **§10 in full** — §10.1 (runtime translation, **an in-progress reply draft
   survives the switch**; error text from Problem Details `type` codes, never server prose), §10.2
   (**logical properties, directional icons mirror and non-directional ones do not, numerals and
   timestamps stay LTR-embedded, PrimeNG components verified in Arabic rather than assumed
   correct**), §10.3 (the three T3-F surfaces at phone width).
2. `docs/architecture.md` **§2.3** — why compile-time localization was ruled out (AD-9), the
   backend returning **codes, not user-facing prose**, and *"Mirroring is handled once in `shared/`
   and `layout/`, never per feature."*
3. `docs/product-scope.md` **A-11** (English and Arabic only; UI chrome translated, user-generated
   content not; **Gregorian dates in both languages**), **A-1** and **T3-F** (responsive web —
   **no native app, no PWA, no offline, no push**), **T3-E** (a single default brand from config —
   **a seam, not a theming engine**).
4. `docs/ui-design.md` §9 — the error table this story must supply translated strings for, keyed by
   Problem Details `type`.
5. `.squad/stories/platform-experience/i18n-responsive-branding/intake.md` — eight acceptance
   criteria and the Out of scope list (no third language, no content translation, **no Hijri or
   dual calendars**, no per-tenant themes, no branding upload UI, no WCAG certification).

---

## Product rules (from story)

- **Switching language must not reload application state.** That requirement is why AD-9 chose a
  runtime library over `@angular/localize`.
- **UI strings are translated. User-generated content is not** — tickets, notes and KB articles
  render exactly as authored, in whatever language they were written (A-11).
- **Dates render in the Gregorian calendar in both languages.** No Hijri, no dual calendar.
- **Arabic sets `dir="rtl"` and the layout mirrors** — navigation, tables, forms and icons — not
  merely text alignment.
- **The backend returns codes, not display prose.** Error text comes from the Problem Details
  `type` slug mapped to a translated string; **the server's `detail` is never rendered raw**.
- **Branding is read at runtime**, never compiled into a component or stylesheet.
- **English and Arabic only.** A third language is out of scope (product-scope §8).

---

# Part A — Scaffolding *(delivered with Story 01; recorded here)*

What Story 01 task 12 put in place, and what every story since has been required to use:

| Artefact | Location | Rule it enforces |
|---|---|---|
| **Transloco** (`@jsverse/transloco`) | `core/i18n/` | AD-9 — runtime switching, no reload. `@angular/localize` was **rejected**: one bundle per locale and a reload |
| `en.json`, `ar.json` | `src/assets/i18n/` | Loaded at bootstrap |
| `DirectionService` | `core/i18n/direction.service.ts` | Sets `document.documentElement.dir` and `lang`; persists the choice per user in browser storage |
| `LanguageSwitcher` | `shared/components/language-switcher/` | Present in **all three shells** |
| **Logical-property stylelint rule** | `.stylelintrc` | `margin-left`, `margin-right`, `padding-left`, `padding-right`, `left`, `right` **fail the build** |
| Breakpoint variables | `shared/styles/_breakpoints.scss` | One breakpoint system: phone / tablet / desktop |
| `RuntimeConfigService` | `core/config/` | Branding from `GET /config/bootstrap`, applied as CSS custom properties before first render |

---

# Part B — Translation pass, RTL audit and branding proof *(Phase 8)*

## Frontend Tasks

### 1 — Extract every UI string

Sweep `features/`, `layout/` and `shared/` for literal user-facing text and replace it with a
translation key. Key convention — namespace by area, then screen, then element:

```
workspace.queue.title          portal.requests.empty        admin.audit.filter.actor
shared.status.New              shared.priority.Urgent       errors.illegal-transition
```

**Two categories must be swept and are easy to miss:**

1. **`shared/` components** — `StatusChip`, `PriorityChip`, `EmptyState`, `ConfirmDialog`,
   `PagedTable` paginator text.
2. **PrimeNG's own strings** — its locale settings are configured **alongside the application
   dictionaries so both switch together** (architecture §2.3). A translated page with an English
   PrimeNG paginator is a fail.

**Do not translate:** ticket subjects and descriptions, message bodies, internal and customer notes,
KB article titles and bodies, customer and user display names, category and department names as
authored. **User-generated content is stored as authored and is not translated** (A-11). Add a
review note listing exactly these fields so a later contributor does not "helpfully" pipe them
through Transloco.

### 2 — Error strings keyed by Problem Details `type`

Populate `errors.*` in both dictionaries for every stable slug in api-design §6.12:

`illegal-transition` · `transition-not-permitted` · `assignee-out-of-department` ·
`feedback-already-submitted` · `user-already-exists` · `customer-email-in-use` ·
`invalid-credentials` · `ai-unavailable` · `attachment-too-large`

plus the status-level entries of ui-design §9 for `401`, `403`, `404`, `413`, `422`, `503`.

**`404` must read identically whether the record is missing or out of scope** — AP-4 exists to stop
the UI distinguishing them, and a translation is the easiest place to break that by accident.

**The server's `detail` is never rendered.** Grep the error interceptor and every error template to
confirm no path prints it.

### 3 — RTL audit against the real screens

Set the interface to العربية and walk **all 24 screens** of ui-design §13. For each, confirm:

- **The layout mirrors**, not merely the text alignment: sidebar, drawers, table column order, form
  labels, the message thread's inbound/outbound sides.
- **Directional icons mirror** — back arrows, thread indentation, the drawer's slide direction.
  **Non-directional icons do not** — clock, paperclip, bell (ui-design §10.2).
- **Numerals, timestamps and SLA countdowns stay LTR-embedded** inside RTL text so they read
  correctly. Wrap them in a `shared/directives/ltr-embed.directive.ts` rather than fixing each site.
- **No physical CSS property survives.** `npx stylelint "src/**/*.scss"` must pass; the rule has
  been active since Part A, so any violation is new.

**Verify the five PrimeNG components ui-design §10.2 names first** — **table, paginator, dropdown,
calendar and drawer** — *"verified in Arabic rather than assumed correct"*. Record the result of
each check in the story report; if one needs a wrapper fix, it goes in `shared/`, **once, never per
feature**.

### 4 — Gregorian dates in both languages

**Create file: `shared/pipes/app-date.pipe.ts`** — one pipe used by every screen, formatting with
the **Gregorian calendar** in both locales (`ar` with `u-ca-gregory`), and the PrimeNG calendar
configured the same way.

**No Hijri and no dual calendar** (A-11, intake Out of scope). Replace every direct `| date` usage
with this pipe so the rule holds in one place.

### 5 — Responsive verification at phone width (T3-F, A-1)

Walk the **three surfaces T3-F names**, at 390 px, in **both directions**:

| Surface | Required behaviour (ui-design §10.3) |
|---|---|
| **Agent queue** | Table becomes **stacked cards**, each leading with subject, status and SLA; filters collapse into a filter sheet |
| **Ticket detail** | Regions become a **single-column accordion** (UI-4); the customer panel becomes a **drawer**; the composer **docks to the bottom** |
| **Customer portal** | **Single column throughout**; the submit form full width; **cards, never tables** |

Plus, everywhere: the staff sidebar becomes an **off-canvas drawer** below tablet, and **wide
content — report tables, the activity region — scrolls inside its own container so the page body
never scrolls sideways** (architecture §2.3).

**Add a regression check:** an automated assertion that `document.body.scrollWidth <=
window.innerWidth` on the queue, the ticket detail and the portal detail at 390 px, in both
directions. It is cheap and it catches the exact failure T3-F cares about.

### 6 — Branding proof (T3-E)

`RuntimeConfigService` already applies `productName`, `logoUrl` and `primaryColor` from
`GET /config/bootstrap`. **Prove the seam rather than asserting it:**

- `grep -rniE "support crm|#0B5FFF|logo\.(png|svg)" frontend/src/` — the product name, the colour
  and the logo path appear **only** in configuration handling, **never** in a component template or
  a stylesheet.
- Change the three values in `appsettings.json`, restart the API, **reload the browser without
  rebuilding the front end**, and confirm the running application shows the new brand. That is the
  intake's acceptance criterion: *"changing the configured values changes the running application
  without code edits."*
- **No branding upload UI, no theme picker, no per-tenant or per-branch theme, no CSS theming
  engine** (T3-E).

### 7 — Language-switch state test

The acceptance criterion most likely to be broken by a late change: **switching language must not
lose application state.**

Manual and automated check: open a ticket, type a partial reply into the composer, switch to
العربية — **the draft is intact, the ticket is still open, the scroll position is preserved, and no
network request re-fetches the ticket.** Repeat on the portal request detail.

### 8 — Missing-key sweep

Configure Transloco's missing-key handler to **log loudly in development**, then walk all 24 screens
in **both** languages and confirm **no untranslated key or raw token is visible in either**
(intake AC). Add `en.json` / `ar.json` key-parity as a small test: the two files have **identical
key sets**.

---

## Backend Tasks

**Almost none — and that is the design.** The backend returns **codes, not user-facing prose**
(architecture §2.3), so translation lives entirely in the front end.

### 9 — Confirm no user-facing prose is served

`grep` the API for user-facing English in responses. Every Problem Details response must carry a
**stable `type` slug**; `title` and `detail` are for developers and logs and are **never rendered**
by the client. If any endpoint returns a sentence intended for display, replace it with a code and
add the string to both dictionaries.

`GET /config/bootstrap` already returns `languages` and `defaultLanguage` from
`LocalizationOptions` (Story 01). **No new endpoint is introduced by this story** — api-design §10.1
records that Story 17 introduces **none**, and that is a conclusion, not an omission.

---

## Verification Steps

1. **Frontend builds:** `cd frontend && npm run build` and `npx stylelint "src/**/*.scss"` — clean.
2. **Key parity:** the `en.json` / `ar.json` key-set test passes; the missing-key handler logs
   nothing across a walk of all 24 screens in both languages.
3. **State survives the switch:** type a draft, switch language — the draft, the route and the
   scroll position survive, with **no re-fetch**.
4. **RTL:** in العربية the sidebar, tables, forms, thread sides and directional icons mirror; the
   clock, paperclip and bell do not; timestamps read LTR inside RTL text.
5. **PrimeNG:** table, paginator, dropdown, calendar and drawer all render correctly in Arabic;
   record each result.
6. **Dates:** every date on every screen renders **Gregorian** in both languages.
7. **Phone width:** the automated `scrollWidth <= innerWidth` check passes on the three T3-F
   surfaces in both directions.
8. **Branding:** change the three configured values, restart the API, reload the browser **without
   rebuilding the front end** — the brand changes.
9. **No hardcoded brand:** the grep in task 6 returns matches only in configuration handling.
10. **Regression:** `dotnet test backend/SupportCrm.sln` — unchanged; this story touches almost no
    backend code.

---

## Done Criteria

- [ ] A language switcher toggles the interface between English and العربية **without a full reload
      of application state**.
- [ ] Selecting Arabic switches the layout to **RTL: navigation, tables, forms and icons mirror
      correctly**, not just text direction.
- [ ] **Every UI string on T1 and T2 screens is translated**; no untranslated key or raw token is
      visible in either language.
- [ ] **User-generated content is displayed exactly as authored** in both languages.
- [ ] **Dates render in the Gregorian calendar in both languages.**
- [ ] The agent queue, ticket view and customer portal are usable at phone width, **with no
      horizontal scrolling of the page body**.
- [ ] Product name, logo and primary colour come from configuration; **changing the configured
      values changes the running application without code edits**.
- [ ] **No branding value is hardcoded in a component or stylesheet.**
- [ ] Error text comes from Problem Details **`type` codes**, and the server's `detail` is never
      rendered raw.
- [ ] The five PrimeNG components named in ui-design §10.2 are **verified in Arabic**, with the
      result of each recorded.
- [ ] **No third language, no content translation, no Hijri or dual calendar, no theming engine, no
      branding upload UI** was introduced.
- [ ] `00-overview.md` updated with this story, **noting both execution points**.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 18.**
