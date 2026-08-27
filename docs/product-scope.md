# Product Scope — Customer Support CRM (Assessment)

**Source of truth:** [requirements.md](requirements.md)
**Status:** Scope definition. No code, no database schema, no implementation.
**Date:** 2026-08-24

---

## 1. Purpose of this document

This document fixes the scope of a **3-day evening assessment** whose goal is to demonstrate
**spec-driven development (SDD)** and **AI-assisted full-stack delivery** — not to deliver a
production enterprise CRM.

Everything here is traceable to a numbered section of `docs/requirements.md`. No new business
features have been introduced. Where a requirement is ambiguous, an explicit assumption is
recorded in §7 rather than resolved silently.

### 1.1 What is actually being assessed

| Assessed | Not assessed |
|---|---|
| Quality of the spec → plan → implementation chain | Feature count |
| Traceability from a requirement line to running code | Enterprise-grade hardening |
| Coherent, honest architecture with clean seams | Third-party integration depth |
| Working vertical slices, end to end | Scale, performance, HA |
| Explicit handling of ambiguity | Visual polish |

### 1.2 Effort budget

Three evenings, approximately **9–12 focused hours total**. Scope is deliberately sized so the
MVP tier can be *finished*, rather than having all twelve requirement domains started and none
completed. A working narrow slice beats a broad broken one.

---

## 2. Scope tiers

Every line item in `requirements.md` is assigned to exactly one tier.

| Tier | Name | Meaning |
|---|---|---|
| **T1** | **MVP** | Must have a genuinely working implementation, end to end (UI → API → persistence). Demoable. |
| **T2** | **Simplified MVP** | Present and working, but deliberately minimal: happy path only, reduced options, no configuration UI, plain UI. |
| **T3** | **Architecture / Future** | Acknowledged in the design with a real seam (interface, adapter, config point) and a stub or in-repo fake behind it. Not fully implemented. |
| **T4** | **Out of scope** | Not built, not stubbed, not designed. Listed in §8 so its absence is a decision, not an oversight. |

**T3 rule of engagement:** a T3 item must be *provably designed for* — a named abstraction plus a
fake the demo can run against — but must require no external account, provider contract, or
production credential to run.

---

## 3. MVP scope (T1) — must genuinely work

The vertical slices the assessment stands or falls on.

### T1-A · Customer Management — `req §1`
- Create, read, update, list customer profiles.
- Contact details on the profile (name, email, phone, branch reference).
- **Interaction history** — a per-customer chronological timeline assembled from that customer's
  tickets and ticket activity.
- **Notes** on a customer, authored by an agent, timestamped and attributed.
- *(Attachments: see T2-A.)*

### T1-B · Ticket Management — `req §2`
The core loop. Must work completely.
- Create a ticket (by an agent on behalf of a customer, and by a customer via the portal).
- Track and list tickets, filterable by status, priority, category, assignee, department.
- **Categories and priorities** — fixed enumerations from configuration (see A-6).
- **Assign / reassign** a ticket to an agent.
- **Status lifecycle** with enforced, explicit transitions (see A-5).
- **Escalation** as a first-class recorded action on a ticket.
- **Ticket history** — an append-only activity trail of every status, assignment, priority and
  category change, plus replies and internal notes, attributed and timestamped.

### T1-C · Agent Dashboard — `req §4`
- **Assigned tickets** — the logged-in agent's queue, ordered by SLA urgency.
- **Customer information in context** — the customer panel is reachable from inside a ticket
  without losing place.
- **Quick replies** — a small library of canned responses insertable into a reply.
- *(Tasks/reminders and team collaboration: see T2-C.)*

### T1-D · Users, Roles & Permissions — `req §10.1–10.3`
- User management: create/deactivate users, assign a role and a department.
- **Four fixed roles** (see A-4): Customer, Agent, Manager, Administrator.
- **Permission enforcement on the server**, not merely hidden UI: an agent cannot read or act on
  another department's tickets; a customer sees only their own.
- Authentication sufficient to distinguish these actors (see A-9).

