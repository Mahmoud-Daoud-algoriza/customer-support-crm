[← Back to PROJECT-PROGRESS.md](../PROJECT-PROGRESS.md)

### 1.1 How the 35% is calculated

Two weighted tracks. The weighting is a **stated planning convention, not a measurement** — it is
recorded here so the number is reproducible rather than invented.

| Track | Contents | Weight | Complete | Contribution |
|---|---|---|---|---|
| **Design & planning** | Tracker rows 1–9 (§2), equally weighted at 3.89% each | 35% | **9 of 9 rows** | 9 ÷ 9 × 35 = **35.0%** |
| **Delivery** | Row 10 — the 18 stories implemented *and* verified | 65% | **10 of 18** | 10 ÷ 18 × 65 = **36.1%** |
| | | **100%** | | **= 71.1%** |

**Story 15 does NOT count yet either, for the same reason as story 14.** All six plan tasks are
implemented and its backend, data-layer and live-stack checks passed on 2026-09-06 — but the
front-end screen checks of the plan's verification step 8 could not be run (no browser-driving
capability), so they are recorded as **outstanding** in §8. The delivery track therefore stays at
**10 of 18** and the total at **71.1%**. **Covering the outstanding UI evidence for both stories
moves the track to 12 of 18 → 12 ÷ 18 × 65 + 35 = 78.3%.**

*(Story 15 has **no** blocked criterion. **PF-4 / S9-9 was closed before implementation began** —
R-18 — so unlike story 14 there is no unbuilt task and no throwing method; the only thing between
story 15 and Verified is the browser pass.)*

**Story 14 does NOT count yet, and the rule is why.** *"A story counts only when it is implemented
**and** verified in full"* — story 14 is implemented (plan tasks 1–7, 9, 10) and its backend,
data-layer and live-stack checks passed on 2026-09-06, but **the front-end screen checks of the
plan's verification step 7 could not be run** (no browser-driving capability in that task) and are
recorded as **outstanding** in §8. A story with outstanding verification evidence is not verified in
full, so the delivery track stays at **10 of 18** and the total stays at **71.1%**. **When the UI
checks are covered the track moves to 11 of 18 → 11 ÷ 18 × 65 + 35 = 74.7%**, and not before.

*(Plan task 8 — the dashboard task region — is separately blocked on **S9-1** and unbuilt. That is an
open acceptance criterion, not an incomplete implementation of an approved decision: the plan
directs that the region not be built until the user chooses Option A or Option B.)*

**Story 13 is the tenth.** All twelve of its plan tasks — the `CustomerFeedback` entity and its
migration, the two Application services, the six endpoints that complete the eleven-endpoint portal
path space, the seeder rows, the sixteen named tests and the five front-end surfaces — are
implemented and verified, so the delivery track moves from 9 to 10 and the total from 67.5% to
**71.1%**. Like stories 05, 06, 07 and 12 it was delivered **whole rather than in slices**.

**Story 12 is the ninth.** All eleven of its plan tasks — the entity and its migration, the search
expression, the three services, the eight endpoints, the seeder, the twelve named tests and the four
front-end surfaces — are implemented and verified, so the delivery track moves from 8 to 9 and the
total from 63.9% to **67.5%**. Like stories 05, 06 and 07 it was delivered **whole rather than in
slices**.

**A story counts only when it is implemented *and* verified in full.** Story 07 now does: all
**ten** plan tasks are implemented and verified, so the delivery track moves from 6 to 7 and the
total from 56.7% to **60.3%**. Like stories 05 and 06 it was delivered **whole rather than in
slices**, so the counting convention that made story 04 sit at zero through five slices did not bite
here either. *(Story 06 moved it from 5 to 6 and 53.1% to 56.7% on the same basis.)*

**Seven of eighteen, and the seventh is the one the remaining stories read from.** Stories 05 and 06
are the two the assessment's core loop rests on, and both are **T1 and uncuttable**; **story 07 is
T2 and is cut last among T2 items**, because the portal (13), the agent dashboard's quick replies
(08), the AI assists that summarize a thread (11) and the channel adapters (18) all consume the
message model it introduces. What remains is broader but shallower: the portal screens, the
dashboards, the KB, the AI seam and the adapters.

