# Story 18 — Channel and ERP integration seams with fakes

> **Source of truth:** `docs/requirements.md` §3.1, §3.2, §3.4, §11.2, §11.3, §11.4 · `docs/product-scope.md` **T3-A**, **T3-B**, T3-C, **T3-D**, **A-7**, A-13, §5 (T3 rule of engagement), §9 questions 2 and 3 · `docs/architecture.md` **§5.2**, **§5.3**, **§5.4**, AD-11 · `docs/data-model.md` **DM-6**, §2.8, §2.4 · `docs/api-design.md` **§8.2**, **§8.3**, §8.4, **AP-11** · `docs/ui-design.md` §13 (no screen)
> **Intake:** `.squad/stories/integration-seams/channel-erp-adapters/intake.md` · **Tier:** T3 — **the safest single story to cut. Nothing depends on it.**
> **Phase:** 8 — Experience and seams. **Backend only. No endpoint, no screen, no entity.**

## Prerequisites

- **Story 07 completed:** `TicketMessage`, `MessageChannel` and `TicketMessageService.PostAsync` —
  **the message model these adapters must match**.
- **Story 09 completed:** notifications, which are in-app only in this assessment (A-13) and would
  route through these adapters later.
- **Story 04 completed:** `Customer.externalReference` — already present, unused by default (DM-6).

> ### ⚠ Blocked decision — **PF-2** — the inbound path has no actor
>
> `Ticket.createdByUserId` and `TicketMessage.authorUserId` are **required** (data-model §2.6, §2.8)
> and `TicketActivity.actorKind = System` is reserved for **the SLA monitor only** (data-model §2.7,
> R-14). **The fake inbound adapter has no human actor.**
>
> `PROJECT-PROGRESS.md` §6.5 records PF-2 as *"avoided in Stage 7 (AP-11 publishes no ingestion
> endpoint) but **still open for story 18**"*, and `api-design.md` §9 says the gap *"remains real
> for story 18 and is unchanged by this document."*
>
> **This is the story where it comes due, and this plan does not resolve it.** Options, each a
> product decision:
>
> | Option | Change | Consequence |
> |---|---|---|
> | **A.** Attribute an inbound channel message to the customer's **linked portal `User`** | No schema change; requires a rule stating that a channel message is authored by that login | Fails for a customer with **no** portal login — most agent-created profiles |
> | **B.** Introduce a **system/service account `User`** for channel ingestion | A seeded row; no schema change | Adds a second system actor, which R-14 deliberately avoided |
> | **C.** Make `createdByUserId` / `authorUserId` nullable with `actorKind = System` | **A data-model change** | Widens `System` beyond the SLA monitor, contradicting data-model §2.7 |
>
> **Do not choose one here.** Tasks 1–3 and 6–7 (the outbound channel adapter, the ERP gateway, the
> seam documentation) are **unblocked and complete this story's other seven acceptance criteria**.
> **Task 4 — the inbound fake — is blocked**, and its acceptance criterion is marked blocked below.
> Recorded as **S9-10** in `00-implementation-plan.md`.

---

## Story Goal

Prove that the external integrations of requirements §3 and §11 were **designed for**, without
building any of them. The T3 rule of engagement: **a named abstraction plus a fake the demo can run
against, requiring no external account, provider contract or production credential.**

1. **One outbound channel-adapter abstraction**, with a **console/log adapter** selected by
   configuration, plus a written contract stating what a real provider adapter must implement.
2. **A normalized inbound message shape** that produces the **same message model** as the web form
   and portal — *"adding a channel adds an adapter, not a second message concept."*
3. **One outbound ERP/external-system gateway interface with a no-op implementation**, and the
   optional `externalReference` on customer records.
4. **A written note, per seam, of what a real implementation would have to add** — so
   *"designed for, not delivered"* is a **verifiable claim rather than an assertion**.

**Also documented here as future consumers and NOT built:** real-time live chat (T3-B) and the AI
chatbot (T3-C, whose seam is the **AI** abstraction of Story 10, **not this one**).

---

## Context — Read These Files First

1. `docs/architecture.md` **§5.2 in full** — one normalized message model owned by the Tickets
   module; one outbound adapter interface with a console/log implementation; **inbound arrives as a
   normalized message into the same ingestion service the web form uses**; and the paragraph that
   matters most: *"adding email, WhatsApp or SMS later means writing an adapter — not a second
   message concept, a second thread model, or a second ingestion path. **That is the whole claim
   this seam makes, and it is the claim a reviewer should test.**"*
