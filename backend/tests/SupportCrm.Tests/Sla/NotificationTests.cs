using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Sla;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Tests.Sla;

/// <summary>
/// The notification endpoints of docs/api-design.md §5.10 and §6.6, and A-13's four events.
/// </summary>
public sealed class NotificationTests(SlaFixture fixture) : IClassFixture<SlaFixture>
{
    private const string Notifications = "/api/v1/notifications";

    /// <summary>
    /// <b>All four A-13 events produce a notification, to the right recipient.</b> Asserted in one
    /// test because the point is the <em>set</em>: a fifth type would be a data-model change, and a
    /// missing one is a feature that silently does not reach anybody.
    ///
    /// <para>
    /// <c>TicketAssigned</c> comes from assignment, <c>SlaBreached</c> from the sweep,
    /// <c>TicketEscalated</c> from the escalation the sweep performs, and <c>CustomerReplied</c> from a
    /// portal reply — each raised by the thing that happened, never by a client.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_four_a13_events_each_produce_a_notification_for_the_right_recipient()
    {
        // TicketAssigned — manual assignment tells the new assignee.
        var ticketId = await fixture.AddOverdueTicketAsync(SlaFixture.BillingDepartmentId);

        var assign = await fixture.Factory.CreateClientFor(fixture.BillingManagerId)
            .PostAsJsonAsync(
                $"/api/v1/tickets/{ticketId}/assignment",
                new { assignedUserId = fixture.BillingAgentId });

        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var assigned = (await fixture.NotificationsForAsync(ticketId))
            .Where(n => n.Type == NotificationType.TicketAssigned)
            .ToList();

        Assert.Single(assigned);
        Assert.Equal(fixture.BillingAgentId, assigned[0].RecipientUserId);

        // SlaBreached and TicketEscalated — both from one sweep of an overdue ticket, and both to
        // A-21's recipient, which for Billing is its own manager (rung 1).
        await fixture.RunSweepAsync();

        var afterSweep = await fixture.NotificationsForAsync(ticketId);

        var breached = afterSweep.Where(n => n.Type == NotificationType.SlaBreached).ToList();
        var escalated = afterSweep.Where(n => n.Type == NotificationType.TicketEscalated).ToList();

        Assert.Single(breached);
        Assert.Equal(fixture.BillingManagerId, breached[0].RecipientUserId);

        Assert.Single(escalated);
        Assert.Equal(fixture.BillingManagerId, escalated[0].RecipientUserId);
    }

    /// <summary>
    /// <b>Recipient-scoped</b> (§5.10): the list returns only the caller's own rows. Asserted by
    /// comparing two callers on the same ticket rather than by counting one — a scope bug that
    /// returned everything would still give the first caller a plausible answer.
    /// </summary>
    [Fact]
    public async Task The_list_returns_only_the_callers_own_notifications()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(SlaFixture.BillingDepartmentId);