**Stories 04, 05 and 06 are not re-litigated here.** Story 04 counted from 2026-08-30 (fourteen
plan tasks, six slices verified), story 05 from 2026-08-31 (thirteen plan tasks) and story 06 from
2026-08-31 (eleven plan tasks).

**Qualifications on that count, stated so the number can be argued with rather than trusted.**
**Story 04's two deferred Done Criteria are now both discharged.** The **ticket** half of the
attachment criterion was published by story 05 (**S9-2** closed), and **the interaction timeline is
populated by story 06** — story 04's last outstanding line, closed by the story its plan named.
**Story 06 carries one qualification of its own, and it is a delivery gap rather than a behaviour
gap:** A-16's customer column cannot be exercised by a customer until **Story 13** publishes the
portal transition route, because no endpoint in this story admits a `Customer` (finding **I-23**).
The rule itself is proven at the authority layer and on the client. **I-16 and I-12 are unchanged and
still open** on the same footing as before. **If the user judges that I-23
should hold story 06 open, the delivery count returns to 6 of 18 and the total to 56.7%** — and the
same standing offer on I-16 / story 05 and I-12 / story 04 is unchanged.

**Story 07 carries one qualification, and it is a product question rather than a gap.** No approved
document states the **priority** a customer-submitted ticket is created with: `Ticket.priority` is
required and both SLA timestamps are computed from it at creation (A-3), while A-6 says priority is
*"set by the agent or by the AI suggestion"* and A-17 forbids `isUrgent` from setting it.
`Medium` was implemented as the smallest defensible reading and recorded as **I-25**. Every Done
Criterion of the story is met either way; **if the user answers differently, the change is one
constant and its test**, and the count does not move.

**Story 16 now counts, and it is the eighth.** Part A alone (verified 2026-08-27) did **not** move
this count — *"a story counts only when it is implemented and verified in full"* — but **Part B**
(this task, verified 2026-09-01) closes it: `GET /audit`, the audit screen and the read-only
configuration view are all in, and the story's own plan states Part B *"is the last administrative
surface"*, so nothing further is deferred. The delivery track moves from 7 to 8 and the total from
60.3% to **63.9%**.

**A larger inconsistency was found while recalculating this, and it predates this task — it is
disclosed here, not corrected.** §10 below already carries full narratives for **stories 08, 09, 10
and 11**, each headed *"is complete — … implemented and verified 2026-08-31"*, with suite counts
(330 → 350 → 364 → 373 passing, 41 → 46 → 51 front-end specs), findings **I-28 through I-34**, and
descriptions of live verification against real SQL Server. **This task's fresh measurements corroborate
those counts exactly**: the backend suite measured **385** passing in this task is **373 (§10's story
11 figure) + 12 (this slice's own two new test files)**, and the front-end **51** measured is **51
(§10's story 11 figure) + 0 (this slice added no spec)** — so the underlying work §10 describes is, at
minimum, really present in the working tree and really adds up. **But none of it reached §1's
headline, §1.1's count, §3's story table (which still reads `Not Started` for all four) or §8's
evidence table (which has no row for any of them)** — and this document's own rule is *"never mark
Verified without evidence named in §8."* **This row's *8 of 18* therefore counts only story 16** on
top of the seven §3 already had verified before this task; it does **not** add stories 08–11, because
doing so here would be marking them Verified on the strength of §10's prose rather than of §8
evidence gathered in this task, which is exactly what the accuracy rules forbid. **If §10 is right,
the true count is 12 of 18 (85.9%), not 8 of 18 (63.9%)** — reconciling §1, §1.1, §3 and §8 for
stories 08–11 is a real and apparently large backlog item, outside Story 16 Part B's scope, and is
not attempted here.

**The design-and-planning track is fully consumed.** Every remaining percentage point is delivery.
This is the point the weighting was chosen to make honest: **a fully planned project with no running
code is 35% done, not 90%** — and one enabling story that carries no business behaviour moves it by
exactly one eighteenth of the delivery track, not more.

**Why 35/65.** [product-scope.md](product-scope.md) §10 defines done in five items, four of which
concern running software. The SDD chain is the method; the working system is the deliverable, so
delivery carries the larger weight. A design-only project is not most of the way finished.

**Recalculate** whenever a row in §2 completes or a story reaches Verified in §3.

