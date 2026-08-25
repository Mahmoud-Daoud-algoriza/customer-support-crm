# Story 12 — Knowledge base articles, search and suggested solutions

> **Source of truth:** `docs/requirements.md` §6, §7.4 · `docs/product-scope.md` T2-E, A-4, A-11 · `docs/architecture.md` §5.1 (what this is *not*), **AD-13**, §8 · `docs/data-model.md` §2.13, §5 constraint 19, §6 · `docs/api-design.md` §5.9, §6.5, §6.11, **AP-14**, AP-4 · `docs/ui-design.md` §5.6, §6, §7.4
> **Intake:** `.squad/stories/knowledge-base/kb-articles-search/intake.md` · **Tier:** T2 — *"if cut in part, keep article CRUD and portal visibility (§8.4 depends on it) and drop suggested solutions first"*
> **Phase:** 6 — Knowledge and portal.

## Prerequisites

- **Story 02 completed:** roles and policies — **authoring is an Administrator capability** (A-4)
  and visibility is role-enforced.
- **Story 05 completed** *for task 4 only* — `GET /tickets/{id}/suggested-articles` reads the
  ticket's subject and description and composes `LoadScopedAsync`.

> **Partly parallelizable.** Tasks 1–3 and 6–8 (the entity, CRUD, search, staff and admin screens)
> depend only on Story 02 and can be built alongside Stories 05–09 by a second worker. Task 4
> (suggested articles) and task 9 (the ticket-detail region) need Story 05. See
> `00-implementation-plan.md` §"Parallelization".

---

## Story Goal

Requirements §6 in full, plus §7.4, at the depth T2-E fixes.

1. **One article concept** — FAQs, help articles and solution guides distinguished by a **type
   field**, not three subsystems. Plain text / basic markdown. **Authoring is Administrator-only.**
2. **Visibility** — each article is `Public` (portal-visible) or `Internal` (staff only), and
   separately `isPublished`.
3. **Search** — keyword over **title and body** using the **database's own text matching**
   (AD-13). No search engine, no vector index, no relevance tuning.
4. **Suggested solutions (§7.4) as retrieval, not generation** — the top keyword matches for a
   ticket, shown to the agent inside the ticket view, and **presented as retrieved existing
   articles**.

---

## Context — Read These Files First

1. `docs/data-model.md` §2.13 `KnowledgeArticle` — the eight fields, **"Deliberately no relationship
   to `Ticket`: suggested solutions are computed by keyword retrieval at read time, not stored
   links"**, **no versioning**, and the invariant that an `Internal` or unpublished article must
   never appear in a portal read. Then §5 constraint 19 and §6's knowledge-base search paragraph.
2. `docs/api-design.md` §5.9 — all nine rows (seven staff/admin, two portal), **`q` searches title
   and body using the database's own text matching**, an `Internal` or unpublished article is
   **`404` on the portal paths — never `403`, which would confirm it exists** (AP-4). Then §6.5 for
   the four payload shapes and §6.11 for the `POST`/`PATCH` bodies.
3. `docs/api-design.md` **AP-14** — *"Suggested articles are a **Knowledge** endpoint, not an AI
   one… putting them under `/ai` would imply generation."*
4. `docs/architecture.md` **AD-13** (SQL Server text matching; no search engine, no vector
   database) and **§5.1's closing line**: *"Suggested solutions do **not** use this seam."*
5. `docs/ui-design.md` §5.6 (staff knowledge — internal articles carry an "internal" badge; *search
   is presented as search, never as an AI answer*), §6 (article authoring and editor — **no rich
   editor, no media library, no versioning, no delete**), §7.4 (portal help).
6. `.squad/stories/knowledge-base/kb-articles-search/intake.md` — nine acceptance criteria and the
   Out of scope list.

---

## Product rules (from story)

- **One entity with a `type` field**, not three subsystems. `Faq` | `HelpArticle` | `SolutionGuide`.
- **Plain text / basic markdown body.** No rich editor, no media library, no file embedding.
- **Authoring, publishing and unpublishing are Administrator-only** (A-4). Agents read.
- **Agents can search all articles; customers can search and read public, published articles
  only — enforced server-side.**
