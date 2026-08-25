# Story 01 — Solution skeleton, local run and API documentation

> **Source of truth:** `docs/requirements.md` §11.1 · `docs/product-scope.md` T2-L, A-12, §10 item 5 · `docs/architecture.md` §6.1, §6.2, §6.3, AD-1, AD-2, AD-8 · `docs/api-design.md` §5.1 · `docs/ui-design.md` §1, §2, §4.3, §9
> **Intake:** `.squad/stories/platform-foundation/solution-skeleton/intake.md` · **Tier:** T2 (enabling)
> **Phase:** 0 — Foundation. Executes together with **Story 17 Part A** (i18n/RTL scaffolding), by the split-in-time exception recorded in `docs/story-backlog.md`.

## Prerequisites

- None. This is the first story in the execution sequence; every other story depends on it.
- **Executes jointly with Story 17 Part A.** The i18n/RTL scaffolding of
  [17-story-i18n-responsive-branding.md](../platform-experience/17-story-i18n-responsive-branding.md)
  is delivered in task 12 below, because retrofitting direction handling and logical properties
  into every component later costs more than doing it once (`docs/story-backlog.md`,
  split-in-time exceptions).

---

## Story Goal

Stand up the single-repository skeleton every later story builds on, and **nothing more**.

1. A clean checkout starts SQL Server, the API and the web app with **one documented command** and
   **no external accounts or credentials** (product-scope §10 item 5).
2. The backend exposes a generated OpenAPI document and a browsable API UI — this *is* the API
   deliverable for requirements §11.1 (T2-L).
3. `GET /api/v1/health` proves the API can reach the database; the front-end shell proves it can
   reach the API.
4. `GET /api/v1/config/bootstrap` serves branding and languages **before sign-in** (T3-E, T2-J).
5. Configuration is read from files and environment with **startup validation**; no connection
   string, secret or branding value is hardcoded.

**No business entity, screen or endpoint beyond health and bootstrap configuration exists in this
story.** No customers, no tickets, no roles, no authentication.

---

## Context — Read These Files First

1. `docs/architecture.md` — §1 (layering diagram and the **dependency rule**), §2.1 (what each
   project may contain), §2.2 (the exact front-end folder tree), §6.1 (repository layout), §6.2
   (the three Compose services), §6.3 (configuration table). AD-1, AD-2, AD-8 in §7.
2. `docs/api-design.md` — §2 (conventions: base path `/api/v1`, camelCase, RFC 3339 UTC, Problem
   Details), §2.2 (the status-code table), §5.1 (the four platform endpoints and the **three
   configuration audience tiers**), §6.9 `BootstrapConfig`, §6.12 Problem Details.
3. `docs/ui-design.md` — §1 (four areas), §2 (route tree), §4.3 (auth shell), §9 (empty / loading /
   error state rules), §10 (i18n, RTL, responsive).
4. `.squad/stories/platform-foundation/solution-skeleton/intake.md` — the acceptance criteria this
   plan must satisfy, and its **Out of scope** list.
5. `docs/product-scope.md` §8 **Technical** exclusions — microservices, brokers, CQRS, event
   sourcing, Kubernetes, caching, search engines, vector databases. None may appear here or later.
6. [00-implementation-plan.md](../00-implementation-plan.md) — the phase table, the workstreams, and
   the shared conventions (project names, namespaces, migration naming) every story reuses.

---

## Product rules (from story)

- **Current:** the repository contains empty `backend/` and `frontend/` directories and a **0-byte**
  `docker-compose.yml`.
- **New:** a buildable solution, a buildable SPA, and a working three-service Compose stack.
- The **dependency rule is compiler-enforced**, not conventional (AD-2): `Domain` references
  nothing; `Application` references `Domain`; `Infrastructure` references `Application` and
  `Domain`; `Api` references `Application` and `Infrastructure`.
- **Migrations and seeding run at API startup** (AD-8). This is a knowingly non-production choice
  and must be commented as such at the call site.

---

## Backend Tasks

### 1 — Create the solution and the five projects

Run from `backend/`. Target framework **net10.0** for every project (SDK 10.0.202 is installed).

