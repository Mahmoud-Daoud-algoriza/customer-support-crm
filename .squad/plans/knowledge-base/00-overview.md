# knowledge-base — plan overview

Entry point for the **knowledge-base** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 12 | [12-story-kb-articles-search.md](12-story-kb-articles-search.md) | Knowledge base articles, search and suggested solutions | — | Story 02; task 4 needs Story 05 |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/knowledge-base/`](../../stories/knowledge-base/).

## Dependency notes

**Phase 6, partly parallelizable from Phase 2.** `KnowledgeArticle` has **no relationship to any
other entity**, so its migration and its admin/staff screens are independent of the ticket line.
Only task 4 (suggested articles) and the ticket-detail region need Story 05.

- **Suggested solutions (§7.4) are retrieval, not generation** — a **Knowledge** endpoint, never an
  `/ai` one (AP-14), and presented as retrieved existing articles.
- The portal visibility rule lives in **one** place, `PortalArticleService.PortalVisible`; an
  internal or unpublished article is **`404`, not `403`** (AP-4).
- T2. If cut in part, **keep article CRUD and portal visibility** — requirements §8.4 depends on it
  — **and drop suggested solutions first**.