### T1-E · Multi-department routing — `req §12`
- Departments as a first-class organizational dimension.
- A ticket belongs to exactly one department; an agent belongs to one department; routing and
  visibility are department-scoped. T1 because SLA, assignment, and permissions all depend on it.

### T1-F · Ticket-facing AI — `req §7.1–7.3`
The assessment is explicitly about AI-assisted work, so a working AI slice is mandatory.
- **Ticket summary** — generate a short summary of a ticket thread on demand.
- **Suggested reply** — generate a draft an agent edits before sending. Advisory only, never
  auto-sent (see A-8).
- **Automatic categorization** — suggest category and priority at creation, overridable by the
  agent, with the suggestion recorded in ticket history.
- All three sit behind **one AI service abstraction** with a deterministic offline fake, so the
  system runs and demos with no live provider.

---

## 4. Simplified MVP scope (T2) — minimal but real

Present, working, demoable — deliberately shallow. No admin UI, no configuration screens, no
edge-case handling beyond the happy path.

### T2-A · Attachments — `req §1.4`
Single-file upload to local disk storage, size-capped, attached to a ticket or a customer. No
virus scanning, no cloud object storage, no previews, no versioning.

### T2-B · Web form + portal messaging — `req §3.5`, `req §3.3 (partial)`
- **Web form** is the one real, fully working inbound channel.
- **In-portal messaging** on a ticket (customer ↔ agent replies) covers the conversational
  behaviour `req §3` implies, over ordinary request/response — **not** real-time chat.
- Both write into the same channel-agnostic message model that email/WhatsApp/SMS adapters would
  plug into (see T3-A).

### T2-C · Tasks, reminders & collaboration — `req §4.3, §4.5`
- **Tasks/reminders**: a due-dated to-do attached to a ticket, assigned to an agent, markable
  done. No calendar, no recurrence, no push reminders.
- **Team collaboration**: **internal notes** on a ticket, visible to agents and managers and never
  to the customer. That is the whole of collaboration here — no @mentions, no presence, no chat,
  no shared ownership.

### T2-D · SLA & automation — `req §5`
The simplest defensible model (see A-3):
- **Targets** — a first-response target and a resolution target per priority, from configuration,
  on a **24/7 clock**. No business hours, holiday calendars, timezone arithmetic, or
  pause-on-customer-reply.
- **Automatic assignment** — round-robin across active agents in the ticket's department. No
  skills, load balancing, or capacity rules.
- **Escalation rules** — a single rule: on target breach, flag the ticket breached, raise priority
  one level, notify the department manager. Rules live in code-level configuration, not a UI.
- **Alerts and notifications** — **in-app only**: a notification list and a badge. Email/SMS/push
  delivery is T3-A.
- Breach evaluation runs as a simple periodic in-process check — no job queue, no broker.

### T2-E · Knowledge Base — `req §6`
- CRUD for articles covering **FAQs, help articles, and solutions/guides** as one "article"
  concept distinguished by a type field — not three subsystems.
- Plain text / basic markdown body. No rich editor, no media library.
- **Search** — keyword search over title and body using the database's built-in text matching. No
  search engine, no vector index, no relevance tuning.
- Visibility flag: public (portal-visible) or internal.
- **AI suggested solutions** (`req §7.4`) is implemented as retrieval: surface the top keyword
  matches for a ticket to the agent. Retrieval-based, not generative.

### T2-F · Customer Portal — `req §8`
A separate, simpler surface for the Customer role over the same backend:
- Submit a ticket, track its status, view own ticket history, browse public KB articles.
- **Feedback** — a one-question satisfaction rating with optional comment, offered when a ticket
  is resolved. This is the sole CSAT input.

### T2-G · Reports & dashboards — `req §9`
One management dashboard with a small fixed metric set — no report builder, no scheduling, no
export:
- Ticket counts by status, priority, category, department (`§9.1`)
- SLA attainment: % met vs. breached, per priority (`§9.2`)
- Agent performance: tickets assigned / resolved / average resolution time (`§9.3`)
- Customer satisfaction: average rating and response count (`§9.4`)

