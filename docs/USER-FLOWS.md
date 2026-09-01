# User flows — UML diagram index

Read-only documentation of the **implemented** system, drawn from the repository at commit
`969bb31` (2026-09-01): the Angular routes and guards, the ASP.NET Core controllers and their
authorization policies, the Application services, the EF Core entities and migrations, and the
approved documents under [docs/](.) and [.squad/](../.squad/).

**Nothing in these diagrams is invented.** Anything designed in the approved documents but absent
from the code is drawn in **red** and labelled `NOT IMPLEMENTED`, with the story that owns it.
These diagrams report state; they define none — the authority order of
[CLAUDE.md](../CLAUDE.md) §1 governs.

## The diagrams

| # | Diagram | Source | Rendered | What it answers |
|---|---|---|---|---|
| 0 | Product journey overview | [USER-FLOWS-overview.puml](USER-FLOWS-overview.puml) | [png](USER-FLOWS-overview.png) | The whole journey on one page — start here |
| 1 | Master user flow | [USER-FLOWS-master.puml](USER-FLOWS-master.puml) | [png](USER-FLOWS-master.png) | Login, authentication, role detection, role-based redirect, areas, logout |
| 2 | Authorization and failure branches | [USER-FLOWS-authorization.puml](USER-FLOWS-authorization.puml) | [png](USER-FLOWS-authorization.png) | What 401 / 403 / 404 / 400 / 409 / 413 / 422 / 503 each mean, and where each is decided |
| 3 | Agent | [USER-FLOWS-agent.puml](USER-FLOWS-agent.puml) | [png](USER-FLOWS-agent.png) | Department-scoped queue, ticket work, messaging, AI, knowledge, notifications |
| 4 | Manager | [USER-FLOWS-manager.puml](USER-FLOWS-manager.puml) | [png](USER-FLOWS-manager.png) | The three differences from an Agent: scope, escalation target, reporting |
| 5 | Administrator | [USER-FLOWS-administrator.puml](USER-FLOWS-administrator.puml) | [png](USER-FLOWS-administrator.png) | Users, audit log, configuration, knowledge authoring, health |
| 6 | Customer | [USER-FLOWS-customer.puml](USER-FLOWS-customer.puml) | [png](USER-FLOWS-customer.png) | Registration, submit a request, thread and reply, help centre |
| 7 | Ticket lifecycle | [USER-FLOWS-ticket-state.puml](USER-FLOWS-ticket-state.puml) | [png](USER-FLOWS-ticket-state.png) | The A-5 state machine with A-16 authority, as a UML state diagram |
| 8 | SLA, escalation, notifications | [USER-FLOWS-sla.puml](USER-FLOWS-sla.puml) | [png](USER-FLOWS-sla.png) | The flow that does not start in a browser |
| 9 | AI assists and Knowledge Base | [USER-FLOWS-ai-kb.puml](USER-FLOWS-ai-kb.puml) | [png](USER-FLOWS-ai-kb.png) | Generated-and-labelled versus retrieved-by-keyword |
| 10 | Not implemented | [USER-FLOWS-not-implemented.puml](USER-FLOWS-not-implemented.puml) | [png](USER-FLOWS-not-implemented.png) | Everything designed but absent, and what is excluded by decision |
| 11 | Use case diagram | [USER-FLOWS-use-cases.puml](USER-FLOWS-use-cases.puml) | [png](USER-FLOWS-use-cases.png) | Who may do what — the four actors, the A-4 generalizations, and every use case as one capability map |

Each `.puml` file holds **exactly one** `@startuml … @enduml` block with no content outside it, so
any of them opens directly in the VS Code PlantUML extension (`Alt+D`).

## Rendering

The repository has no local Java or Graphviz; the PNGs were produced with the PlantUML Docker
image, from the `docs/` directory:

```bash
docker run --rm -e PLANTUML_LIMIT_SIZE=16384 \
  -v "E:/Others/customer-support-crm/docs:/data" plantuml/plantuml \
  -tpng -failfast2 /data/USER-FLOWS-*.puml
```

Add `-checkonly` to validate the syntax without writing images. The `PLANTUML_LIMIT_SIZE` value
matters: the master and agent diagrams are wider than PlantUML's 4096 px default and are cropped
without it.

## Regenerating

These diagrams describe the **committed** state. Story 13 (customer portal self-service) was
landing in the working tree when they were drawn — see the note in
[USER-FLOWS-not-implemented.puml](USER-FLOWS-not-implemented.puml). Re-render after any story
that changes a route, an endpoint, a guard or the ticket lifecycle, and update the commit
reference in each file's header comment.
