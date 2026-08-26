# Customers

> Source: `docs/data-model.md` §3 · `docs/architecture.md` §1

| Entity | Scope | Mutability | Introduced by |
|---|---|---|---|
| `Customer` | Organization | Mutable | `customer-management/customer-records` (Story 04) |
| `CustomerNote` | Staff-only | **Immutable** | `customer-management/customer-records` (Story 04) |
| `Attachment` | Follows owner | Immutable | `customer-management/customer-records` (Story 04) — shared with `Tickets` |

**Owning stories:** 04. `AttachmentService` + `LocalDiskAttachmentStorage` land here (Story 04).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
