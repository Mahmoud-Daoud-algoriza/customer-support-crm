using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;
using SupportCrm.Tests.Tickets;

namespace SupportCrm.Tests.Administration;

/// <summary>
/// The AD-10 assertion the intake requires: the audit log and ticket history remain
/// <b>independently queryable</b>, and <b>neither is derived from the other</b>
/// (docs/architecture.md §2.4, §2.5).
/// <para>
/// Reuses <see cref="TicketApiFixture"/> — the same world Story 05/06/07's ticket tests run
/// against — because generating one row of each kind means generating one real ticket lifecycle
/// event and one real message, not inventing a second fixture for it.
/// </para>
/// </summary>
public sealed class AuditAndHistoryAreSeparateTests(TicketApiFixture fixture)
    : IClassFixture<TicketApiFixture>
{
    /// <summary>
    /// Plan test 15 — a <c>UserRoleChanged</c> audit entry has no ticket at all: its target is a
    /// <c>User</c>, and no <c>TicketActivity</c> row references that id in any of its own Guid
    /// columns (<c>TicketId</c>, <c>MessageId</c>, <c>InternalNoteId</c>).
    /// </summary>
    [Fact]
    public async Task A_UserRoleChanged_audit_entry_has_no_corresponding_ticket_activity_row()
    {
        var adminId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Administrator, "ad10.admin@test.local");
        var subjectId = await fixture.Factory.AddStaffUserAsync(UserRole.Agent, "ad10.subject@test.local");

        var patch = await fixture.Factory.CreateClientFor(adminId)
            .PatchAsJsonAsync($"/api/v1/users/{subjectId}", new { role = "Manager" });
        patch.EnsureSuccessStatusCode();

        var hasActivity = await fixture.Factory.WithDbAsync(db =>
            db.TicketActivities.AsNoTracking().AnyAsync(a =>
                a.TicketId == subjectId || a.MessageId == subjectId || a.InternalNoteId == subjectId));

        Assert.False(hasActivity);
    }

    /// <summary>
    /// Plan test 16 — a <c>MessagePosted</c> activity row has no corresponding <c>AuditEntry</c>:
    /// messaging is business data, not a security event, and <c>MessagePosted</c> is not one of the
    /// action codes <c>AuditAction</c> defines (docs/data-model.md §2.14).
    /// </summary>
    [Fact]
    public async Task A_MessagePosted_activity_row_has_no_corresponding_audit_entry()
    {
        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var created = await agent.PostAsJsonAsync("/api/v1/tickets", new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "AD-10 separation",
            description = "Proving the two logs stay apart.",
            categoryCode = "billing",
            priority = "Medium",
        });
        created.EnsureSuccessStatusCode();
        var ticketId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var messaged = await agent.PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/messages", new { body = "A reply, for the activity row." });
        messaged.EnsureSuccessStatusCode();
        var messageId = (await messaged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var hasAuditEntry = await fixture.Factory.WithDbAsync(db =>
            db.AuditEntries.AsNoTracking().AnyAsync(a =>
                a.Action == "MessagePosted" || a.TargetId == messageId));

        Assert.False(hasAuditEntry);
    }

    /// <summary>
    /// Plan test 17 — both stay independently queryable. The audit log carries the
    /// <c>UserRoleChanged</c> row and knows nothing of the ticket; the ticket's activity carries the
    /// <c>MessagePosted</c> row and knows nothing of the audit log. Neither response's shape leaks
    /// the other's fields — an <c>AuditEntry</c> has no <c>activityType</c>, and a
    /// <c>TicketActivity</c> has no <c>outcome</c>.
    /// </summary>
    [Fact]
    public async Task Audit_and_ticket_activity_are_read_independently_with_no_shared_shape()
    {
        var adminId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Administrator, "ad10.independent.admin@test.local");
        var subjectId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Agent, "ad10.independent.subject@test.local");

        (await fixture.Factory.CreateClientFor(adminId)
            .PatchAsJsonAsync($"/api/v1/users/{subjectId}", new { role = "Manager" }))
            .EnsureSuccessStatusCode();

        var agent = fixture.Factory.CreateClientFor(fixture.BillingAgentId);

        var created = await agent.PostAsJsonAsync("/api/v1/tickets", new
        {
            customerId = fixture.HeadOfficeCustomerId,
            subject = "AD-10 independent reads",
            description = "Proving neither read joins the other's table.",
            categoryCode = "billing",
            priority = "Medium",
        });
        created.EnsureSuccessStatusCode();
        var ticketId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await agent.PostAsJsonAsync($"/api/v1/tickets/{ticketId}/messages", new { body = "Reply." }))
            .EnsureSuccessStatusCode();

        var auditResponse = await fixture.Factory.CreateClientFor(adminId)
            .GetAsync("/api/v1/audit?action=UserRoleChanged");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditItems = (await auditResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(auditItems, e => e.GetProperty("targetId").GetGuid() == subjectId);
        Assert.All(auditItems, e => Assert.False(e.TryGetProperty("activityType", out _)));

        var activityResponse = await agent.GetAsync($"/api/v1/tickets/{ticketId}/activity");
        Assert.Equal(HttpStatusCode.OK, activityResponse.StatusCode);
        var activityItems = (await activityResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().ToArray();

        Assert.Contains(activityItems, e => e.GetProperty("activityType").GetString() == "MessagePosted");
        Assert.All(activityItems, e => Assert.False(e.TryGetProperty("outcome", out _)));
    }
}
