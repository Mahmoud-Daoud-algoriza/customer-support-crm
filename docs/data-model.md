# Data Model — Customer Support CRM

> **Source of truth:** [requirements.md](requirements.md) · [product-scope.md](product-scope.md) T1–T4, A-1…A-19 · [architecture.md](architecture.md) §2.1, §2.4, §2.5, §4.1.1, §4.3, §5, §6.3 · [story-backlog.md](story-backlog.md) and the 18 story intakes
> **SDD stage:** 6 of 10. Gate 6 → 7 per [sdd-workflow.md](sdd-workflow.md) §4.
> **Status:** Conceptual and logical model, plus the string-column convention of §6.1 (amended 2026-08-26). No SQL, no EF Core entities, no `DbContext`, no migrations, no endpoints, no UI.

**What this document decides:** which entities exist, what each one owns, how they relate, which
fields are required, which invariants hold, where indexes are justified, and which entities are
mutable versus append-only.

**What it deliberately leaves out:** EF Core configuration, migration ordering, and index fill
factors. Those belong to implementation (stage 10). Endpoint shapes are stage 7; screens are
stage 8.

**One deliberate exception, added by amendment on 2026-08-26: §6.1 fixes string column lengths and
collation.** Physical types were originally left out too. That proved untenable: this document
declares four *unique* indexes on string columns, and SQL Server cannot build one over an unbounded
string — so the declaration silently obliged every story to invent a length, and nothing would have
kept `User.email` and `Customer.email` the same width. §6.1 is the smallest addition that makes the
uniqueness this document already asserts actually implementable. Nothing else physical is decided
here.

**Discipline applied.** Every entity and every field below traces to a requirement line, a scope
item, an assumption, or an approved architectural decision. Fields that a CRM *usually* has —
ticket reference numbers, customer companies, tags, SLA policies as rows, soft-delete flags,
created/modified stamps on everything — are absent unless something in the sources demanded them.
§2.16 lists what was consciously left out.

---

## 1. Ownership questions resolved before modelling

Six questions had to be answered before any entity could be drawn, and a seventh was answered
afterwards (DM-7). Each answer is a modelling decision (DM-n) with its rationale and the
alternative rejected.

### DM-1 · A person who logs in and a customer record are two different things

**Question.** [product-scope.md](product-scope.md) A-4 makes `Customer` a *role*, while A-2 says
"a user belongs to exactly one department" and "a customer belongs to one branch". Those cannot
both be true of one row.

**Decision.** Two entities:

- **`User`** — a login identity with exactly one role. This is the **authoritative record** for
  role, department and active status that [architecture.md](architecture.md) §4.1.1 requires to be
  re-read on every request.
- **`Customer`** — the CRM profile of requirements §1: contact details, branch, notes, tickets,
  interaction history.

They are linked **one-to-at-most-one**: a customer who uses the portal has a `User` row of role
`Customer` pointing at their profile. When someone self-registers with an email that already has
a profile, the new `User` **links to that profile** rather than creating a second customer
(A-15), which is what keeps A-10's one-customer-per-email rule true. A customer who has never logged in has a profile and no
login — which is required, because an agent creates tickets on behalf of customers
(`ticket-core` intake) who may never touch the portal.

**Consequence for A-2.** `User.departmentId` is required for staff roles and forbidden for the
`Customer` role; `Customer.branchId` is required. The A-2 asymmetry is expressed in the structure
rather than in prose.

**Rejected.** One table holding both, with null passwords for profile-only rows. It would put
agent-managed customer data and admin-managed staff accounts in the same place, make
"deactivate a user" and "edit a customer" the same operation, and force `departmentId` to be
nullable for everyone — dissolving the constraint that §4.3 depends on.

### DM-2 · Assignment is a field plus history, not an entity

**Question.** Requirements §2.3 asks for assignment; does it need a `TicketAssignment` table?

**Decision.** No. The **current** assignee is `Ticket.assignedUserId`; the **history** of
assignment changes is already required as `TicketActivity` rows
([architecture.md](architecture.md) §2.5). A third representation would be a third thing to keep
consistent.

**Rejected.** An assignment table with validity ranges — needed only if a ticket could have
several concurrent assignees or if assignment periods were reported on. Neither is in scope.

### DM-3 · SLA state lives on the ticket

**Question.** Do the A-3 clocks need their own entity?

**Decision.** No. Two due timestamps and two latching breach flags are fields on `Ticket`. There is
exactly one SLA state per ticket, always loaded with it, and the agent queue orders by it
(T1-C) — a 1:1 side table would add a join to every query for no gain.

**SLA targets themselves are configuration, not data** ([architecture.md](architecture.md) §6.3,
T2-I). There is no `SlaPolicy` entity; per-priority hours come from configuration.

**Rejected.** `SlaPolicy` rows and a `TicketSla` 1:1 — the shape a configurable SLA engine needs,
which [product-scope.md](product-scope.md) §8 excludes.

### DM-4 · Content lives once; activity is the ordering spine

**Question.** [architecture.md](architecture.md) §2.5 says the recorder writes "status changes,
assignment changes, priority and category changes, escalations, **messages, and internal notes**"
into ticket history. Does that mean message bodies live in the history table?

**Decision.** No. Messages and internal notes are **their own entities**, because they are content
with an author, a channel and a visibility rule, and because the story boundaries own them
separately (`ticket-intake-messaging` owns messages, `tasks-internal-notes` owns notes). Posting
either one also writes a `TicketActivity` row that **references** it and carries no body.

The activity table is therefore the single ordered spine of everything that happened to a ticket,
and a body exists exactly once. The timeline is one ordered query rather than a three-way merge.

**Cost accepted.** One extra insert in the same transaction as a message or note.

**Rejected.** (a) Bodies inside activity rows — makes messages second-class and mixes content with
change records. (b) No activity row for messages — the timeline becomes a merge of three sources
in every reader, and [architecture.md](architecture.md) §2.5's "one recorder" claim stops being
true.

### DM-5 · AI needs no entity of its own

**Question.** The prompt asks for AI assistance records *only if persistence is actually required*.
Is it?

**Decision.** **No AI entity exists.** Checking each capability against its source:

| AI capability | Persistence required? | Where it lands |
|---|---|---|
| Ticket summary (§7.1) | **No** — `ai-ticket-assists` intake: "Read-only aid. It is not stored as a ticket field that pretends to be authored content." | Nowhere; computed on demand |
| Suggested reply (§7.2) | **No** — lands in the composer as editable text; if the agent sends it, it is an ordinary `TicketMessage` authored by that agent | Nowhere until sent, then a normal message |
| Auto-categorization (§7.3) | **Yes, but as history** — the intake requires "the suggested values, and whether the agent accepted or overrode them, are written to **ticket history**" | Two `TicketActivity` types |

So the one real persistence requirement is satisfied by the activity spine that already exists.
An `AiSuggestion` table would be an unused CRM pattern.

**Rejected.** A suggestions/interactions log for cost tracking or model evaluation — no requirement
asks for it, and [product-scope.md](product-scope.md) §8 excludes evaluation tooling.

### DM-6 · Integration seams persist exactly one field

**Question.** Which of the T3 seams need stored data?

**Decision.** One field, one enum value set:

- **`Customer.externalReference`** (optional) — required explicitly by
  [architecture.md](architecture.md) §5.3 and the `channel-erp-adapters` intake. Unused by default.
- **`TicketMessage.channel`** — the channel a message arrived on or was sent through. Present from
  day one with the two real values, which is what makes T3-A a seam
  ([architecture.md](architecture.md) §5.2).
- **Nothing else.** The log adapter's outbound send "produces a log entry and is recorded against
  the ticket" — that record is a `TicketActivity` row, not an integration table. No adapter
  configuration, credential, webhook, or delivery-receipt storage exists, because no adapter that
  would need it is built.

### DM-7 · `CustomerFeedback` belongs to the `Tickets` module

**Question.** Raised 2026-08-24, after this document was approved. The entity was labelled as owned
by a "Portal" module — but [architecture.md](architecture.md) §1 defines **ten** backend modules and
`Portal` is not one of them. `customer-portal` is a feature slug and an Angular area, not a backend
module. The label pointed at something that does not exist.

**Decision.** `CustomerFeedback` is owned by the existing **`Tickets`** module.

- Feedback is domain behaviour **attached to a ticket** — one row per ticket, unique on `ticketId`.
- It is offered when a ticket reaches `Resolved` (T2-F), a ticket lifecycle event.
- `customer-portal` is a front-end and planning concern, not a backend module.

**No new module, and no change to the ten-module architecture.** Nothing about the entity's shape,
fields, relationships or invariants changes — this fixes an ownership *label* only.

