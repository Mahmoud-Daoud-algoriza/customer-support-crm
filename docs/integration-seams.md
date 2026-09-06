# Integration seams — what is delivered, and what a real implementation would have to add

> **Source of truth:** [requirements.md](requirements.md) §3.1, §3.2, §3.4, §11.2, §11.3, §11.4 ·
> [product-scope.md](product-scope.md) **T3-A**, **T3-B**, **T3-C**, **T3-D**, **A-7**, A-13, §5, §8, §9 ·
> [architecture.md](architecture.md) **§5.2**, **§5.3**, **§5.4**, AD-11 · [data-model.md](data-model.md) **DM-6**, §2.16 ·
> [api-design.md](api-design.md) **§8.2**, **§8.3**, **§8.4**, **AP-11**
>
> Written by **Story 18** (`.squad/plans/integration-seams/18-story-channel-erp-adapters.md`), which
> owns every seam below. **This document is an acceptance criterion of that story, not a nicety** —
> [architecture.md](architecture.md) §5.4 requires it so that *"designed for, not delivered"* is a
> **verifiable claim rather than an assertion**.

## What "designed for, not delivered" means here

Product-scope §5's **T3 rule of engagement**: a **named abstraction plus a fake the demo can run
against**, requiring **no external account, provider contract or production credential**.

So for each seam this document answers two questions, and keeps them apart:

1. **What is actually in the repository**, and where — so a reviewer can read it rather than take
   it on trust.
2. **What a real implementation would have to add** — the work that was consciously *not* done, so
   nobody mistakes the seam for the feature.

**The whole application runs, starts and demos with every real integration absent and no
credentials configured.** Neither `SupportCrm:Channels` nor `SupportCrm:Erp` appears in
`appsettings.json`: the defaults *are* the shipped implementations. At startup the API logs which
implementation of each seam is live, so the claim is observable rather than asserted.

---

## 1. Communication channels — email, WhatsApp, SMS (T3-A)

> requirements §3.1, §3.2, §3.4, §11.3 · [architecture.md](architecture.md) §5.2

### What is delivered

| Piece | Where |
|---|---|
| The outbound adapter interface, `OutboundMessage`, `OutboundDeliveryResult` | `backend/src/SupportCrm.Application/Modules/Integrations/IOutboundChannelAdapter.cs` |
| The **console/log adapter** — the shipped implementation | `backend/src/SupportCrm.Infrastructure/Seams/Channels/ConsoleChannelAdapter.cs` |
| The normalized **inbound** shape | `backend/src/SupportCrm.Application/Modules/Integrations/InboundChannelMessage.cs` |
| The inbound ingestion interface (**blocked — see §1.3**) | `backend/src/SupportCrm.Application/Modules/Integrations/IInboundChannelIngestion.cs` |
| Selection by configuration — `SupportCrm:Channels:Outbound`, default `Console` | `backend/src/SupportCrm.Application/Configuration/ChannelOptions.cs` |
| The channel a message arrived on | `TicketMessage.channel` — present from day one (DM-6) |

**Sending produces a log entry and a recorded ticket activity, and attempts no external call.** The
adapter writes through `TicketMessageService.PostAsync` — *the same method the staff reply endpoint
and the portal call* — so the record of an outbound send is an ordinary `TicketMessage` with its one
linked `MessagePosted` activity row (DM-4, §5 constraint 17). The body is **not** written to the log;
its length is.

### 1.1 The claim this seam makes

[architecture.md](architecture.md) §5.2, and it is the claim a reviewer should test:

> *"Adding email, WhatsApp or SMS later means writing an adapter — not a second message concept, a
> second thread model, or a second ingestion path."*

Three things in the repository make that checkable rather than asserted, each with a test in
`backend/tests/SupportCrm.Tests/Integrations/`:

- **One interface, one implementation.** A reflection test asserts exactly one
  `IOutboundChannelAdapter` implementation exists.
- **Nothing branches on the channel.** A test greps `Modules/Tickets` and fails on any `switch` over
  `MessageChannel`. Channel origin is **data**, never a code path.
- **`MessageChannel` gained no new value in this story.** A new value arrives *with* the adapter that
  implements it.

### 1.2 What a real provider adapter would have to add

None of this is built, and none of it is stubbed:

