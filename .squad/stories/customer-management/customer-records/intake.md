# Story intake — Customer profiles, notes, attachments and interaction history

> **Source of truth:** `docs/requirements.md` §1 · `docs/product-scope.md` T1-A, T2-A
> **Scope tier:** T1 (profiles, contact details, notes, interaction history) + T2 (attachments)

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `customer-records`)
- **Work item type:** Story

---

## Title

```
Customer profiles, notes, attachments and interaction history
```

---

## Description

```
Deliver requirements §1 in full, at the depth set by product-scope T1-A and T2-A.

Customer profiles and contact details (T1):
- Create, read, update and list customer profiles.
- Contact details held on the profile: name, email, phone, and the customer's branch reference.
- A customer is identified by an email address unique within the organization (A-10).
  There is no merge/dedupe tooling.

Interaction history (T1):
- A per-customer chronological timeline assembled from that customer's tickets and ticket
  activity. It is a read view over existing ticket data, not a separately maintained log.

Notes (T1):
- Free-text notes on a customer, authored by an agent, timestamped and attributed to the author.

Attachments (T2 — minimal):
- Single-file upload to local disk storage, size-capped, attached to a ticket or a customer.
```

---

## Acceptance criteria

```
- [ ] An agent can create, view, edit and list customers, and search/filter the list.
- [ ] Name, email, phone and branch are captured; email uniqueness is enforced with a clear error.
- [ ] Opening a customer shows a chronological interaction timeline built from that customer's
      tickets and ticket activity, newest first.
- [ ] An agent can add a note to a customer; the note shows author and timestamp and cannot be
      silently altered by another user.
- [ ] A file can be attached to a customer and to a ticket, and downloaded again.
- [ ] Attachment size is capped and an oversized upload is rejected with a clear message.
- [ ] Role rules hold: a Customer cannot browse the customer directory; an Agent can.
- [ ] The timeline for a customer with no tickets renders an empty state rather than an error.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `platform-foundation/solution-skeleton`,
  `identity-access/auth-and-roles`, `organization/departments-branches` (branch reference).
- **Depends on code areas or other stories:** The interaction timeline reads ticket data owned by
  `ticket-management/ticket-core` and `ticket-management/ticket-lifecycle`. Plan the timeline
  after those, or build it against an empty ticket set and enrich when tickets land.

## Extra notes (optional)

- Cut order: attachments (T2-A) are cut before profiles/notes/timeline (T1-A).
- Attachment storage is local disk by design — no cloud object storage in this assessment.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG.
- **Do not invent** tables or endpoints here; Stage 5 and Stage 6 fix those first.

## Out of scope

- Virus scanning, cloud object storage, file previews, attachment versioning (T2-A).
- Customer merge/dedupe, bulk import/export, data migration (product-scope §8).
- Cross-channel identity resolution — not applicable, only web form and portal are real
  inbound channels (A-10).
- Contracts, entitlements, or per-customer SLA agreements (§8).