**Rejected.** Adding a `Portal` backend module. It would exist to hold one write-once entity whose
only relationship is to `Ticket`, and it would split ticket-lifecycle behaviour across two modules
for no benefit.

---

## 2. Entity catalogue

Fifteen entities. Every one carries a surrogate primary key; PKs are not repeated in the field
tables below. **Req** = required (non-null).

### 2.1 `User`

**Purpose.** A login identity, and the **authoritative source of role, department and active
status** that [architecture.md](architecture.md) §4.1.1 re-reads on every authenticated request.
Requirements §10.1–10.2, T1-D, A-4, A-9.

| Field | Req | Notes |
|---|---|---|
| `email` | ✔ | Unique, case-insensitive. The sign-in identifier (A-9) |
| `passwordHash` | ✔ | ASP.NET Core standard hashing ([architecture.md](architecture.md) §4.1) |
| `displayName` | ✔ | Shown as the actor on messages, notes and activity |
| `role` | ✔ | `Customer` \| `Agent` \| `Manager` \| `Administrator` — fixed enum, four values, no role table (A-4) |
| `departmentId` | conditional | **Required for `Agent`, `Manager`, `Administrator`; must be null for `Customer`** (DM-1, A-2) |
| `customerId` | conditional | **Required for `Customer`; must be null for staff roles.** The portal login's link to its profile (DM-1) |
| `branchId` | ✖ | Staff location. Reporting attribute only (T2-K) |
| `isActive` | ✔ | Deactivation flag. Read on every request, not just at sign-in ([architecture.md](architecture.md) §4.1.1) |
| `createdAt` | ✔ | |

**Relationships.** `Department` 0..1 ← many staff users · `Branch` 0..1 ← many users ·
`Customer` 0..1 ↔ 0..1 `User`.
**Ownership/scope.** Identity module. Organization-wide. Written by Administrators only (A-4).
**Mutability.** Mutable — role, department, active status and name change over the account's life.
That mutability is precisely why AD-15 forbids caching these values in a token.
**Invariants.** Email unique · role/department/customer combination as above · a `Customer`-role
user's `customerId` is unique across users (one login per profile) · deactivating never deletes.

### 2.2 `Department`

**Purpose.** The routing and permission boundary of A-2 and requirements §12. Requirements
§12 multi-department, T1-E.

| Field | Req | Notes |
|---|---|---|
| `name` | ✔ | Unique |
| `managerUserId` | ✖ | The escalation recipient (`departments-branches` and `sla-routing-escalation` intakes) |

**Relationships.** One department → many `User` (staff) · one → many `Ticket` · one → 0..1 manager
`User`.
**Ownership/scope.** Organization module. Organization-wide, seeded/configured (T2-I: no admin UI).
**Mutability.** Mutable.
**Invariants.** `managerUserId`, when set, must reference an active user of role `Manager` or
`Administrator` — an Application-layer rule, not a foreign key.
**✅ A department with no manager — resolved 2026-08-31 as A-21. This was OQ-3.**
`managerUserId` is optional because a department can exist before anyone is appointed to it, and
because nothing in the sources makes it mandatory. That left a real gap:
[product-scope.md](product-scope.md) T2-D defines the single escalation rule as "flag the ticket
breached, raise priority one level, **notify the department manager**", and the
`sla-routing-escalation` intake requires that the manager receive an in-app notification on breach —
but **neither covered the manager's absence**.

**The recipient is now a cascade** ([product-scope.md](product-scope.md) §7, **A-21**): the
department's own manager when `managerUserId` is set and that user is still active and still holds
`Manager` or `Administrator` — the eligibility this section's invariant already requires; otherwise
**every active `Manager`**; otherwise **every active `Administrator`**; otherwise **nobody**.

**What this section stated before the decision remains true and is now the decision's first clause:**
the breach flag and the priority raise are **unaffected** by a missing manager and must still occur.
A missing manager suppresses a notification, never an escalation.

**Model consequence: none.** `managerUserId` stays optional, no column changes, and no entity gains
a field — A-21 is a policy over rows this model already carries. `Notification` (§2.12) is unchanged
and still recipient-scoped, which is why the decision alters no contract: which rung fired is
observable only in whose notification list gains a row. Recorded in §8.

**Seed data still appoints a manager for every department.** That remains a demo convenience rather
than a requirement — A-21 is precisely what makes the un-seeded case well defined.

### 2.3 `Branch`

**Purpose.** A location, used **for reporting and filtering only**. Requirements §12 multi-branch,
T2-K, A-2.

| Field | Req | Notes |
|---|---|---|
| `name` | ✔ | Unique |

**Relationships.** One branch → many `Customer` · one → many `User` (optional).
**Ownership/scope.** Organization module. Organization-wide.
**Mutability.** Mutable.
**Invariant — the important one.** **`Branch` never appears in an authorization predicate.**
`Ticket` deliberately has **no** `branchId`: ticket visibility is department-based (§4.3), and a
branch column on `Ticket` would be an invitation to scope by it.

#### How a ticket's branch is obtained — derived, by intent

A ticket's branch is **derived through `Ticket → Customer → Branch`**. It is not stored on the
ticket, and that is a deliberate choice rather than an omission.

**No source requires a ticket-level branch relationship.** Every branch reference in the sources
was checked:

| Source | What it says about branch | Ticket-level relationship required? |
|---|---|---|
| requirements §12 | "Multi-branch" — the whole of it | No |
| [product-scope.md](product-scope.md) T2-K | "an organizational attribute **on customers and users**, filterable in reports" | No — names customers and users, not tickets |
| [product-scope.md](product-scope.md) A-2 | "a reporting and filtering attribute only… A customer belongs to one branch" | No |
| `departments-branches` intake | "Every customer carries exactly one branch"; "Ticket lists and reports can be filtered by department and by branch" | No — the filter is a query capability, not a column |
| `management-dashboard` intake | "The dashboard can be filtered by department and by branch" | No |
| `customer-records` intake | Branch is part of the customer's contact details | No |

**The decisive line.** The `departments-branches` intake states the acceptance criterion:
*"Branch is demonstrably NOT a permission boundary: an agent can see in-department tickets
regardless of **the customer's branch**."* The sources therefore already speak of a ticket's branch
as *the customer's* branch — which is exactly the derivation above. No source proposes the assigned
agent's branch as a ticket's branch, so "filter tickets by branch" means the customer's branch.

**Why derivation satisfies T2-K.** The requirement on branch is that it be *filterable in reports*,
not that it be *stored per ticket*. A join from ticket to customer answers every branch filter the
stories ask for — the dashboard's combined department-and-branch filter included — at data volumes
where the join cost is irrelevant. `Customer(branchId)` is indexed for exactly this (§6).

**Why derivation is safer than storage.** A `Ticket.branchId` column would be denormalized state to
keep in step with the customer, and — more importantly — it would put a branch value in arm's reach
of the ticket scoping helper, where A-2 forbids it from being used. Absence makes the misuse
impossible rather than merely discouraged.

**If this is wrong, it is a scope change, not a model tweak.** Should a branch-level rule ever be
required — a branch manager who sees only their branch's tickets, or a ticket whose branch differs
from its customer's — that contradicts A-2 and must be raised against
[product-scope.md](product-scope.md) first. It is flagged here rather than pre-empted.

### 2.4 `Customer`

**Purpose.** The CRM profile of requirements §1: who the support is for. T1-A.

| Field | Req | Notes |
|---|---|---|
| `fullName` | ✔ | §1 contact details |
| `email` | ✔ | Unique, case-insensitive. The identifier of A-10 |
| `phone` | ✖ | §1 contact details |
| `branchId` | ✔ | A-2: a customer belongs to one branch. A self-registering customer receives the configured **default branch** (A-15) |
| `externalReference` | ✖ | ERP seam, unused by default (DM-6, [architecture.md](architecture.md) §5.3) |
| `createdAt` | ✔ | |

**Relationships.** `Branch` 1 → many customers · one customer → many `Ticket` · one → many
`CustomerNote` · one → 0..1 `User` (portal login) · one → many `Attachment` (customer-owned).
**Ownership/scope.** Customers module. Organization-wide; readable by any staff role, not by other
customers.
**Mutability.** Mutable.
**Invariants.** Email unique · **a change to `email` propagates to the linked `User.email` in the
same unit of work (A-19, §5 constraint 1a)** · no merge or dedupe operation exists (T2-A/§8) ·
deleting a customer is not an application operation.
**Note on the interaction timeline.** Requirements §1.3 is a **read projection** over this
customer's tickets and their activity ([architecture.md](architecture.md) §2.5). It is not stored.