### T2-H · Audit logs — `req §10.4`
Append-only audit entries written by a single service on security- and data-relevant actions
(login, user administration, permission-relevant changes, ticket lifecycle actions). Read-only
list view for Administrators. No tamper-proofing, no retention policy, no export.

### T2-I · System configuration — `req §10.5`
File- and environment-based configuration for categories, priorities, SLA targets, roles, and
branding values. **No configuration UI** — changing configuration is a redeploy.

### T2-J · Arabic & English — `req §12`
- i18n wired through the front end from the first screen, with a language switcher and **RTL
  layout support for Arabic**.
- **UI strings** are translated for the T1 and T2 screens. **User-generated content** (tickets,
  notes, KB articles) is stored as authored and is *not* translated.

### T2-K · Multi-branch — `req §12`
Branch exists as an organizational attribute on customers and users and is filterable in reports.
It is **not** a security boundary in this assessment — department is (see A-2).

### T2-L · Public API — `req §11.1`
The application's own HTTP API is the single documented interface, consumed by the web front end
and self-documented (OpenAPI). This *is* the API deliverable — no separate partner API, no API
keys, no rate limiting, no versioning strategy beyond a version prefix.

---

## 5. Architecture / Future scope (T3) — designed for, not delivered

Each item gets a real seam and a runnable fake. None requires an external account to demo.

### T3-A · External communication channels — `req §3.1, §3.2, §3.4`, `req §11.3`
**Email, WhatsApp, SMS.**
- *Design:* one outbound channel-adapter abstraction plus a normalized inbound message shape, so
  every channel produces and consumes the same message model as T2-B.
- *Delivered:* the adapter interface, a **console/log adapter** used in the demo, and a documented
  contract for what a real provider adapter must implement.
- *Not delivered:* provider accounts, WhatsApp Business API onboarding and template approval,
  sender-ID registration, inbound webhooks, delivery receipts, retries, opt-out handling.

### T3-B · Live chat (real-time) — `req §3.3`
The design acknowledges a real-time transport with agent presence and queueing as a future
addition. Delivered behaviour is the polled in-portal messaging of T2-B. No WebSocket
infrastructure.

### T3-C · AI chatbot — `req §7.5`
The AI service abstraction from T1-F is the extension point. A customer-facing conversational bot,
its knowledge grounding, and human handoff are **designed as a future consumer of that abstraction
and not built**. Handoff semantics remain an open question (see §9).

### T3-D · ERP and external systems — `req §11.2, §11.4`
- *Design:* an outbound integration boundary isolating any external-system call behind an
  interface, with customer records carrying an optional external-reference field.
- *Delivered:* the interface and a no-op fake.
- *Not delivered:* any real ERP connection, field mapping, sync strategy, or conflict resolution.
  `req §11.4 "External systems"` is unbounded and cannot be scoped further without a named system.

### T3-E · Custom branding — `req §12`
*Design:* branding values (product name, logo, primary colour) resolved from configuration rather
than hardcoded. *Delivered:* a single default brand loaded from config. *Not delivered:* per-tenant
or per-branch themes, upload UI, or a CSS theming engine.

### T3-F · Mobile — `req §12 "Web and mobile friendly"`
Interpreted as **responsive web** (see A-1). Layouts work at phone width for the agent queue,
ticket view, and customer portal. Native iOS/Android apps and PWA offline support are future
scope, not stubbed.

### T3-G · Multi-tenancy — `req §12` (implied)
The system is built **single-organization** (see A-2). The design notes where a tenant boundary
would be introduced and avoids decisions that would make it impossible, but no tenant isolation is
implemented.

---

## 6. Requirement traceability matrix

Every line of `docs/requirements.md`, assigned.

