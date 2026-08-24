# Story intake — Channel and ERP integration seams with fakes

> **Source of truth:** `docs/requirements.md` §3.1, §3.2, §3.4, §11.2, §11.3, §11.4 · `docs/product-scope.md` T3-A, T3-D, A-7
> **Scope tier:** T3 (architecture / future — seam plus runnable fake, no external account)

## Feature

- **Feature name (display):** Integration Seams
- **Feature slug (folder under `plans/`):** `integration-seams`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `channel-erp-adapters`)
- **Work item type:** Story

---

## Title

```
Channel and ERP integration seams with fakes
```

---

## Description

```
Prove that the external integrations of requirements §3 and §11 were DESIGNED FOR without
building any of them. Product-scope T3 rule of engagement: a named abstraction plus a fake the
demo can run against, requiring no external account, provider contract or production credential.

Communication channel adapters (§3.1 Email, §3.2 WhatsApp, §3.4 SMS; §11.3):
- ONE outbound channel-adapter abstraction, plus a normalized inbound message shape.
- Every channel produces and consumes the SAME message model as
  `ticket-management/ticket-intake-messaging` (T2-B). Adding a channel adds an adapter, not a
  second message concept.
- Delivered: the adapter interface, a CONSOLE/LOG adapter used in the demo, and a written
  contract stating what a real provider adapter must implement.

ERP and external systems (§11.2, §11.4):
- An outbound integration boundary isolating any external-system call behind an interface.
- Customer records carry an optional external-reference field.
- Delivered: the interface and a no-op fake.
- §11.4 "External systems" is unbounded and cannot be scoped further without a named system
  (product-scope §9, open question 3). Do not invent one.

Also documented here as future consumers of these seams, and NOT built:
- Real-time live chat transport (T3-B).
- AI chatbot (T3-C) — its seam is the AI abstraction, not this one.
```

---

## Acceptance criteria

```
- [ ] A single outbound channel-adapter interface exists, with a console/log implementation
      selected by configuration.
- [ ] Sending an outbound notification through the log adapter produces a log entry and is
      recorded against the ticket, with no external call attempted.
- [ ] An inbound message delivered through the fake adapter creates or updates a ticket using
      the SAME message model as the web form and portal, carrying its channel of origin.
- [ ] Adding a hypothetical channel requires implementing the adapter only — demonstrated by the
      written contract and by the log adapter being the sole channel-specific code.
- [ ] An external-system (ERP) interface exists with a no-op implementation selected by
      configuration.
- [ ] Customer records carry an optional external-reference field that is unused by default.
- [ ] The whole application runs and demos with every real integration absent and no credentials
      configured.
- [ ] A short written note records, for each seam, what a real implementation would have to add
      (accounts, webhooks, retries, delivery receipts, opt-outs, field mapping).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-intake-messaging` (owns the message
  model these adapters must match), `sla-automation/sla-routing-escalation` (owns notifications,
  which are in-app only in this assessment — A-13 — and would route through these adapters later),
  `customer-management/customer-records` (external-reference field).
- **Depends on code areas or other stories:** None depends on this story; it is the safest
  single story to cut.

## Extra notes (optional)

- Cut order: T3 — cut before T2 items. If cut, `docs/product-scope.md` §5 already documents these
  as designed-not-delivered, so the cut costs traceability, not correctness.
- The value here is architectural honesty: the seam must be real enough that a reviewer can see
  where the provider goes, and no more.

## Technical hints (optional)

- Repos/roots: `backend`.
- Keep the fakes in the application, not in test-only code — the demo runs against them.
- **Do not invent** endpoints, provider SDK usage, or webhook routes; Stage 5 (Architecture) and
  Stage 7 (API Design) fix the boundary first.

## Out of scope

- Any real email, WhatsApp or SMS send or receive; provider accounts; WhatsApp Business API
  onboarding and template approval; sender-ID registration; inbound webhooks; delivery receipts;
  retries; opt-out handling (T3-A, §8).
- Any real ERP connection, field mapping, sync strategy or conflict resolution (T3-D, §8).
- Real-time chat transport (T3-B) and the AI chatbot (T3-C).
- A partner-facing API, API keys, rate limiting or a versioning strategy — the app's own API is
  the deliverable (T2-L).
- Message brokers or queues to carry integration traffic (§8 technical exclusions).
