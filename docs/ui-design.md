# UI Design — Customer Support CRM

> **Source of truth:** [requirements.md](requirements.md) · [product-scope.md](product-scope.md) T1–T4, A-1…A-18 · [architecture.md](architecture.md) §2.2, §2.3, §4.2 · [data-model.md](data-model.md) · [api-design.md](api-design.md) (65 endpoints, AP-1…AP-18) · [story-backlog.md](story-backlog.md) and the 18 intakes
> **SDD stage:** 8 of 10. Gate 8 → 9 per [sdd-workflow.md](sdd-workflow.md) §4.
> **Status:** Design only. No Angular components, no routes file, no CSS, no front-end source.

**What this document decides:** the shells, the route tree, every screen and its responsibility,
the shared components, the state conventions (empty, loading, error), responsive behaviour, RTL and
i18n handling, role-based visibility, and **which API endpoints each screen consumes**.

**What it does not decide:** visual design, spacing, component libraries beyond PrimeNG's presence,
or anything a plan would specify (stage 9).

**No business rule is invented.** Every behaviour traces to a source. Where a UI choice was
required, it is recorded as **UI-n** in §3. Where a screen depends on an **open question**, the
dependency is marked in §11 rather than resolved.

---

## 1. Surfaces

Three role-based areas inside **one** Angular application (AD-14), each a lazily loaded route tree
behind a guard, plus an unauthenticated area.

| Area | Roles | Shell | Path space | API path space |
|---|---|---|---|---|
| **Auth** | Anonymous | Minimal, centred | `/auth/**` | `/api/v1/auth/*` |
| **Workspace** | Agent, Manager | Staff shell | `/workspace/**` | `/api/v1/*` |
| **Admin** | Administrator | Staff shell (extra nav) | `/admin/**` | `/api/v1/*` |
| **Portal** | Customer | Portal shell | `/portal/**` | `/api/v1/portal/*` |

**Guards hide; they do not protect.** Every route guard mirrors a server rule that is independently
enforced ([architecture.md](architecture.md) §2.2). A guard failure is a redirect; the server
failure behind it is `403` or `404` (AP-4). No screen relies on a guard for correctness.

**Workspace and Admin share one shell** because Manager and Administrator move between them
constantly; Admin is a navigation section, not a separate application. **Portal is a different
shell** — different navigation, different density, and no staff vocabulary.

---

## 2. Route tree

```
/                                  → redirect by role: Customer → /portal, staff → /workspace
/auth
  /login                           Sign in
  /register                        Customer self-registration
/workspace                         [guard: Agent+]
  /queue                           My queue — the agent landing screen
  /tickets                         All tickets in scope
  /tickets/:id                     Ticket detail
  /customers                       Customer directory
  /customers/:id                   Customer detail
  /knowledge                       Knowledge base (staff view)
  /knowledge/:id                   Article reader
  /reports                         Management dashboard        [guard: Manager+]
  /notifications                   Notification list
/admin                             [guard: Administrator]
  /users                           User directory
  /users/:id                       User detail / edit
  /knowledge                       Article authoring list
  /knowledge/new, /knowledge/:id   Article editor
  /audit                           Audit log
  /configuration                   Effective configuration (read-only)
/portal                            [guard: Customer]
  /requests                        My requests
  /requests/new                    Submit a request
  /requests/:id                    Request detail
  /help                            Knowledge base (public)
  /help/:id                        Article reader
/403, /404, /error                 Shared status screens
```

**Deep links survive a reload.** Every `:id` route loads its own data; nothing depends on state
carried from the previous screen.

---

## 3. UI decisions