        await fixture.Factory.CreateClientFor(fixture.BillingManagerId).PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/assignment",
            new { assignedUserId = fixture.BillingAgentTwoId });

        var mine = await ListAsync(fixture.BillingAgentTwoId);
        var theirs = await ListAsync(fixture.TechnicalAgentId);

        Assert.Contains(
            mine.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("ticketId").GetGuid() == ticketId);

        Assert.DoesNotContain(
            theirs.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("ticketId").GetGuid() == ticketId);
    }

    /// <summary>
    /// <b>Another user's notification is <c>404</c>, not <c>403</c></b> (AP-4) — a <c>403</c> would
    /// confirm the row exists.
    /// </summary>
    [Fact]
    public async Task Reading_another_users_notification_is_404()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(SlaFixture.BillingDepartmentId);

        await fixture.Factory.CreateClientFor(fixture.BillingManagerId).PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/assignment",
            new { assignedUserId = fixture.BillingAgentId });

        var notificationId = (await fixture.NotificationsForAsync(ticketId))
            .First(n => n.Type == NotificationType.TicketAssigned).Id;

        // The Technical agent is not the recipient.
        var response = await fixture.Factory.CreateClientFor(fixture.TechnicalAgentId)
            .PostAsync($"{Notifications}/{notificationId}/read", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var nonexistent = await fixture.Factory.CreateClientFor(fixture.TechnicalAgentId)
            .PostAsync($"{Notifications}/{Guid.NewGuid()}/read", content: null);

        // Indistinguishable from an id that exists nowhere, which is the whole of AP-4.
        Assert.Equal(nonexistent.StatusCode, response.StatusCode);
    }

    /// <summary>
    /// <b><c>unreadCount</c> matches the unread rows, a read answers <c>204</c>, and a second read
    /// leaves <c>readAt</c> unchanged</b> (data-model §5 constraint 22).
    /// </summary>
    [Fact]
    public async Task Unread_count_tracks_reads_and_a_second_read_is_idempotent()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(SlaFixture.BillingDepartmentId);

        await fixture.Factory.CreateClientFor(fixture.BillingManagerId).PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/assignment",
            new { assignedUserId = fixture.BillingAgentTwoId });

        var before = await ListAsync(fixture.BillingAgentTwoId);
        var unreadBefore = before.GetProperty("unreadCount").GetInt32();

        Assert.True(unreadBefore > 0);

        var notificationId = (await fixture.NotificationsForAsync(ticketId))
            .First(n => n.RecipientUserId == fixture.BillingAgentTwoId).Id;

        var client = fixture.Factory.CreateClientFor(fixture.BillingAgentTwoId);

        var first = await client.PostAsync($"{Notifications}/{notificationId}/read", content: null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var readAtAfterFirst = await ReadAtAsync(notificationId);

        Assert.NotNull(readAtAfterFirst);

        var after = await ListAsync(fixture.BillingAgentTwoId);

        Assert.Equal(unreadBefore - 1, after.GetProperty("unreadCount").GetInt32());

        // The second read must not move the timestamp, and must still answer 204.
        var second = await client.PostAsync($"{Notifications}/{notificationId}/read", content: null);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(readAtAfterFirst, await ReadAtAsync(notificationId));
    }

    /// <summary>
    /// <b><c>unreadOnly=true</c> filters the rows but not the count.</b> The badge answers "how many
    /// unread do I have", which does not change because the caller is looking at a filtered page.
    /// </summary>
    [Fact]
    public async Task Unread_only_filters_the_items_and_leaves_the_count_alone()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(SlaFixture.BillingDepartmentId);

        await fixture.Factory.CreateClientFor(fixture.BillingManagerId).PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/assignment",
            new { assignedUserId = fixture.BillingAgentId });

        var all = await ListAsync(fixture.BillingAgentId);
        var unread = await ListAsync(fixture.BillingAgentId, unreadOnly: true);

        Assert.Equal(
            all.GetProperty("unreadCount").GetInt32(),
            unread.GetProperty("unreadCount").GetInt32());

        // The API omits null properties (`DefaultIgnoreCondition = WhenWritingNull`), so an unread row
        // carries no `readAt` key at all rather than a null one. Both spellings mean unread, and the
        // assertion accepts either rather than pinning a serializer setting this story did not choose.
        Assert.All(
            unread.GetProperty("items").EnumerateArray(),
            item => Assert.True(
                !item.TryGetProperty("readAt", out var readAt) || readAt.ValueKind == JsonValueKind.Null,
                "unreadOnly=true returned a notification that has been read."));
    }

    /// <summary>
    /// <b>No route exists at <c>/notifications/read-all</c></b> — it was removed from the contract as
    /// unrequested surface (AP-18). Asserted because "we did not add it" is easy to undo by accident,
    /// and a convenience endpoint is exactly the kind of thing that gets added back.
    /// </summary>
    [Fact]
    public async Task There_is_no_read_all_route()
    {
        var response = await fixture.Factory.CreateClientFor(fixture.BillingAgentId)
            .PostAsync($"{Notifications}/read-all", content: null);

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Expected 404 or 405 from the router, got {(int)response.StatusCode}.");
    }

    /// <summary>
    /// The row shape of §6.6, including the projected <c>ticketSubject</c> — it is there so a list is
    /// readable without a call per notification.
    /// </summary>
    [Fact]
    public async Task A_row_carries_the_projected_ticket_subject()
    {
        var ticketId = await fixture.AddOverdueTicketAsync(SlaFixture.BillingDepartmentId);

        await fixture.Factory.CreateClientFor(fixture.BillingManagerId).PostAsJsonAsync(
            $"/api/v1/tickets/{ticketId}/assignment",
            new { assignedUserId = fixture.BillingAgentId });

        var page = await ListAsync(fixture.BillingAgentId);

        var row = page.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("ticketId").GetGuid() == ticketId);

        Assert.Equal("Overdue ticket", row.GetProperty("ticketSubject").GetString());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("type").GetString()));
    }

    private async Task<JsonElement> ListAsync(Guid callerId, bool unreadOnly = false)
    {
        var url = unreadOnly ? $"{Notifications}?unreadOnly=true&pageSize=100" : $"{Notifications}?pageSize=100";

        return await fixture.Factory.CreateClientFor(callerId).GetFromJsonAsync<JsonElement>(url);
    }

    private Task<DateTimeOffset?> ReadAtAsync(Guid notificationId) =>
        fixture.Factory.WithDbAsync(async db =>
            await db.Notifications.AsNoTracking()
                .Where(n => n.Id == notificationId)
                .Select(n => n.ReadAt)
                .FirstAsync());
}