- **An internal or unpublished article never appears in the portal or in a customer's search
  results**, and the refusal is **`404`, not `403`**.
- **`isPublished` defaults to false**, so an article is drafted before it is visible.
- **`isPublished` is not patchable** — publication changes through the dedicated `/publish` and
  `/unpublish` action pair, so publication state changes through one path only (api-design §6.11).
- **No versioning, no review/approval workflow, no scheduled publishing, no delete.**
- **Suggested solutions are retrieval, clearly presented as retrieved existing articles, not as
  AI-generated text.**
- **Article content is stored as authored and is never translated** (A-11). UI chrome is
  translated; user-generated content is not.

---

## Backend Tasks

### 1 — Domain: `KnowledgeArticle`

**Create file: `src/SupportCrm.Domain/Modules/Knowledge/ArticleType.cs`**

```csharp
// T2-E: one concept with a type, not three subsystems.
public enum ArticleType { Faq, HelpArticle, SolutionGuide }
```

**Create file: `src/SupportCrm.Domain/Modules/Knowledge/ArticleVisibility.cs`**

```csharp
public enum ArticleVisibility { Public, Internal }
```

**Create file: `src/SupportCrm.Domain/Modules/Knowledge/KnowledgeArticle.cs`** — `Title`, `Body`,
`Type`, `Visibility`, `IsPublished`, `AuthorUserId`, `CreatedAt`, `UpdatedAt`. Mutators: `Update(...)`
which bumps `UpdatedAt`, plus `Publish()` and `Unpublish()`.

**`Publish()` and `Unpublish()` are separate methods, and `Update` does not touch `IsPublished`** —
the one-path rule of api-design §6.11 is enforced by the entity, not only by the controller.

**No navigation to `Ticket` exists** (data-model §2.13). Do not add one.

### 2 — Infrastructure: EF configuration and migration

**Create file: `Persistence/Configurations/KnowledgeArticleConfiguration.cs`** — enums to string;
`Title` and `Body` sized for text matching. **No special index is required**: data-model §6 states
that at assessment data volumes a straightforward contains-match over `title` and `body` suffices,
and records SQL Server full-text indexing as *the available upgrade if matching quality proves
inadequate — a database feature, not a new component.* **Do not add a full-text catalogue in this
story.**

```bash
dotnet ef migrations add KnowledgeArticles -p src/SupportCrm.Infrastructure -s src/SupportCrm.Api
```

### 3 — Application: article service and search

**Create file: `src/SupportCrm.Application/Modules/Knowledge/KnowledgeArticleService.cs`**

| Method | Endpoint | Role | Notes |
|---|---|---|---|
| `SearchAsync` | `GET /kb/articles` | Agent+ | Filters `q`, `type`, `visibility`, `isPublished`. **All articles, internal included** |
| `GetAsync` | `GET /kb/articles/{id}` | Agent+ | |
| `CreateAsync` | `POST /kb/articles` | **Administrator** | `{ title, body, type, visibility, isPublished? }`; `isPublished` defaults **false**; `author` is the authenticated Administrator and is **never supplied** |
| `UpdateAsync` | `PATCH /kb/articles/{id}` | **Administrator** | `title`, `body`, `type`, `visibility` — **`isPublished` is not patchable** |
| `PublishAsync` | `POST /kb/articles/{id}/publish` | **Administrator** | |
| `UnpublishAsync` | `POST /kb/articles/{id}/unpublish` | **Administrator** | |

**Create file: `src/SupportCrm.Application/Modules/Knowledge/ArticleSearch.cs`** — the **one**
keyword-matching expression, used by staff search, portal search and suggested articles:

```csharp
// AD-13: SQL Server's own text matching. No search engine, no vector index, no relevance tuning.
public static IQueryable<KnowledgeArticle> MatchingKeywords(this IQueryable<KnowledgeArticle> q, string terms) => ...
```

Split the query into terms, match `Title` or `Body` with `EF.Functions.Like`, and rank by the count
of matched terms with a title match weighted above a body match. **That ranking is the whole of
relevance** — the intake excludes tuning, synonyms and semantic search.