| # | Decision | Rationale | Rejected |
|---|---|---|---|
| **UI-1** | Three shells: auth, staff, portal | Portal and workspace differ in navigation, density and vocabulary; sharing one shell would leak staff concepts into the customer surface | One shell with conditional navigation |
| **UI-2** | **My queue** is the agent landing screen, not an "all tickets" list | T1-C is the agent's own queue ordered by SLA urgency; landing on everything would bury it | Landing on `/workspace/tickets` |
| **UI-3** | Available transitions are **computed client-side** from status + role using the A-5/A-16 matrix, and the server is still the authority | The ticket payload does not expose `allowedTransitions` ([api-design.md](api-design.md) §6). See **F-1** in §12 — this duplicates the matrix in the client and is worth an API addition |
| **UI-4** | Ticket detail is **one screen with regions**, not tabs, on desktop; regions collapse into an accordion at phone width | T1-C requires the customer panel reachable "without losing place"; tabs would lose the thread on every switch | Tabbed detail on all widths |
| **UI-5** | Internal notes live in a **visually distinct region with a persistent "not visible to the customer" marker** | T2-C's visibility rule is the highest-risk detail in the product; the UI states it continuously rather than relying on memory | A single merged thread with per-item badges |
| **UI-6** | Every AI output renders in an **AI panel with an explicit "AI-generated" label and an explicit accept action** | A-8 requires labelling and human approval; a suggestion that flows straight into a field would blur authorship | Inline ghost text in the composer |
| **UI-7** | The reply composer is **one component** shared by quick replies and the AI suggested reply | Both produce editable draft text (T1-C, A-8); one insertion point keeps "never auto-sent" true by construction | Separate send paths |
| **UI-8** | **Optimistic UI is not used** for lifecycle changes | Transitions can be refused by the server (`403`/`409`); showing success before confirmation would misrepresent A-16 | Optimistic status updates |
| **UI-9** | Filters live in the URL as query parameters | A filtered queue must be shareable and survive a reload; it also mirrors the API's own filter names ([api-design.md](api-design.md) §2.1) | Component-local filter state |
| **UI-10** | Lists use **PrimeNG table on desktop, stacked cards below the table breakpoint** | T3-F requires phone-width usability; a horizontally scrolling table is not usable on a phone for the primary work surface | One responsive table everywhere |
| **UI-11** | The portal never shows assignee, department, priority or SLA data | [api-design.md](api-design.md) AP-16 and the portal payload omit them; the UI cannot show what the contract does not return | Showing "your agent" |
| **UI-12** | Destructive or authority-bearing actions confirm in a dialog naming the effect | Cancel and escalate change state others depend on; escalation raises priority and notifies a manager (A-16) | Immediate action on click |

---

## 4. Application shell and navigation

### 4.1 Staff shell (Workspace + Admin)

```
┌────────────────────────────────────────────────────────────────┐
│ [brand] Support CRM      [search]     🔔3   [EN|ع]   [avatar ▾] │
├──────────┬─────────────────────────────────────────────────────┤
│ My queue │                                                     │
│ Tickets  │                  routed content                     │
│ Customers│                                                     │
│ Knowledge│                                                     │
│ Reports* │                                                     │
│ ─────────│                                                     │
│ Admin**  │                                                     │
└──────────┴─────────────────────────────────────────────────────┘
   * Manager+   ** Administrator only
```

- **Brand block** — product name and logo from `GET /config/bootstrap` (T3-E). Never hardcoded.
- **Notification bell** — unread count from `GET /notifications`; opens the notification panel.
- **Language switcher** — English / العربية, switches without losing application state (T2-J).
- **Avatar menu** — display name, role, department from `GET /auth/me`; sign-out clears the token
  client-side (there is no logout endpoint — AP-8).
- **Sidebar** collapses to an off-canvas drawer below the tablet breakpoint.

**Role-based navigation:** *Reports* appears for Manager and Administrator; the *Admin* section for
Administrator only. Hiding is convenience — the routes are guarded and the endpoints return `403`.

### 4.2 Portal shell

```
┌────────────────────────────────────────────────────────────────┐
│ [brand]        My requests   Help      [EN|ع]   [avatar ▾]      │
├────────────────────────────────────────────────────────────────┤
│                        routed content                          │
└────────────────────────────────────────────────────────────────┘
```

Two destinations, no sidebar, no notification bell — A-13's notifications are staff-facing events
(assignment, breach, escalation, customer reply) and no requirement gives a customer an in-app
notification feed.

### 4.3 Auth shell

Centred card, brand block from `/config/bootstrap`, language switcher. Nothing else.

