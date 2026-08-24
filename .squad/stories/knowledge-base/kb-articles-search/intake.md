# Story intake — Knowledge base articles, search and suggested solutions

> **Source of truth:** `docs/requirements.md` §6, §7.4 · `docs/product-scope.md` T2-E
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `kb-articles-search`)
- **Work item type:** Story

---

## Title

```
Knowledge base articles, search and suggested solutions
```

---

## Description

```
Requirements §6 in full, plus §7.4, at the depth fixed by product-scope T2-E.

One article concept (§6.1–6.3):
- FAQs, help articles, and solutions/guides are ONE "article" concept distinguished by a type
  field — not three subsystems. This is a deliberate simplification recorded in T2-E.
- Plain text / basic markdown body. No rich editor, no media library.
- Authoring is an Administrator capability (A-4).

Visibility:
- Each article is either public (visible in the customer portal) or internal (agents only).

Search (§6.4):
- Keyword search over title and body using the database's built-in text matching.
- No search engine, no vector index, no relevance tuning.
- Available to agents in the workspace and to customers in the portal (public articles only).

Suggested solutions (§7.4):
- Implemented as RETRIEVAL, not generation: surface the top keyword matches for a ticket to the
  agent inside the ticket view.
- Product-scope T2-E states this explicitly — retrieval-based, not generative.
```

---

## Acceptance criteria

```
- [ ] An Administrator can create, edit, publish and unpublish an article with a title, a
      markdown/plain-text body, and a type (FAQ / help article / solution guide).
- [ ] Each article carries a visibility flag: public or internal.
- [ ] Agents can search all articles; customers can search and read public articles only,
      enforced server-side.
- [ ] Keyword search matches title and body and returns results ranked by the database's own
      text matching.
- [ ] A search with no matches returns a clean empty state.
- [ ] Inside a ticket, an agent sees suggested articles retrieved by keyword from the ticket's
      subject and description.
- [ ] Suggested solutions are clearly presented as retrieved existing articles, not as
      AI-generated text.
- [ ] An internal article never appears in the portal or in a customer's search results.
- [ ] Seed data includes enough articles, both public and internal, to demonstrate search and
      suggestions.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `identity-access/auth-and-roles` (authoring is an Administrator
  capability; visibility is role-enforced).
- **Depends on code areas or other stories:** Suggested solutions render inside
  `agent-workspace/agent-dashboard`'s ticket view and read ticket text from
  `ticket-management/ticket-core`. Public articles are consumed by
  `customer-portal/portal-self-service` (requirements §8.4).

## Extra notes (optional)

- Cut order: T2. If cut in part, keep article CRUD and portal visibility (§8.4 depends on it)
  and drop suggested solutions first.
- Requirements §7.4 sits in this story rather than in `ai-assist` on purpose: product-scope
  classifies it as retrieval, so it belongs with the KB it retrieves from.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG.
- Use SQL Server's built-in text matching. Do not add a search engine or a vector store
  (product-scope §8 technical exclusions).
- **Do not invent** tables or endpoints here; Stage 5 and Stage 6 fix those first.

## Out of scope

- Article versioning, review/approval workflow, scheduled publishing (§8).
- Rich media, image libraries, file embedding in articles (T2-E).
- Multilingual article variants — UI chrome is translated, content is stored as authored (A-11).
- Relevance tuning, synonyms, semantic/vector search, analytics on article usefulness.
- Generative answers over the KB — that is the chatbot, T3-C.