| § | Requirement line | Tier | Where |
|---|---|---|---|
| 1 | Customer profiles | T1 | T1-A |
| 1 | Contact details | T1 | T1-A |
| 1 | Interaction history | T1 | T1-A |
| 1 | Notes | T1 | T1-A |
| 1 | Attachments | T2 | T2-A |
| 2 | Create and track tickets | T1 | T1-B |
| 2 | Categories and priorities | T1 | T1-B |
| 2 | Assign tickets to agents | T1 | T1-B |
| 2 | Status and escalation | T1 | T1-B |
| 2 | Ticket history | T1 | T1-B |
| 3 | Email | T3 | T3-A |
| 3 | WhatsApp | T3 | T3-A |
| 3 | Live chat | T3 | T3-B (polled messaging in T2-B) |
| 3 | SMS | T3 | T3-A |
| 3 | Web forms | T2 | T2-B |
| 4 | Assigned tickets | T1 | T1-C |
| 4 | Customer information | T1 | T1-C |
| 4 | Tasks and reminders | T2 | T2-C |
| 4 | Quick replies | T1 | T1-C |
| 4 | Team collaboration | T2 | T2-C (internal notes only) |
| 5 | Response and resolution targets | T2 | T2-D |
| 5 | Automatic assignment | T2 | T2-D (round-robin) |
| 5 | Escalation rules | T2 | T2-D (one rule) |
| 5 | Alerts and notifications | T2 | T2-D (in-app only) |
| 6 | FAQs | T2 | T2-E |
| 6 | Help articles | T2 | T2-E |
| 6 | Solutions and guides | T2 | T2-E |
| 6 | Search | T2 | T2-E (keyword) |
| 7 | Ticket summaries | T1 | T1-F |
| 7 | Suggested replies | T1 | T1-F |
| 7 | Automatic categorization | T1 | T1-F |
| 7 | Suggested solutions | T2 | T2-E (retrieval-based) |
| 7 | AI chatbot | T3 | T3-C |
| 8 | Submit tickets | T2 | T2-F |
| 8 | Track requests | T2 | T2-F |
| 8 | View history | T2 | T2-F |
| 8 | Access FAQs | T2 | T2-F |
| 8 | Submit feedback | T2 | T2-F |
| 9 | Ticket reports | T2 | T2-G |
| 9 | SLA performance | T2 | T2-G |
| 9 | Agent performance | T2 | T2-G |
| 9 | Customer satisfaction | T2 | T2-G |
| 9 | Management dashboards | T2 | T2-G |
| 10 | Users and roles | T1 | T1-D |
| 10 | Permissions | T1 | T1-D |
| 10 | Audit logs | T2 | T2-H |
| 10 | System configuration | T2 | T2-I (no UI) |
| 11 | APIs | T2 | T2-L |
| 11 | ERP | T3 | T3-D |
| 11 | Email, SMS & WhatsApp | T3 | T3-A |
| 11 | External systems | T3 | T3-D |
| 12 | Arabic & English | T2 | T2-J |
| 12 | Web and mobile friendly | T3 | T3-F (responsive web) |
| 12 | Multi-department | T1 | T1-E |
| 12 | Multi-branch | T2 | T2-K (attribute only) |
| 12 | Custom branding | T3 | T3-E |

**Coverage:** all 56 requirement lines are addressed in some tier. None is silently dropped.

---

## 7. Explicit assumptions

These resolve ambiguities in `requirements.md` **for this assessment only**. Each is a decision
made to keep moving, not a claim about what the real product should do. If any is wrong, the scope
above changes.

**A-1 · Mobile.** "Web and mobile friendly" means **responsive web**, verified at phone width on
the agent queue, ticket detail, and customer portal. No native app, no PWA, no offline mode, no
push notifications.

**A-2 · Tenancy and organization structure.** The system serves **one organization**. `Department`
and `Branch` are two independent attributes, not a hierarchy:
- **Department** (e.g. Billing, Technical) is the **routing and permission boundary** — it drives
  assignment, SLA ownership, and what an agent can see.
- **Branch** (a location) is a **reporting and filtering attribute only** and grants no isolation.
- A ticket has exactly one department. A user belongs to exactly one department. A customer
  belongs to one branch.
- **Not assumed:** multi-tenant SaaS. No tenant concept exists.

**A-3 · SLA semantics.**
- Two clocks per ticket: **first response** and **resolution**.
- Targets are defined **per priority**, in configuration, in hours.
- The clock is **24/7 wall-clock**, starting at ticket creation. It does **not** pause while
  waiting on the customer, and ignores business hours, holidays, and per-branch timezones.