---

## 5. Workspace screens (Agent, Manager)

### 5.1 My queue — `/workspace/queue`

**Story 08** · the agent's landing screen (T1-C).

| | |
|---|---|
| **Purpose** | The logged-in agent's own tickets, ordered by SLA urgency |
| **API** | `GET /tickets?assigneeId=me&sort=resolutionDueAt:asc`, `GET /config/staff`, `GET /notifications` |
| **Roles** | Agent+ |

- **SLA urgency ordering with breached tickets first**, exactly as T1-C requires. The ordering is
  the API's default sort, not a client re-sort.
- Rows: subject, customer, status chip, priority chip, **SLA indicator**, age, category.
- **Open tasks and overdue tasks** for this agent appear as a secondary region (T2-C).
- Quick filters: status, priority, breached-only.

**States** — *loading*: skeleton rows. *Empty*: "No tickets assigned to you" with a link to all
tickets — an expected state, not an error. *Error*: inline retry, navigation still usable.

**Responsive**: table → cards at phone width, each card leading with subject, status and SLA.

### 5.2 Ticket list — `/workspace/tickets`

**Story 05.**

| | |
|---|---|
| **API** | `GET /tickets` with filters; `GET /departments`, `GET /config`, `GET /config/staff` for filter options |
| **Roles** | Agent+ |

Filters mirror the API exactly: `status`, `priority`, `categoryCode`, `assigneeId`, `departmentId`,
`breached`, `q`. Sort whitelist: `resolutionDueAt`, `firstResponseDueAt`, `createdAt`, `priority`.
Filters are URL query parameters (UI-9); paging uses the standard envelope.

**Scoping is visible, not hidden.** An Agent sees only their department and the department filter is
**fixed to their own department and disabled**, with a hint explaining why. A Manager sees the
filter enabled across all departments. This makes [architecture.md](architecture.md) §4.3 legible
instead of mysterious — the server enforces it either way.

**Branch is not a filter here.** Branch is a reporting attribute (A-2, T2-K); it appears on the
reports screen and the customer directory, never as a ticket scope.

### 5.3 Ticket detail — `/workspace/tickets/:id`

**Stories 05, 06, 07, 11, 12, 14** · the densest screen in the product.

```
┌───────────────────────────────────────────┬────────────────────┐
│ #ID  Subject                    [status]  │  CUSTOMER          │
│ priority · category · department          │  name, email,      │
│ SLA: first response · resolution          │  phone, branch     │
│ [Transition ▾] [Escalate] [Assign ▾]      │  recent tickets    │
├───────────────────────────────────────────┤  [open profile]    │
│ THREAD (messages)                         ├────────────────────┤
│   inbound / outbound, author, time        │  AI ASSISTS        │
│                                           │  [Summarize]       │
│ [ reply composer ]                        │  [Suggest reply]   │
│   [Quick replies ▾] [Suggest reply ✨]    │  ⚠ AI-generated    │
├───────────────────────────────────────────┼────────────────────┤
│ INTERNAL NOTES — not visible to customer  │  SUGGESTED ARTICLES│
├───────────────────────────────────────────┤  TASKS             │
│ ACTIVITY (history)                        │  ATTACHMENTS       │
└───────────────────────────────────────────┴────────────────────┘
```

**API consumed:** `GET /tickets/{id}` · `/messages` · `/internal-notes` · `/activity` · `/tasks` ·
`/attachments` · `/suggested-articles` · `POST /messages` · `/internal-notes` · `/tasks` ·
`/transition` · `/escalate` · `/assignment` · `PATCH /tickets/{id}` · `POST /ai/summary` ·
`/ai/suggested-reply` · `GET /customers/{id}`.

**Lifecycle controls.**

- The **Transition** menu offers only transitions legal from the current status **and** permitted
  for the caller's role (A-5 + A-16), computed per UI-3.
- **Escalate** is a separate control, never inside the transition menu — escalation is an action,
  not a status change (A-7, A-16). Its confirmation dialog states the effect: priority rises one
  level, status is unchanged. **See §11 for the OQ-3 dependency on what it may claim about
  notification.**
