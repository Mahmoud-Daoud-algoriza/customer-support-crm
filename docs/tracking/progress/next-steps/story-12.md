[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

0. **Story 12 (`kb-articles-search`) is complete — all eleven plan tasks, implemented and verified
   2026-09-01**, delivered **whole rather than in slices**. **Phase 6 opens.**

   **What landed.** One `KnowledgeArticle` entity with a **`type`** field, which is T2-E's
   simplification made structural: FAQs, help articles and solution guides are one concept, and there
   is no second table to keep in step. `visibility` and `isPublished` are **two independent facts**,
   and the seeded demo contains a **public-but-unpublished** article precisely so that is visible
   rather than asserted. `ArticleSearch` is the **one implementation of AD-13** that
   00-implementation-plan §6 names — `LIKE` over title and body, scored by matched-term count with a
   title match above a body match, built as a single SQL expression so the database filters and orders
   by the same thing the payload publishes. `PortalArticleService.PortalVisible` is the **one
   implementation of §5 constraint 19**, composed *before* the id comparison, so an internal article is
   **not found** rather than found-and-refused.

   **§7.4 is retrieval, and the code says so in three places.** `SuggestedArticleService` references no
   `IAiAssistService`, is registered beside the Knowledge services rather than the AI seam, and is
   published at `GET /tickets/{id}/suggested-articles` — **AP-14**. A test asserts the negative two
   ways: the `/ai/suggested-solutions` path is not routable, and **no registered endpoint's route
   template mentions it at all**, which is what would catch it being added under another verb. On the
   screen the region carries its own wording — *"Existing articles found by keyword in this ticket"* —
   with **no sparkle icon, no AI-generated label and no dismiss**, because it sits directly below a
   panel that has all three.

   **Verified against real SQL Server**: the migration applied and 10 articles seeded at startup; an
   internal article, an unpublished public article and a missing id all answer **`404` with
   byte-identical Problem Details**, while the same customer token on a staff path answers **`403`**;
   the refund ticket retrieves **five suggestions ordered by `matchScore`**, none carrying
   `generatedBy`; an out-of-department ticket answers **`404`**; `PATCH` with `isPublished` answers
   **`400`** and publish/unpublish moves the portal read `404 → 200 → 404`. **Case-insensitive
   matching — which the SQLite test host cannot prove — holds**: `refund`, `REFUND` and `ReFuNd` each
   return 2. Backend suite **401 passing, 1 skipped** (16 this slice's own); front end **62 specs** (11
   this slice's).

   **One implementation choice is recorded at the code rather than as a numbered finding**, on the same
   basis as story 16 Part B's inclusive `to` bound: no approved document defines *keyword extraction*
   for §7.4, so `ArticleSearch.KeywordsFrom` prunes words under three characters and a short English
   stop-word list, and says in its own comment that this is a choice and not a documented rule. It
   decides retrieval noise, not a product or contract question. **No new finding was raised, and no
   open finding was closed.**

   **The bookkeeping gap disclosed on 2026-09-01 is unchanged and still open** — §3's table shows
   stories 08–11 `Not Started` while the entries below narrate them complete, and §1.1 still counts
   only the stories it has verified against §8's standard. This task did not reconcile it: that is a
   tracking correction of its own, not part of story 12.

   **Awaiting explicit approval before the next unit of work. This task does not start story 13.**

