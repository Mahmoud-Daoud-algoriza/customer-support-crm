# Customer Support CRM

A customer support CRM: customers, tickets, SLAs, a knowledge base, a customer portal and
role-based administration. One repository, one deployable API, one Angular application, one SQL
Server database.

> **Status — Story 01 of 18 complete.** This is the solution skeleton only: the health endpoint and
> the public bootstrap configuration endpoint. There is deliberately **no business entity, screen or
> endpoint** beyond those two yet. See [`docs/story-backlog.md`](docs/story-backlog.md) for what
> comes next.

---

## Prerequisites

**Docker Desktop, and nothing else.** No .NET SDK, no Node.js and **no local SQL Server
installation** are needed to run the stack — all three services build and run in containers.

**No external accounts and no credentials are required.** The only two values you supply are local
secrets you invent yourself (below).

For working on the code outside containers you will also want .NET SDK 10, Node.js 22 and the
Angular CLI; see [Developing outside Docker](#developing-outside-docker).

---

## Run it — one command

```bash
cp .env.example .env       # then set MSSQL_SA_PASSWORD (and SUPPORTCRM_JWT_KEY, unused until Story 02)
docker compose up --build
```

`MSSQL_SA_PASSWORD` must satisfy SQL Server's policy: at least 8 characters, with upper case, lower
case, and a digit or symbol. `.env` is git-ignored — no credential is ever committed.

The `api` service waits for the database's health check before it starts.

### URLs

| URL | What it is |
|---|---|
| <http://localhost:4200> | The Angular application |
| <http://localhost:5080/api/v1/health> | Liveness plus database reachability |
| <http://localhost:5080/api/v1/config/bootstrap> | Branding and languages, served before sign-in |
| <http://localhost:5080/scalar/v1> | Browsable API reference |
| <http://localhost:5080/openapi/v1.json> | The generated OpenAPI document |

### Stopping and resetting

```bash
docker compose down             # stop; the database keeps its data in the named volume
docker compose down -v          # stop and discard the database volume, for a clean slate
```

---

## Demo data

**Migrations and demo seeding run automatically at API startup** — there is no separate seed step
and no script to remember. A clean checkout comes up with a usable database.

This is a deliberate assessment trade-off, recorded as **AD-8** in
[`docs/architecture.md`](docs/architecture.md) §7, and it is explicitly **not** production practice;
the call site in `backend/src/SupportCrm.Infrastructure/Persistence/DatabaseInitializer.cs` says so.

Story 01 introduces no entity, so today there is nothing to migrate and nothing to seed. The
mechanism is in place: each later story adds an EF Core migration and, where the concept needs it, an
`IDataSeeder` that the initializer picks up in `Order` sequence. The schema is only ever created and
changed by **EF Core migrations** generated from the approved data model — never by hand-written SQL.

---

## Layout

```
backend/     ASP.NET Core solution — Api · Application · Domain · Infrastructure · Tests
frontend/    Angular + PrimeNG application, built on the PrimeNG Sakai template (MIT)
docs/        Requirements, scope and the SDD-stage design documents
.squad/      Story intakes and implementation plans
docker-compose.yml
```

The backend's layering is **compiler-enforced**, not conventional (AD-2): `Domain` references
nothing at all, `Application` references `Domain`, `Infrastructure` references both, and `Api`
references `Application` and `Infrastructure`.

---

## Developing outside Docker

Requires .NET SDK 10, Node.js 22 and Docker (for the database only).

```bash
# 1. Database only
docker compose up -d db

# 2. API — reads the connection string from the environment; nothing is hardcoded
export ConnectionStrings__SupportCrm='Server=localhost,1433;Database=SupportCrm;User Id=sa;Password=<yours>;TrustServerCertificate=True'
dotnet run --project backend/src/SupportCrm.Api      # http://localhost:5080

# 3. Front end — ng serve proxies /api to http://localhost:5080 (frontend/proxy.conf.json)
cd frontend && npm ci && npm start                   # http://localhost:4200
```

The `db` service does not publish port 1433 to the host by default. Add a `ports: ['1433:1433']`
entry to it if you want to run the API on the host against the containerized database.

### Checks

```bash
dotnet build backend/SupportCrm.sln     # must be warning-free: TreatWarningsAsErrors is on
dotnet test  backend/SupportCrm.sln
cd frontend && npm run build && npm run lint:styles
```

`npm run lint:styles` fails the build on physical CSS properties (`margin-left`, `right`, …). Arabic
must **mirror**, not merely re-align, so stylesheets use logical properties
(`margin-inline-start`, `inset-inline`) — [`docs/ui-design.md`](docs/ui-design.md) §10.2.

---

## Configuration

Layered and standard: `appsettings.json` → `appsettings.{Environment}.json` → **environment
variables**, bound to strongly typed options and **validated at startup**, so invalid configuration
fails fast instead of degrading at runtime.

**No connection string, secret or branding value is hardcoded.** Secrets come from the environment
only. There is no configuration UI: changing configuration is a redeploy (T2-I).

| Key | Supplied by |
|---|---|
| `ConnectionStrings__SupportCrm` | `.env` via Compose |
| `SupportCrm__Jwt__SigningKey` | `.env` via Compose (used from Story 02) |
| `SupportCrm:Branding` | `appsettings.json` — product name, logo, primary colour |
| `SupportCrm:Localization` | `appsettings.json` — `en`, `ar`, and the default |
| `SupportCrm:Cors:AllowedOrigins` | `appsettings.json`, overridden by Compose |

The front end reads branding and the language set from `GET /api/v1/config/bootstrap` **before the
first screen renders**, so branding is never compiled into a component or a stylesheet.

---

## Attribution

The front end is built on the [PrimeNG Sakai](https://github.com/primefaces/sakai-ng) template
(tag `20.0.0`), MIT licensed — see [`frontend/LICENSE.md`](frontend/LICENSE.md). Angular and PrimeNG
versions are pinned to the ones that Sakai release resolves.