### 2.5 `CustomerNote`

**Purpose.** An agent's note on a customer — requirements §1.4 "Notes", T1-A. Distinct from a
ticket internal note (§2.9), which is about one ticket.

| Field | Req | Notes |
|---|---|---|
| `customerId` | ✔ | Owner |
| `authorUserId` | ✔ | Attribution required by the `customer-records` intake |
| `body` | ✔ | Plain text |
| `createdAt` | ✔ | |

**Relationships.** `Customer` 1 → many notes · `User` 1 → many authored notes.
**Ownership/scope.** Customers module. Staff-visible only; never exposed to the portal.
**Mutability.** **Immutable once written.** The intake requires notes to be attributed and "not
silently editable by other users"; immutability is the cheapest way to guarantee that.

### 2.6 `Ticket`

**Purpose.** The central work item. Requirements §2, T1-B, A-5, A-6.

| Field | Req | Notes |
|---|---|---|
| `customerId` | ✔ | Exactly one customer (`ticket-core` intake) |
| `departmentId` | ✔ | Exactly one department — **the authorization boundary** (A-2, §4.3) |
| `subject` | ✔ | `ticket-core` AC |
| `description` | ✔ | The originating text — the web-form body or what the agent typed. Replies are `TicketMessage` rows, not copies of this |
| `categoryCode` | ✔ | Validated against the configured flat list; **not** a table (A-6, [architecture.md](architecture.md) §6.3) |
| `priority` | ✔ | `Low` \| `Medium` \| `High` \| `Urgent` (A-6) |
| `status` | ✔ | `New` \| `Open` \| `Pending` \| `Resolved` \| `Closed` \| `Cancelled` (A-5) |
| `assignedUserId` | ✖ | The current assignee (DM-2). **May be set while `status = New`** — assignment is not the start of work (A-18). Null only until assignment happens |
| `isUrgent` | ✔ | Boolean, default false. **Customer input only** — an urgency indication that does **not** set priority; agents and the AI suggestion may use it when deciding priority (A-17) |
| `createdByUserId` | ✔ | The agent or the customer who raised it |
| `createdAt` | ✔ | **The SLA clock origin** (A-3) |
| `firstResponseDueAt` | ✔ | At creation: `createdAt` + the configured hours for the ticket's priority (A-3). **A later priority change does not move it** — frozen at creation (**A-20**, closing OQ-2 on 2026-08-30) |
| `resolutionDueAt` | ✔ | Same basis, same rule |
| `firstRespondedAt` | ✖ | Set once, on the first outbound message |
| `resolvedAt` | ✖ | Set on entering `Resolved` |
| `closedAt` | ✖ | Set on entering `Closed` or `Cancelled` |
| `firstResponseBreached` | ✔ | Latching flag, default false |
| `resolutionBreached` | ✔ | Latching flag, default false |

**Relationships.** `Customer` 1 → many · `Department` 1 → many · `User` 0..1 (assignee) → many ·
one ticket → many `TicketActivity`, `TicketMessage`, `TicketInternalNote`, `TicketTask`,
`Attachment`, `Notification` · one ticket → 0..1 `CustomerFeedback`.
**Ownership/scope.** Tickets module. **Department-scoped** for agents, **customer-scoped** for
customers, unrestricted for managers and administrators (§4.3).
**Mutability.** Mutable — status, priority, category, assignee and SLA fields all change. Every
change writes a `TicketActivity` row.

**Invariants.**

1. Status transitions follow the A-5 machine, enforced in the Domain layer
   ([architecture.md](architecture.md) §2.1). Illegal transitions are refused for every role.
2. `Closed` and `Cancelled` are terminal: no further messages, notes or transitions.
2b. **A customer reply reopens a `Pending` ticket automatically.** Posting an `Inbound`
    `TicketMessage` while `status = Pending` transitions the ticket to `Open` in the same
    transaction as the message, and writes a `StatusChanged` activity row alongside the
    `MessagePosted` one. **The actor on that status row is the replying customer** — an approved
    rule recorded in A-5, not an implementation choice: A-5 requires every transition to carry an
    actor, and the customer's reply is what caused this one. It is **not** a `System` actor; the
    SLA monitor remains the only system actor in this design. The rule fires **only**
    from `Pending`: a reply on `New` leaves it `New`, and a reply on `Resolved` does **not** reopen
    it (reopening `Resolved` remains the explicit action of A-16). No new entity or field is
    involved (A-5, A-16).
2a. **`status` and `assignedUserId` are independent.** A `New` ticket may carry an assignee
    (A-18); nothing may infer one field from the other. `New → Open` records an agent starting
    work, not an assignment.
3. `assignedUserId`, when set, must be an **active staff user in this ticket's department** —
   a cross-row rule enforced in the Application layer, so a ticket can never be assigned to someone
   who could not then see it (`ticket-core` AC).
4. Escalation raises priority exactly one level and **Urgent stays Urgent** (A-5).
5. **Breach flags latch.** Once true they never return to false, so history and SLA reporting stay
   honest even if priority later changes.
6. **The due timestamps freeze at creation and do not move when priority changes** — **A-20**,
   decided 2026-08-30, closing OQ-2. A-3 fixes targets per priority and starts the clock at
   `createdAt` but said nothing about a ticket whose priority changes afterwards, which the
   escalation rule of T2-D causes routinely. Two behaviours were consistent with the sources, and
   this document deliberately settled neither:

   - **(a) Recompute** — due dates become `createdAt` + the new priority's hours. An escalation
     tightens the deadline, and a ticket can become breached the moment it is escalated.
   - **(b) Freeze** — due dates stay as computed at creation. Escalation raises urgency and
     notifies, but does not move the deadline. ← **chosen**

   **(b) was chosen** because (a) can mark a ticket breached as a direct consequence of the
   escalation that the breach itself triggered, and because a breach verdict should stay a statement
   about the promise in force when the ticket arrived. The two readings produce materially different
   SLA-attainment numbers in the §9.2 report, which is why this was a business rule for the product
   owner and not a modelling detail. **The model is unchanged either way** — it stores the two
   timestamps and asserts no rule about them; A-20 is what asserts the rule. The wider SLA policy
   question remains [product-scope.md](product-scope.md) §9 question 5, and **A-20 does not close
   it**.
7. `Pending` does **not** pause any clock (A-3).

### 2.7 `TicketActivity` — the history spine

**Purpose.** Requirements §2.5 ticket history: the append-only trail of everything that happened to
a ticket. T1-B, [architecture.md](architecture.md) §2.5. **This is not the audit log** (§2.14).

| Field | Req | Notes |
|---|---|---|
| `ticketId` | ✔ | Owner |
| `occurredAt` | ✔ | Ordering key |
| `activityType` | ✔ | See the set below |
| `actorUserId` | ✖ | Null when the actor is the system (the SLA monitor) |
| `actorKind` | ✔ | `User` \| `System` — so a null actor is explicit, never ambiguous. `System` is used **only** by the SLA monitor; the automatic `Pending → Open` transition is `User`, attributed to the replying customer (A-5) |
| `oldValue` | ✖ | Short text; the before value for change types |
| `newValue` | ✖ | Short text; the after value |
| `visibility` | ✔ | `CustomerVisible` \| `Internal` — the portal read filter (T2-C) |
| `messageId` | ✖ | Set for `MessagePosted`; the body lives on the message (DM-4) |
| `internalNoteId` | ✖ | Set for `InternalNotePosted` |

**Activity types**, each traceable: `Created`, `StatusChanged`, `Assigned`, `PriorityChanged`,
`CategoryChanged`, `Escalated` (§2.4, A-5), `SlaBreached` (T2-D), `MessagePosted` (T2-B),
`InternalNotePosted` (T2-C), `AiSuggestionOffered` and `AiSuggestionResolved` (T1-F, DM-5 — the
suggested category/priority and whether it was accepted or overridden), `FeedbackSubmitted` (T2-F).

**Relationships.** `Ticket` 1 → many · `User` 0..1 (actor) → many · 0..1 `TicketMessage` ·
0..1 `TicketInternalNote`.
**Ownership/scope.** Tickets module. Inherits the ticket's scope; entries marked `Internal` are
additionally excluded from every customer-facing read.
**Mutability.** **Append-only.** No update or delete path exists — not merely no UI, but no service
method ([architecture.md](architecture.md) §2.5).
**Invariants.** `messageId` is set if and only if `activityType = MessagePosted`; likewise
`internalNoteId` · `InternalNotePosted` is always `Internal` visibility · `actorUserId` is null
exactly when `actorKind = System`.

