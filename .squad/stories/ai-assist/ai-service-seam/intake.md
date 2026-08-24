# Story intake — AI service abstraction with deterministic offline fake

> **Source of truth:** `docs/requirements.md` §7 · `docs/product-scope.md` T1-F, A-7, A-8, T3-C
> **Scope tier:** T1 (must genuinely work — enabling half of the AI slice)

## Feature

- **Feature name (display):** AI Assist
- **Feature slug (folder under `plans/`):** `ai-assist`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** — (tracker disabled; the story id is the folder name `ai-service-seam`)
- **Work item type:** Story

---

## Title

```
AI service abstraction with deterministic offline fake
```

---

## Description

```
Build the single seam that all AI features in requirements §7 sit behind, before building any
of the features themselves.

The abstraction (product-scope T1-F):
- ONE AI service abstraction on the server. Every AI capability — summaries, suggested replies,
  categorization, and the future chatbot (T3-C) — is a consumer of it.
- Two implementations: a real provider implementation, and a DETERMINISTIC OFFLINE FAKE.
- Selection is configuration-driven (T2-I: file/environment configuration, no UI).

The offline fake (A-7) is a hard requirement, not a convenience:
- Product-scope §10 (Definition of done #5) requires the system to run with NO external
  accounts or credentials. The fake is what makes that true.
- Deterministic: the same ticket produces the same output every run, so demos and tests are
  repeatable.

Guardrails (A-8) — enforced at this layer so no consumer can bypass them:
- All AI output is ADVISORY and HUMAN-APPROVED. The abstraction returns suggestions; it never
  performs an action, sends a message, or mutates a ticket by itself.
- AI output is labelled as AI-generated wherever it reaches a user.
- Acceptance or override of a suggestion is recorded in ticket history.
- Failure of the provider degrades gracefully: the feature is unavailable and the ticket
  workflow continues. An AI outage must never block support work.
```

---

## Acceptance criteria

```
- [ ] A single server-side AI abstraction exists, with a real-provider implementation and a
      deterministic offline fake behind it.
- [ ] Which implementation runs is chosen by configuration, with the fake as the default for
      local runs.
- [ ] With no credentials and no network access, the whole application starts and every AI
      feature responds using the fake.
- [ ] The fake returns identical output for identical input across runs.
- [ ] The abstraction cannot mutate a ticket, send a message, or take any action — it only
      returns suggestions. Verified by the shape of the interface, not by convention.
- [ ] A provider failure or timeout surfaces as an unavailable AI feature; ticket creation,
      replies and status changes all continue to work.
- [ ] Provider calls and failures are logged without logging customer content beyond what is
      necessary.
- [ ] The abstraction is documented as the extension point for the future chatbot (T3-C).
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids:** `platform-foundation/solution-skeleton`.
- **Depends on code areas or other stories:** Consumed by `ai-assist/ai-ticket-assists` and,
  for retrieval, by `knowledge-base/kb-articles-search`. Should be planned before either.

## Extra notes (optional)

- Cut order: T1 — cannot be cut. If the AI provider is unavailable on demo day, the fake IS the
  demo, and that is by design.
- Open question carried from product-scope §9 (1): which provider, and whether data-residency or
  confidentiality limits apply to sending customer content to it. The offline fake sidesteps this
  for the assessment; do not resolve it here.

## Technical hints (optional)

- Repos/roots: `backend`. Provider credentials come from environment configuration and are never
  committed (`.squad/secrets.yaml` is already git-ignored; apply the same discipline to app secrets).
- **Do not invent** endpoints here; Stage 6 (API Design) fixes how assists are exposed.

## Out of scope

- The individual AI features themselves — sibling story `ai-assist/ai-ticket-assists`.
- The customer-facing AI chatbot, its knowledge grounding and human handoff (T3-C, §8).
- Autonomous AI action of any kind, including auto-sending replies (A-8).
- Vector databases, embeddings infrastructure, retrieval-augmented pipelines (§8 technical
  exclusions; KB suggestion is keyword retrieval — see `knowledge-base/kb-articles-search`).
- Model fine-tuning, prompt-management tooling, evaluation harnesses.
