# Story 10 — AI service abstraction with deterministic offline fake

> **Source of truth:** `docs/requirements.md` §7 · `docs/product-scope.md` T1-F, **A-7**, **A-8**, T3-C, §9 question 1, §10 item 5 · `docs/architecture.md` **§5.1**, §2.1, §6.3, **AD-11**, **AD-12** · `docs/data-model.md` **DM-5** · `docs/api-design.md` §5.8, §8.1, AP-12 · `docs/ui-design.md` §13 (no screen)
> **Intake:** `.squad/stories/ai-assist/ai-service-seam/intake.md` · **Tier:** T1 — cannot be cut. *"If the AI provider is unavailable on demo day, the fake IS the demo, and that is by design."*
> **Phase:** 5 — Automation and AI. **Backend only. No endpoint, no screen, no entity.**

## Prerequisites

- **Story 01 completed** — that is the whole dependency list. This story touches nothing else.

> **Parallelizable.** It depends only on the skeleton and shares no file with Stories 02–09, so it
> can be built at any point from Phase 1 onward, by a second worker, without conflict. See
> `00-implementation-plan.md` §"Parallelization".

---

## Story Goal

Build the **single seam** every AI feature in requirements §7 sits behind, **before** building any
of the features themselves.

1. **One** AI service abstraction in Application covering summary, suggested reply and suggested
   category/priority. The future chatbot (T3-C) is documented as a further consumer of this same
   interface and is **not built**.
2. **Two implementations** in Infrastructure: a real provider adapter, and a **deterministic offline
   fake**, selected by configuration with the **fake as the local default**.
3. **A-8's guardrails made structural, not remembered:** the interface has **no method that can
   send, transition or assign**. Advisory-only is a property of the seam's shape (AD-12).
4. **Failure is contained**: a provider error or timeout surfaces as "AI unavailable" for that one
   feature; ticket creation, replies and status changes continue.

**No endpoint exists in this story** — Story 11 publishes the three of api-design §5.8.
**No entity exists at all** — DM-5: summaries and suggested replies are not stored, and the
categorization record is ticket history.

---

## Context — Read These Files First

1. `docs/architecture.md` **§5.1 in full** — one interface, two implementations,
   configuration-driven selection, *"the interface can only return suggestions"*, contained failure,
   the provider question deliberately unanswered, and the line that **suggested solutions do not use
   this seam** (they are Knowledge-module keyword retrieval, Story 12).