### 2.8 `TicketMessage`

**Purpose.** Customer-visible correspondence on a ticket — requirements §3.5 web form and §3.3
portal messaging. T2-B, and the model every future channel adapter writes into (T3-A, DM-6).

| Field | Req | Notes |
|---|---|---|
| `ticketId` | ✔ | Owner |
| `authorUserId` | ✔ | The agent or the customer who wrote it |
| `direction` | ✔ | `Inbound` (from customer) \| `Outbound` (to customer) |
| `channel` | ✔ | `WebForm` \| `Portal` today. A new value arrives with the adapter that implements it — that is the seam ([architecture.md](architecture.md) §5.2) |
| `body` | ✔ | Plain text |
| `postedAt` | ✔ | |

**Relationships.** `Ticket` 1 → many · `User` 1 → many authored · 1 ↔ 1 `TicketActivity` of type
`MessagePosted` (DM-4).
**Ownership/scope.** Tickets module. Visible to the ticket's customer and to staff in scope.
**Mutability.** **Immutable once posted.** No edit or delete; a correction is a new message.
**Invariants.** Never accepted on a `Closed` or `Cancelled` ticket (A-5) · the first `Outbound`
message sets `Ticket.firstRespondedAt` if it is still null · every message has exactly one
`MessagePosted` activity row · **an `Inbound` message posted while the ticket is `Pending`
transitions it to `Open` automatically**, in the same transaction, writing a `StatusChanged`
activity row attributed to the replying customer, per the approved rule in A-5 — `actorKind` is
`User`, never `System` (§2.6 invariant 2b). The transition fires from
`Pending` only — never from `New` or `Resolved` — and generates no notification of its own,
because A-13 defines exactly four notification types and none of them is a status change.

### 2.9 `TicketInternalNote`

**Purpose.** The whole of "team collaboration" for this assessment — requirements §4.5, T2-C.

| Field | Req | Notes |
|---|---|---|
| `ticketId` | ✔ | Owner |
| `authorUserId` | ✔ | Attribution |
| `body` | ✔ | Plain text |
| `createdAt` | ✔ | |

**Relationships.** `Ticket` 1 → many · `User` 1 → many authored · 1 ↔ 1 `TicketActivity` of type
`InternalNotePosted`.
**Ownership/scope.** Tickets module. **Staff only — never visible to a customer through any path**:
not the portal thread, not the interaction timeline, not a notification (T2-C).
**Mutability.** Immutable once written.
**Invariant.** It is a separate entity from `TicketMessage` **on purpose**: a customer-visible read
assembles from messages and never touches this table, so the visibility rule is structural rather
than a filter someone must remember to apply.

### 2.10 `TicketTask`

**Purpose.** Requirements §4.3 tasks and reminders. T2-C.

| Field | Req | Notes |
|---|---|---|
| `ticketId` | ✔ | Always attached to a ticket — standalone to-dos are out of scope (T2-C) |
| `title` | ✔ | |
| `dueAt` | ✔ | The intake requires a due date |
| `assignedUserId` | ✔ | The agent responsible |
| `isDone` | ✔ | Default false |
| `completedAt` | ✖ | Set when marked done |
| `createdByUserId` | ✔ | |
| `createdAt` | ✔ | |

**Relationships.** `Ticket` 1 → many · `User` 1 → many assigned.
**Ownership/scope.** Tickets module (agent workspace). Staff only; never surfaced to customers.
**Mutability.** Mutable — completion is the point.
**Invariant.** `completedAt` is set if and only if `isDone` is true.

### 2.11 `Attachment`

**Purpose.** Requirements §1.4 attachments. T2-A — local disk, size-capped, minimal.

| Field | Req | Notes |
|---|---|---|
| `ticketId` | conditional | **Exactly one of `ticketId` / `customerId` is set** (T2-A: attached to a ticket or a customer) |
| `customerId` | conditional | As above |
| `fileName` | ✔ | Original name, for download |
| `contentType` | ✔ | |
| `sizeBytes` | ✔ | Enforced against the configured cap at upload |
| `storagePath` | ✔ | Location on local disk. Not a URL — download goes through the owner's authorization path ([architecture.md](architecture.md) §4.4) |
| `uploadedByUserId` | ✔ | |
| `uploadedAt` | ✔ | |

**Relationships.** `Ticket` 1 → many, **or** `Customer` 1 → many.
**Ownership/scope.** Inherits the scope of its owner: a ticket attachment is department-scoped, a
customer attachment is staff-visible.
**Mutability.** Immutable metadata; no delete path in scope.
**Invariants.** The owner XOR rule above · `sizeBytes` ≤ configured cap · no virus scanning, cloud
storage, preview or versioning (T2-A).

### 2.12 `Notification`

**Purpose.** Requirements §5.4 alerts and notifications — **in-app only** (A-13, T2-D).

| Field | Req | Notes |
|---|---|---|
| `recipientUserId` | ✔ | |
| `type` | ✔ | `TicketAssigned` \| `SlaBreached` \| `TicketEscalated` \| `CustomerReplied` — exactly the four events A-13 lists |
| `ticketId` | ✔ | All four events are about a ticket |
| `createdAt` | ✔ | |
| `readAt` | ✖ | Null until read; drives the unread badge |

**Relationships.** `User` 1 → many · `Ticket` 1 → many.
**Ownership/scope.** `Sla` module. **Recipient-scoped** — a user reads only their own.
**Mutability.** Only `readAt`, and only null → timestamp. Nothing else changes.
**Invariants.** No per-user preferences exist (A-13) · no email, SMS or push row — delivery
channels are T3-A and are not modelled (DM-6).

### 2.13 `KnowledgeArticle`

**Purpose.** Requirements §6 — FAQs, help articles and solution guides as **one entity with a type**
(T2-E), plus the retrieval source for §7.4 suggested solutions.

| Field | Req | Notes |
|---|---|---|
| `title` | ✔ | Searched |
| `body` | ✔ | Plain text / basic markdown. Searched. No rich editor, no media (T2-E) |
| `type` | ✔ | `Faq` \| `HelpArticle` \| `SolutionGuide` — the one-concept decision of T2-E |
| `visibility` | ✔ | `Public` (portal) \| `Internal` (staff only) |
| `isPublished` | ✔ | The intake requires publish/unpublish |
| `authorUserId` | ✔ | Authoring is an Administrator capability (A-4) |
| `createdAt` | ✔ | |
| `updatedAt` | ✔ | Articles are edited; the list shows recency |

**Relationships.** `User` 1 → many authored. Deliberately **no** relationship to `Ticket`:
suggested solutions are computed by keyword retrieval at read time (AD-13), not stored links.
**Ownership/scope.** Knowledge module. Organization-wide; `Internal` articles never leave staff
surfaces.
**Mutability.** Mutable. **No versioning** — explicitly out of scope (T2-E).
**Invariant.** An article that is `Internal` or unpublished must never appear in a portal read or a
customer search result, enforced server-side.

### 2.14 `AuditEntry`

**Purpose.** Requirements §10.4 — the security and administration record. T2-H,
[architecture.md](architecture.md) §2.4. **Separate from ticket history by decision AD-10.**

| Field | Req | Notes |
|---|---|---|
| `occurredAt` | ✔ | |
| `actorUserId` | ✖ | Null when no user could be resolved — a failed sign-in |
| `actorDescriptor` | ✖ | The submitted identifier when `actorUserId` is null, so a failed sign-in is still attributable. Required by "recorded actions: sign-in" in the `audit-configuration` intake |
| `action` | ✔ | e.g. `SignInSucceeded`, `SignInFailed`, `UserCreated`, `UserDeactivated`, `UserRoleChanged`, `UserDepartmentChanged`, **`UserEmailChanged`** (A-19 — see the note below), `TicketStatusChanged`, `TicketEscalated` |
| `targetType` | ✖ | e.g. `User`, `Ticket` |
| `targetId` | ✖ | |
| `outcome` | ✔ | `Success` \| `Failure` — the intake names outcome explicitly |

**Relationships.** `User` 0..1 (actor). Targets are referenced by type + id rather than by foreign
key, because the target may be any entity — a deliberate, contained exception to relational purity
that avoids one nullable FK column per auditable type.
**Ownership/scope.** Administration module. **Administrator-only read**, enforced server-side.
**Mutability.** **Append-only**, like `TicketActivity`, and for the same reason: no update or delete
service method exists.
**Why it is not merged with `TicketActivity`.** Different actors, different questions, different
visibility. `UserRoleChanged` has no ticket; `MessagePosted` is not a security event. Both remain
independently queryable (AD-10, `audit-configuration` AC). The two tables share a discipline, not a
schema.