- **Assign** does **not** change status. When an unassigned `New` ticket is assigned, the status
  chip stays `New` — A-18 made this explicit and the UI must not imply otherwise. The header shows
  assignee and status as two independent facts.
- `Closed` and `Cancelled` disable the composer, the note field and the transition menu, with a
  reason line rather than silently inert controls.

**Customer panel** — reachable without leaving the screen (T1-C): a side region on desktop, a
drawer at phone width. Never a navigation away from an unsent draft.

**Internal notes** carry a persistent "not visible to the customer" marker (UI-5) and are a
different colour block from the thread. **They are fetched from a different endpoint**; no filtering
of a merged list is involved, so a rendering bug cannot leak one.

**AI assists** (A-8): each result appears labelled **AI-generated**, with *Insert into reply* for a
suggested reply and a dismiss for the summary. Nothing is sent automatically. On `503` the panel
shows "AI assistance is unavailable" and **every other control keeps working** — that is T1-F's
degradation rule made visible.

**States** — *loading*: header skeleton, regions load independently so a slow AI call never blocks
the thread. *Empty*: "No replies yet", "No internal notes", "No tasks". *Error*: per-region, so one
failed region does not blank the screen.

### 5.4 Customer directory — `/workspace/customers`

**Story 04** · `GET /customers` (filters `q`, `branchId`), `GET /branches`. Agent+.
Table: name, email, phone, branch, open-ticket count. **Branch is a filter here** — its legitimate
reporting/filtering use (T2-K).

### 5.5 Customer detail — `/workspace/customers/:id`

**Story 04** · `GET /customers/{id}` · `/timeline` · `/notes` · `/attachments`,
`PATCH /customers/{id}`, `POST /notes`, `POST /attachments`. Agent+.

Regions: **profile** (editable form), **interaction timeline** (§1.3 — a read projection over
tickets and ticket activity, **internal entries excluded**), **notes** (staff-only, immutable once
written — no edit control is offered), **attachments**.

The email field is editable — no source makes it immutable ([api-design.md](api-design.md) §5.5) —
with `409 customer-email-in-use` surfaced inline on the field. **See §11 for the OQ-5 dependency.**

### 5.6 Knowledge base (staff) — `/workspace/knowledge`, `/workspace/knowledge/:id`

**Story 12** · `GET /kb/articles` (`q`, `type`, `visibility`, `isPublished`), `GET /kb/articles/{id}`.
Agent+. Staff see **internal and public** articles; internal ones carry an "internal" badge.
Search is keyword over title and body (AD-13) — presented as search, never as an AI answer.

### 5.7 Reports — `/workspace/reports`

**Story 15** · `GET /reports/dashboard?departmentId&branchId`. **Manager+** — an Agent who reaches
the route is redirected, and the endpoint returns `403`.

Four regions matching the API response: ticket counts, SLA attainment per priority, agent
performance, satisfaction. Each renders **its own empty state**: "No ratings submitted yet" is not
"0.0". **See §11 for the PF-4 dependency on the agent-performance label.**

### 5.8 Notifications — `/workspace/notifications`

**Story 09** · `GET /notifications`, `POST /notifications/{id}/read`. Agent+.
Four types (A-13). Each row links to its ticket. There is no mark-all-read control — the endpoint
was removed as unrequested surface (AP-18). Available both as a full screen and as a bell panel.

---

## 6. Admin screens (Administrator)

