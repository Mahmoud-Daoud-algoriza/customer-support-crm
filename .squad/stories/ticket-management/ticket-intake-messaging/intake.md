# Story intake — Web form intake and in-portal messaging

> **Source of truth:** `docs/requirements.md` §3.5, §3.3 (partial) · `docs/product-scope.md` T2-B, T3-A, T3-B
> **Scope tier:** T2 (minimal but real)

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `ticket-intake-messaging`)
- **Work item type:** Story

---

## Title

```
Web form intake and in-portal messaging
```

---

## Description

```
Deliver the one real communication channel for this assessment, and the message model every
other channel would plug into.

Web form (requirements §3.5) — the single real, fully working inbound channel:
- A form that creates a ticket for the submitting customer.
- Submission requires an authenticated customer (A-9: no anonymous submission).

In-portal messaging (requirements §3.3, partial):
- Customer and agent exchange replies on a ticket using ordinary request/response.
- This is NOT real-time chat. No WebSocket infrastructure, no presence, no typing indicators.
  Real-time live chat is acknowledged as future scope T3-B.

Channel-agnostic message model:
- Both the web form and portal replies write into ONE message model that carries the channel
  it arrived on.
- That model is the contract the email/WhatsApp/SMS adapters of `integration-seams/
  channel-erp-adapters` (T3-A) will produce and consume. Design it so adding a channel adds an
  adapter, not a second message concept.
- Every message appears in ticket history (§2.5) and in the customer interaction timeline (§1.3).
```

---

## Acceptance criteria

```
- [ ] An authenticated customer can submit a ticket through a web form; a ticket is created and
      linked to that customer.
- [ ] An unauthenticated submission attempt is refused.
- [ ] Customer and agent can exchange replies on a ticket; both see the thread in order.
- [ ] Every message records its channel of origin, author and timestamp.
- [ ] Messages are visible to the ticket's customer; internal notes are NOT (see
      `agent-workspace/tasks-internal-notes`).
- [ ] Messages appear in ticket history and in the customer's interaction timeline.
- [ ] A reply on a Closed or Cancelled ticket is refused (A-5).
- [ ] The message model demonstrably supports a second channel without schema change — shown by
      the log adapter in `integration-seams/channel-erp-adapters` writing an inbound message.
- [ ] No polling implementation is presented as, or described as, real-time chat.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `ticket-management/ticket-core`,
  `ticket-management/ticket-lifecycle`.
- **Depends on code areas or other stories:** Provides the message model consumed by
  `customer-portal/portal-self-service`, `agent-workspace/agent-dashboard` (replies and quick
  replies), `ai-assist/ai-ticket-assists` (summaries and suggested replies read the thread),
  and `integration-seams/channel-erp-adapters`.

## Extra notes (optional)

- Cut order: T2. If cut, the portal and AI reply stories lose their thread — cut this last
  among T2 items.
- The channel field exists from day one even though only two values are real. That field is what
  makes T3-A a seam rather than a rewrite.

## Technical hints (optional)

- Repos/roots: `backend`, `frontend`. Front end is Angular + PrimeNG.
- **Do not invent** tables or endpoints here; Stage 6 (Data Model) and Stage 7 (API Design) fix the message shape first.

## Out of scope

- Real email, WhatsApp or SMS send/receive; provider accounts; webhooks; delivery receipts;
  opt-out handling (T3-A, product-scope §8).
- Real-time transport, agent presence, chat queueing and routing (T3-B, §8).
- The AI chatbot (T3-C).
- Attachments on messages beyond what `customer-management/customer-records` delivers (T2-A).
