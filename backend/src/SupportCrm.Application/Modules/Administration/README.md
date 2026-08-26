# Administration

> Source: `docs/data-model.md` §3 · `docs/architecture.md` §1, §2.4

| Entity | Scope | Mutability | Introduced by |
|---|---|---|---|
| `AuditEntry` | **Administrator-only** | **Append-only** | `administration/audit-configuration` (Story 16) |

**Owning stories:** 16. **One writer** — a single `AuditRecorder` service (architecture §2.4),
introduced by Story 02. `AuditEntry` and `TicketActivity` are deliberately separate tables
(AD-10). Configuration is read-only in the API: changing it is a redeploy (T2-I).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