| Screen | Route | API | Notes |
|---|---|---|---|
| **User directory** | `/admin/users` | `GET /users` (`role`, `departmentId`, `isActive`, `q`) | Table with role, department, active state |
| **User detail** | `/admin/users/:id` | `GET`/`PATCH /users/{id}`, `POST /users/{id}/deactivate` | Deactivate confirms; the form enforces "staff role requires a department" (DM-1) client-side and the server re-validates |
| **Create user** | `/admin/users` dialog | `POST /users` | The `Customer` role is **absent from the role selector** — customers arrive by registration or agent creation (DM-1) |
| **Article authoring** | `/admin/knowledge` | `GET /kb/articles` | Author's list with publish state |
| **Article editor** | `/admin/knowledge/new`, `/admin/knowledge/:id` | `POST`/`PATCH /kb/articles`, `/publish`, `/unpublish` | Plain-text/markdown editor. No rich editor, no media library (T2-E). No versioning, no delete |
| **Audit log** | `/admin/audit` | `GET /audit` (`actorUserId`, `action`, `from`, `to`) | **Read-only.** No row action of any kind — the log is append-only (T2-H) |
| **Configuration** | `/admin/configuration` | `GET /config`, `GET /config/staff` | **Read-only view with no save control anywhere.** A banner states that changing configuration is a redeploy (T2-I) |

The audit screen and the ticket activity region are **different screens on purpose** (AD-10):
different actors, different questions.

---

## 7. Portal screens (Customer)

Explicitly separate from every staff screen above: different shell, different routes, different
API path space, and **no staff vocabulary** — no department, no priority, no assignee, no SLA
(UI-11).

### 7.1 My requests — `/portal/requests`

**Story 13** · `GET /portal/tickets` (filter `status`). Cards, not a dense table: subject, status,
last update, a "response needed from you" cue when the status is `Pending`.
*Empty*: "You haven't submitted any requests yet" with the submit action.

### 7.2 Submit a request — `/portal/requests/new`

**Story 07, 13** · `POST /portal/tickets`, `GET /config` for the category list.

The form has exactly four inputs, and that is a direct consequence of the contract:

| Field | Why |
|---|---|
| Subject | Required |
| Description | Required |
| **Category** | Required. **The customer chooses a category, never a department** (A-14) — the department is derived server-side and never appears in this form |
| **"This is urgent"** checkbox | The `isUrgent` boolean of A-17. Labelled as an indication, **not** as a priority, because customers do not set priority (A-6) |

Attachment upload is available after submission on the request detail screen.

### 7.3 Request detail — `/portal/requests/:id`

**Stories 07, 13** · `GET /portal/tickets/{id}` · `/messages` · `/attachments`,
`POST /portal/tickets/{id}/messages` · `/transition` · `/feedback` · `/attachments`.

- **Thread** — the customer's own messages and agent replies. **Internal notes are unreachable**:
  a different endpoint, never requested here (UI-5, AP-5).
- **Reply composer** — and the one status side effect in the product: **replying to a `Pending`
  request returns it to `Open` automatically** (R-13). The response carries the ticket's new status,
  so the UI reflects it immediately rather than guessing. The status chip updates in place with a
  short "reopened" cue. **The UI must not offer a manual reopen for a `Pending` request** — no such
  transition is available to a customer (A-16).
- **Cancel** — offered **only while the status is `New`** (A-16), and A-18 means a request that has
  already been auto-assigned is still `New`, so the control is genuinely available. Once `Open`,
  the control disappears with a line explaining that work has started.
- **Reopen** — offered only on a `Resolved` request (A-16).
- **Feedback** — appears when the request reaches `Resolved`. Declining is normal: it is simply not
  submitting, and the UI never nags or blocks. **See §11 for the OQ-1 dependency on the control's
  shape.**
- `Closed` and `Cancelled` requests are read-only, with a line saying so.

### 7.4 Help — `/portal/help`, `/portal/help/:id`

**Story 12** · `GET /portal/kb/articles`, `GET /portal/kb/articles/{id}`. **Public, published
articles only** — internal articles return `404` (never `403`, which would confirm they exist).
Prominent search; article reader renders markdown.

---

## 8. Shared components

