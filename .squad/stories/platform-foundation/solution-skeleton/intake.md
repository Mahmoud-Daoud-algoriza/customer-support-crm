# Story intake — Solution skeleton, local run and API documentation

> **Source of truth:** `docs/requirements.md` §11 · `docs/product-scope.md` T2-L, A-12, §10 (Definition of done #5)
> **Scope tier:** T2 (enabling story — introduces no business feature)

## Feature

- **Feature name (display):** Platform Foundation
- **Feature slug (folder under `plans/`):** `platform-foundation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `solution-skeleton`)
- **Work item type:** Chore

---

## Title

```
Solution skeleton, local run and API documentation
```

---

## Description

```
Stand up the single-repository skeleton that every later story builds on, and nothing more.

- One repository containing a backend application (ASP.NET Core Web API) under `backend/`
  and a front-end application (Angular + PrimeNG) under `frontend/`.
- A SQL Server database and both applications orchestrated by the existing root
  `docker-compose.yml`, started with one documented command (product-scope A-12).
- Self-documented HTTP API surface (OpenAPI) exposed by the backend. Per product-scope T2-L,
  the application's own API *is* the API deliverable for requirements §11.1 — there is no
  separate partner API.
- A single health/readiness route proving the front end can reach the API and the API can
  reach the database.
- A documented path for seeding demo data (the data itself arrives with the stories that
  own each concept).

This story deliberately contains no business behaviour: no customers, no tickets, no roles.
It exists so that later stories are pure feature work.
```

---

## Acceptance criteria

```
- [ ] A clean checkout starts the full stack with one documented command, with no external
      accounts or credentials required.
- [ ] The backend serves an OpenAPI document and a browsable API UI in the local environment.
- [ ] The front-end shell loads in a browser and successfully calls the backend health route.
- [ ] The backend health route reports database connectivity.
- [ ] Configuration is read from files/environment (no hardcoded connection strings).
- [ ] README documents: prerequisites, the start command, the URLs, and how to seed demo data.
- [ ] No business entity, screen, or endpoint beyond health exists in this story.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** None. This is the first story in the execution sequence.
- **Depends on code areas or other stories:** None. Every other story depends on this one.

## Extra notes (optional)

- Cut order: this story cannot be cut — all others depend on it.
- Requires Stage 4 (Architecture) to be agreed before planning, since it fixes the project layout.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Primary language: C# (ASP.NET Core) + TypeScript (Angular).
- Stack is fixed: Angular + PrimeNG, ASP.NET Core Web API, SQL Server, Docker Compose, single repo.
- Explicitly excluded by product-scope §8: microservices, message brokers, CQRS, event sourcing,
  Kubernetes, caching layers, distributed systems.
- **Do not invent** database tables or business endpoints in this intake. Those are fixed by
  SDD Stage 5 (Data Model) and Stage 6 (API Design) before any plan is generated.

## Out of scope

- Any business feature from requirements §1–§10.
- Production deployment, CI/CD, HA, backups (product-scope §8).
- Authentication — that is the `identity-access/auth-and-roles` story.