2. `docs/architecture.md` **§5.2's warning**: *"In-app notifications are a different thing and must
   not be confused with this seam."* Notifications (A-13) are in-app only, served by their own
   Application service (Story 09). **When email or SMS delivery is added later, it becomes a
   *consumer* of the channel adapter.**
3. `docs/architecture.md` **§5.3** (ERP: one outbound gateway interface, a no-op implementation,
   `externalReference` unused by default, and §11.4 **unbounded on purpose**) and **§5.4** (what
   each seam must document).
4. `docs/api-design.md` **§8.2**, **§8.3**, **§8.4** and **AP-11** — **no inbound endpoint, no
   adapter-management endpoint, no ERP surface, no WebSocket, no long-poll.** The only trace in the
   API is `externalReference` on the customer payload, **read-only and not settable**.
5. `docs/product-scope.md` §5 (T3-A, T3-B, T3-C, T3-D) and **§9 questions 2 and 3** — the ERP
   product and the unbounded "external systems" **stay open**. *"Do not invent one."*
6. `.squad/stories/integration-seams/channel-erp-adapters/intake.md` — eight acceptance criteria
   and *"Keep the fakes in the application, not in test-only code — the demo runs against them."*

---

## Product rules (from story)

- **No real email, WhatsApp or SMS send or receive.** No provider account, no WhatsApp Business API
  onboarding, no sender-ID registration, no inbound webhook, no delivery receipt, no retry, no
  opt-out handling.
- **No real ERP connection**, no field mapping, no sync strategy, no conflict resolution.
- **§11.4 "External systems" is unbounded and cannot be scoped further without a named system.**
  **Do not name one.**
- **The whole application runs and demos with every real integration absent and no credentials
  configured.**
- **No message broker or queue** carries integration traffic (product-scope §8).
- **No partner-facing API, API keys, rate limiting or versioning strategy** — the app's own API is
  the deliverable (T2-L).
- **The value here is architectural honesty:** the seam must be real enough that a reviewer can see
  where the provider goes, **and no more**.

---

## Backend Tasks

### 1 — Application: the outbound channel-adapter interface (AD-11)

**Create file: `src/SupportCrm.Application/Modules/Integrations/IOutboundChannelAdapter.cs`**

```csharp
public interface IOutboundChannelAdapter
{
    MessageChannel Channel { get; }
    Task<OutboundDeliveryResult> SendAsync(OutboundMessage message, CancellationToken ct);
}

public sealed record OutboundMessage(Guid TicketId, string RecipientAddress, string Body);
public sealed record OutboundDeliveryResult(bool Accepted, string? AdapterReference);
```

**Owned by Application, implemented in Infrastructure** — that dependency direction is what makes
the seam swappable rather than a diagram (AD-11).

**`AdapterReference` is returned but not persisted.** DM-6: the seams persist exactly one field,
`Customer.externalReference`; there is **no** adapter-config, webhook, receipt or delivery table
(data-model §2.16). Record the reference in the log line and in the ticket activity, not in a new
column.

### 2 — Infrastructure: the console/log adapter

**Create file: `src/SupportCrm.Infrastructure/Seams/Channels/ConsoleChannelAdapter.cs`**

- Writes a structured log entry (ticket id, channel, recipient, body length — **not the full body**)
  and **attempts no external call**.
- Returns `Accepted: true` with a deterministic adapter reference derived from the message, so the
  demo is repeatable.
- Records the send against the ticket as a `MessagePosted` activity through the **existing**
  `TicketMessageService` — *"sending produces a log entry **and a recorded ticket activity**"*
  (intake AC). **Do not write an activity row directly**; go through the one recorder.

**It lives in the application, not in test-only code** — the demo runs against it (intake).

### 3 — Configuration and selection

**Create file: `src/SupportCrm.Application/Configuration/ChannelOptions.cs`**

```csharp
public sealed class ChannelOptions
{
    public const string SectionName = "SupportCrm:Channels";
    public ChannelAdapterKind Outbound { get; init; } = ChannelAdapterKind.Console;  // the only shipped value
}
public enum ChannelAdapterKind { Console }
```