**`UserEmailChanged` — what it records, and what it deliberately does not (A-19).** When a customer's
email change propagates to their linked portal login (§5 constraint 1a), that propagation is
recorded here: it changes a **sign-in identifier**, which is user administration of the kind T2-H and
the `audit-configuration` intake already require to be logged.

| Field | Value |
|---|---|
| `action` | `UserEmailChanged` |
| `actorUserId` | The **authenticated agent who issued the `PATCH`**, resolved from the request like every other entry. The `actorUserId` override exists for one case only — a successful sign-in, where the request is anonymous — and **is not used here** |
| `targetType` / `targetId` | `User`, and **the linked user's id** — not the customer's. The audited fact is that a login's identifier changed; the profile edit beside it is business data, not a security event, and is not audited |
| `outcome` | `Success`. **A rejected change writes nothing at all**, audit entry included, so no `Failure` row exists for this action — the same convention as every user-administration call site. Only a failed *sign-in* is recorded as `Failure` |
| `occurredAt` | Server clock |

**It does not record the old or the new address**, because this entity has **no value columns** —
exactly as `UserRoleChanged` records that a role changed without recording which roles. Adding
`oldValue`/`newValue` here would be a schema change **and** would copy a personal identifier into an
append-only log that is never deleted, so it is not done. `TicketActivity` (§2.7) carries value
columns; this table is not that table, and AD-10 is the reason.

**One entry, only on a real change.** No entry is written when the `PATCH` omits `email`, when the
new address equals the old one, when the customer has **no** linked login, or when the operation is
rejected. The entry is added to the same unit of work as the two row updates and commits with them
([architecture.md](architecture.md) §3), so an audited change and its record cannot come apart.

### 2.15 `CustomerFeedback`

**Purpose.** Requirements §8.5 — the **sole** CSAT input, feeding the §9.4 satisfaction metric.
T2-F, T2-G.

| Field | Req | Notes |
|---|---|---|
| `ticketId` | ✔ | **Unique** — one rating per ticket (T2-F AC) |
| `rating` | ✔ | An ordinal value. **The model fixes no range** — see OQ-1 below |
| `comment` | ✖ | The optional comment of T2-F |
| `submittedAt` | ✔ | |

**Relationships.** `Ticket` 1 → 0..1 feedback. The submitter is the ticket's customer, already
reachable through the ticket; no separate column.
**Ownership/scope.** **`Tickets` module** (DM-7). Written by the customer; read by managers in
reports. There is no `Portal` backend module — `customer-portal` is a front-end area
([architecture.md](architecture.md) §1).
**Mutability.** Write-once. Not editable, not resubmittable.
**Invariants.** One per ticket · offered only once a ticket reaches `Resolved` · **declining is a
normal outcome** (T2-F), so the absence of a row is meaningful and reporting must treat it as
"no response", not as a zero.

**OQ-1 · The rating scale is an open question, not a modelling assumption.**
[product-scope.md](product-scope.md) T2-F specifies "a one-question satisfaction rating with an
optional comment" and fixes **no scale**. Requirements §8.5 and §9.4 say nothing about one either.

Accordingly **this model encodes no range**. `rating` is an ordinal value whose permitted set is
undetermined; no minimum, maximum, or step is stated here, and none may be inferred from this
document into a validation rule, a check constraint, or a UI control.

The scale must be **decided before `portal-self-service` is implemented**, because it fixes three
things at once: what the portal renders, what the server validates, and what the §9.4 average
means. It is a product decision, not a modelling one — the natural candidates (a 1–5 ordinal, a
1–10 ordinal, or a binary thumbs up/down) differ in what they tell a manager, and nothing in the
sources chooses between them. Recorded in §8.

### 2.16 Explicitly not modelled

Absent by decision, each with the reason:

| Not an entity | Why |
|---|---|
| `Role` | Four fixed hierarchical roles, no role editor (A-4). An enum |
| `Category`, `Priority` | Configuration, not user-managed taxonomies (A-6, §6.3) |
| `SlaPolicy` | Per-priority hours are configuration; no rules engine (T2-D, §8) |
| `QuickReply` | Configuration — a canned-response library from config (T1-C, §6.3) |
| `Branding`, `Setting` | Configuration; **no configuration UI**, changing it is a redeploy (T2-I) |
| `Tenant`, `Organization` | Single-organization (A-2, T3-G). No tenant concept exists |
| `AiSuggestion` | No persistence requirement (DM-5) |
| `ChannelAdapterConfig`, `ErpMapping`, `Webhook`, `DeliveryReceipt` | No adapter that needs them is built (DM-6, T3-A, T3-D) |
| `TicketAssignment` | Field plus history (DM-2) |
| `CustomerInteraction` | The §1.3 timeline is a read projection, not a stored table (§2.4) |
| `TicketTag`, `TicketLink`, `TicketMerge`, parent/child | Merging, splitting and linking are out of scope (§8) |
| Ticket reference number | Nothing in the sources asks for a human-readable ticket number |
| `Company` / `Account` above `Customer` | Not in requirements §1; branch is the only grouping |
| Soft-delete columns, `RowVersion`, generic `CreatedBy/ModifiedBy` on every table | No requirement; §8 excludes soft-delete frameworks. Stamps exist only where a story reads them |

---

## 3. Entity list

| # | Entity | Module | Scope | Mutability | Primary source |
|---|---|---|---|---|---|
| 1 | `User` | Identity | Organization | Mutable | §10.1–10.2, T1-D, A-4, A-9 |
| 2 | `Department` | Organization | Organization | Mutable | §12, T1-E, A-2 |
| 3 | `Branch` | Organization | Organization | Mutable | §12, T2-K, A-2 |
| 4 | `Customer` | Customers | Organization | Mutable | §1, T1-A |
| 5 | `CustomerNote` | Customers | Staff-only | **Immutable** | §1.4, T1-A |
| 6 | `Ticket` | Tickets | **Department / customer** | Mutable | §2, T1-B, A-5, A-6 |
| 7 | `TicketActivity` | Tickets | Follows ticket | **Append-only** | §2.5, T1-B |
| 8 | `TicketMessage` | Tickets | Follows ticket | **Immutable** | §3.3, §3.5, T2-B |
| 9 | `TicketInternalNote` | Tickets | **Staff-only** | **Immutable** | §4.5, T2-C |
| 10 | `TicketTask` | Tickets | Staff-only | Mutable | §4.3, T2-C |
| 11 | `Attachment` | Customers/Tickets | Follows owner | Immutable | §1.4, T2-A |
| 12 | `Notification` | Sla | **Recipient-only** | `readAt` only | §5.4, T2-D, A-13 |
| 13 | `KnowledgeArticle` | Knowledge | Organization | Mutable | §6, §7.4, T2-E |
| 14 | `AuditEntry` | Administration | **Administrator-only** | **Append-only** | §10.4, T2-H |
| 15 | `CustomerFeedback` | Tickets | Customer / reports | Write-once | §8.5, §9.4, T2-F |

Fifteen entities. Four are append-only or immutable by construction; two more are effectively
write-once.

---

## 4. Relationship summary

```
        Branch ──1:*──▶ Customer ──1:0..1──▶ User (role = Customer)
           │                │
           │ 1:*            │ 1:*                      Department ──1:0..1──▶ User (manager)
           ▼                ▼                               │
        User (staff)     Ticket ◀────────1:*────────────────┘
      (role/dept/active        │
       authoritative)          ├──1:*──▶ TicketActivity ──0..1──▶ TicketMessage
           │                   │              (spine)   └─0..1──▶ TicketInternalNote
           │ 0..1 assignee     ├──1:*──▶ TicketMessage
           └───────────────────┤──1:*──▶ TicketInternalNote
                               ├──1:*──▶ TicketTask
                               ├──1:*──▶ Attachment   (or Customer ──1:*──▶ Attachment)
                               ├──1:*──▶ Notification ──*:1──▶ User (recipient)
                               └──1:0..1─▶ CustomerFeedback

        Customer ──1:*──▶ CustomerNote ──*:1──▶ User (author)
        KnowledgeArticle ──*:1──▶ User (author)        [no link to Ticket — retrieval at read time]
        AuditEntry ──*:0..1──▶ User (actor)            [target by type + id, not by FK]
```

