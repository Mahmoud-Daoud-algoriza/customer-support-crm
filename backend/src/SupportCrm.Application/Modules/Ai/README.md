# Ai

> Source: `docs/data-model.md` §3, DM-5 · `docs/architecture.md` §1

**No entity.** AI is advisory and stateless: suggestions are recorded only as `TicketActivity`
history, never as their own table (DM-5, AD-12).

**Owning stories:** `ai-assist/ai-service-seam` (Story 10) defines the seam interface here;
`ai-assist/ai-ticket-assists` (Story 11) adds the three assists. The seam is owned by Application
and implemented in `Infrastructure/Seams/Ai/` (AD-11). The default provider is `fake` and the
system must run with no credentials (architecture §6.3).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