**The enum has one value on purpose.** A second arrives with the adapter that implements it — that
is the seam, stated in the type. Register in the composition root with `Console` as the default, so
the system runs with no configuration.

### 4 — The normalized inbound shape — **BLOCKED on PF-2, see the box above**

**Create file: `src/SupportCrm.Application/Modules/Integrations/InboundChannelMessage.cs`** — the
normalized shape, which is **not** blocked and is worth having:

```csharp
public sealed record InboundChannelMessage(MessageChannel Channel, string FromAddress,
                                           string? Subject, string Body, DateTimeOffset ReceivedAt);
```

**Create file: `src/SupportCrm.Application/Modules/Integrations/IInboundChannelIngestion.cs`** with
a single method, and **an implementation whose body is:**

```csharp
// PF-2 / S9-10 — BLOCKED. Ticket.createdByUserId and TicketMessage.authorUserId are required
// (data-model §2.6, §2.8) and actorKind = System is reserved for the SLA monitor (§2.7, R-14).
// An inbound channel message has no human actor and no rule says who it is attributed to.
// Do NOT invent an attribution. See 00-implementation-plan.md, finding S9-10.
throw new NotImplementedException("PF-2: inbound actor attribution is undecided.");
```

**Two things are still true and must be preserved:**

1. **When it is unblocked, ingestion calls `TicketMessageService.PostAsync` — the same method the
   web form and the portal use.** It does **not** get a second ingestion path (architecture §5.2).
2. **No HTTP route is added** (AP-11). The adapter calls the service **in-process**. Publishing an
   ingestion endpoint is exactly what AP-11 refused to do, and for this same reason.

### 5 — Application: the ERP / external-system gateway (T3-D)

**Create file: `src/SupportCrm.Application/Modules/Integrations/IExternalSystemGateway.cs`**

```csharp
public interface IExternalSystemGateway
{
    Task<string?> LookupExternalReferenceAsync(Guid customerId, CancellationToken ct);
    Task PushCustomerChangedAsync(Guid customerId, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Seams/Erp/NoOpExternalSystemGateway.cs`** — returns
`null` and does nothing, logging at `Debug` that the no-op is in effect. Selected by
`SupportCrm:Erp:Gateway`, default `NoOp`.

- **`Customer.externalReference` already exists** and is **unused by default** (Story 04, DM-6). It
  is **read-only in the API and settable through no endpoint** (api-design §8.3). Confirm both;
  change neither.
- **No real connection, no field mapping, no sync strategy, no conflict resolution.**
- **Do not name an ERP product.** Product-scope §9 questions 2 and 3 stay open; the gateway is the
  place they would attach.

### 6 — Seam documentation (§5.4 — a graded acceptance criterion)

**Create file: `docs/integration-seams.md`** — one section per seam, each stating **what a real
implementation would have to add**, so *"designed for, not delivered"* is verifiable:

| Seam | Must document |
|---|---|
| **Email / WhatsApp / SMS (T3-A)** | Provider accounts and onboarding, **WhatsApp Business API template approval**, sender-ID registration, inbound **webhooks** and their signature verification, **delivery receipts**, retry and backoff, **opt-out handling**, per-channel rate limits, and **the PF-2 actor question this story could not answer** |
| **ERP / external systems (T3-D)** | A **named** system (absent — product-scope §9 questions 2 and 3), direction of sync, the object set, **field mapping**, conflict resolution, scheduling, and idempotence |
| **Live chat (T3-B)** | A real-time transport, agent presence, chat queueing and routing. **Delivered behaviour is the polled request/response messaging of T2-B, and nothing in this design describes polling as real-time.** |
| **AI chatbot (T3-C)** | **Its seam is the AI abstraction of Story 10, not this one.** Conversation flows, knowledge grounding and **bot-to-human handoff semantics remain an open question** (product-scope §9 question 6) |

Link it from `README.md`. **This document is a deliverable of the story, not a nicety** — it is the
eighth acceptance criterion.

### 7 — Tests

**Create file: `tests/SupportCrm.Tests/Integrations/ChannelSeamTests.cs`**

1. `ConsoleChannelAdapter.SendAsync` returns `Accepted: true`, writes **one** log entry and
   **attempts no HTTP call** (assert with a failing `HttpMessageHandler`).