- One breach action: flag breached → raise priority one level → notify the department manager.
- Breach detection is a periodic in-process check at coarse granularity (minutes, not seconds).
  Timing precision is explicitly not a goal.

**A-4 · Roles.** Exactly **four fixed roles**, not a configurable RBAC engine:

| Role | Can |
|---|---|
| **Customer** | Submit tickets; view and reply to *own* tickets; read public KB; submit feedback |
| **Agent** | Full work on tickets in *own department*; read/write customers; internal notes; use AI assists |
| **Manager** | Everything an Agent can, across *all departments*; reassign; view reports and dashboards |
| **Administrator** | Everything a Manager can, plus user management, KB authoring, audit log access |

Roles are hierarchical and hardcoded. No per-field permissions, no custom role builder, no
delegation, no branch-scoped restriction.

**A-5 · Ticket lifecycle.** One fixed status set with enforced transitions:

```
New ──▶ Open ──▶ Pending ──▶ Resolved ──▶ Closed
         ▲         │             │
         │         │             └── reopen ─────────┐  (Resolved → Open, explicit action)
         │         └── customer reply ───────────────┤  (Pending → Open, AUTOMATIC)
         └───────────────────────────────────────────┘

Any non-terminal status → Cancelled
```

- **New** — created, and **not yet being worked**. A ticket in `New` **may already have an
  assignee**: automatic assignment (T2-D) runs at creation, and being assigned is not the same
  as being worked (A-18).
- **Open** — **an agent has started work.** The `New → Open` transition *is* the act of starting.
- **Pending** — awaiting customer input. *(Does not pause the SLA clock — see A-3.)*
  **A customer reply moves it back to `Open` automatically.** The reply itself is the trigger; no
  agent action is needed and no agent has to reopen it. The rule applies **only** from `Pending`:
  a reply on a `New` ticket leaves it `New` (an agent has still not started work), and a reply on a
  `Resolved` ticket does **not** reopen it — reopening `Resolved` stays the explicit action below.
- **Resolved** — the agent believes it is done; triggers the feedback request.
- **Closed** — terminal, reached from Resolved **by an agent, manually**. There is no
  automatic closure (A-16); no timer moves a ticket to Closed. No further replies.
- **Cancelled** — terminal, for tickets abandoned or created in error.
- **Escalation is an action, not a status** — it raises priority, records an escalation entry in
  ticket history, and notifies the manager, leaving status unchanged. There are no escalation
  tiers and no L1/L2/L3 support levels.
- Every transition is recorded in ticket history with actor, timestamp, and before/after values.
- **Actor attribution for the automatic `Pending → Open` transition — decided.** The transition has
  no invoking user, but every transition must carry an actor. It is attributed to the **replying
  customer**, because their reply is what caused it. It is **not** recorded as a system action: the
  SLA monitor is the only system actor in this design, and attributing a customer-caused change to
  the system would make ticket history less truthful, not more. Nothing new is stored to support
  this — `TicketActivity` already carries an actor and an actor kind.
- **Which role may perform which transition is fixed by A-16.** A-5 states which transitions are
  legal; A-16 states who may invoke them.

**A-6 · Categories and priorities.** Both are fixed configuration enumerations, not user-managed
taxonomies. Priority is a four-level scale (Low, Medium, High, Urgent), set by the agent or by the
AI suggestion; a customer may indicate urgency at submission but does not set priority directly —
that indication is the boolean flag of A-17.
Categories are a flat list — no sub-categories. **A category also determines the ticket's
department**, through the configured mapping of A-14.

**A-7 · Integrations.** No live external system is contacted during the assessment. Email,
WhatsApp, SMS, and ERP are represented by adapter interfaces with logging/no-op fakes (T3-A,
T3-D). The one real integration is the AI provider in T1-F, and it must degrade to a deterministic
offline fake so the application runs and demos with no network access or credentials.

