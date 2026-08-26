# Integrations

> Source: `docs/data-model.md` §3, DM-6 · `docs/architecture.md` §1, §5

**No entity.** `Customer.externalReference` is the only persisted trace of an external system
(DM-6). Channels (email, WhatsApp, SMS) appear only as `TicketMessage.channel`.

**Owning stories:** `integration-seams/channel-erp-adapters` (Story 18). Seam interfaces are owned
by Application and implemented in `Infrastructure/Seams/{Channels,Erp}/` (AD-11). Each seam records
what a real implementation would have to add, so "designed for, not delivered" stays verifiable
(architecture §5).

A module is a folder with a public service surface — not an assembly and not a deployable
(architecture §1).
