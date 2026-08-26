# Organization

> Source: `docs/data-model.md` §3 · `docs/architecture.md` §1

| Entity | Scope | Mutability | Introduced by |
|---|---|---|---|
| `Department` | Organization | Mutable | `organization/departments-branches` (Story 03) |
| `Branch` | Organization | Mutable | `organization/departments-branches` (Story 03) |

**Owning stories:** 03. Department is the access boundary; branch is reporting only (A-2).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