| Component | Used by | Notes |
|---|---|---|
| `StatusChip` | Queue, lists, details, portal | One colour per A-5 status; **the portal uses the same status vocabulary** — no separate customer wording was authorized |
| `PriorityChip` | Staff only | Four levels (A-6). Never rendered in the portal |
| `SlaIndicator` | Queue, ticket detail | Remaining or overdue; breached is visually distinct. Staff only |
| `TicketFilterBar` | Queue, ticket list | Bound to URL query parameters (UI-9) |
| `PagedTable` | Every staff list | Wraps the paged envelope; cards below the breakpoint (UI-10) |
| `MessageThread` | Ticket detail, portal detail | Two configurations of one component; the staff one shows channel, the portal one does not |
| `ReplyComposer` | Ticket detail, portal detail | Staff configuration adds quick replies and the AI insert (UI-7) |
| `AiAssistPanel` | Ticket detail | Always labelled, always dismissible, `503`-aware (UI-6) |
| `TransitionMenu` | Ticket detail, portal detail | Computes offers from status + role (UI-3) |
| `CustomerPanel` | Ticket detail | Side region / drawer |
| `AttachmentList` + uploader | Ticket, customer, portal | Size cap from configuration; `413` surfaced inline |
| `EmptyState`, `LoadingState`, `ErrorState` | Everywhere | §9 |
| `ConfirmDialog` | Cancel, escalate, deactivate | Names the effect (UI-12) |
| `LanguageSwitcher` | All three shells | §10 |

---

## 9. Empty, loading and error states

**Every list and region defines all three.** The rules:

- **Loading** — skeletons that match the final layout, not spinners over blank space. Regions load
  independently so one slow call never blanks a screen.
- **Empty** — a sentence saying what would fill it and, where one exists, the action that would.
  **An empty state is never an error**: no tickets assigned, no ratings yet, no search results and
  no notifications are all normal.
- **Error** — the message comes from the Problem Details `type` (AP-2) mapped to a **translated**
  string; the server's `detail` is never rendered raw, because the API returns codes and the front
  end owns display text (T2-J, [architecture.md](architecture.md) §2.3).

| Status | Presentation |
|---|---|
| `400` | Inline on the offending field |
| `401` | Session ended → redirect to `/auth/login`, preserving the return URL |
| `403` | "You don't have access to this" — a role denial the user can understand |
| `404` | "Not found" — **identical wording whether the record is missing or out of scope**, because AP-4 exists to prevent the UI distinguishing them |
| `409` | Contextual: "This ticket has already been closed", "That email is already in use" |
| `413` | Inline on the uploader with the configured cap |
| `422` | Inline: "That agent is not in this ticket's department" |
| `503` | Only the AI panel; the rest of the screen stays live |

---

## 10. Internationalization, direction and responsiveness

### 10.1 Language (T2-J, A-11)

- **Runtime translation** (AD-9): switching between English and العربية happens **without a reload
  and without losing application state** — an in-progress reply draft survives the switch.
- **UI strings are translated. User-generated content is not** (A-11): ticket text, notes and
  articles render exactly as authored, in whatever language they were written.
- **Dates render in the Gregorian calendar in both languages** (A-11).
- Error text comes from Problem Details `type` codes, never from server prose (§9).
- The switcher lives in all three shells; the choice persists per user in browser storage.

### 10.2 Direction — RTL (T2-J)

Arabic sets `dir="rtl"` on the document root. **Layout mirrors, it does not merely re-align:**

- CSS uses **logical properties** (`margin-inline-start`, `inset-inline`) throughout — no physical
  left/right in feature code.
- **Directional icons mirror** (back arrows, thread indentation, the sidebar drawer's slide
  direction); **non-directional icons do not** (clock, paperclip, bell).
- **Numerals, timestamps and SLA countdowns stay LTR-embedded** inside RTL text so they read
  correctly.
- **PrimeNG components are verified in Arabic rather than assumed correct**
  ([architecture.md](architecture.md) §2.3) — the table, paginator, dropdown, calendar and drawer
  are the ones to check first.
- Mirroring is handled once in `shared/` and `layout/`, never per feature.

### 10.3 Responsive (T3-F, A-1)

**Responsive web. No native app, no PWA, no offline mode.** Three breakpoints: phone, tablet,
desktop. The three surfaces T3-F names are designed at phone width first:

| Surface | Phone-width behaviour |
|---|---|
| **Agent queue** | Table → stacked cards, each leading with subject, status and SLA. Filters collapse into a filter sheet |
| **Ticket detail** | Regions become a single-column accordion (UI-4); the customer panel becomes a drawer; the composer docks to the bottom |
| **Customer portal** | Single column throughout; the submit form is full width; cards, never tables |

