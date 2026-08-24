# Story intake — Ticket summaries, suggested replies and auto-categorization

> **Source of truth:** `docs/requirements.md` §7.1–7.3 · `docs/product-scope.md` T1-F, A-8
> **Scope tier:** T1 (must genuinely work)

## Feature

- **Feature name (display):** AI Assist
- **Feature slug (folder under `plans/`):** `ai-assist`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `ai-ticket-assists`)
- **Work item type:** Story

---

## Title

```
Ticket summaries, suggested replies and auto-categorization
```

---

## Description

```
The three agent-facing AI capabilities of requirements §7.1–7.3, all consuming the abstraction
from `ai-assist/ai-service-seam`. Product-scope T1-F makes these mandatory: the assessment is
explicitly about AI-assisted work, so a working AI slice must exist.

Ticket summary (§7.1):
- Generate a short summary of a ticket thread on demand, shown in the agent's ticket view.
- Read-only aid. It is not stored as a ticket field that pretends to be authored content.

Suggested reply (§7.2):
- Generate a draft reply into the composer, which the agent edits before sending.
- Advisory only, NEVER auto-sent (A-8).
- Sits alongside the configured quick replies (T1-C) without replacing them.

Automatic categorization (§7.3):
- At ticket creation, suggest a category and a priority from the fixed enumerations (A-6).
- The suggestion is a pre-selection the agent can override.
- The suggestion, and whether it was accepted or overridden, is recorded in ticket history.

Cross-cutting (A-8): every AI output is labelled as AI-generated in the UI. Nothing is sent or
committed without a human action.
```

---

## Acceptance criteria

```
- [ ] An agent can request a summary of a ticket thread and receive one within the ticket view.
- [ ] An agent can request a suggested reply; it lands in the composer as editable text and is
      never sent automatically.
- [ ] At ticket creation, a category and priority are suggested from the fixed enumerations and
      are freely overridable before saving.
- [ ] The suggested values, and whether the agent accepted or overrode them, are written to
      ticket history.
- [ ] Every AI-produced element is visibly labelled as AI-generated.
- [ ] All three features work end to end with the deterministic offline fake, with no credentials
      and no network access.
- [ ] When the AI service is unavailable, each feature degrades to an unavailable state and the
      agent can still summarise nothing, write their own reply, and pick a category manually.
- [ ] A suggested category outside the configured enumeration is rejected rather than created.
- [ ] No AI path can send a message, change a status, or reassign a ticket.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ai-assist/ai-service-seam`, `ticket-management/ticket-core`,
  `ticket-management/ticket-lifecycle`, `ticket-management/ticket-intake-messaging`
  (summaries and replies read the message thread), `agent-workspace/agent-dashboard`
  (the surface these appear on).
- **Depends on code areas or other stories:** Requirements §7.4 "Suggested solutions" is NOT in
  this story — product-scope T2-E implements it as keyword retrieval inside
  `knowledge-base/kb-articles-search`.

## Extra notes (optional)

- Cut order: T1 — cannot be cut. If time is short, reduce to the smallest working version of all
  three rather than delivering one polished feature and dropping two.
- The value demonstrated here is the guardrail discipline (advisory, labelled, audited,
  degradable), not model sophistication.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG.
- All three features call the abstraction from `ai-service-seam`. Do not call a provider SDK
  directly from a controller or a component.
- **Do not invent** endpoints here; Stage 7 (API Design) fixes how assists are exposed.

## Out of scope

- Suggested solutions from the knowledge base (§7.4) — `knowledge-base/kb-articles-search`.
- The AI chatbot (§7.5, T3-C).
- Auto-sending replies, auto-closing tickets, or any autonomous action (A-8).
- Customer-facing generated content of any kind (A-8).
- Sentiment analysis, language detection, translation of user content (A-11 — user-generated
  content is not translated).
