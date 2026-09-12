[← Back to PROJECT-PROGRESS.md](../PROJECT-PROGRESS.md)

## 5. Architecture / Technical Decisions

All approved. Full text in [architecture.md](architecture.md) §7 and [data-model.md](data-model.md) §1.

**This table is not the whole decision inventory.** It covers technical decisions (AD-*) and
modelling decisions (DM-*). **Business** decisions are the numbered assumptions A-1…A-19 in
[product-scope.md](product-scope.md) §7; the six most recent (**A-14…A-19**) are recorded as
R-6…R-11 in §6.3, with their document and story impact traced in §6.4.

**Known issues.** One remains, and it is cosmetic: in [architecture.md](architecture.md) §7 the
**AD-15 row renders above AD-14** — AD-15 was inserted at the wrong position during the token-staleness correction. No
content is affected; ordering only. Recorded here so it is not lost.
*(The `CustomerFeedback` "Portal module" mismatch flagged on 2026-08-24 is **resolved** — see
R-12 in §6.3. It is no longer an open issue.)*

| ID | Decision | Status | Why it matters | Source |
|---|---|---|---|---|
| AD-1 | Layered modular monolith, single deployable | ✅ Approved | Rules out microservices for every downstream plan | arch §7 |
| AD-2 | Four projects (Api / Application / Domain / Infrastructure) | ✅ Approved | Layering is compiler-enforced, not conventional | arch §7 |
| AD-3 | No repository or unit-of-work over EF Core | ✅ Approved | `DbContext` is both; avoids dead indirection | arch §7 |
| AD-4 | Domain free of EF attributes | ✅ Approved | Lifecycle and SLA rules stay unit-testable | arch §7 |
| AD-5 | Access scoping as an explicit Application helper | ✅ Approved | Global query filters fail open; this is testable | arch §4.3, §7 |
| AD-6 | SLA breach detection as a periodic hosted service | ✅ Approved | No broker or scheduler enters the stack | arch §7 |
| AD-7 | JWT bearer, **token asserts identity only** | ✅ Approved (amended) | Original version carried role/department — corrected | arch §4.1, §7 |
| AD-8 | Migrations + seed at API startup | ✅ Approved | One-command demo; knowingly not production practice | arch §7 |
| AD-9 | Runtime i18n library, not compile-time | ✅ Approved | T2-J needs a live switcher without losing state | arch §7 |
| AD-10 | Ticket history separate from audit log | ✅ Approved | Different actors, questions and visibility rules | arch §7 |
| AD-11 | Seam interfaces owned by Application | ✅ Approved | Makes AI / channels / ERP genuinely swappable | arch §7 |
| AD-12 | AI seam can only return suggestions | ✅ Approved | A-8's advisory rule becomes structural | arch §7 |
| AD-13 | Keyword search in SQL Server | ✅ Approved | No search engine, no vector database | arch §7 |
| AD-14 | One Angular app with role-based lazy areas | ✅ Approved | Shared i18n, branding and API client | arch §7 |
| AD-15 | Role, department, active status resolved **per request** | ✅ Approved | Closes the stale-claim authorization hole | arch §4.1.1, §7 |
| DM-1 | `User` and `Customer` are separate, linked 1:0..1 | ✅ Approved | A-4 role vs A-2 department/branch cannot share a row | model §1 |
| DM-2 | Assignment = field + history, no entity | ✅ Approved | Avoids a third representation to keep in sync | model §1 |
| DM-3 | SLA state on `Ticket`; targets are configuration | ✅ Approved | No `SlaPolicy` rows, no rules engine | model §1 |
| DM-4 | Content lives once; activity is the ordering spine | ✅ Approved | One timeline query; bodies never duplicated | model §1 |
| DM-5 | No AI entity | ✅ Approved | Only categorization needs persistence — as history | model §1 |
| DM-6 | Seams persist one field (`externalReference`) | ✅ Approved | No adapter config, webhook or receipt storage | model §1 |