```bash
dotnet new sln -n SupportCrm
dotnet new classlib -o src/SupportCrm.Domain         -f net10.0
dotnet new classlib -o src/SupportCrm.Application    -f net10.0
dotnet new classlib -o src/SupportCrm.Infrastructure -f net10.0
dotnet new webapi   -o src/SupportCrm.Api            -f net10.0 --use-controllers
dotnet new xunit    -o tests/SupportCrm.Tests        -f net10.0
dotnet sln add src/SupportCrm.Domain src/SupportCrm.Application src/SupportCrm.Infrastructure src/SupportCrm.Api tests/SupportCrm.Tests
dotnet add src/SupportCrm.Application    reference src/SupportCrm.Domain
dotnet add src/SupportCrm.Infrastructure reference src/SupportCrm.Application src/SupportCrm.Domain
dotnet add src/SupportCrm.Api            reference src/SupportCrm.Application src/SupportCrm.Infrastructure
dotnet add tests/SupportCrm.Tests        reference src/SupportCrm.Api src/SupportCrm.Application src/SupportCrm.Domain src/SupportCrm.Infrastructure
```

**`src/SupportCrm.Domain/SupportCrm.Domain.csproj` must end this story with zero
`PackageReference` and zero `ProjectReference` elements.** That emptiness is AD-4 made checkable.

Delete the `Class1.cs` and `WeatherForecast` templates.

**Create file: `backend/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

### 2 — Create the ten module folders

`docs/architecture.md` §1 fixes **ten** backend modules. Create the folder skeleton now so no later
story has to invent a location:

```
src/SupportCrm.Domain/Modules/{Identity,Organization,Customers,Tickets,Sla,Knowledge,Ai,Reporting,Administration,Integrations}/
src/SupportCrm.Application/Modules/{ ...the same ten... }/
```

Add a `README.md` in each `Application/Modules/<Name>/` naming the entities and the stories that own
it, copied from `docs/data-model.md` §3. **Modules are folders with a public service surface, not
assemblies or deployables** (architecture §1).

**Do not create module folders in `Infrastructure`** — it is organized by concern
(`Persistence/`, `Seams/`, `Storage/`), not by module.

### 3 — Persistence skeleton

**Create file: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`**

```csharp
public sealed class SupportCrmDbContext(DbContextOptions<SupportCrmDbContext> options) : DbContext(options)
{
    // Entity DbSets are added by the story that introduces each entity (data-model.md §3).
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportCrmDbContext).Assembly);
}
```

Packages on `SupportCrm.Infrastructure`: `Microsoft.EntityFrameworkCore.SqlServer`.
Package on `SupportCrm.Api`: `Microsoft.EntityFrameworkCore.Design` (for `dotnet ef`).

**Create file: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — an
`AddInfrastructure(this IServiceCollection, IConfiguration)` extension registering the `DbContext`
with `UseSqlServer(configuration.GetConnectionString("SupportCrm"))`.

**AD-3: no repository and no unit-of-work type is created here or in any later story.** `DbContext`
is already both. One unit of work per request, committed once (architecture §3).

**Create file: `src/SupportCrm.Application/Abstractions/IDataSeeder.cs`**

```csharp
public interface IDataSeeder { int Order { get; } Task SeedAsync(CancellationToken ct); }
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/DatabaseInitializer.cs`** — an
`IHostedService` that calls `Database.MigrateAsync()` on start and then runs every registered
`IDataSeeder` in `Order` sequence. Add the comment AD-8 requires at the call site:
`// AD-8: migrations at startup is a deliberate assessment trade-off, not production practice.`

No seeder implementations exist in this story — **the demo data arrives with the story that owns
each concept** (intake). **No migration is generated here** either; there are no entities yet. The
first migration is created by Story 03.

### 4 — Configuration and options binding

Architecture §6.3: `appsettings.json` -> `appsettings.{Environment}.json` -> environment variables,
**bound to strongly typed options and validated at startup**, so invalid configuration fails fast.

**Create file: `src/SupportCrm.Application/Configuration/BrandingOptions.cs`**

```csharp
public sealed class BrandingOptions
{
    public const string SectionName = "SupportCrm:Branding";
    [Required] public string ProductName  { get; init; } = default!;
    [Required] public string LogoUrl      { get; init; } = default!;
    [Required] public string PrimaryColor { get; init; } = default!;
}
```

**Create file: `src/SupportCrm.Application/Configuration/LocalizationOptions.cs`** — `Languages`
(`string[]`, required, non-empty) and `DefaultLanguage` (required, must be one of `Languages`;
validate with an `IValidateOptions<LocalizationOptions>`).

Register with the pattern every later story reuses by adding one line:

