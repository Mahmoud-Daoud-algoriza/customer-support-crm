# Story 11 — Ticket summaries, suggested replies and auto-categorization

> **Source of truth:** `docs/requirements.md` §7.1–7.3 · `docs/product-scope.md` T1-F, **A-6**, **A-8**, A-17 · `docs/architecture.md` §5.1, AD-12 · `docs/data-model.md` §2.7 (`AiSuggestionOffered`, `AiSuggestionResolved`), **DM-5** · `docs/api-design.md` §5.8, §6.10, AP-12, AP-14 · `docs/ui-design.md` §5.3, §8, **UI-6**, **UI-7**
> **Intake:** `.squad/stories/ai-assist/ai-ticket-assists/intake.md` · **Tier:** T1 — cannot be cut. *"If time is short, reduce to the smallest working version of all three rather than delivering one polished feature and dropping two."*
> **Phase:** 5 — Automation and AI.

## Prerequisites

- **Story 10 completed:** `IAiAssistService`, the deterministic fake, configuration selection,
  `AiUnavailableException`.
- **Story 05 completed:** `Ticket`, `TicketScope`, `LoadScopedAsync`, the configured category list.
- **Story 06 completed:** `TicketActivityRecorder` and the `AiSuggestion*` activity types.
- **Story 07 completed:** `TicketMessage` — summaries and suggested replies read the thread.
- **Story 08 completed:** the shared `reply-composer` and the ticket-detail region layout.

> ### ⚠ Contradiction found in audit — **S9-4** — no contract path records suggestion acceptance
>
> This story's acceptance criterion requires *"The suggested values, and whether the agent accepted
> or overrode them, are written to ticket history"*; `data-model.md` §2.7 provides the
> `AiSuggestionOffered` and `AiSuggestionResolved` activity types for exactly that; and
> `api-design.md` §5.8 states the recording happens *"when the agent saves the ticket, not by these
> endpoints (DM-5)"*.
>
> **But `POST /tickets` accepts no field that could carry it.** Its body is
> `{ customerId, subject, description, categoryCode, priority, departmentId? }` (api-design §5.6),
> §6.11 adds no other request body, and §7 lists no such server-derived field. **There is no
> endpoint, and no field on an existing endpoint, through which the server can learn that a
> suggestion was offered or whether it was taken.**
>
> **No field and no endpoint is invented here.** Tasks 1–4 deliver the three assists in full. Task 5
> — the history recording — is **blocked on a Stage 7 decision**: either add a request field (for
> example an `aiSuggestion` object on `POST /tickets`) or add a small recording endpoint. Both
> change an approved contract and are a decision to take explicitly. Recorded as **S9-4** in
> `00-implementation-plan.md`; the corresponding Done Criterion below is marked blocked.

---

## Story Goal

The three agent-facing AI capabilities of requirements §7.1–7.3, all consuming Story 10's
abstraction.

1. **Ticket summary** (§7.1) — a short summary of a ticket thread, on demand, in the ticket view.
   A read-only aid; **not stored as a ticket field pretending to be authored content** (DM-5).
2. **Suggested reply** (§7.2) — a draft into the composer, edited before sending. **Advisory only,
   never auto-sent.** It sits **alongside** the configured quick replies without replacing them.
3. **Automatic categorization** (§7.3) — at ticket creation, suggest a category and a priority from
   the **fixed enumerations**, overridable before saving.
4. Every AI output is **visibly labelled AI-generated** and requires **an explicit human action** to
   be used (A-8).

---

## Context — Read These Files First

