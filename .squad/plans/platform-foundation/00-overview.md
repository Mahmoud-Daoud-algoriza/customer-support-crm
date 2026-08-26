# platform-foundation — plan overview

Entry point for the **platform-foundation** feature. Stories execute in order by their `NN` prefix,
which is the **global** execution sequence across all feature folders
(`naming.globalSequence: true`). The programme-level view is
[00-implementation-plan.md](../00-implementation-plan.md).

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-solution-skeleton.md](01-story-solution-skeleton.md) | Solution skeleton, local run and API documentation | — | — |

Tracker integration is `none`; story ids are the folder names under
[`.squad/stories/platform-foundation/`](../../stories/platform-foundation/).

## Dependency notes

**Phase 0.** The first story in the sequence; every other story depends on it.

- Fixes the project layout, the four-project dependency rule (AD-2), the Compose stack, the
  configuration mechanism, the Problem Details handler and the Angular folder skeleton.
- **Also delivers Story 17 Part A** — the i18n/RTL scaffolding — by the split-in-time exception in
  `docs/story-backlog.md`. See [17-story-i18n-responsive-branding.md](../platform-experience/17-story-i18n-responsive-branding.md).
- Delivers `/health` and the anonymous `/config/bootstrap` only. The two **authenticated**
  configuration tiers (`/config`, `/config/staff`) belong to
  [16 Part A](../administration/16-story-audit-configuration.md) — finding **S9-13**.
- **Cannot be cut.**

## Delivery log

### 01 — Solution skeleton, local run and API documentation — **complete** (2026-08-25)

| Area | Delivered |
|---|---|
| Backend | `backend/SupportCrm.sln` — five projects on `net10.0`, `TreatWarningsAsErrors`. `Domain` has zero project and zero package references (AD-2, AD-4). Ten module folders in `Domain/Modules` and `Application/Modules`; `Infrastructure` organized by concern. |
| Persistence | `SupportCrmDbContext` (no repository, no unit of work — AD-3), `DatabaseInitializer` hosted service applying migrations then `IDataSeeder`s in `Order` (AD-8 comment at the call site). No entity and therefore **no migration** in this story — the first one is Story 03. |
| Configuration | `BrandingOptions` and `LocalizationOptions`, bound and `ValidateOnStart`; the default-language rule enforced by `IValidateOptions`. No secret, connection string or branding value in a committed file. |
| API | `/api/v1` applied once via `ApiControllerBase` plus a slug route-token transformer, so the served path and the OpenAPI document both read `/api/v1/health`. camelCase JSON, RFC 3339 UTC with trailing `Z`, `AddProblemDetails` with one `IExceptionHandler` over the `AppException` family, named CORS policy, `MapOpenApi` + Scalar. |
| Endpoints | `GET /api/v1/health` and `GET /api/v1/config/bootstrap` — **and nothing else**. |
| Front end | Built on the **PrimeNG Sakai template, tag `20.0.0`** (MIT), not scaffolded from scratch — a change from task 8 of the plan, made on the user's instruction. Angular 20.1.2 / PrimeNG 20.0.0, pinned to that Sakai release's own lockfile resolutions. Folder tree of architecture §2.2 added alongside Sakai's `layout/`; four lazy areas, three status routes. |
| Story 17 Part A | Transloco, `en`/`ar` dictionaries with a checked-identical key set, `DirectionService` (document `dir`/`lang`, PrimeNG locale, persisted choice), language switcher in all three shells, `property-disallowed-list` stylelint rule, breakpoint mixins. |
| Infrastructure | Three-service `docker compose up --build`, `backend/Dockerfile`, `frontend/Dockerfile`, `frontend/nginx.conf`, `.dockerignore`, `.env.example`; `.env` git-ignored. |
| Docs | Root `README.md` — prerequisites (Docker only), the one command, the URLs, and how demo data is seeded. |

**Verified end to end** against a containerized SQL Server: build and tests warning-free, OpenAPI
lists exactly the two paths, the SPA renders the configured branding and the health result, and
switching to العربية sets `dir="rtl"` with no reload and no loss of state.

**Deviations from the plan, and why** — all recorded in the Story 01 report:
Sakai instead of `ng new` (user instruction); Angular/PrimeNG pinned to Sakai 20.0.0's resolutions
rather than the plan's looser "Angular 20 / PrimeNG 20"; the stylelint logical-property rule scoped
to project-authored stylesheets, with the vendored Sakai template stylesheets ignored;
`frontend/proxy.conf.json` added so `ng serve` shares an origin with the API; the temporary health
screen placed in `features/platform/`, to be removed with Story 02's role redirect.