| Area | What a real implementation must bring |
|---|---|
| **Accounts and onboarding** | A provider account per channel; for WhatsApp, **Business API onboarding and message-template approval**; for SMS, **sender-ID registration** and the per-country rules that go with it |
| **Credentials** | API keys or tokens, from the environment only, never committed — plus rotation |
| **Inbound transport** | **Webhook endpoints** and their **signature verification**, replay protection, and the ordering/duplication guarantees the provider does *or does not* give. Note that publishing an inbound endpoint is refused today by **AP-11** — see §1.3 |
| **Delivery semantics** | **Delivery receipts** and status callbacks; **retry with backoff**; dead-lettering; distinguishing "accepted by the provider" from "delivered to a person" — today `Accepted` means only the former |
| **Compliance** | **Opt-out / STOP handling** and suppression lists; consent capture; per-channel content and template rules; data-residency and retention |
| **Throughput** | Per-channel **rate limits** and quota handling; how throttling interacts with the request that triggered the send |
| **Addressing** | Where a recipient address comes from and how it is validated per channel. `OutboundMessage.RecipientAddress` is deliberately an **opaque string**: typing it would name a channel |
| **Storage** | Anything the above needs to persist. **DM-6 permits none of it today**: the seams persist exactly one field, `Customer.externalReference`. There is no adapter-configuration, credential, webhook or delivery-receipt table (§2.16), and `OutboundDeliveryResult.AdapterReference` is **returned and never stored** |
| **Attribution** | **The PF-2 question below** — which this story could not answer |

### 1.3 ⛔ The open question this story could not answer — **PF-2 / S9-10**

**The inbound path has no actor.**

`Ticket.createdByUserId` and `TicketMessage.authorUserId` are **required**
([data-model.md](data-model.md) §2.6, §2.8), and `TicketActivity.actorKind = System` is reserved for
**the SLA monitor only** (§2.7, **R-14**). An inbound channel message has no human actor, and **no
approved document says who it is attributed to.**

Three options, each a **product decision**. Story 18 chose **none**:

| Option | Change | Consequence |
|---|---|---|
| **A** — attribute to the customer's linked portal `User` | No schema change; needs a rule saying a channel message is authored by that login | **Fails for a customer with no portal login** — most agent-created profiles |
| **B** — a seeded system/service-account `User` for ingestion | A seeded row; no schema change | Adds a **second system actor**, which R-14 deliberately avoided |
| **C** — nullable author with `actorKind = System` | **A data-model change** | Widens `System` beyond the SLA monitor, contradicting §2.7 |

**Current state:** the normalized shape and the ingestion interface exist; the implementation
(`BlockedInboundChannelIngestion`) **throws with the reason**, and its test is **skipped with the
reason**. A skipped test with a stated reason is the honest artefact; a passing test built on an
invented attribution rule is not.

**Two things must survive the unblocking:** ingestion calls `TicketMessageService.PostAsync` — the
same method the web form and the portal use, **not** a second ingestion path (§5.2) — and it gets
**no HTTP route** (**AP-11**), because publishing an ingestion endpoint is what would force this
undecided question into the contract.

### 1.4 What is deliberately absent from the API

**No inbound endpoint and no adapter-management endpoint** ([api-design.md](api-design.md) §8.2,
AP-11). Adapters are internal and are called in-process. A test asserts that no route exists at
`/channels/inbound`, `/webhooks/*` or `/adapters/*`.

---

## 2. ERP and external systems (T3-D)

> requirements §11.2, §11.4 · [architecture.md](architecture.md) §5.3

### What is delivered

| Piece | Where |
|---|---|
| The outbound gateway interface | `backend/src/SupportCrm.Application/Modules/Integrations/IExternalSystemGateway.cs` |
| The **no-op implementation** — the shipped one | `backend/src/SupportCrm.Infrastructure/Seams/Erp/NoOpExternalSystemGateway.cs` |
| Selection by configuration — `SupportCrm:Erp:Gateway`, default `NoOp` | `backend/src/SupportCrm.Application/Configuration/ErpOptions.cs` |
| The one persisted trace | `Customer.externalReference` — optional, **unused by default** (DM-6) |

`LookupExternalReferenceAsync` returns `null`; `PushCustomerChangedAsync` does nothing. Both log at
`Debug` that the no-op is in effect. **No connection is opened and no credential is read.**

**`Customer.externalReference` is read-only in the API and settable through no endpoint**
([api-design.md](api-design.md) §8.3). It is returned on the customer payload; sending it to
`POST /customers` or `PATCH /customers/{id}` is a **`400`** under AP-10's unmapped-member rule, and a
test asserts exactly that. **Nothing writes it** — which is what *"unused by default"* means.

### 2.1 No system is named, and that is the point

