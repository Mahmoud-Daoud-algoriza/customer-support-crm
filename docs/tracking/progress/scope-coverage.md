[← Back to PROJECT-PROGRESS.md](../PROJECT-PROGRESS.md)

## 4. Scope Coverage

Progress view only — [product-scope.md](product-scope.md) holds the definitions.

**The Design column is now fully earned.** ✅ means all four design stages — architecture (5),
data model (6), API design (7) and UI design (8) — cover the item. `n/a` means the item has no
data-model footprint by design (front-end or configuration only).

**The Plan column is complete for every item.** ✅ means a stage-9 plan file covers it, with the
plan named in §4.1 below. **Impl and Verified remain ⬜ everywhere** — no code exists.

### T1 — must genuinely work (6 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T1-A Customer management | 04 | ✅ | ✅ | ⬜ | ⬜ |
| T1-B Ticket management | 05, 06 | ✅ | ✅ | ⬜ | ⬜ |
| T1-C Agent dashboard | 08 | ✅ | ✅ | ⬜ | ⬜ |
| T1-D Users, roles, permissions | 02 | ✅ | ✅ | ⬜ | ⬜ |
| T1-E Multi-department routing | 03 | ✅ | ✅ | ⬜ | ⬜ |
| T1-F Ticket-facing AI | 10, 11 | ✅ (no entity — DM-5) | ✅ | ⬜ | ⬜ |

### T2 — simplified but real (12 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T2-A Attachments | 04 | ✅ | ✅ | ⬜ | ⬜ |
| T2-B Web form + portal messaging | 07 | ✅ | ✅ | ⬜ | ⬜ |
| T2-C Tasks & internal notes | 14 | ✅ | ✅ | ⬜ | ⬜ |
| T2-D SLA & automation | 09 | ✅ | ✅ | ⬜ | ⬜ |
| T2-E Knowledge base | 12 | ✅ | ✅ | ⬜ | ⬜ |
| T2-F Customer portal | 13 | ✅ | ✅ | ⬜ | ⬜ |
| T2-G Reports & dashboards | 15 | ✅ (aggregates, no entity) | ✅ | ⬜ | ⬜ |
| T2-H Audit logs | 16 | ✅ | ✅ | ⬜ | ⬜ |
| T2-I System configuration | 16 | ✅ (config, n/a to model) | ✅ | ⬜ | ⬜ |
| T2-J Arabic & English | 17 | ✅ (front end, n/a to model) | ✅ | ⬜ | ⬜ |
| T2-K Multi-branch | 03 | ✅ | ✅ | ⬜ | ⬜ |
| T2-L Public API | 01 | ✅ (n/a to model) | ✅ | ⬜ | ⬜ |

### T3 — seam plus fake, designed not delivered (7 items)

| Item | Story | Design | Plan | Impl | Verified |
|---|---|---|---|---|---|
| T3-A External channels | 18 | ✅ | ✅ | ⬜ | ⬜ |
| T3-B Live chat | 18 | ✅ documented, not building | — | — | — |
| T3-C AI chatbot | 10 (seam), 18 (doc) | ✅ documented, not building | — | — | — |
| T3-D ERP & external systems | 18 | ✅ | ✅ | ⬜ | ⬜ |
| T3-E Custom branding | 17 | ✅ (config) | ✅ | ⬜ | ⬜ |
| T3-F Mobile / responsive | 17 (asserted in 08, 13) | ✅ | ✅ | ⬜ | ⬜ |
| T3-G Multi-tenancy | 03 | ✅ boundary noted, not building | — | — | — |

### 4.1 Scope item → plan file

Every T1, T2 and T3 item now has a named plan. The full matrix, including endpoints, entities and
screens per story, is [00-implementation-plan.md](../.squad/plans/00-implementation-plan.md) §8.2.

| Scope item | Plan(s) |
|---|---|
| T1-A | 04 |
| T1-B | 05, 06 |
| T1-C | 08 |
| T1-D | 02 (+ ticket scoping in 05) |
| T1-E | 03 (+ enforcement in 05) |
| T1-F | 10, 11 |
| T2-A | 04 (+ ticket endpoints in 05) |
| T2-B | 07 |
| T2-C | 14 |
| T2-D | 09 |
| T2-E | 12 |
| T2-F | 13 |
| T2-G | 15 |
| T2-H | **02** (write path) + **16 Part B** (read surface) |
| T2-I | **16 Part A** |
| T2-J | **17 Part A** + **17 Part B** |
| T2-K | 03, 04, 15 |
| T2-L | 01 |
| T3-A | 18 |
| T3-B | 18 (documented) |
| T3-C | 10 (extension point documented), 18 (documented) |
| T3-D | 18 |
| T3-E | 01 (loader) + 17 Part B (proof) |
| T3-F | 08, 13, 17 Part B |
| T3-G | 03 (boundary noted, not built) |

### T4 — excluded

Not built, not stubbed, not designed. [product-scope.md](product-scope.md) §8 lists them in three
groups — product exclusions, technical exclusions (microservices, brokers, CQRS, event sourcing,
Kubernetes, cache, search engine, vector database, native apps), and quality attributes not
pursued. [architecture.md](architecture.md) §8 restates them so they survive into planning.
**Status: excluded by decision. No tracking required.** A change here is a scope change.