**A-8 · AI behaviour.** All AI output is **advisory and human-approved**. Summaries are read-only
aids; suggested replies land in an editable draft and are never sent automatically; suggested
categories are pre-selections the agent can override. AI output is labelled as AI-generated in the
UI, and its acceptance or override is recorded in ticket history. No autonomous action, no direct
customer-facing generation.

**A-9 · Identity and authentication.** Email + password authentication with session or token auth,
sufficient to distinguish the four roles. Customers self-register or are created by an agent —
A-15 fixes what self-registration does about branch and about an email that already has a profile.
**No** SSO, OAuth, MFA, password-policy engine, or account-recovery flow. No anonymous ticket
submission.

**A-10 · Customer identity across channels.** Because only the web form and portal are real
inbound channels (T2-B), cross-channel identity resolution does not arise. A customer is
identified by a unique email address within the organization. No merge/dedupe tooling.

**A-11 · Language.** English and Arabic only. UI chrome is translated; user-generated content is
not. Arabic implies RTL layout. Dates are displayed in the Gregorian calendar in both languages.

**A-12 · Data and deployment.** Greenfield — no data migration, no incumbent system. A single
relational database, one backend application, and one front-end application, run locally via the
existing `docker-compose.yml`. Seed/demo data is provided so the demo is meaningful.

**A-13 · Notifications.** In-app only, generated for: assignment, SLA breach, escalation, and a
customer reply on an assigned ticket. No per-user notification preferences.

---

### Assumptions added after the architecture and data-model stages

A-14…A-17 were decided on 2026-08-24, when the Stage 7 (API Design) review found four business
questions that no source answered and that materially changed an API contract. They are decisions,
not inferences, and they extend A-5, A-6, A-9 and A-10 rather than contradicting them.

**A-14 · A ticket's department comes from its category.** Customers do not choose a department.
- A customer chooses a **category**; every category maps to **exactly one department** through
  configuration.
- The ticket receives its `departmentId` from that mapping **at creation, before assignment** —
  which T2-D requires, since automatic assignment is round-robin *within* the ticket's department.
- The mapping is configuration alongside the category list itself (T2-I: no configuration UI).
- Every configured category must map to a department; an unmapped category is a configuration
  error and fails at startup validation.
- An agent creating a ticket on behalf of a customer may set the department directly; the mapping
  is the default, not a cage.

**A-15 · Self-registration uses a default branch, and links to an existing profile.**
- `Customer.branchId` is required (A-2). A self-registering customer is assigned the
  **system default branch**, a configured value. They are not asked to choose one.
- **If a `Customer` profile already exists with the submitted email** (an agent created it
  earlier), registration **creates the `User` account and links it to that existing profile**. It
  does **not** create a second customer, and it does not fail.
- This preserves A-10 — one customer per email address — and DM-1's one-login-per-profile rule.

**A-16 · Who may perform each ticket transition.** A-5 fixes which transitions are legal; this
fixes who may invoke them.

| Transition | Customer | Agent | Manager | Administrator |
|---|---|---|---|---|
| Create (→ New) | ✔ own | ✔ on behalf of a customer | ✔ | ✔ |
| → Open | ✖ directly — but a customer **reply on a `Pending` ticket triggers it automatically** (A-5) | ✔ | ✔ all departments | ✔ |
| → Pending | ✖ | ✔ | ✔ all departments | ✔ |
| → Resolved | ✖ | ✔ | ✔ all departments | ✔ |
| → Closed | ✖ | ✔ | ✔ all departments | ✔ |
| Reopen (Resolved → Open) | ✔ own | ✔ | ✔ all departments | ✔ |
| → Cancelled | ✔ own, **only while `New`** | ✔ | ✔ all departments | ✔ |
| Escalate (action, not a transition) | ✖ | ✔ | ✔ all departments | ✔ |

- Agent and Manager authority is otherwise identical; Manager's applies across all departments
  (A-4). Administrator is unrestricted.
- **Closure is manual only. No automatic closure exists** — no timer, no scheduled job, no
  configured close-after period.