```csharp
builder.Services.AddOptions<BrandingOptions>()
    .Bind(builder.Configuration.GetSection(BrandingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

**`appsettings.json` carries no secret.** The connection string and the JWT signing key come from
environment variables only (architecture §6.3).

The remaining configuration keys of architecture §6.3 — categories, category-to-department map,
default branch, priorities, SLA targets, quick replies, feedback rating scale — are **defined and
validated by Story 16 Part A**, not here. This story delivers the *mechanism* only.

### 5 — Cross-cutting API concerns

**File: `src/SupportCrm.Api/Program.cs`**

- `AddControllers()` with JSON options: `camelCase`, and timestamps serialized RFC 3339 UTC with a
  trailing `Z` (api-design §2). The server never returns a local time.
- **Route prefix `/api/v1` applied once.** Use a shared `ApiControllerBase` with
  `[Route("api/v1/[controller]")]` or a route-prefix convention. Do not repeat the prefix by hand
  in each controller.
- `AddProblemDetails()` plus **one** `IExceptionHandler` at
  `src/SupportCrm.Api/Errors/ProblemDetailsExceptionHandler.cs`, translating application exceptions
  into RFC 9457 responses carrying the **stable `type` slugs** listed in api-design §6.12.
  **No `try`/`catch` in a controller** (architecture §2.1).
- **Create file: `src/SupportCrm.Application/Abstractions/AppExceptions.cs`** — the small exception
  family the handler maps, each carrying its slug so the handler never invents one:

  | Exception | Status | Used by |
  |---|---|---|
  | `NotFoundException` | `404` | missing **or out of scope** (AP-4) |
  | `ForbiddenException(type)` | `403` | capability denial, `transition-not-permitted` |
  | `ConflictException(type)` | `409` | `illegal-transition`, `user-already-exists`, `customer-email-in-use`, `feedback-already-submitted` |
  | `ValidationException` | `400` | malformed input, unknown filter/sort field |
  | `UnprocessableException(type)` | `422` | `assignee-out-of-department` |
  | `PayloadTooLargeException` | `413` | `attachment-too-large` |
  | `SeamUnavailableException` | `503` | `ai-unavailable` **only** (api-design §2.2) |

- OpenAPI: `AddOpenApi()` + `app.MapOpenApi()`. Add `Scalar.AspNetCore` and
  `app.MapScalarApiReference()` for the browsable UI in Development.
- CORS: one named policy reading allowed origins from `SupportCrm:Cors:AllowedOrigins`. Separate
  origins under Compose are exactly why AD-7 chose a bearer token.

### 6 — The two platform endpoints

**Create file: `src/SupportCrm.Api/Controllers/HealthController.cs`**

`GET /api/v1/health` — **Anonymous**. Returns `200`:

```json
{ "status": "ok", "database": "reachable", "utcNow": "2026-08-25T18:00:00Z" }
```

`database` comes from `SupportCrmDbContext.Database.CanConnectAsync()`; an unreachable database
returns `503` with `"status": "degraded"`. This is the only endpoint in the API that reports its
own dependency state.

**Create file: `src/SupportCrm.Api/Controllers/ConfigController.cs`**

`GET /api/v1/config/bootstrap` — **Anonymous**, returning exactly api-design §6.9 `BootstrapConfig`:

```json
{ "productName": "...", "logoUrl": "...", "primaryColor": "#0B5FFF",
  "languages": ["en", "ar"], "defaultLanguage": "en" }
```

built from `BrandingOptions` + `LocalizationOptions`.

**Do not add `GET /config` or `GET /config/staff` in this story.** Both require authentication
(Story 02) and publish values Story 16 Part A defines. This controller file is created here and
*extended* there.

### 7 — Tests project baseline

Add `Microsoft.AspNetCore.Mvc.Testing` to `tests/SupportCrm.Tests` and
`public partial class Program;` at the end of `Program.cs` so `WebApplicationFactory<Program>` can
reach it.

**Create file: `tests/SupportCrm.Tests/Api/PlatformEndpointTests.cs`** — two tests:
`/api/v1/health` returns `200` with `status: ok`; `/api/v1/config/bootstrap` returns `200` with a
non-empty `productName` and `languages` containing `en` and `ar`.

Coverage is **targeted, not exhaustive** (product-scope §8).

---

## Frontend Tasks

### 8 — Create the Angular application

Run from the repository root (Angular CLI 20.3.7 and Node 22 are installed):

```bash
ng new frontend --style=scss --routing --ssr=false --skip-git --package-manager=npm
cd frontend && npm i primeng @primeuix/themes primeicons @jsverse/transloco
```

**PrimeNG is the component library — use its components rather than hand-rolled equivalents**
(agent-dashboard intake). Configure the Aura preset in `app.config.ts` via
`providePrimeNG({ theme: { preset: Aura } })`.

### 9 — Create the folder skeleton of architecture §2.2, exactly

```
frontend/src/app/
  core/      auth/ api/ config/ i18n/ interceptors/ guards/ notifications/
  shared/    components/ pipes/ directives/ styles/
  layout/    staff-shell/ portal-shell/ auth-shell/
  features/  auth/ workspace/ portal/ admin/