**Requirements §11.4 "External systems" is unbounded on purpose.**
[product-scope.md](product-scope.md) §9 **questions 2 and 3 remain open** and cannot be scoped until
a system is named. **Do not name one** — not in this document, not in `ExternalSystemGatewayKind`,
not in configuration. The gateway is the place such a system would attach, and holding it at exactly
two methods is what keeps the question open rather than quietly answering it.

### 2.2 What a real implementation would have to add

| Area | What a real implementation must bring |
|---|---|
| **A named system** | Absent by decision — product-scope §9 questions 2 and 3. Everything below depends on this answer and cannot be specified before it |
| **Direction of sync** | Inbound, outbound or bidirectional; which side is authoritative for which field |
| **The object set** | Which entities cross the boundary at all — customers only, or tickets, or organization structure |
| **Field mapping** | The mapping itself, its versioning, and what happens to fields with no counterpart |
| **Conflict resolution** | Last-writer-wins, source-of-truth-per-field, or human review — and where a rejected change goes |
| **Scheduling** | On-change, batched or polled; the trigger, and what happens to changes made while the far side is down |
| **Idempotence** | Correlation keys and replay safety, so a retried push does not duplicate a record |
| **Failure handling** | Whether a failed push may block the user's request. Today it cannot, because there is no push |
| **Credentials and transport** | Endpoints, authentication and network reachability — from the environment, never committed |

### 2.3 What is deliberately absent from the API

**No ERP endpoints of any kind** ([api-design.md](api-design.md) §8.3). A test asserts no route
exists at `/erp/*`.

---

## 3. Real-time live chat (T3-B) — a future *consumer*, not built

> [architecture.md](architecture.md) §5.2 · [api-design.md](api-design.md) §8.4

**Delivered behaviour is the ordinary request/response messaging of T2-B**, and **nothing in this
design describes polling as real-time chat.** There is **no WebSocket, no long-poll, no SignalR and
no presence** — asserted by a test and by a grep over both `backend/src/` and `frontend/src/`.

What a real implementation would have to add:

| Area | What a real implementation must bring |
|---|---|
| **Transport** | A real-time transport (WebSocket or similar), connection lifecycle, reconnection and backpressure. Product-scope §8 excludes a **message broker or queue**, so this arrives with its own hosting decision |
| **Presence** | Agent online/away/busy state, and who owns it |
| **Queueing and routing** | A live queue distinct from the ticket queue; wait times, abandonment, and routing to an available agent |
| **Session model** | Whether a chat is a ticket, becomes one, or is a different thing — and what its transcript is |
| **Operating hours** | What happens outside them, given A-3's SLA clock is 24/7 |

---

## 4. AI chatbot (T3-C) — a future consumer of a **different** seam

> [product-scope.md](product-scope.md) §9 question 6 · [architecture.md](architecture.md) §5.1

**Its seam is the AI abstraction of Story 10 (`IAiAssistService`), not this one.** It is listed here
only so nobody looks for it in the channel adapter.

That seam is deliberately **suggestion-only** (A-8, AD-12): every method returns a suggestion and no
method can take an action — a reflection test enforces it. A chatbot that replied to a customer
would be an *action*, so it is not a small extension of what exists.

| Area | What a real implementation must bring |
|---|---|
| **Conversation flows** | Turn-taking and state across a conversation; the current seam is stateless and single-shot |
| **Knowledge grounding** | Retrieval over the knowledge base with citations. T2-E is **keyword** retrieval (AD-13), not embeddings |
| **Bot-to-human handoff** | **An open question — product-scope §9 question 6.** When the bot yields, what the agent inherits, and how the customer is told |
| **Guardrails** | A-8 requires AI output to be reviewable before it reaches a customer. An autonomous replier changes that rule, which is a **product decision**, not an implementation detail |

---

## 5. What no seam introduced

Asserted by the tests in `backend/tests/SupportCrm.Tests/Integrations/` and by the greps in Story
18's verification steps:

- **No endpoint** — no inbound, adapter-management or ERP route; no WebSocket, no long-poll (AP-11,
  §8.2, §8.3, §8.4).
- **No screen** — [ui-design.md](ui-design.md) §13 records this story as having none. There is no
  adapter-status panel, channel picker or integration settings screen; T2-I forbids configuration UI.
- **No entity and no migration** — DM-6 and §2.16. The seams persist exactly one field, which Story
  04 already added.
- **No message broker and no queue** (product-scope §8).
- **No partner-facing API, API keys, rate limiting or versioning strategy** — the application's own
  API is the deliverable (T2-L).
- **No named provider and no named ERP product.** Product-scope §9 **questions 2, 3 and 6 remain
  open.**