1. `docs/api-design.md` §5.8 — the three endpoints, why all three are `POST` (*"because they perform
   work, not because they mutate: **none of them changes a ticket**"*),
   `/ai/classification-suggestion` being callable **before a ticket exists**, `503 ai-unavailable`
   (AP-12), and the DM-5 note. Then §6.10 for the three response shapes, each carrying
   `"generatedBy": "ai"`.
2. `docs/product-scope.md` **A-8 in full** — advisory, human-approved, labelled, acceptance or
   override recorded, **no autonomous action, no customer-facing generation**.
3. `docs/ui-design.md` §5.3 (the AI assists region and its `503` behaviour), **UI-6** (an AI panel
   with an explicit label and an explicit accept action — *"a suggestion that flows straight into a
   field would blur authorship"*, so **no inline ghost text**), **UI-7** (one composer, one
   insertion point).
4. `docs/api-design.md` **AP-14** — suggested articles are a **Knowledge** endpoint, not an AI one
   (Story 12). **Do not add anything to `/ai` for §7.4.**
5. `docs/architecture.md` §5.1 — failure is contained: *"A provider error or timeout surfaces as
   'AI unavailable' for that one feature; ticket creation, replies and status changes continue."*
6. `.squad/stories/ai-assist/ai-ticket-assists/intake.md` — nine acceptance criteria and the Out of
   scope list (no chatbot, no auto-send, no auto-close, no sentiment, no language detection, **no
   translation of user content** — A-11).

---

## Product rules (from story)

- **No AI path can send a message, change a status, or reassign a ticket.** Story 10 made that
  structural; this story must not route around it.
- **A suggested reply lands in the composer as editable text and is never sent automatically.**
- **A suggested category outside the configured enumeration is rejected rather than created** —
  server-side, before the response leaves the API (api-design §5.8).
- **A suggested priority is a pre-selection the agent can override**, and `isUrgent` may be one
  input to it but **never sets priority** (A-6, A-17).
- **Every AI-produced element is visibly labelled AI-generated** (A-8).
- **When the AI service is unavailable**, each feature degrades to an unavailable state and the
  agent can still write their own reply and pick a category manually. **Every other control keeps
  working.**
- **The value demonstrated here is the guardrail discipline** — advisory, labelled, audited,
  degradable — **not model sophistication** (intake).

---

## Backend Tasks

### 1 — Application: the assist service

**Create file: `src/SupportCrm.Application/Modules/Ai/TicketAiAssistService.cs`**

`SummarizeAsync(ticketId)`:

1. `LoadScopedAsync(ticketId)` — **the AI path composes the same scoping helper as every other
   ticket query** (architecture §4.3 point 2). An out-of-department ticket is `404` **before** any
   provider call, so no customer content leaves the process for a ticket the caller may not read.
2. Build `AiThreadContext` from the ticket and its `TicketMessage` rows.
3. Call `IAiAssistService.SummarizeThreadAsync`.
4. Return `{ summary, generatedBy: "ai", generatedAt }`. **Nothing is persisted** (DM-5).

`SuggestReplyAsync(ticketId)` — identical shape, returning `{ draft, generatedBy, generatedAt }`.
**It does not create a `TicketMessage`.**

`SuggestClassificationAsync(subject, description, isUrgent?)`:

- Takes **no ticket id** and is callable **before a ticket exists**, which is what "suggest at
  creation" requires.
- Validates the returned `categoryCode` **against the configured list** and the priority against
  the four values; an out-of-range suggestion is **replaced by the configured fallback or rejected
  before returning**, so a client never receives an unusable suggestion (api-design §5.8).
- Returns `{ categoryCode, priority, generatedBy: "ai", generatedAt }`.

**All three catch `AiUnavailableException` nowhere** — they let it propagate to the Story 01
Problem Details handler, which maps it to **`503 ai-unavailable`** (AP-12). One mapping, no
per-endpoint code.

### 2 — Api: the three endpoints

**Create file: `src/SupportCrm.Api/Controllers/AiController.cs`** — policy `RequireAgent`:

```
POST  /api/v1/tickets/{id}/ai/summary            -> 200 { summary, generatedBy, generatedAt }
POST  /api/v1/tickets/{id}/ai/suggested-reply    -> 200 { draft,   generatedBy, generatedAt }
POST  /api/v1/ai/classification-suggestion       { subject, description, isUrgent? }
                                                 -> 200 { categoryCode, priority, generatedBy, generatedAt }
```

**No AI endpoint mutates a ticket.** No `PATCH`, no side effect, no persistence call. If a future
change needs one, it is a contract decision, not a controller edit.

**Do not add `/ai/suggested-solutions`** — AP-14 puts §7.4 under Knowledge as
`GET /tickets/{id}/suggested-articles` (Story 12), because it **retrieves rather than generates**.

### 3 — Tests

**Create file: `tests/SupportCrm.Tests/Ai/AiAssistEndpointTests.cs`**

1. Summary and suggested reply return `200` with `generatedBy: "ai"` **using the fake, with no
   credentials configured**.
2. The **same ticket produces the same summary** across two calls (determinism through the stack).
3. `/ai/classification-suggestion` works with **no ticket** in the request and returns a
   `categoryCode` that is in the configured list.
4. A stubbed `IAiAssistService` returning an **out-of-list** category never reaches the client.
5. An `Agent` calling an assist on an **out-of-department** ticket -> **`404`** — asserted
   **before** any provider interaction (use a recording stub and assert it was never called).
6. A `Customer` calling any `/ai` endpoint -> `403`.
7. With a throwing `IAiAssistService`: each assist -> **`503 ai-unavailable`**, **and** in the same
   test run `POST /tickets`, `POST /tickets/{id}/messages` and `POST /tickets/{id}/transition` all
   still return success. *That pairing is the T1-F degradation requirement.*
8. **No AI endpoint changes any ticket field** — snapshot the ticket row before and after each
   assist and assert equality.

---

## Frontend Tasks

### 4 — `AiAssistPanel` — `shared/components/ai-assist-panel/` (UI-6)

Fills the AI region slot Story 05 left in the ticket detail.

- Two actions: **`[Summarize]`** and **`[Suggest reply]`**.
- **Every result renders inside the panel with an explicit "AI-generated" label**, always visible —
  not a tooltip, not a hover state (A-8, UI-6).
- The summary has a **dismiss**; the suggested reply has **`Insert into reply`**.
- **No inline ghost text and no auto-fill** (UI-6). A suggestion never flows straight into a field,
  because that would blur authorship.
- **`503` behaviour:** the panel shows *"AI assistance is unavailable"* and **every other control on
  the screen keeps working** — the thread, the composer, the transition menu, assignment. Verify
  this by hand; it is the visible half of T1-F.
- The panel is **staff-only** and is never imported by a `features/portal/` component — A-8 excludes
  customer-facing generation entirely.

### 5 — Insert into the one composer (UI-7)

`Insert into reply` writes into the **same draft, at the same insertion point**, as Story 08's quick
replies. **One composer, one send action.** That is what keeps *"never auto-sent"* true by
construction rather than by discipline — there is no second path a message can leave by.

After insertion the text is **ordinary editable draft text**; it carries no special styling that
would imply the agent may not change it.

### 6 — Categorization at ticket creation

`features/workspace/ticket-create/`:

- On blur of **subject** or **description**, call `POST /ai/classification-suggestion`.
- The suggested category and priority appear as **pre-selections** with the **AI-generated** label
  beside them, **freely overridable**.
- **`503` leaves both selectors empty and enabled** — the agent picks manually and the form works
  exactly as it did before Story 11 (intake AC).
- The response `priority` is a **suggestion only**; `isUrgent` on the portal form remains a separate
  customer input that **never sets priority** (A-17).

### 7 — Recording acceptance or override — **blocked, see S9-4**

Capture, in component state, whether the agent kept or changed each suggested value, and leave a
single marked call site:

```ts
// S9-4 — blocked on a Stage 7 decision: no request field or endpoint exists to carry this.
// Do NOT invent one. When the contract is extended, send the captured suggestion + outcome here.
```

**Do not** silently attach an undocumented field to `POST /tickets` — the server would reject it
(`400`, AP-10), and a client believing an unsupported field works is precisely what AP-10 exists to
prevent.

---

## Verification Steps

1. **Backend builds:** `dotnet build backend/SupportCrm.sln`.
2. **Backend tests pass:**
   `dotnet test backend/SupportCrm.sln --filter FullyQualifiedName~AiAssist` — all eight green,
   including the ticket-unchanged snapshot test.
3. **No credentials:** with no AI key in `.env`, all three assists work end to end through the fake.
4. **Degradation by hand:** set `SupportCrm:Ai:Provider=Provider` with an unreachable endpoint,
   restart, and confirm each assist returns `503 ai-unavailable` **while** creating a ticket,
   replying and transitioning all still succeed.
5. **Labelling:** every AI-produced element on the screen carries a visible **AI-generated** label.
6. **Never auto-sent:** insert a suggested reply and navigate away without pressing Send — the
   thread is unchanged and no `TicketMessage` row was created.
7. **Regression:** Stories 05–10 suites still pass; the quick-reply insert still works into the same
   composer.

---

## Done Criteria

- [ ] An agent can request a summary of a ticket thread and receive one within the ticket view.
- [ ] An agent can request a suggested reply; it lands in the composer as **editable text** and is
      **never sent automatically**.
- [ ] At ticket creation a category and priority are suggested from the fixed enumerations and are
      **freely overridable** before saving.
- [ ] Every AI-produced element is **visibly labelled AI-generated**.
- [ ] All three features work end to end with the deterministic offline fake, **with no credentials
      and no network access**.
- [ ] When the AI service is unavailable each feature degrades to an unavailable state, and the
      agent can still write their own reply and pick a category manually.
- [ ] A suggested category outside the configured enumeration is **rejected rather than created**.
- [ ] **No AI path can send a message, change a status, or reassign a ticket** — asserted by a
      before/after snapshot test.
- [ ] AI assists honour ticket scoping: an out-of-department ticket is `404` **before** any provider
      call.
- [ ] ⛔ **BLOCKED — S9-4:** *"The suggested values, and whether the agent accepted or overrode them,
      are written to ticket history."* No contract path exists to carry this. The client-side
      capture and the marked call site are in place; **the Stage 7 decision must be taken before
      this criterion can be met.** Do not close this box by inventing a field.
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user, **including the S9-4 blocker**, and wait for confirmation before
proceeding to Story 12.**