| From | To | Cardinality | Required? | Note |
|---|---|---|---|---|
| `Customer` | `User` | 1 → 0..1 | Optional | Portal login; profile-only customers exist (DM-1) |
| `User` | `Department` | * → 0..1 | Required for staff | Forbidden for `Customer` role |
| `User` | `Branch` | * → 0..1 | Optional | Reporting only |
| `Customer` | `Branch` | * → 1 | Required | Reporting only |
| `Department` | `User` | 1 → 0..1 | Optional | Manager, the escalation recipient |
| `Ticket` | `Customer` | * → 1 | Required | |
| `Ticket` | `Department` | * → 1 | Required | **The authorization edge** |
| `Ticket` | `User` | * → 0..1 | Optional | Assignee; null while `New` |
| `Ticket` | `TicketActivity` | 1 → * | — | Append-only spine |
| `TicketActivity` | `TicketMessage` | 1 → 0..1 | Conditional | Set iff `MessagePosted` |
| `TicketActivity` | `TicketInternalNote` | 1 → 0..1 | Conditional | Set iff `InternalNotePosted` |
| `Ticket` | `CustomerFeedback` | 1 → 0..1 | Optional | Absence means "no response" |
| `Attachment` | `Ticket` / `Customer` | * → 1 | Exactly one | XOR owner |
| `Notification` | `User`, `Ticket` | * → 1, * → 1 | Required | |
| `KnowledgeArticle` | `Ticket` | — | **None** | Suggestions are computed, not stored |

**Note on the one relationship that does not exist.** `Ticket` has no `branchId`. A ticket's branch
is **derived** through `Ticket → Customer → Branch` — the reading the `departments-branches` intake
already uses when it speaks of "the customer's branch" in a ticket context. The absence is
load-bearing: it makes scoping by branch impossible by construction, while still answering every
branch filter the reports ask for. Full audit of the sources in §2.3.

---

## 5. Important constraints

**Identity and authorization**

1. `User.email` and `Customer.email` are each unique, case-insensitive.
1a. **A customer and their portal login hold the same address (A-19).** When `Customer.email`
    changes and a linked `User` exists, `User.email` is set to the same value **in the same unit of
    work** ([architecture.md](architecture.md) §3) — there is no committed state in which the two
    differ. Constraint 1 still applies to the propagated value across **all** users, staff included;
    a collision rejects the whole operation and writes neither row. A profile-only customer has no
    login to propagate to, which DM-1 makes the ordinary case.
1b. **A propagated login-email change is audited (A-19).** When — and only when — constraint 1a
    actually changes a `User.email`, one `AuditEntry` with `action = UserEmailChanged` is written
    against that user, in the **same unit of work** as the two row updates. Shape and rationale in
    §2.14. The customer-profile edit on its own is **not** audited: it is business data, not a
    security event (AD-10).
2. A `User` of role `Customer` has `customerId` set and `departmentId` null; a staff user is the
   reverse.
3. At most one `User` per `Customer`.
4. `User.role`, `User.departmentId` and `User.isActive` are **the authoritative values**, read per
   request ([architecture.md](architecture.md) §4.1.1). No token claim substitutes for them, and
   nothing in this model caches them.
5. Ticket visibility is decided by `Ticket.departmentId` (agents), `Ticket.customerId` via
   `User.customerId` (customers), or unrestricted (managers, administrators) — §4.3.
6. **Branch appears in no authorization predicate anywhere.**

**Ticket lifecycle**

7. Status follows the A-5 machine; illegal transitions are refused in the Domain layer.
8. `Closed` and `Cancelled` are terminal — no messages, notes or further transitions.
9. Escalation raises priority one level; Urgent stays Urgent; status is unchanged.
9a. An `Inbound` message on a `Pending` ticket transitions it to `Open` automatically, in the
    same transaction, recorded as a `StatusChanged` activity **attributed to the replying
    customer with `actorKind = User`** (A-5). From `Pending` only (§2.6 invariant 2b, §2.8).
10. `assignedUserId` must be an active staff user in the ticket's department.
11. `categoryCode` must match the configured list; an unknown value is rejected.
11a. For a customer-submitted ticket, `departmentId` is **derived from the category** through the
    configured mapping, at creation and before assignment (A-14). An agent creating a ticket may
    set it directly. Either way the column is stored, not computed on read.
11b. Which role may invoke which transition is fixed by **A-16**; the Domain enforces legality,
    the Application layer enforces authority. Closure is manual — nothing in this model schedules
    a transition to `Closed`. A customer may cancel **only while `status = New`** (A-16, A-18);
    agents, managers and administrators may cancel any non-terminal ticket.

**SLA**

12. Due timestamps are computed at creation from `createdAt` and the ticket's priority, and **a
    later priority change does not move them** (**A-20**, closing OQ-2 on 2026-08-30). The rule is
    asserted by A-20 rather than by this model, which stores the two timestamps and is compatible
    with either reading.
13. Breach flags latch — never reset.
14. `Pending` does not pause the clock (A-3).

**History, audit and visibility**

15. `TicketActivity` and `AuditEntry` are append-only: no update or delete service method exists.
16. `TicketMessage`, `TicketInternalNote` and `CustomerNote` are immutable once written.
17. Every message and every internal note has exactly one corresponding activity row (DM-4).
18. No customer-facing read touches `TicketInternalNote`, and no read returns an activity row whose
    `visibility` is `Internal`.
19. `Internal` or unpublished knowledge articles never reach a portal read.

**Other**

20. An `Attachment` has exactly one owner (ticket XOR customer) and respects the configured size cap.
21. `CustomerFeedback` is unique per ticket, write-once, and offered only after `Resolved`.
22. `Notification.readAt` transitions only null → timestamp.
23. `TicketTask.completedAt` is set exactly when `isDone` is true.

Constraints 2, 3, 10, 11, 17, 18, 19 and 21 are **cross-row or configuration-dependent rules
enforced in the Application or Domain layer**, not by database constraints — consistent with
[architecture.md](architecture.md) §2.1, which puts business rules in one place rather than
splitting them between code and schema.

---

## 6. Indexing strategy

Primary keys and foreign keys are indexed as a matter of course and are not listed. Everything
below exists because a named query in a story needs it — no speculative indexing.

| Index | Serves |
|---|---|
| `User(email)` **unique** | Sign-in lookup (A-9); duplicate rejection |
| `User(departmentId, isActive)` | Round-robin assignment candidates (T2-D); user administration filtered by department |
| `User(customerId)` **unique, non-null** | Per-request resolution of a portal login to its profile (§4.1.1); one login per customer |
| `Customer(email)` **unique** | A-10 identification; duplicate rejection |
| `Customer(branchId)` | Branch filtering in reports (T2-K, T2-G) |
| `Ticket(departmentId, status)` | The department-scoped queue and every §9.1 count |
| `Ticket(assignedUserId, status)` | The agent's own queue — the T1-C primary screen |
| `Ticket(customerId, createdAt)` | Portal ticket list (T2-F) and the customer interaction timeline (§1.3) |
| `Ticket(firstResponseDueAt)` and `Ticket(resolutionDueAt)`, restricted to non-terminal, non-breached rows | The SLA monitor's periodic sweep (AD-6). Without these it scans every ticket on every tick |
| `TicketActivity(ticketId, occurredAt)` | The history read and the timeline projection — the single most frequent read after the queue |
| `TicketMessage(ticketId, postedAt)` | Thread rendering |
| `TicketInternalNote(ticketId, createdAt)` | Staff thread rendering |
| `TicketTask(assignedUserId, isDone, dueAt)` | Open and overdue tasks on the dashboard (T2-C) |
| `Notification(recipientUserId, readAt)` | The list and the unread badge (A-13) |
| `AuditEntry(occurredAt)`, `AuditEntry(actorUserId)` | Administrator filtering by date range and actor (T2-H) |
| `CustomerFeedback(ticketId)` **unique** | Enforces one rating per ticket; feeds the §9.4 average |

**Knowledge-base search.** T2-E fixes search as the database's built-in text matching, and AD-13
excludes a search engine. At assessment data volumes a straightforward contains-match over `title`
and `body` is sufficient and needs no special index. SQL Server full-text indexing on those two
columns is the available upgrade if matching quality proves inadequate — it is a database feature,
not a new component, so taking it would not breach §8. The model requires nothing either way.

**Not indexed on purpose.** `Ticket.priority` and `Ticket.categoryCode` — they are low-cardinality
and always queried alongside `departmentId` or `status`, which the composite indexes already cover.
Reporting aggregates at this data volume do not justify more.

### 6.1 String columns — length, collation and index eligibility