The staff sidebar becomes an off-canvas drawer below tablet. **Wide content — report tables,
activity — scrolls inside its own container, so the page body never scrolls sideways**
([architecture.md](architecture.md) §2.3).

---

## 11. Screens that depend on an open question

**None of these is resolved here.** Each is marked so a plan cannot quietly invent the answer.

| Question | Screen | Dependency | Interim UI behaviour |
|---|---|---|---|
| **OQ-1** CSAT scale | Portal request detail — feedback control (§7.3) | **The control's shape is undecided.** An ordinal range renders as a rating scale; a binary scale renders as two buttons. The design **does not pick one** | Render from `feedback.ratingScale` in `GET /config`. **The plan must not hardcode a star widget** until OQ-1 is answered |
| **OQ-2** SLA due dates on priority change | Ticket detail SLA region, queue ordering | **No UI dependency.** Both readings write the same fields; the UI displays whatever the server computes | None |
| **OQ-3** breach with no department manager | Ticket detail — escalate confirmation (§5.3) | The dialog **must not claim "the department manager will be notified"**, because a department may have none and no fallback exists | Word the effect as priority rise + status unchanged. Add the notification claim only once OQ-3 is answered |
| **OQ-5** customer email change vs linked login | Customer detail — email field (§5.5) | Whether editing the email also changes a linked portal sign-in is unanswered. The UI **must not promise either** | Save the field; show no claim about the login. Add a warning only once OQ-5 is answered |
| **PF-4** "tickets assigned" semantics | Reports — agent performance (§5.7) | Currently-assigned and ever-assigned are different numbers under one label | Label it exactly as T2-G words it — "tickets assigned" — and add no clarifying tooltip that would assert a meaning |
| **PF-5** `firstRespondedAt` null | Ticket detail SLA region | A ticket resolved without a reply has no first-response time | Render "—", not "breached" and not "0" |
| **N-1** undefined response shapes | Every screen consuming User, Customer, Task, Article, Notification, Audit or report payloads | Field lists here are taken from [data-model.md](data-model.md) where [api-design.md](api-design.md) §6 is silent | Confirm against the response shapes when N-1 is completed **before stage 9** |

---

## 12. Consistency audit

Performed against [product-scope.md](product-scope.md), [architecture.md](architecture.md),
[data-model.md](data-model.md), [api-design.md](api-design.md), [story-backlog.md](story-backlog.md)
and the 18 intakes.

**One finding.**

> **F-1 · NON-BLOCKING · The ticket payload does not expose `allowedTransitions`.**
> [api-design.md](api-design.md) returns `allowedTransitions` only inside the `409` problem detail
> when a transition is *refused*. To render a correct transition menu, the client must therefore
> reimplement the A-5 legality set and the A-16 authority matrix (UI-3) — a second copy of a rule
> that already exists in two approved documents, and one that can drift.
> **Not blocking:** the server remains the authority, and a client that offers a forbidden
> transition gets `403`/`409` rather than performing it.
> **Recommendation:** add a read-only `allowedTransitions` array to the ticket response so the menu
> is server-driven. That is an API change and belongs to [api-design.md](api-design.md); it is
> **reported, not applied**.

**Verified consistent — no contradiction found:**

