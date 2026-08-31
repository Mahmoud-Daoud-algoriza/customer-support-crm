# The AI seam — `IAiAssistService`

One interface, two implementations, chosen by configuration. This file records what the interface is
for, what a real implementation would still have to add, and what is deliberately absent — so none of
it has to be rediscovered.

**Canonical sources:** [architecture.md §5.1](../../../../../docs/architecture.md),
[product-scope.md](../../../../../docs/product-scope.md) T1-F, A-7, A-8, T3-C,
[api-design.md](../../../../../docs/api-design.md) §8.1, AP-12,
[data-model.md](../../../../../docs/data-model.md) DM-5.

---

## What sits behind it

| Capability | Requirement | Method |
|---|---|---|
| Ticket summary | §7.1 | `SummarizeThreadAsync` |
| Suggested reply | §7.2 | `SuggestReplyAsync` |
| Automatic categorization | §7.3 | `SuggestClassificationAsync` |

**Two implementations**, selected by `SupportCrm:Ai:Provider`:

- `DeterministicFakeAiService` — **the default**. No network, no credential, same output for the same
  input on every run.
- `ProviderAiService` — a real provider over HTTP. Requires `Endpoint` and `ApiKey`, and fails to
  construct without them.

---

## This interface is the extension point for the future chatbot (T3-C)

**A chatbot would be a *consumer* of `IAiAssistService`, not a new seam.** It would assemble an
`AiThreadContext` from a conversation instead of from a ticket and call the same methods. Nothing
about adding one requires a second abstraction, a second configuration section, or a change to this
interface.

T3-C is **not built**. What exists is the seam it would plug into.

---

## What a real implementation must add

The adapter in this repository is a working shape, not a production integration. A real one needs:

- **A provider account and credential handling** — key rotation, and a secret store rather than an
  environment variable on a single host.
- **Rate-limit and retry policy** — backoff, and a circuit breaker so a degraded provider does not
  consume every request thread. Today a timeout simply reports unavailable.
- **Token accounting** — per-request cost, a budget, and a ceiling that degrades to the fake rather
  than failing.
- **Prompt versioning** — prompts are inline strings here. A real system versions them, because
  changing one silently changes every suggestion.
- **A decision on data residency and confidentiality for customer content.** Sending a ticket body to
  a third party is a data-processing decision, not a technical one.

> **Product-scope §9 question 1 — which provider, and whether data-residency limits apply — is
> deliberately unanswered, and this story does not answer it.** The seam is what lets it stay open:
> no provider name, model or provider parameter appears in the API contract (api-design §8.1, AP-12),
> so answering it later changes one file in Infrastructure and nothing else.

---

## What is deliberately absent

Not oversights — exclusions (product-scope §8, and this story's intake):

- No vector database, no embeddings, no retrieval-augmented pipeline
- No fine-tuning
- No prompt-management tooling
- No evaluation harness

---

## Two things that must not be wired in here

**1. Suggested solutions (§7.4) do not use this seam.** They are **keyword retrieval in the Knowledge
module** (AD-13, Story 12) — the top text matches for a ticket, retrieval-based and not generative.
Routing them through `IAiAssistService` would turn a database `LIKE` into a provider call and a
running cost, for a worse answer.

**2. Nothing here may act.** The interface has **no method that can send, assign, transition or
apply** — and no ticket id parameter to act on (A-8, AD-12). `AiSeamShapeTests` asserts this by
reflection, so adding an action method fails the build rather than passing review. If a future
capability seems to need one, that is a product decision about autonomy, not a refactor.

---

## No entity, no endpoint, no screen

**DM-5.** Summaries and suggested replies are **not stored** — they are generated on demand, and a
table of them would drift from the thread it described. The only thing persisted is the
categorization *decision*, as `AiSuggestionOffered` / `AiSuggestionResolved` **ticket history**.

Story 10 adds no endpoint and no screen. The three endpoints of api-design §5.8 and the AI panel are
[Story 11](../../../../../.squad/plans/ai-assist/11-story-ai-ticket-assists.md).