**Create file: `src/SupportCrm.Application/Modules/Knowledge/PortalArticleService.cs`** — the
customer-facing read, and **the single place the visibility rule lives**:

```csharp
// data-model §5 constraint 19 — enforced here, once, for every portal read.
private static IQueryable<KnowledgeArticle> PortalVisible(IQueryable<KnowledgeArticle> q) =>
    q.Where(a => a.Visibility == ArticleVisibility.Public && a.IsPublished);
```

An id that exists but is `Internal` or unpublished -> **`NotFoundException`** -> **`404`**, never
`403` (AP-4). Assert that in a comment and in a test.

### 4 — Application: suggested articles (§7.4)

**Create file: `src/SupportCrm.Application/Modules/Knowledge/SuggestedArticleService.cs`**

`ForTicketAsync(ticketId)`:

1. `LoadScopedAsync(ticketId)` — the same scoping helper every ticket read composes; out of scope
   -> `404`.
2. Extract keywords from the ticket's **subject and description**.
3. Run `MatchingKeywords` over **staff-visible** articles (internal included — the agent may read
   them), take the top N (default 5, configurable).
4. Return `{ id, title, type, matchScore }` (api-design §6.5). **`matchScore` is the text-match
   ranking, exposed so a screen can order results — a query artefact, not a stored field.**

**This service does not reference `IAiAssistService` and must never be moved under `/ai`** (AP-14,
architecture §5.1). Put that sentence in the file.

### 5 — Api: controllers

**Create file: `src/SupportCrm.Api/Controllers/KnowledgeController.cs`**

```
GET   /api/v1/kb/articles                      RequireAgent
GET   /api/v1/kb/articles/{id}                 RequireAgent
POST  /api/v1/kb/articles                      RequireAdministrator
PATCH /api/v1/kb/articles/{id}                 RequireAdministrator
POST  /api/v1/kb/articles/{id}/publish         RequireAdministrator
POST  /api/v1/kb/articles/{id}/unpublish       RequireAdministrator
```

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add
`GET /api/v1/tickets/{id}/suggested-articles`, `RequireAgent`.

**File: `src/SupportCrm.Api/Controllers/PortalTicketsController.cs`** *(or a sibling
`PortalKnowledgeController.cs` under the same `api/v1/portal` prefix)* — add, `RequireCustomer`:

```
GET   /api/v1/portal/kb/articles         ?q=
GET   /api/v1/portal/kb/articles/{id}
```

**The portal payload is a different shape** — `{ id, title, body, type, updatedAt }`, with **no
`visibility`, no `isPublished`, no author**: the portal only ever receives public published
articles, so returning those fields would state the obvious and leak the taxonomy (api-design §6.5).

**There is no delete endpoint anywhere** (T2-E, ui-design §6).

### 6 — Seed data

**Create file: `Persistence/Seeders/KnowledgeSeeder.cs` (`Order = 50`)** — enough articles, **both
public and internal**, across all three types, with bodies whose words overlap the seeded tickets'
subjects so **suggested articles return non-trivial results in the demo** (intake AC). Include at
least one **unpublished public** article, to prove `isPublished` is enforced separately from
`visibility`.

### 7 — Tests

**Create file: `tests/SupportCrm.Tests/Knowledge/KnowledgeVisibilityTests.cs`**

1. An `Agent` search returns **both** public and internal articles.
2. A `Customer` calling `GET /kb/articles` (the staff path) -> **`403`**.
3. `GET /portal/kb/articles` returns **only** public **and** published articles.
4. `GET /portal/kb/articles/{id}` for an **internal** article -> **`404`, not `403`** (AP-4).
5. `GET /portal/kb/articles/{id}` for a **public but unpublished** article -> **`404`**.
6. An `Agent` calling `POST /kb/articles` -> `403`; an `Administrator` -> `201` with
   `isPublished: false`.
7. `PATCH` with `isPublished` in the body -> **`400`** — publication changes only through
   `/publish` and `/unpublish`.

**Create file: `tests/SupportCrm.Tests/Knowledge/SearchAndSuggestionTests.cs`**