| Check | Result |
|---|---|
| Every screen's endpoints exist in [api-design.md](api-design.md) | ✅ No invented endpoint |
| Role gates match A-4 and the API's Roles column | ✅ Reports Manager+, Admin area Administrator, portal Customer |
| Department scoping | ✅ §5.2 — agent's filter fixed and disabled; server enforces regardless |
| Branch used only for reporting and the customer directory | ✅ Never a ticket scope; no ticket branch field exists |
| A-5 lifecycle | ✅ Only legal transitions offered; terminal statuses disable controls |
| A-16 authority | ✅ Customer gets cancel-while-`New` and reopen-from-`Resolved` only; escalate is staff-only |
| A-18 assignment ≠ work start | ✅ §5.3 — assigning does not change the status chip; §7.3 cancel stays available on an assigned `New` request |
| R-13 automatic `Pending → Open` | ✅ §7.3 — status updates from the reply response; no manual reopen offered for `Pending` |
| R-14 customer attribution | ✅ The activity region shows the customer as the actor of that status change; no "system" actor is rendered |
| A-8 AI advisory | ✅ Labelled, human-inserted, never auto-sent; `503` degrades one panel |
| T2-C internal-note visibility | ✅ Separate endpoint, separate region, persistent marker; portal never requests it |
| T2-I no configuration UI | ✅ `/admin/configuration` is read-only with no save control |
| T2-H audit append-only | ✅ No row action on the audit screen |
| T2-E knowledge base | ✅ One article concept with a type; plain editor; no versioning |
| T2-J and T3-F | ✅ §10 |
| Open questions not silently resolved | ✅ §11 marks all seven dependencies |

---

## 13. Story coverage

| Story | Screens |
|---|---|
| 01 `solution-skeleton` | App shells, brand block, `/403`, `/404`, `/error` |
| 02 `auth-and-roles` | Sign in, register, avatar menu, guards |
| 03 `departments-branches` | Department filter (§5.2), branch filter (§5.4), report filters |
| 04 `customer-records` | Customer directory, customer detail |
| 05 `ticket-core` | Ticket list, ticket detail header, assign |
| 06 `ticket-lifecycle` | Transition menu, escalate, activity region |
| 07 `ticket-intake-messaging` | Thread, composer, portal submit, portal reply |
| 08 `agent-dashboard` | **My queue**, customer panel, quick replies |
| 09 `sla-routing-escalation` | SLA indicator, queue ordering, notifications |
| 10 `ai-service-seam` | — no screen; the seam is server-side |
| 11 `ai-ticket-assists` | AI assist panel |
| 12 `kb-articles-search` | Staff knowledge, article reader, suggested articles, portal help |
| 13 `portal-self-service` | All four portal screens, feedback control |
| 14 `tasks-internal-notes` | Tasks region, internal notes region, queue task list |
| 15 `management-dashboard` | Reports |
| 16 `audit-configuration` | Audit log, configuration view |
| 17 `i18n-responsive-branding` | §10 — cross-cutting across every screen |
| 18 `channel-erp-adapters` | — no screen; internal seams |

**Screen totals:** 2 auth · 8 workspace · 7 admin · 4 portal · 3 status = **24 screens**.

---

## 14. Stage 8 gate check

Gate 8 → 9 from [sdd-workflow.md](sdd-workflow.md) §4: *"`docs/ui-design.md` lists every screen for
the agent workspace, the customer portal and the admin surfaces, notes RTL implications (T2-J), and
confirms phone-width behaviour (T3-F)."*

| Condition | Status |
|---|---|
| Every screen for the **agent workspace** | ✅ 8 screens (§5), each with route, purpose, API, roles and states |
| Every screen for the **customer portal** | ✅ 4 screens (§7), explicitly separated by shell, route and API path space |
| Every screen for the **admin surfaces** | ✅ 7 screens (§6) |
| **RTL implications noted (T2-J)** | ✅ §10.2 — logical properties, icon mirroring, LTR-embedded numerals, PrimeNG verification |
| **Phone-width behaviour confirmed (T3-F)** | ✅ §10.3 — the three T3-F surfaces designed at phone width; no horizontal body scroll |
| *(beyond the gate)* role-based visibility | ✅ Every screen carries its roles; guards documented as convenience, not protection |
| *(beyond the gate)* endpoint mapping | ✅ Every screen maps to endpoints that exist in [api-design.md](api-design.md) |
| *(beyond the gate)* no invented rule or endpoint | ✅ §12 |
| *(beyond the gate)* open questions preserved | ✅ §11 |

**Gate 8 → 9 is met**, with one non-blocking finding (F-1) reported and not applied.

**Next stage:** 9 — Implementation Plans, generated per story via `/squad-plan`. Not started.
**Before stage 9:** N-1 (response shapes) should be completed, per the Stage 7 post-flight decision.