```

Each `features/*` folder gets a `*.routes.ts` returning `[]` for now, wired as a **lazy** route in
`app.routes.ts` (AD-14). Add the three shared status routes of ui-design §2: `/403`, `/404`,
`/error`.

**Feature components never call `HttpClient` directly** (architecture §2.2). All HTTP goes through
typed services in `core/api/`, so the api-design contracts are absorbed in one place.

### 10 — Runtime configuration loader

**Create file: `frontend/src/app/core/config/runtime-config.service.ts`** — fetches
`GET {apiBaseUrl}/config/bootstrap` and exposes `productName`, `logoUrl`, `primaryColor`,
`languages`, `defaultLanguage` as signals.

Wire it with `provideAppInitializer(...)` in `app.config.ts` so **branding is resolved before the
first screen renders** (architecture §6.3). Apply `primaryColor` by setting a CSS custom property on
`document.documentElement`; **no branding value is hardcoded in a component or a stylesheet**.

**Create file: `frontend/src/environments/environment.ts`** with `apiBaseUrl: '/api/v1'` — the
`web` container proxies `/api` to `api` (task 13), so the SPA and the API share an origin in
Compose.

### 11 — HTTP plumbing and shells

- `core/interceptors/error.interceptor.ts` — normalizes an RFC 9457 body into a typed
  `ApiProblem { type, title, status, detail, errors? }`. **The `type` slug is what the UI maps to a
  translated string; `detail` is never rendered raw** (ui-design §9, T2-J).
- `core/api/api-client.base.ts` — `get/post/patch/put` helpers plus the paged-envelope type
  `Paged<T> { items, page, pageSize, totalItems, totalPages }` (api-design §2.1). Every list
  service in every later story returns this type.
- `layout/auth-shell/` — the centred card of ui-design §4.3: brand block plus language switcher.
- `layout/staff-shell/` and `layout/portal-shell/` — created as **empty shells with their
  navigation regions marked TODO**; Stories 02 and 13 fill them.
- `shared/components/` — `EmptyStateComponent`, `LoadingStateComponent`, `ErrorStateComponent`
  (ui-design §9). Every later screen reuses these three; none re-invents them.
- A temporary `HealthCheckComponent` on the default route calling `GET /health` and rendering the
  result, proving the SPA reaches the API. **Story 02 replaces it with the role redirect.**

### 12 — Story 17 Part A: i18n, RTL and responsive scaffolding

Delivered here by the split-in-time exception. Part B (the translation pass) stays at position 17;
see [17-story-i18n-responsive-branding.md](../platform-experience/17-story-i18n-responsive-branding.md).

- **Transloco** (`@jsverse/transloco`) is the runtime translation library AD-9 requires. The
  architecture left the choice to the plan and this plan takes it. Compile-time
  `@angular/localize` is **rejected**: it needs one bundle per locale and a reload, and T2-J
  requires switching **without losing application state**.
- `frontend/src/assets/i18n/en.json` and `ar.json`, loaded at bootstrap.
- `core/i18n/direction.service.ts` — sets `document.documentElement.dir` to `rtl` for `ar` and
  `lang` to the active language; persists the choice in browser storage.
- `shared/components/language-switcher/` — used by all three shells.
- PrimeNG locale settings switch alongside the application dictionaries (architecture §2.3).
- **`frontend/src/styles.scss` uses logical properties only** — `margin-inline-start`,
  `padding-inline-end`, `inset-inline`. Add a stylelint rule
  `property-disallowed-list: [margin-left, margin-right, padding-left, padding-right, left, right]`
  so a physical property fails the build rather than being caught in review.
- Three breakpoints (phone / tablet / desktop) declared once in
  `shared/styles/_breakpoints.scss` and used by every later screen.

---

## Infrastructure Tasks

### 13 — Docker Compose and Dockerfiles

**File: `docker-compose.yml`** (currently **0 bytes**) — three services, one network, one volume,
per architecture §6.2. Nothing else: no reverse proxy, no cache, no queue, no orchestrator.

```yaml
services:
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=${MSSQL_SA_PASSWORD}
    volumes:
      - supportcrm-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1'"]
      interval: 10s
      retries: 12
  api:
    build:
      context: .
      dockerfile: backend/Dockerfile
    depends_on:
      db:
        condition: service_healthy
    environment:
      - ConnectionStrings__SupportCrm=Server=db;Database=SupportCrm;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True
      - SupportCrm__Jwt__SigningKey=${SUPPORTCRM_JWT_KEY}
    ports:
      - "5080:8080"
  web:
    build:
      context: .
      dockerfile: frontend/Dockerfile
    depends_on:
      - api
    ports:
      - "4200:80"
volumes:
  supportcrm-data:
```

- **Create file: `backend/Dockerfile`** — multi-stage `mcr.microsoft.com/dotnet/sdk:10.0` build to
  `mcr.microsoft.com/dotnet/aspnet:10.0` runtime.
- **Create file: `frontend/Dockerfile`** — `node:22` build stage to `nginx:alpine` runtime.
- **Create file: `frontend/nginx.conf`** — `try_files $uri $uri/ /index.html;` for SPA routing, and
  `location /api/ { proxy_pass http://api:8080/api/; }`. CORS still exists for `ng serve` in
  development.
- **Create file: `.env.example`** with `MSSQL_SA_PASSWORD=` and `SUPPORTCRM_JWT_KEY=`, and
  **add `.env` to `.gitignore`**. No credential is committed (architecture §6.3, secrets
  discipline).

### 14 — README

**Create file: `README.md`** documenting prerequisites (Docker only), the **one command**
`docker compose up --build`, the URLs (`http://localhost:4200`,
`http://localhost:5080/api/v1/health`, `http://localhost:5080/scalar/v1`), how demo data is seeded
(automatically at API startup, AD-8), and an explicit statement that **no external accounts or
credentials are required**.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln` — no errors and no warnings
   (`TreatWarningsAsErrors`).
2. **Dependency rule holds:** `cat backend/src/SupportCrm.Domain/SupportCrm.Domain.csproj` —
   contains **no** `ProjectReference` and **no** `PackageReference` (AD-2, AD-4).
3. **Backend tests pass:** `dotnet test backend/SupportCrm.sln` — both platform-endpoint tests green.
4. **Frontend builds:** `cd frontend && npm ci && npm run build`, then
   `npx stylelint "src/**/*.scss"` — no physical-property violations.