2. `docs/architecture.md` **AD-11** (seam interfaces owned by Application, implemented in
   Infrastructure — this is what makes them swappable) and **AD-12** (*"a general 'AI action'
   interface would make autonomous behaviour a one-line mistake away"*).
3. `docs/product-scope.md` **A-7** (no live external system is contacted; the AI provider is the one
   real integration and **must degrade to a deterministic offline fake**) and **A-8** (advisory and
   human-approved; labelled; acceptance or override recorded in ticket history; **no autonomous
   action**).
4. `docs/api-design.md` §8.1 and **AP-12** — the contract never names a provider, exposes a model or
   accepts provider parameters; unavailability is **`503 ai-unavailable`**, and it is the **only**
   place in the API that uses `503`.
5. `docs/data-model.md` **DM-5** — no AI entity; only the categorization *decision* is persisted, as
   `AiSuggestionOffered` / `AiSuggestionResolved` ticket history.
6. `.squad/stories/ai-assist/ai-service-seam/intake.md` — eight acceptance criteria and the Out of
   scope list (no vector databases, no embeddings, no RAG pipelines, no fine-tuning, no prompt
   tooling, no evaluation harness).

---

## Product rules (from story)

- **All AI output is advisory and human-approved.** The abstraction returns suggestions; it never
  performs an action, sends a message, or mutates a ticket by itself.
- **The offline fake is a hard requirement, not a convenience.** Product-scope §10 item 5 requires
  the whole system to run with **no external accounts or credentials**; the fake is what makes that
  true.
- **Deterministic:** the same input produces the same output on every run, so demos and tests are
  repeatable.
- **A provider failure degrades one feature.** An AI outage must never block support work.
- **Provider calls and failures are logged without logging customer content beyond what is
  necessary.**
- **Which provider is deliberately unanswered** (product-scope §9 question 1), including whether
  data-residency limits apply to sending customer content. **The seam is what lets that stay open —
  do not resolve it here.**

---

## Backend Tasks

### 1 — Application: the one interface (AD-11, AD-12)

**Create file: `src/SupportCrm.Application/Modules/Ai/IAiAssistService.cs`**

```csharp
public interface IAiAssistService
{
    Task<AiSummary>        SummarizeThreadAsync(AiThreadContext context, CancellationToken ct);
    Task<AiSuggestedReply> SuggestReplyAsync(AiThreadContext context, CancellationToken ct);
    Task<AiClassification> SuggestClassificationAsync(AiClassificationRequest request, CancellationToken ct);
}
```

**Every method returns a suggestion record and nothing else.** There is **no** `SendReplyAsync`, no
`ApplyCategoryAsync`, no `AssignAsync`, no ticket id parameter that could be written back. **A-8's
rule is therefore a property of this file** rather than a discipline someone has to remember
(AD-12).

**Create file: `src/SupportCrm.Application/Modules/Ai/AiContracts.cs`**

```csharp
public sealed record AiThreadContext(string Subject, string Description,
                                     IReadOnlyList<AiMessage> Messages, bool IsUrgent);
public sealed record AiMessage(string AuthorRole, string Body, DateTimeOffset PostedAt);
public sealed record AiClassificationRequest(string Subject, string Description, bool IsUrgent);

public sealed record AiSummary(string Summary, DateTimeOffset GeneratedAt);
public sealed record AiSuggestedReply(string Draft, DateTimeOffset GeneratedAt);
public sealed record AiClassification(string CategoryCode, TicketPriority Priority, DateTimeOffset GeneratedAt);
```

`AiThreadContext` is **assembled by the caller from data it already has**; the seam never queries
the database itself, so it cannot reach a ticket it was not given.

**Create file: `src/SupportCrm.Application/Modules/Ai/AiUnavailableException.cs`** deriving from the
`SeamUnavailableException` Story 01 created, carrying the slug **`ai-unavailable`** so the Story 01
Problem Details handler maps it to `503` with no per-endpoint code (AP-12).

### 2 — Infrastructure: the deterministic offline fake

**Create file: `src/SupportCrm.Infrastructure/Seams/Ai/DeterministicFakeAiService.cs`**

- **Deterministic by construction.** Derive every output from a stable hash of the input — for
  example `SHA256(subject + description)` — and never from `Random`, `DateTime.Now`, `Guid.NewGuid`
  or any ambient state. The **only** non-deterministic field is `GeneratedAt`, which is a timestamp
  and not part of the suggestion.
- **Summary:** the first N characters of the description plus a count of messages and the last
  author role — recognisable, useful in a demo, and obviously extractive.
- **Suggested reply:** a template chosen by the hash from a small set of neutral drafts, with the
  customer's display name substituted. **It is a draft, not a send.**
- **Classification:** map the hash onto the **configured** category list and the four priorities.
  `isUrgent` may be used as one input (A-17) — say so in a comment, because that is exactly what
  A-17 permits and nothing more.
- **It makes no network call and reads no credential.** Add a test asserting the type has no
  `HttpClient` dependency.

### 3 — Infrastructure: the real provider adapter

**Create file: `src/SupportCrm.Infrastructure/Seams/Ai/ProviderAiService.cs`**

- Takes an `HttpClient` and `AiOptions` (endpoint, model name, API key **from environment only**).
- **Never named in the contract.** api-design §8.1: the API exposes no provider, no model and no
  provider parameters. Keep provider specifics inside this file.
- **Timeout and error containment:** a configured timeout (default 15 s); any timeout,
  non-success status or deserialization failure becomes `AiUnavailableException`. **It never
  bubbles a provider exception type into Application.**
- Logging: log the capability invoked, the outcome and the duration. **Do not log the full ticket
  body**; log lengths and identifiers instead (intake AC).
- If the key is absent the type **fails to construct** rather than calling anonymously — a missing
  credential is a configuration error, not a runtime surprise.

### 4 — Configuration and selection

**Create file: `src/SupportCrm.Application/Configuration/AiOptions.cs`**

```csharp
public sealed class AiOptions
{
    public const string SectionName = "SupportCrm:Ai";
    public AiProviderKind Provider { get; init; } = AiProviderKind.Fake;   // default: fake (A-7)
    public string? Endpoint { get; init; }
    public string? Model { get; init; }
    public string? ApiKey { get; init; }          // environment only, never committed
    public int TimeoutSeconds { get; init; } = 15;
}
public enum AiProviderKind { Fake, Provider }
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — register the implementation
`AiOptions.Provider` selects, with **`Fake` as the default** so the application starts and demos
with no configuration at all (architecture §6.3, product-scope §10 item 5). Validate at startup:
`Provider = Provider` **requires** `Endpoint` and `ApiKey`, and fails fast with a clear message if
either is missing.

### 5 — Documentation: the chatbot extension point (T3-C)

**Create file: `src/SupportCrm.Application/Modules/Ai/README.md`** recording, as the intake's final
acceptance criterion requires:

- **This interface is the extension point for the future chatbot (T3-C).** A bot would be a
  *consumer* of `IAiAssistService`, not a new seam.
- **What a real implementation must add:** a provider account, credential handling, rate-limit and
  retry policy, token accounting, prompt versioning, and a decision on **data residency and
  confidentiality for customer content** — product-scope §9 question 1, **which this story does not
  answer**.
- **What is deliberately absent:** vector databases, embeddings, retrieval-augmented pipelines,
  fine-tuning, prompt-management tooling and evaluation harnesses (product-scope §8, intake).
- **Suggested solutions (§7.4) do not use this seam** — they are keyword retrieval in the Knowledge
  module (AD-13, Story 12). State it here so nobody wires them in later.

### 6 — Tests

**Create file: `tests/SupportCrm.Tests/Ai/AiSeamShapeTests.cs`** — the tests that make the
guardrails checkable rather than asserted:

1. **Reflection over `IAiAssistService`: every method's return type is one of the three suggestion
   records, and no method name matches `Send|Assign|Transition|Update|Apply|Post`.** This is the
   AD-12 test — it fails the build if someone adds an action method.
2. `DeterministicFakeAiService` has **no `HttpClient` constructor parameter**.
3. The fake returns **byte-identical** output for identical input across two separate service
   instances, twice each.
4. A `ProviderAiService` given a stub `HttpClient` that times out throws
   **`AiUnavailableException`**, not a provider or transport exception.
5. With `SupportCrm:Ai:Provider` unset, the container resolves **the fake**.
6. With `Provider = Provider` and no `ApiKey`, **startup validation fails with a clear message**.

**Create file: `tests/SupportCrm.Tests/Ai/AiOutageDoesNotBlockWorkTests.cs`** — register a
throwing `IAiAssistService`, then confirm ticket creation, a reply and a status transition **all
still succeed**. This is T1-F's degradation requirement, tested at the seam rather than at the UI.

---

## Frontend Tasks

**No frontend changes required.** `ui-design.md` §13 records this story as having **no screen** —
the seam is server-side. The AI panel is built by
[Story 11](11-story-ai-ticket-assists.md).

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Seam tests pass:** `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~Ai` —
   including the reflection test that no action method exists.
3. **No credentials required:** with `.env` containing **no** AI key, `docker compose up --build`
   starts cleanly and the log records that the **fake** implementation was selected.
4. **Determinism by hand:** call the fake twice with the same context from a scratch test and diff
   the output — identical.
5. **No network:** `grep -rn "HttpClient" backend/src/SupportCrm.Infrastructure/Seams/Ai/` matches
   `ProviderAiService.cs` **only**.
6. **No provider leaks into the contract:** `grep -rniE "openai|anthropic|azure|gemini" backend/src/SupportCrm.Application/`
   returns nothing.
7. **Regression:** every prior suite still passes; no endpoint was added
   (`grep -rn "ai" backend/src/SupportCrm.Api/Controllers/` returns nothing).

---

## Done Criteria

- [ ] A **single** server-side AI abstraction exists, with a real-provider implementation and a
      deterministic offline fake behind it.
- [ ] Which implementation runs is chosen by **configuration**, with the fake as the local default.
- [ ] With **no credentials and no network access**, the whole application starts and every AI
      capability responds using the fake.
- [ ] The fake returns identical output for identical input across runs.
- [ ] **The abstraction cannot mutate a ticket, send a message, or take any action — verified by the
      shape of the interface, not by convention.**
- [ ] A provider failure or timeout surfaces as an unavailable AI feature; ticket creation, replies
      and status changes all continue.
- [ ] Provider calls and failures are logged **without logging customer content beyond what is
      necessary**.
- [ ] The abstraction is **documented as the extension point for the future chatbot (T3-C)**, along
      with what a real implementation must add.
- [ ] **No endpoint, no screen and no entity** was created (DM-5).
- [ ] **Product-scope §9 question 1 (provider, data residency) is not answered.**
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 11.**
