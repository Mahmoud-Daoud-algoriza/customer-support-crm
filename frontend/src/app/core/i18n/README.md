# Translation rules — read this before editing `src/assets/i18n/*.json`

Story 17 (`.squad/plans/platform-experience/17-story-i18n-responsive-branding.md`) owns these rules.
The canonical sources are `docs/product-scope.md` **A-11**, `docs/ui-design.md` **§10** and
`docs/architecture.md` **§2.3** — if this file and one of those disagree, the document wins.

## UI strings are translated. User-generated content is not.

**A-11:** *"user-generated content is stored as authored and is never translated."* A ticket written
in Arabic reads in Arabic under an English interface, and vice versa. Machine translation, per-article
language variants and any "translate this" affordance are **out of scope** (story intake).

### The fields that must never be piped through Transloco

This list is exhaustive for the current schema. Wrapping any of them in `| transloco` — or in a
lookup keyed by their value — is a defect, not a helpful improvement:

| Entity | Fields |
|---|---|
| `Ticket` | `subject`, `description` |
| `TicketMessage` | `body` |
| `CustomerNote`, `TicketInternalNote` | `body` |
| `KnowledgeArticle` | `title`, `body` |
| `Customer` | `fullName`, `email`, `phone`, `externalReference` |
| `User` | `displayName`, `email` |
| `Department`, `Branch` | `name` |
| `TicketTask` | `title` |
| `CustomerFeedback` | `comment` |

**Category display names are configuration, not content.** `categoryCode` is the stable API code
(`docs/api-design.md` §6.4) and is *never* rendered; the name shown comes from
`GET /config` and is displayed as configured — it is not a dictionary key either.

## What *is* translated

Everything the application says in its own voice: labels, headings, buttons, empty and error
messages, status and priority names, activity types, notification types, and **PrimeNG's own
component strings** under the `primeng` namespace, which switch alongside these dictionaries
(`core/i18n/direction.service.ts`).

## Error text comes from codes, never from server prose

The API returns a **stable `type` slug** and the front end owns the sentence
(`docs/api-design.md` §6.12, `docs/ui-design.md` §9). `ApiProblem.detail` is carried for diagnostics
and **is never rendered**. Every slug needs an `errors.<slug>` entry in both files; the mapping is
`core/api/api-problem.ts`.

`errors.http-404` and `errors.not-found` must stay **word for word identical** — AP-4 exists to stop
the UI distinguishing a missing record from one out of scope, and differing wording would leak it.

## Dates

Every date renders through `shared/pipes/app-date.pipe.ts`, which pins the **Gregorian** calendar in
both languages (`ar-u-ca-gregory`). **No Hijri, no dual calendar** (A-11, intake Out of scope). Do
not reintroduce Angular's `DatePipe`.

## The two files stay in step

`en.json` and `ar.json` must have **identical key sets**; `core/i18n/dictionaries.spec.ts` fails the
build otherwise. A missing key logs loudly in development (`core/i18n/missing-key-handler.ts`) and
renders as the raw key, which is a visible defect — not an acceptable fallback.

**English and Arabic only.** A third language is out of scope (`docs/product-scope.md` §8).