- **Agents and Managers may cancel** (a Manager across all departments); Administrators are
  unrestricted. **Customers may cancel their own ticket only while it is `New`** — the window
  defined by A-18. Once an agent starts work and the ticket becomes `Open`, the customer can no
  longer cancel it.
- One consequence remains deliberate and worth stating plainly: **customers cannot close** a
  ticket — they can only reopen a `Resolved` one.

**A-17 · Customers indicate urgency with a boolean, not a priority.**
- The ticket-creation input accepts **`isUrgent`** — a boolean, supplied by the customer.
- It **does not set priority**. Priority remains agent-set or AI-suggested (A-6).
- Agents and the AI categorization suggestion **may use it** as one input when deciding priority,
  so it is stored on the ticket and remains visible after creation.
- It is **customer input only**: an agent creating a ticket on behalf of a customer sets priority
  directly and does not supply this flag.

**A-18 · Assignment is not the start of work.** Decided 2026-08-24, resolving what had been open
question 8.
- Automatic assignment (T2-D) runs at creation. **A ticket may be assigned and still be `New`.**
- `New` means: created, possibly already assigned, **but no agent has begun working on it**.
- **`New → Open` is the act of an agent starting work.** It is a deliberate agent action, not a
  side effect of assignment.
- Therefore the customer's cancellation window (A-16) is real rather than theoretical: it lasts
  from creation until an agent actually picks the ticket up.
- Consequence for the data model: `Ticket.assignedUserId` may be set while `status = New`. Nothing
  may infer status from the presence of an assignee, or an assignee from the status.

**A-19 · A customer's email and their portal sign-in are one address.** Decided 2026-08-27,
resolving **OQ-5**, which [api-design.md](api-design.md) §5.5 raised on 2026-08-25 and deliberately
left open rather than invent.

- **When `Customer.email` changes, the linked portal `User.email` changes to the same value.** The
  customer signs in with the new address; the old one stops working.
- **It applies only where a login exists.** A profile-only customer has nothing to propagate to —
  DM-1 makes that the normal case, since an agent creates tickets for customers who may never touch
  the portal.
- **The two writes are one atomic operation.** Both rows change together or neither changes. There
  is no state in which the profile and the login hold different addresses. This is the general rule
  of [architecture.md](architecture.md) §3 — *"one unit of work per request, committed once"* —
  applied here, not a new mechanism.
- **`User.email` uniqueness is unchanged and still applies to the propagated value.** It remains
  unique case-insensitively across **all** users, staff included ([data-model.md](data-model.md) §5
  constraint 1). If the new address already belongs to another user, **the whole operation is
  rejected and the customer profile is not changed either** — the atomicity above cuts both ways.
- **Why this reading.** A-10 identifies a customer by a unique email address. Letting the profile
  and the login diverge would give one person two identifying addresses, and A-9 excludes account
  recovery, so no mechanism would exist to reconcile them afterwards.
- **The change to the login is audited** (added 2026-08-27, part of this assumption). Changing
  someone's sign-in identifier is a permission-relevant act of user administration, which T2-H and
  the `audit-configuration` intake already require to be recorded. It uses the **existing** audit
  mechanism — one action code, `UserEmailChanged`, alongside `UserRoleChanged` and
  `UserDepartmentChanged` — and introduces no new audit architecture, no new endpoint and no schema
  change. It is written **only when a linked login's email actually changes**, and it commits in the
  **same unit of work** as the change itself: both rows and the audit entry, or none of them.
  Details in [data-model.md](data-model.md) §2.14 and §5 constraint 1a.
- **The cost is accepted and is not mitigated here.** An agent who mistypes a customer's email
  changes what that customer must type to sign in, and A-9 provides no recovery flow. No
  confirmation step, no grace period and no notification is introduced by this decision; the UI
  states the consequence before the save ([ui-design.md](ui-design.md) §5.5). The audit entry above
  is what makes the change **traceable** afterwards — it is not a mitigation, and it does not undo
  anything.

---

## 8. Explicitly out of scope (T4)

Not built, not stubbed, not designed — listed so the absence is visible and deliberate.

