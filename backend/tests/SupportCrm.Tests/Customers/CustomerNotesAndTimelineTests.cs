using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Customers;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04 <b>slice 3</b> — plan tasks 4 (notes) and 5 (the interaction timeline).
/// <para>
/// Both services are exercised directly; their endpoints are task 8. See
/// <see cref="CustomerModuleHarness"/> for what is substituted and why.
/// </para>
/// </summary>
public sealed class CustomerNotesAndTimelineTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    // ---------------------------------------------------------------- Notes (task 4)

    /// <summary>
    /// <b>Author and timestamp are server-set from <c>ICurrentUser</c></b> and never accepted from
    /// the client (docs/api-design.md §7). The response embeds a <c>UserSummary</c>, not a bare id
    /// (§6.3).
    /// </summary>
    [Fact]
    public async Task A_note_is_attributed_and_timestamped_by_the_server()
    {
        var (customerId, agentId) = await SeedCustomerAndAgentAsync("notes.attribution");

        var note = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
        {
            var service = new CustomerNoteService(
                db, new CustomerModuleHarness.Caller(agentId, UserRole.Agent, "Bilal Haddad"),
                TimeProvider.System);

            return await service.AddAsync(
                customerId, new CreateNoteRequest { Body = "Called the customer back." }, default);
        });

        Assert.Equal(agentId, note.Author.Id);
        Assert.Equal("Bilal Haddad", note.Author.DisplayName);
        Assert.Equal("Called the customer back.", note.Body);
        Assert.NotEqual(default, note.CreatedAt);

        // Re-read from the database rather than trusting the returned DTO: the author on the row is
        // what attribution actually means.
        var stored = await factory.WithDbAsync(db =>
            db.CustomerNotes.AsNoTracking().SingleAsync(n => n.Id == note.Id));

        Assert.Equal(agentId, stored.AuthorUserId);
        Assert.Equal(customerId, stored.CustomerId);
    }

    /// <summary>Paged and <b>newest first</b> (docs/api-design.md §5.5).</summary>
    [Fact]
    public async Task Notes_are_listed_newest_first()
    {
        var (customerId, agentId) = await SeedCustomerAndAgentAsync("notes.order");

        var page = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
        {
            var service = new CustomerNoteService(
                db, new CustomerModuleHarness.Caller(agentId, UserRole.Agent),
                new CustomerModuleHarness.StepClock(new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero)));

            foreach (var body in new[] { "first", "second", "third" })
            {
                await service.AddAsync(customerId, new CreateNoteRequest { Body = body }, default);
            }

            return await service.ListAsync(customerId, null, default);
        });

        Assert.Equal(3, page.TotalItems);
        Assert.Equal(["third", "second", "first"], page.Items.Select(n => n.Body));
    }

    /// <summary>
    /// <b>docs/data-model.md §5 constraint 16 — immutable once written</b>, and §2.5 makes it
    /// structural. The plan is explicit: <em>"There is no update and no delete method — not merely
    /// no endpoint."</em> So the service's public surface is asserted exhaustively; a future
    /// <c>EditAsync</c> breaks this test, which is the intended alarm.
    /// </summary>
    [Fact]
    public void The_note_service_offers_nothing_that_could_change_or_remove_a_note()
    {
        var methods = typeof(CustomerNoteService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["AddAsync", "ListAsync"], methods);
    }

    /// <summary>
    /// <c>CustomerNoteDto</c> has <b>no <c>updatedAt</c></b>, because the entity is immutable
    /// (docs/api-design.md §6.3). Its absence is the contract saying no edit path exists.
    /// </summary>
    [Fact]
    public void The_note_payload_has_no_updatedAt_and_the_request_has_only_a_body()
    {
        Assert.DoesNotContain("UpdatedAt", typeof(CustomerNoteDto).GetProperties().Select(p => p.Name));

        Assert.Equal(["Body"], typeof(CreateNoteRequest).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task A_note_against_an_unknown_customer_is_not_found()
    {
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "notes.missing@test.local");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
            {
                var service = new CustomerNoteService(
                    db, new CustomerModuleHarness.Caller(agentId, UserRole.Agent), TimeProvider.System);

                return await service.AddAsync(
                    Guid.NewGuid(), new CreateNoteRequest { Body = "orphan" }, default);
            }));
    }

    // ---------------------------------------------------------------- Timeline (task 5)

    /// <summary>
    /// <b>The intake's acceptance criterion:</b> <em>"the timeline for a customer with no tickets
    /// renders an empty state rather than an error"</em>. It is met now and must stay met when
    /// Story 06 fills the projection in.
    /// </summary>
    [Fact]
    public async Task The_timeline_of_a_customer_with_no_tickets_is_an_empty_page_not_an_error()
    {
        var (customerId, _) = await SeedCustomerAndAgentAsync("timeline.empty");

        var page = await CustomerModuleHarness.InScopeAsync(factory, (sp, db) =>
            new CustomerTimelineService(db).GetAsync(customerId, null, default));

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalItems);
        Assert.Equal(0, page.TotalPages);

        // A well-formed envelope, not a degenerate one: a client paging over it must not divide by
        // zero or render "page 0 of 0".
        Assert.Equal(1, page.Page);
        Assert.Equal(PageQuery.DefaultPageSize, page.PageSize);
    }

    /// <summary>
    /// An absent customer and an empty timeline are <b>different outcomes</b>: <c>404</c> against
    /// the customer, an empty page against the activity.
    /// </summary>
    [Fact]
    public async Task The_timeline_of_an_unknown_customer_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CustomerModuleHarness.InScopeAsync(factory, (sp, db) =>
                new CustomerTimelineService(db).GetAsync(Guid.NewGuid(), null, default)));
    }

    /// <summary>
    /// <b>Customer notes are a separate collection and do not appear in the timeline</b>
    /// (docs/api-design.md §5.5). Asserted with notes actually present, so the test would fail if
    /// Story 06 later "enriched" the projection by joining them.
    /// </summary>
    [Fact]
    public async Task Customer_notes_do_not_appear_in_the_timeline()
    {
        var (customerId, agentId) = await SeedCustomerAndAgentAsync("timeline.notes");

        var page = await CustomerModuleHarness.InScopeAsync(factory, async (sp, db) =>
        {
            var notes = new CustomerNoteService(
                db, new CustomerModuleHarness.Caller(agentId, UserRole.Agent), TimeProvider.System);

            await notes.AddAsync(customerId, new CreateNoteRequest { Body = "A note, not activity." }, default);

            return await new CustomerTimelineService(db).GetAsync(customerId, null, default);
        });

        Assert.Empty(page.Items);
    }

    /// <summary>
    /// <b>Exclusion 2 — <c>TicketInternalNote</c> is never touched by the timeline query</b>
    /// (docs/data-model.md §2.9, §5 constraint 18).
    ///
    /// <para>
    /// <b>Revisited by Story 14, exactly as this guard demanded.</b> Its earlier form asserted that
    /// <c>IApplicationDbContext</c> exposed <b>no</b> internal-note set — true only while the entity
    /// did not exist — and its own remarks said it <em>"fails loudly the moment a set for internal
    /// notes appears without the guard being revisited"</em>. Story 14 added the set, so the guard
    /// fired as designed and is re-expressed here rather than deleted or weakened.
    /// </para>
    ///
    /// <para>
    /// <b>The rule it protects is unchanged</b>, and it is now the stronger statement: the set
    /// exists and is reachable from this layer, and the timeline projection <b>still</b> does not
    /// read it. That is asserted by <c>CustomerTimelineService</c>'s query naming only the two sets
    /// it is built from. The end-to-end proof — a real note, a real customer token, and a canary
    /// searched for in the raw JSON — is
    /// <c>InternalNotesAreUnreachableTests.The_customer_timeline_carries_no_internal_entry</c>,
    /// which is where the intake requires it to live (<em>"performed as a customer"</em>).
    /// </para>
    /// </summary>
    [Fact]
    public void The_timeline_projection_reads_no_internal_note_set()
    {
        var sets = typeof(IApplicationDbContext).GetProperties().Select(p => p.Name).ToList();

        // The set now EXISTS — Story 14 added it. The guard is no longer "there is no table".
        Assert.Contains("TicketInternalNotes", sets);

        // The positive half, so neither assertion can pass by reflecting over nothing.
        Assert.Contains(nameof(IApplicationDbContext.CustomerNotes), sets);

        // And the rule itself: the timeline is assembled from activities and tickets, and names the
        // internal-note set nowhere. A future "enrichment" that joined it would have to add the
        // reference this assertion forbids.
        var timelineSource = File.ReadAllText(TimelineServicePath());

        Assert.DoesNotContain("TicketInternalNotes", timelineSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// The one file the assertion above reads. Resolved from the test assembly's location rather
    /// than hardcoded, so it survives a different working directory.
    /// </summary>
    private static string TimelineServicePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var path = Path.Combine(
            directory!.FullName,
            "src", "SupportCrm.Application", "Modules", "Customers", "CustomerTimelineService.cs");

        Assert.True(File.Exists(path), $"Expected the timeline service at {path}.");

        return path;
    }

    private async Task<(Guid CustomerId, Guid AgentId)> SeedCustomerAndAgentAsync(string slug)
    {
        var branchId = await factory.EnsureBranchAsync($"{slug} branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, $"{slug}.agent@test.local");

        var customerId = await factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), slug, $"{slug}@test.local", null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });

        return (customerId, agentId);
    }
}