2. The send is **recorded against the ticket** as an activity row.
3. **The adapter is the only channel-specific code:** reflection finds exactly one
   `IOutboundChannelAdapter` implementation, and `grep` finds no `switch` on `MessageChannel`
   anywhere in `Modules/Tickets`. *That is the claim the seam makes, tested.*
4. **`MessageChannel` gained no new value in this story** — a new value arrives with the adapter
   that implements it.

**Create file: `tests/SupportCrm.Tests/Integrations/ErpSeamTests.cs`**

5. `NoOpExternalSystemGateway` returns `null` and performs no side effect.
6. **`Customer.externalReference` is settable through no endpoint** — `POST /customers` and
   `PATCH /customers/{id}` with the field -> **`400`**; it is returned read-only on `GET`.

**Create file: `tests/SupportCrm.Tests/Integrations/NoIntegrationSurfaceTests.cs`**

7. **No route exists** at `/channels/inbound`, `/webhooks/*`, `/erp/*`, or `/adapters/*` (AP-11,
   §8.3).
8. **No WebSocket or long-poll endpoint exists** (§8.4, T3-B).
9. The application **starts and every prior test suite passes with no integration credential
   configured** — the T3 rule of engagement.

**Create file: `tests/SupportCrm.Tests/Integrations/InboundIngestionTests.cs`** — **one skipped
test** with an explicit reason naming **PF-2 / S9-10**, asserting the shape the test will take once
the decision is recorded: an inbound message reaches `TicketMessageService.PostAsync` and produces a
message carrying its channel of origin. **A skipped test with a reason is the honest artefact here;
a passing test built on an invented attribution rule is not.**

---

## Frontend Tasks

**No frontend changes required.** `ui-design.md` §13 records this story as having **no screen** —
these are internal seams. **Do not add an adapter-status panel, a channel picker or an integration
settings screen**; none is authorized, and T2-I forbids configuration UI.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Seam tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Integrations` — eight green, one
   skipped **with its PF-2 reason printed**.
3. **No credentials:** `docker compose up --build` with an `.env` containing no provider value —
   everything starts; the log records that the **console** channel adapter and the **no-op** ERP
   gateway were selected.
4. **No integration surface:** `grep -rniE "webhook|/channels/|/erp/|websocket|signalr|socket\.io" backend/src/ frontend/src/`
   returns nothing.
5. **No excluded infrastructure:** `grep -rniE "rabbit|kafka|azure\.messaging|sqs|servicebus" backend/`
   returns nothing.
6. **One channel-specific file:** the reflection test in task 7 confirms exactly one adapter
   implementation.
7. **Documentation:** `docs/integration-seams.md` exists, covers all four seams, and is linked from
   `README.md`.
8. **Regression:** every suite from Stories 01–17 still passes — this story adds seams and changes
   no existing behaviour.

---

## Done Criteria

- [ ] A **single outbound channel-adapter interface** exists, with a console/log implementation
      **selected by configuration**.
- [ ] Sending an outbound notification through the log adapter **produces a log entry and is
      recorded against the ticket, with no external call attempted**.
- [ ] ⛔ **BLOCKED — PF-2 / S9-10:** *"An inbound message delivered through the fake adapter creates
      or updates a ticket using the SAME message model, carrying its channel of origin."*
      **The inbound path has no actor and no source says who it is attributed to.** The normalized
      shape and the ingestion interface exist; the implementation throws with the reason, and the
      test is skipped with the reason. **Take Option A, B or C before closing this box — do not
      invent an attribution.**
- [ ] **Adding a hypothetical channel requires implementing the adapter only** — demonstrated by the
      written contract **and** by the reflection test showing the log adapter is the sole
      channel-specific code.
- [ ] An **external-system (ERP) interface** exists with a **no-op implementation** selected by
      configuration.
- [ ] Customer records carry an **optional external-reference field, unused by default** and
      settable through no endpoint.
- [ ] **The whole application runs and demos with every real integration absent and no credentials
      configured.**
- [ ] **A short written note records, for each seam, what a real implementation would have to add**
      — `docs/integration-seams.md`, covering T3-A, T3-D, T3-B and T3-C.
- [ ] **No endpoint, no screen, no entity, no broker, no queue and no named ERP product** was
      introduced.
- [ ] **Product-scope §9 questions 2, 3 and 6 remain open.**
- [ ] `00-overview.md` updated with this story.

**This is the final story. Report to the user, including the PF-2 blocker, and confirm the full
implementation sequence is complete.**