### Product
- Native mobile applications; PWA/offline; push notifications
- Real-time live chat transport, agent presence, chat queueing and routing
- AI chatbot conversation flows and bot→human handoff
- Real email/WhatsApp/SMS sending or receiving; inbound webhooks; delivery receipts; opt-outs
- Any live ERP or third-party business-system connection
- Multi-tenant SaaS: tenant provisioning, per-tenant isolation, subscriptions, billing
- Configurable workflow/rules engine; user-editable SLA policies, categories, or roles
- Custom report builder; scheduled reports; PDF/Excel export
- Any CSAT/NPS survey system beyond the single closing rating
- KB article versioning, review/approval workflow, rich media, multilingual article variants
- Customer merge/dedupe, bulk import/export, data-migration tooling
- Ticket merging, splitting, linking, parent/child tickets, recurring tickets
- Contracts, entitlements, or per-customer SLA agreements
- Telephony, call logging, screen sharing, co-browsing
- Any third or subsequent language

### Technical (excluded by the assessment brief)
- **Microservices** — the system is a single backend application
- **Message brokers / queues** (Kafka, RabbitMQ, SQS) — a periodic in-process check instead
- **CQRS, event sourcing** — straightforward CRUD behind a service layer
- **Kubernetes, service mesh, serverless** — local Docker Compose only
- Caching layers, read replicas, sharding, dedicated search engines, vector databases
- Multi-region deployment, HA, disaster recovery, backup/restore procedures

### Quality attributes not pursued
- Performance, load, and scalability targets or testing
- Security hardening beyond baseline: no pen testing, threat model, encryption-at-rest work,
  secrets-management platform, rate limiting, or WAF
- Regulatory compliance programs (GDPR/PDPL tooling, data residency, right to erasure, consent
  management, retention policies)
- WCAG accessibility certification — sensible semantics only, no audit
- Cross-browser matrix testing; a visual design system; a production observability stack
- Exhaustive automated test coverage — testing targets the T1 slices and core business rules

---

## 9. Known open questions (not resolved by assumption)

Carried over from the requirements analysis and deliberately left open: they do not block the
assessment, but would block a real build.

1. Which AI provider, and are there data-residency or confidentiality limits on sending customer
   content to it? *(Sidestepped here via the offline fake — A-7.)*
2. Which ERP product, in which direction, over which objects? `req §11.2` is unspecifiable as written.
3. What "External systems" (`req §11.4`) refers to — unbounded until a system is named.
4. Single-organization vs. multi-tenant SaaS. A-2 assumes the former; this is the single decision
   with the largest architectural consequence if wrong.
5. Real SLA policy: business hours, holiday calendars, per-branch timezones, pause-on-customer-reply.
6. Chatbot → human handoff semantics.
7. Whether ticket submission must be possible without an account. A-9 assumes not.

*(A question 8 — the boundary of the customer cancellation window — was raised and **resolved** on
2026-08-24. Assignment does not start work; see A-18. It is recorded there rather than left here.)*

---

## 10. Definition of done for this assessment

1. Every **T1** item is implemented end to end and demonstrable against seeded data.
2. Every **T2** item is present and exercises its happy path.
3. Every **T3** item has a named abstraction plus a runnable fake, and is documented as future scope.
4. Every **T4** item is absent by decision, per §8.
5. The system starts from a clean checkout with a single documented command and runs with **no
   external accounts or credentials**.
6. Each implemented feature traces back to a requirement line via §6, and each spec/plan artifact
   traces forward to the code satisfying it — this traceability is the SDD deliverable.

### Suggested sequencing across the three evenings

| Evening | Focus |
|---|---|
| 1 | Specs and plans for T1; foundation; auth and roles (T1-D); departments (T1-E); customers (T1-A) |
| 2 | Tickets end to end (T1-B); agent dashboard (T1-C); portal (T2-F); SLA and assignment (T2-D) |
| 3 | AI slice (T1-F); KB (T2-E); reports (T2-G); i18n/RTL (T2-J); T3 seams and fakes; docs |

If time runs short, **T2 items are cut before T1 items, and T3 fakes are cut before T2 items.**
Any cut is recorded in this document rather than left silent.