> **Amendment, 2026-08-26.** This subsection is the one place where this document descends to a
> physical type, and it does so deliberately. The rest of the document remains conceptual and
> logical; see the amended scope note in the header and the §8 gate row.
>
> **Why it had to be decided here rather than left to implementation.** A unique index is a
> *logical* statement — this document makes it about `User.email`, `Customer.email`,
> `Department.name` and `Branch.name`. SQL Server cannot build one over `nvarchar(max)`, so
> declaring uniqueness silently obliges every implementer to pick a length. Left unstated, eighteen
> stories would pick eighteen different ones, and `Customer.email` and `User.email` — the same
> address, compared across two tables — could end up different widths. The convention below is
> therefore a consequence of decisions this document already made, not a new one.

**Every string column takes one of five tiers.** An implementer picks a tier, never a number.

| Tier | Type | Use | Index key? |
|---|---|---|---|
| **Code** | `nvarchar(64)` | A short token from a fixed enumeration or a configured list, persisted as a stable string code (api-design §2) — and compact identifier-like values | ✅ Yes |
| **Name** | `nvarchar(200)` | A single-line human-readable label, title or subject | ✅ Yes |
| **Email** | `nvarchar(256)` | An email address. RFC 5321 caps a forward path at 254 characters; 256 is that plus headroom | ✅ Yes |
| **Line** | `nvarchar(512)` | Single-line free text too long for a name — file names, storage paths, opaque hashes | ⚠️ Avoid |
| **Text** | `nvarchar(max)` | Multi-line authored content | ❌ **Never** |

**Lengths are in characters, not bytes.** Every string column is `NVARCHAR`: the product stores
Arabic and English user-generated content side by side (A-11), and user content is never
transliterated or normalized.

#### The index-key rule

**A column in an index key must be `Code`, `Name` or `Email`.**

SQL Server limits a non-clustered index key to **1700 bytes**, and `NVARCHAR` costs 2 bytes per
character — so a single-column key tops out at **850 characters**, and `nvarchar(max)` cannot be an
index key at all. Composite keys must fit the same budget across all their columns.

Checked against every index this model requires — the table above plus the four unique constraints
declared in §2:

| Index | Key bytes |
|---|---|
| `User(email)` unique · `Customer(email)` unique | **512** — the widest in the model |
| `Department(name)` unique · `Branch(name)` unique (§2.2, §2.3) | 400 |
| `Ticket(departmentId, status)` · `Ticket(assignedUserId, status)` | 144 |
| Every other index (all keys are `uniqueidentifier`, `datetimeoffset` or `bit`) | ≤ 27 |

**The widest key uses 30% of the 1700-byte budget**, and `Ticket.status` — a `Code` at 128 bytes —
is the only string that ever appears inside a composite key. A future index that would not fit is a
signal that the wrong column is being indexed, not that a tier needs widening.

`Text` columns are searched but never indexed: T2-E fixes knowledge-base search as the database's
built-in matching over `KnowledgeArticle.title` and `.body`, which is a scan, not an index seek.

#### Collation

The two case-insensitive unique columns — `User.email` (A-9) and `Customer.email` (A-10) — declare
**`SQL_Latin1_General_CP1_CI_AS`** explicitly on the column.

The SQL Server 2022 image's default collation is already case-insensitive, so this changes no
behaviour today. It is declared anyway because *"two addresses differing only in case are the same
address"* is a **product rule**, and a product rule must not depend on a server-level default that
a different deployment could set otherwise. No other column declares a collation.

#### Tier per field

Every string field in §2, so none is left to be invented:

| Tier | Fields |
|---|---|
| **Code** (64) | `User.role` · `Department`/`Branch` — none · `Customer.phone` · `Ticket.categoryCode`, `.priority`, `.status` · `TicketActivity.activityType`, `.actorKind`, `.visibility` · `TicketMessage.direction`, `.channel` · `Notification.type` · `KnowledgeArticle.type`, `.visibility` · `AuditEntry.action`, `.targetType`, `.outcome` |
| **Name** (200) | `User.displayName` · **`Department.name`** · **`Branch.name`** · `Customer.fullName`, `.externalReference` · `Ticket.subject` · `TicketActivity.oldValue`, `.newValue` · `TicketTask.title` · `Attachment.contentType` · `KnowledgeArticle.title` |
| **Email** (256) | **`User.email`** · **`Customer.email`** · `AuditEntry.actorDescriptor` |
| **Line** (512) | `User.passwordHash` · `Attachment.fileName`, `.storagePath` |
| **Text** (max) | `CustomerNote.body` · `Ticket.description` · `TicketMessage.body` · `TicketInternalNote.body` · `KnowledgeArticle.body` · `CustomerFeedback.comment` |

Bold marks the four columns carrying a unique index.

Three entries earn a word of explanation:

- **`Attachment.contentType` is `Name`, not `Code`.** Real MIME types exceed 64 characters —
  `application/vnd.openxmlformats-officedocument.wordprocessingml.document` is 73 — so the `Code`
  tier would reject a legitimate `.docx` upload.
- **`AuditEntry.actorDescriptor` is `Email`** because it holds the submitted identifier of a failed
  sign-in (§2.14), which is an email address in every flow this product has. It is nevertheless
  **unvalidated client input**, so the audit recorder **truncates to fit rather than throwing** — a
  sign-in attempt with an absurd identifier must still be recorded, since recording it is the whole
  point of the column.
- **`User.passwordHash` is `Line`.** ASP.NET Core's `PasswordHasher` v3 format is 84 characters
  today; the tier leaves room for a future algorithm without a migration. It is never indexed and
  never leaves the server.

**A field that seems to fit no tier is a modelling question, not a licence to invent a number.**
Raise it against this section.

---

## 7. Traceability summary

**Requirement → entity**

| Req | Entities |
|---|---|
| §1 Customer profiles, contact details, notes, attachments | `Customer`, `CustomerNote`, `Attachment` |
| §1.3 Interaction history | *No entity* — read projection over `Ticket` + `TicketActivity` |
| §2.1–2.3 Create, track, categorize, assign | `Ticket` |
| §2.4 Status and escalation | `Ticket.status`, `TicketActivity` (`StatusChanged`, `Escalated`) |
| §2.5 Ticket history | `TicketActivity` |
| §3.3, §3.5 Live chat (portal messaging), web forms | `TicketMessage` |
| §3.1, §3.2, §3.4 Email, WhatsApp, SMS | *No entity* — `TicketMessage.channel` is the seam (DM-6) |
| §4.1, §4.2, §4.4 Queue, customer context, quick replies | `Ticket` (+ configuration for quick replies) |
| §4.3 Tasks and reminders | `TicketTask` |
| §4.5 Team collaboration | `TicketInternalNote` |
| §5.1 SLA targets | `Ticket` SLA fields + configuration (DM-3) |
| §5.2 Automatic assignment | `Ticket.assignedUserId` + `User(departmentId, isActive)` |
| §5.3 Escalation rules | `Ticket` breach flags, `TicketActivity` |
| §5.4 Alerts and notifications | `Notification` |
| §6 FAQs, articles, guides, search | `KnowledgeArticle` |
| §7.1, §7.2 Summaries, suggested replies | *No entity* (DM-5) |
| §7.3 Automatic categorization | `TicketActivity` AI types (DM-5) |
| §7.4 Suggested solutions | `KnowledgeArticle`, retrieved at read time |
| §7.5 AI chatbot | *Not built* (T3-C) |
| §8.1–8.4 Portal submit, track, history, FAQs | `Ticket`, `TicketMessage`, `KnowledgeArticle` |
| §8.5 Feedback | `CustomerFeedback` |
| §9.1–9.5 Reports | *No entity* — aggregates over `Ticket`, `TicketActivity`, `CustomerFeedback`, `User` |
| §10.1–10.3 Users, roles, permissions | `User`, `Department` |
| §10.4 Audit logs | `AuditEntry` |
| §10.5 System configuration | *No entity* — configuration (T2-I) |
| §11.1 APIs | *No entity* |
| §11.2, §11.4 ERP, external systems | `Customer.externalReference` only (DM-6) |
| §12 Arabic/English, branding, mobile | *No entity* — front end and configuration |
| §12 Multi-department | `Department`, `Ticket.departmentId`, `User.departmentId` |
| §12 Multi-branch | `Branch`, `Customer.branchId`, `User.branchId` |

**Assumption → how the model expresses it**

