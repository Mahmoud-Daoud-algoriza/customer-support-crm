[← Back to PROJECT-PROGRESS.md](../../PROJECT-PROGRESS.md)

### Story 15 — `management-dashboard` (2026-09-06)

**Every row below records a command executed in story 15's own task.** None is carried over.
⚠ **The last row is outstanding, so story 15 is Implemented with verification partial — not
Verified.**

| Check | Result | Evidence |
|---|---|---|
| Backend build | ✅ Passed 2026-09-06 | `dotnet build backend/SupportCrm.sln` → **Build succeeded, 0 Warning(s), 0 Error(s)** |
| Story 15 suite (plan step 2) | ✅ Passed 2026-09-06 | `dotnet test backend/SupportCrm.sln --filter "FullyQualifiedName~Reporting"` → **21 passed, 0 failed, 0 skipped**. The plan names ten; the other eleven are additional cases named in §7's Tests row |
| Full backend suite (plan step 7) | ✅ Passed 2026-09-06 | `dotnet test backend/SupportCrm.sln` → **451 passed, 0 failed, 0 skipped**. Delta **+21** against story 14's 430 — exactly this story's tests, so **stories 05–14 are unregressed** |
| Front-end build (plan step 8, build half) | ✅ Passed 2026-09-06 | `npm run build` → bundle generation complete |
| Style lint | ✅ Passed 2026-09-06 | `npm run lint:styles` → exit **0**. The dashboard styles inline, which stylelint does not scan, so it was additionally grepped for physical properties → **none** |
| Front-end specs | ✅ Passed 2026-09-06 | `npx ng test --watch=false --browsers=ChromeHeadless` → **TOTAL: 66 SUCCESS**, unchanged and with no existing spec edited |
| Role gate by hand (plan step 3) | ✅ Passed 2026-09-06 | Live against real SQL Server: **Agent → 403**, **Customer → 403**, **Manager → 200**, **Administrator → 200**, **anonymous → 401**. The `403`/`401` split is the point — no token is a different failure from the wrong role |
| No export (Done Criterion) | ✅ Passed 2026-09-06 | Live: `GET /reports/dashboard?format=csv` → **400**. Before finding I-39 was fixed this returned **200 with a valid dashboard**, which is why the check exists |
| Branch derivation (plan step 4) | ✅ Passed 2026-09-06 | Live, compared against SQL on the same database: for each branch the API's total equals the count joined **through `Customers`** (**6** and **5**) and **differs** from the count joined through the **assigned agent** (**4** and **3**). The mismatch is the proof — §4.4 says a ticket's branch is its customer's |
| Empty is not zero (plan step 5) | ✅ Passed 2026-09-06 | On a **scratch** database with every `CustomerFeedback` row deleted, the raw JSON reads **`"satisfaction":{"averageRating":null,"responseCount":0,...}`** — the null **present**, not omitted, and not `0.0`. The dev volume was not touched |
| Aggregation on the server (plan step 6) | ✅ Passed 2026-09-06 | Live response over 11 tickets is **1535 bytes** and contains **0** occurrences of `subject`, `description`, `ticketId`, `createdAt` or `categoryCode` — there is no ticket array |
| Seed data makes every tile non-trivial (plan task 3) | ✅ Passed 2026-09-06 | On a from-scratch database the seeder logged **"11 ticket(s), 7 message(s), 2 rating(s), …"** and the live dashboard showed: **4 statuses/priorities/categories and both departments populated**, SLA rows spanning **100%, 0% and 8 breached**, **two agents with real averages and two with `null`**, and satisfaction **3 of 5 from 2 responses** — a real average rather than a single row echoed back. The second rating is this story's seeder addition |
| PF-4 encoded once (R-18) | ✅ Passed 2026-09-06 | `DashboardMetricsTests.Assigned_count_is_currently_assigned_and_excludes_terminal_tickets` → green. Its distinguishing case is a **closed** ticket still stamped with the agent's id: `assignedCount` **1**, `resolvedCount` **1** — the two columns measuring different things is what pins the decision |
| Aggregates not narrowed by the caller (Done Criterion) | ✅ Passed 2026-09-06 | `DashboardAccessTests.A_managers_unfiltered_dashboard_spans_both_departments` → green. A Manager whose own department is Billing still sees Technical in `byDepartment` — the test that fails the moment someone composes `TicketScope` into the report |
| Published path space | ✅ Passed 2026-09-06 | `openapi/v1.json` fetched live → **49 paths, 66 operations**, and `/api/v1/reports/dashboard` is the only reporting route. **66 is api-design's full endpoint count** — story 15 closed the last gap |
| **Front-end screen checks (plan step 8, browser half)** | ⚠ **OUTSTANDING — not run** | **No browser-driving capability was available in this task** (puppeteer and playwright both absent; karma's ChromeHeadless runs specs, not screens), and installing one was outside the slice. **Nothing was fabricated.** Still to be covered: **(1)** all four regions render against seeded data; **(2)** the department and branch filters **combine** in the UI and **survive a reload** (UI-9); **(3)** the report tables **scroll inside their own container** at phone width with no horizontal body scroll; **(4)** the empty states read correctly in both languages, and RTL mirrors. **Until these are covered story 15 is not verified in full and does not count toward §1.1** |

