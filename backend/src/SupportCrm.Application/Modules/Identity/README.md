# Identity

> Source: `docs/data-model.md` §3 · `docs/architecture.md` §1

| Entity | Scope | Mutability | Introduced by |
|---|---|---|---|
| `User` | Organization | Mutable | `identity-access/auth-and-roles` (Story 02) |

**Owning stories:** 02 (auth, roles, users). `AuditRecorder` (architecture §2.4) is written by
Story 02 but lives in `Administration`.

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