8. A keyword search matches **title** and matches **body**, and a title match ranks higher.
9. A search with no matches returns a **clean empty page**, not an error.
10. `GET /tickets/{id}/suggested-articles` returns articles whose text overlaps the ticket's
    subject/description, each with a `matchScore`.
11. The same endpoint on an **out-of-department** ticket -> `404`.
12. **No `/ai` route serves suggested solutions** — assert that `POST /api/v1/ai/suggested-solutions`
    is not routable (AP-14).

---

## Frontend Tasks

### 8 — Staff knowledge — `/workspace/knowledge`, `/workspace/knowledge/:id` (ui-design §5.6)

- Search box plus filters `type`, `visibility`, `isPublished`, bound to URL query parameters (UI-9).
- **Internal articles carry an "internal" badge.**
- **Search is presented as search, never as an AI answer** — no sparkle icon, no "AI" wording, no
  generated summary above the results (ui-design §5.6).
- The reader renders markdown; **article text is never translated** (A-11) — do not pass it through
  a translation pipe.
- Empty state: *"No articles match that search."*

### 9 — Suggested articles region in the ticket detail

`features/workspace/ticket-detail/suggested-articles-region/` — fills the slot Story 05 left, below
the AI panel. Rows are article titles with type and `matchScore`, linking to the reader.

**Its heading and copy must make clear these are existing articles retrieved by keyword** — it sits
next to the AI panel, which is exactly why it must not look like AI output (T2-E, AP-14).

### 10 — Admin authoring — `/admin/knowledge` (ui-design §6)

- List with publish state; create and edit at `/admin/knowledge/new` and `/admin/knowledge/:id`.
- **Plain-text / markdown editor with a preview. No rich text toolbar, no media library, no image
  upload.**
- **Publish and unpublish are explicit buttons**, separate from Save, mirroring the API's action
  pair.
- **No version history and no delete control anywhere** — neither exists server-side.

### 11 — Portal help — `/portal/help`, `/portal/help/:id` (ui-design §7.4)

- Prominent search; card list; the reader renders markdown.
- `404` renders as *"Not found"* — **the same wording whether the article is missing or internal**,
  because AP-4 exists to stop the UI distinguishing them (ui-design §9).
- Reachable from the portal shell's two destinations. Story 13 completes the surrounding shell.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Knowledge` — all twelve green.
3. **No excluded infrastructure:**
   `grep -rniE "elastic|opensearch|lucene|embedding|vector|pgvector" backend/ frontend/src/` returns
   nothing (architecture §8, AD-13).
4. **Portal isolation by hand:** note an internal article's id, then `GET
   /api/v1/portal/kb/articles/{thatId}` with a customer token -> **`404`**.
5. **Suggestions:** open a seeded ticket and confirm the suggested-articles region returns relevant
   seeded articles.
6. **Regression:** Stories 05–11 suites still pass; the AI panel and the suggested-articles region
   are visibly different components with different wording.
7. **Frontend:** `npm run build`; article text renders identically with the interface in English and
   in Arabic (content is not translated), while the surrounding chrome switches.

---

## Done Criteria

- [ ] An Administrator can create, edit, publish and unpublish an article with a title, a
      markdown/plain-text body and a type.
- [ ] Each article carries a visibility flag: public or internal.
- [ ] **Agents can search all articles; customers can search and read public articles only,
      enforced server-side.**
- [ ] Keyword search matches title and body and returns results ranked by the database's own text
      matching.
- [ ] A search with no matches returns a clean empty state.
- [ ] Inside a ticket, an agent sees suggested articles retrieved by keyword from the ticket's
      subject and description.
- [ ] **Suggested solutions are clearly presented as retrieved existing articles, not as
      AI-generated text**, and are served by a **Knowledge** endpoint, not an `/ai` one (AP-14).
- [ ] **An internal article never appears in the portal or in a customer's search results**, and the
      refusal is `404`, not `403`.
- [ ] An unpublished article is equally unreachable from the portal.
- [ ] `isPublished` is not patchable; publication changes only through `/publish` and `/unpublish`.
- [ ] Seed data includes enough articles, both public and internal, to demonstrate search and
      suggestions.
- [ ] **No search engine, vector store, versioning, media library or delete path** was introduced.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 13.**