5. **One command, clean checkout:** `cp .env.example .env` (fill the password), then
   `docker compose up --build`. All three services reach healthy.
6. **Health:** `curl http://localhost:5080/api/v1/health` returns `200` with
   `"database":"reachable"`.
7. **OpenAPI:** open `http://localhost:5080/scalar/v1` — the document lists `/api/v1/health` and
   `/api/v1/config/bootstrap` **and nothing else**.
8. **SPA:** open `http://localhost:4200` — the shell renders the configured product name, the health
   result and the language switcher; switching to العربية sets `dir="rtl"` on `<html>` with no
   reload and no loss of state.

---

## Done Criteria

- [ ] A clean checkout starts the full stack with **one documented command**, with no external
      accounts or credentials required.
- [ ] The backend serves an OpenAPI document and a browsable API UI in the local environment.
- [ ] The front-end shell loads in a browser and successfully calls the backend health route.
- [ ] The backend health route reports database connectivity.
- [ ] Configuration is read from files/environment; **no hardcoded connection string, secret or
      branding value** exists.
- [ ] `README.md` documents prerequisites, the start command, the URLs, and how to seed demo data.
- [ ] **No business entity, screen or endpoint beyond health and `/config/bootstrap` exists.**
- [ ] `SupportCrm.Domain` has zero project and package references.
- [ ] The ten module folders of architecture §1 exist under `Domain/Modules` and
      `Application/Modules`.
- [ ] Story 17 Part A is in place: Transloco, `en`/`ar` dictionaries, direction service, language
      switcher, logical-property lint rule, breakpoint variables.
- [ ] `.env` is git-ignored and `.env.example` is committed.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 02.**