| Assumption | Expression |
|---|---|
| A-2 Department is the boundary, branch is reporting | `Ticket.departmentId` required; **`Ticket` has no branch** (§2.3) |
| A-3 24/7 clock, per-priority targets, no pause | Due timestamps from `createdAt`; targets in configuration; no pause field exists |
| A-4 Four fixed roles | `User.role` enum; no role table |
| A-5 Six statuses, escalation is an action | `Ticket.status` enum; `Escalated` is an activity type, not a status |
| A-6 Fixed categories and priorities | `priority` enum; `categoryCode` validated against configuration |
| A-7, A-8 AI advisory, offline-capable | No AI entity; suggestions recorded only as history (DM-5) |
| A-9 Email + password, no anonymous submission | `User.email` + `passwordHash`; every ticket has a `createdByUserId` |
| A-10 Customer identified by unique email | `Customer(email)` unique; no merge tooling |
| A-12 Single database | One model, one schema, no per-module contexts |
| A-13 In-app notifications only | `Notification` with four types; no delivery or preference tables |

**Architectural decision → model consequence**

| Decision | Consequence |
|---|---|
| AD-3 No repository layer | Entities are plain; no aggregate-root scaffolding |
| AD-4 Domain free of EF attributes | This document describes shape, not mapping |
| AD-5 Explicit access scoping | `Ticket.departmentId` and `User.customerId` are the two columns the scoping helper reads |
| AD-6 Periodic SLA sweep | The two filtered due-date indexes exist for it |
| AD-10 History separate from audit | `TicketActivity` and `AuditEntry` are separate tables (§2.14) |
| AD-13 Database keyword search | `KnowledgeArticle.title` / `.body`; no vector or search-engine structures |
| AD-15 Authoritative user record | `User.role` / `.departmentId` / `.isActive` are the read-per-request source (§5, constraint 4) |

**Story → entities it introduces**

`auth-and-roles` → `User` · `departments-branches` → `Department`, `Branch` ·
`customer-records` → `Customer`, `CustomerNote`, `Attachment` · `ticket-core` → `Ticket` ·
`ticket-lifecycle` → `TicketActivity` · `ticket-intake-messaging` → `TicketMessage` ·
`tasks-internal-notes` → `TicketTask`, `TicketInternalNote` · `sla-routing-escalation` →
`Ticket` SLA fields, `Notification` · `kb-articles-search` → `KnowledgeArticle` ·
`portal-self-service` → `CustomerFeedback` · `audit-configuration` → `AuditEntry` ·
`agent-dashboard`, `management-dashboard`, `i18n-responsive-branding`, `ai-service-seam`,
`ai-ticket-assists`, `solution-skeleton`, `channel-erp-adapters` → **no new entity** (they read
existing ones, or persist nothing).

---

## 8. Stage 6 gate check

Gate 6 → 7 from [sdd-workflow.md](sdd-workflow.md) §4: *"`docs/data-model.md` covers every entity
implied by the T1/T2 stories, with the A-5 status set and A-2 department/branch asymmetry
represented. Conceptual and logical only — migrations are written during implementation, not during
design."*

| Condition | Status |
|---|---|
| Every T1/T2 story's entities covered | ✅ Eleven stories introduce entities; seven introduce none, each named in §7 with the reason |
| A-5 status set represented | ✅ Six statuses on `Ticket.status`; escalation modelled as an activity, not a status (§2.6, §2.7) |
| A-2 department/branch asymmetry represented | ✅ `Ticket.departmentId` required and authorization-bearing; `Ticket` has no branch at all (§2.3, §4) |
| Conceptual and logical only | ✅ No SQL, no EF configuration, no migration, no DDL. **One amendment (2026-08-26):** §6.1 fixes string lengths and collation, because the unique indexes this document declares cannot be built over an unbounded string. Column widths only — still no DDL, and §2 is untouched |
| Ticket history separate from audit log | ✅ `TicketActivity` and `AuditEntry`, §2.14 states why (AD-10) |
| Department-based authorization preserved | ✅ §5 constraints 4–5; the two columns the scoping helper reads named in §7 |
| Branch is reporting-only | ✅ §2.3 invariant; no `Ticket.branchId`; derived `Ticket → Customer → Branch` with a source audit confirming no requirement asks for a ticket-level branch (§2.3); §5 constraint 6 |
| Authoritative user record preserved | ✅ §2.1 and §5 constraint 4 — role, department and active status live on `User` |
| JWT claims not authoritative | ✅ Nothing in the model stores or caches a claim; §5 constraint 4 states it |
| No excluded patterns introduced | ✅ No CQRS, event sourcing, repositories, soft-delete framework, or separate read models. The §1.3 timeline and all reports are computed, not materialized |

**Gate 6 → 7 is met.**

**Open questions — decisions this model deliberately does not make**

Each must be answered before the story named in its last column is implemented. None is a modelling
detail; all three are product or business rules that the sources leave undetermined. None has been
pre-empted by an assumption baked into the model.

| # | Question | What the sources say | This model's position | Blocks |
|---|---|---|---|---|
| **OQ-1** | What is the CSAT rating scale? | T2-F says "a one-question satisfaction rating"; no scale anywhere | Stores an ordinal; **encodes no range** — no min, max, or step (§2.15) | `portal-self-service`, and the §9.4 average in `management-dashboard` |
| ~~**OQ-2**~~ | ~~When priority changes, do the SLA due dates recompute or stay frozen?~~ | A-3 fixes per-priority targets and a `createdAt` origin; silent on later changes, which T2-D escalation causes routinely | Stores both timestamps; **compatible with either rule and asserts neither** (§2.6 invariant 6) — **unchanged by the answer** | **✅ Closed 2026-08-30 by A-20 — they stay frozen.** See the resolved list below |
| ~~**OQ-3**~~ | ~~Who is notified on breach when a department has no manager?~~ | T2-D and the intake both say "notify the department manager"; neither covers its absence | ✅ **Closed 2026-08-31 by A-21** — the notification escalates to the next authority level; `managerUserId` stays optional and **no model change was needed** (§2.2). Breach flag and priority raise still occur | ~~`sla-routing-escalation` |

**Resolved and closed since the first draft of this document:**

- **OQ-3 — the escalation recipient when a department has no manager** (raised 2026-08-24 by §2.2;
  resolved 2026-08-31). **The notification escalates to the next authority level** (**A-21**): the
  department's manager when set and still eligible, otherwise every active `Manager`, otherwise
  every active `Administrator`, otherwise nobody — and **the escalation itself is never blocked**.
  The rejected alternatives were notifying nobody (it lets the one automated safety net T2-D
  provides fail exactly when a department is unstaffed at the top) and making `managerUserId`
  required (a schema and contract change to avoid a policy decision, contradicting this section's
  own reason for optionality). **Model consequence: none** — §2.2 keeps the field optional and
  §2.12 is untouched, which is why it could stay open this long. The cascade climbs to `Manager` and
  `Administrator` only, both of which already hold cross-department authority (A-4, A-16), so a
  `Notification`'s `ticketId` is always readable by its recipient and nothing leaks across the
  boundary AP-4 protects.
- **OQ-2 — the SLA due timestamps on a priority change** (raised 2026-08-24 by §2.6 invariant 6;
  resolved 2026-08-30). **They freeze** (**A-20**): both timestamps are computed once at creation
  and a later priority change — agent `PATCH`, manual escalation or automatic breach escalation —
  leaves them exactly as they are. The alternative, recomputing from `createdAt` with the new
  priority's hours, was rejected because it lets an escalation tighten a deadline retroactively and
  breach a ticket as a consequence of the escalation its own breach triggered. **Model consequence:
  none** — §2.6 stores both timestamps and asserts no rule about them, which is precisely why it
  could stay open this long; constraint 12 and invariant 6 now cite A-20 rather than the question.
  [product-scope.md](product-scope.md) §9 question 5 — real SLA policy — is its parent and **stays
  open**.
- **Whether a ticket needs its own branch relationship.** It does not — a ticket's branch is derived
  `Ticket → Customer → Branch`, no source requires otherwise, and §2.3 records the full audit.
- **OQ-5 — a customer's email versus their portal sign-in** (raised 2026-08-25 by
  [api-design.md](api-design.md) §5.5's N-2 correction; resolved 2026-08-27). **They are one
  address** (A-19): changing `Customer.email` sets the linked `User.email` to the same value,
  atomically, with constraint 1 still applied to the new value across all users. Model consequence:
  **new constraint 1a**, and a matching invariant on §2.4. The alternative — letting them diverge —
  would have given one person two identifying addresses, which A-10 does not admit, with no
  reconciliation path since A-9 excludes account recovery.
- **OQ-4 — the customer cancellation window** (raised and resolved 2026-08-24). **Assignment is not
  the start of work** (A-18): a ticket may be assigned while still `New`, and `New → Open` is an
  agent deliberately starting work. The customer's window therefore lasts from creation until an
  agent picks the ticket up. Model consequence: `status` and `assignedUserId` are independent
  (§2.6 invariant 2a).

**Next stage:** 7 — API Design (`docs/api-design.md`). Not started.
