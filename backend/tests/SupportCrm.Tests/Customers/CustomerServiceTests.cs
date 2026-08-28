using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Administration;
using SupportCrm.Application.Modules.Customers;
using SupportCrm.Domain.Modules.Administration;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Persistence;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04, <b>slice 2</b> — plan task 3, <see cref="CustomerService"/>, and nothing else.
///
/// <para>
/// <b>Why these tests call the service instead of an endpoint.</b> The four customer routes are
/// plan task 8 and do not exist yet, so there is nothing to <c>GET</c>. The service is therefore
/// exercised directly, with the <b>real</b> <see cref="SupportCrmDbContext"/> and the <b>real</b>
/// <see cref="AuditRecorder"/> resolved from the running composition root — only
/// <see cref="ICurrentUser"/> is a stub, because that is the one thing the missing middleware would
/// have filled. The atomicity and audit assertions below are consequently genuine, not simulated.
/// </para>
///
/// <para>
/// Two Done Criteria are deliberately <b>not</b> asserted here because they belong to task 8:
/// <c>GET /customers</c> as a <c>Customer</c> returning <c>403</c>, and the <c>409</c> reaching the
/// wire as Problem Details. Plan task 10's <c>CustomerAccessTests</c> owns both.
/// </para>
/// </summary>
public sealed class CustomerServiceTests(SupportCrmApiFactory factory)
    : IClassFixture<SupportCrmApiFactory>
{
    // ---------------------------------------------------------------- List, get, create

    [Fact]
    public async Task Create_then_get_returns_the_profile_with_its_nested_branch()
    {
        var branchId = await factory.EnsureBranchAsync("Create Branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.create@test.local");

        var created = await ActAsync(agentId, (svc, _) => svc.CreateAsync(new CreateCustomerRequest
        {
            FullName = "  Ada Lovelace  ",
            Email = "  Ada@Example.com  ",
            Phone = " 555-0100 ",
            BranchId = branchId,
        }, default));

        // The domain factory trims; the service does not store what the client's whitespace implied.
        Assert.Equal("Ada Lovelace", created.FullName);
        Assert.Equal("Ada@Example.com", created.Email);
        Assert.Equal("555-0100", created.Phone);

        // branch is the nested { id, name } of docs/api-design.md §6.2, not a bare id.
        Assert.Equal(branchId, created.Branch.Id);
        Assert.Equal("Create Branch", created.Branch.Name);

        // DM-6: the ERP seam field is returned and is null by default. No request model can set it.
        Assert.Null(created.ExternalReference);

        var fetched = await ActAsync(agentId, (svc, _) => svc.GetAsync(created.Id, default));
        Assert.Equal(created, fetched);
    }

    /// <summary>
    /// <b>A-10 on create.</b> A duplicate is rejected, never reconciled — there is no merge or
    /// dedupe tooling (T2-A, docs/product-scope.md §8).
    /// </summary>
    [Fact]
    public async Task Create_with_an_email_another_customer_holds_is_a_customer_email_conflict()
    {
        var branchId = await factory.EnsureBranchAsync("Dup Branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.dup@test.local");

        await ActAsync(agentId, (svc, _) => svc.CreateAsync(NewCustomer("dup.create@test.local", branchId), default));

        var error = await Assert.ThrowsAsync<ConflictException>(() => ActAsync(agentId,
            (svc, _) => svc.CreateAsync(NewCustomer("dup.create@test.local", branchId), default)));

        Assert.Equal("customer-email-in-use", error.ProblemType);
    }

    /// <summary>
    /// <c>branchId</c> is required and carries a foreign key, so an unknown one is a <c>400</c> that
    /// names the field — not a <c>DbUpdateException</c> surfacing as a <c>500</c>.
    /// </summary>
    [Fact]
    public async Task Create_with_an_unknown_branch_is_a_validation_error()
    {
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.badbranch@test.local");

        var error = await Assert.ThrowsAsync<ValidationException>(() => ActAsync(agentId,
            (svc, _) => svc.CreateAsync(NewCustomer("badbranch@test.local", Guid.NewGuid()), default)));

        Assert.Contains("branchId", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_for_an_unknown_id_is_not_found()
    {
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.missing@test.local");

        await Assert.ThrowsAsync<NotFoundException>(() => ActAsync(agentId,
            (svc, _) => svc.GetAsync(Guid.NewGuid(), default)));
    }

    /// <summary>
    /// The two filters docs/api-design.md §5.5 names, and no others. <c>branchId</c> is a legitimate
    /// filter here (T2-K) and is <b>not</b> scoping: it narrows a list the agent may already see in
    /// full.
    /// </summary>
    [Fact]
    public async Task List_filters_by_q_over_name_and_email_and_by_branch()
    {
        var north = await factory.EnsureBranchAsync("List North");
        var south = await factory.EnsureBranchAsync("List South");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.list@test.local");

        await ActAsync(agentId, async (svc, _) =>
        {
            await svc.CreateAsync(new CreateCustomerRequest
            { FullName = "Zephyr Quint", Email = "zephyr.q@filter.local", BranchId = north }, default);
            await svc.CreateAsync(new CreateCustomerRequest
            { FullName = "Bartholomew Quint", Email = "bart.q@filter.local", BranchId = south }, default);
            return true;
        });

        // q matches the name...
        var byName = await ActAsync(agentId, (svc, _) =>
            svc.ListAsync(new CustomerListFilter { Q = "Zephyr" }, null, default));
        Assert.Equal("Zephyr Quint", Assert.Single(byName.Items).FullName);

        // ...and the email.
        var byEmail = await ActAsync(agentId, (svc, _) =>
            svc.ListAsync(new CustomerListFilter { Q = "bart.q@filter" }, null, default));
        Assert.Equal("Bartholomew Quint", Assert.Single(byEmail.Items).FullName);

        // Different parameters AND together (docs/api-design.md §2.1).
        var both = await ActAsync(agentId, (svc, _) =>
            svc.ListAsync(new CustomerListFilter { Q = "Quint", BranchId = north }, null, default));
        var row = Assert.Single(both.Items);
        Assert.Equal("Zephyr Quint", row.FullName);
        Assert.Equal("List North", row.Branch.Name);

        // Story 05 replaces the literal. Until then the directory says it knows nothing, rather than
        // guessing (docs/api-design.md §6.3).
        Assert.Equal(0, row.OpenTicketCount);
    }

    /// <summary>
    /// <b>AP-15.</b> docs/api-design.md §5.5 enumerates this whitelist — <c>fullName</c> and
    /// <c>createdAt</c> — so a third field is a contract change, and anything unlisted is a
    /// <c>400</c> rather than a silently ignored parameter.
    ///
    /// <para>
    /// The assertion is specifically about <see cref="ValidationException"/>, which is the
    /// whitelist's verdict. It is not "the query ran": <c>createdAt</c> is a
    /// <c>DateTimeOffset</c>, and <b>SQLite cannot <c>ORDER BY</c> one</b> while SQL Server can — so
    /// on this test host an accepted <c>createdAt</c> sort reaches the provider and fails there, one
    /// layer below the rule under test. The ordering it produces is verified against real SQL Server
    /// in the story's verification steps, exactly as the case-insensitive collation is.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("fullName", true)]
    [InlineData("fullName:desc", true)]
    [InlineData("createdAt", true)]
    [InlineData("createdAt:desc", true)]
    [InlineData("email", false)]
    [InlineData("branchId", false)]
    [InlineData("openTicketCount", false)]
    [InlineData("phone", false)]
    [InlineData("fullName:sideways", false)]
    public async Task List_accepts_only_the_two_sort_fields_the_contract_names(string sort, bool allowed)
    {
        var agentId = await factory.AddStaffUserAsync(
            UserRole.Agent, $"cs.sort.{sort.Replace(':', '-')}@test.local");

        var act = async () =>
        {
            try
            {
                await ActAsync(agentId, (svc, _) =>
                    svc.ListAsync(new CustomerListFilter(), new PageQuery { Sort = sort }, default));
            }
            catch (NotSupportedException)
            {
                // SQLite's DateTimeOffset ORDER BY limitation, described above. The whitelist
                // already accepted the field, which is what this test asserts.
            }
        };

        if (allowed)
        {
            await act();
        }
        else
        {
            await Assert.ThrowsAsync<ValidationException>(act);
        }
    }

    /// <summary>
    /// The default sort is <c>fullName</c> ascending, and <c>:desc</c> reverses it. Paging is not
    /// meaningful without a stable order, so the default is asserted rather than assumed.
    /// </summary>
    [Fact]
    public async Task List_sorts_by_full_name_ascending_by_default_and_descending_on_request()
    {
        var branchId = await factory.EnsureBranchAsync("Sort Order Branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.sortorder@test.local");

        await ActAsync(agentId, async (svc, _) =>
        {
            foreach (var name in new[] { "Mira Sortcheck", "Alan Sortcheck", "Zoe Sortcheck" })
            {
                await svc.CreateAsync(new CreateCustomerRequest
                {
                    FullName = name,
                    Email = $"{name.Split(' ')[0].ToLowerInvariant()}@sortorder.local",
                    BranchId = branchId,
                }, default);
            }

            return true;
        });

        var ascending = await ActAsync(agentId, (svc, _) => svc.ListAsync(
            new CustomerListFilter { BranchId = branchId }, null, default));

        Assert.Equal(
            ["Alan Sortcheck", "Mira Sortcheck", "Zoe Sortcheck"],
            ascending.Items.Select(c => c.FullName));

        var descending = await ActAsync(agentId, (svc, _) => svc.ListAsync(
            new CustomerListFilter { BranchId = branchId },
            new PageQuery { Sort = "fullName:desc" }, default));

        Assert.Equal(
            ["Zoe Sortcheck", "Mira Sortcheck", "Alan Sortcheck"],
            descending.Items.Select(c => c.FullName));
    }

    // ---------------------------------------------------------------- A-19, case by case

    /// <summary>
    /// <b>A-19 case 1</b> — no <c>email</c> in the request. A PATCH carries only what changes, so
    /// the other fields move and the address does not. <b>No audit entry</b>: no login changed.
    /// </summary>
    [Fact]
    public async Task Patching_without_an_email_changes_the_other_fields_and_audits_nothing()
    {
        var (agentId, customerId, userId, _) = await SeedLinkedPairAsync("a19.case1");
        var otherBranch = await factory.EnsureBranchAsync("A19 Other Branch");

        var before = await CountEmailAuditEntriesAsync();

        var updated = await ActAsync(agentId, (svc, _) => svc.UpdateAsync(
            customerId, new PatchCustomerRequest { FullName = "Renamed Person", BranchId = otherBranch }, default));

        Assert.Equal("Renamed Person", updated.FullName);
        Assert.Equal(otherBranch, updated.Branch.Id);
        Assert.Equal("a19.case1@test.local", updated.Email);

        Assert.Equal("a19.case1@test.local", await UserEmailAsync(userId));
        Assert.Equal(before, await CountEmailAuditEntriesAsync());
    }

    /// <summary>
    /// <b>A-19 case 2</b> — the same address in a different case. It is a no-op for both rows and
    /// explicitly <b>not</b> a <c>409</c> against the customer's own record, which is the trap the
    /// duplicate check would otherwise fall into. <b>No audit entry</b>: nothing changed.
    /// </summary>
    [Fact]
    public async Task Patching_the_same_email_in_a_different_case_is_a_no_op_and_not_a_conflict()
    {
        var (agentId, customerId, userId, _) = await SeedLinkedPairAsync("a19.case2");

        var before = await CountEmailAuditEntriesAsync();

        var updated = await ActAsync(agentId, (svc, _) => svc.UpdateAsync(
            customerId, new PatchCustomerRequest { Email = "A19.CASE2@TEST.LOCAL" }, default));

        // Neither row was rewritten — the stored value is the original casing.
        Assert.Equal("a19.case2@test.local", updated.Email);
        Assert.Equal("a19.case2@test.local", await UserEmailAsync(userId));
        Assert.Equal(before, await CountEmailAuditEntriesAsync());
    }

    /// <summary>
    /// <b>A-19 case 3</b> — another <b>customer</b> holds it. <c>409 customer-email-in-use</c>, and
    /// <b>both rows are left untouched</b> — asserted by re-reading each after the failed call, not
    /// merely by the exception type.
    /// </summary>
    [Fact]
    public async Task A_collision_with_another_customer_is_rejected_and_writes_neither_row()
    {
        var (agentId, customerId, userId, branchId) = await SeedLinkedPairAsync("a19.case3");

        await ActAsync(agentId, (svc, _) =>
            svc.CreateAsync(NewCustomer("a19.case3.rival@test.local", branchId), default));

        var before = await CountEmailAuditEntriesAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(() => ActAsync(agentId,
            (svc, _) => svc.UpdateAsync(customerId,
                new PatchCustomerRequest { Email = "a19.case3.rival@test.local" }, default)));

        Assert.Equal("customer-email-in-use", error.ProblemType);

        Assert.Equal("a19.case3@test.local", await CustomerEmailAsync(customerId));
        Assert.Equal("a19.case3@test.local", await UserEmailAsync(userId));
        Assert.Equal(before, await CountEmailAuditEntriesAsync());
    }

    /// <summary>
    /// <b>A-19 case 4</b> — another <b>user</b> holds it, <b>staff included</b>. Constraint 1
    /// applies the uniqueness of <c>User.email</c> to the propagated value across all users, so the
    /// whole operation is rejected. The slug is <c>user-already-exists</c> — <b>PF-6's existing slug
    /// for PF-6's existing rule</b>; no new problem type is minted.
    /// </summary>
    [Fact]
    public async Task A_collision_with_a_staff_user_is_rejected_with_PF6s_slug_and_writes_neither_row()
    {
        var (agentId, customerId, userId, _) = await SeedLinkedPairAsync("a19.case4");

        // A STAFF user, with no customer profile at all — the case a customer-only check would miss.
        await factory.AddStaffUserAsync(UserRole.Manager, "a19.case4.staff@test.local");

        var before = await CountEmailAuditEntriesAsync();

        var error = await Assert.ThrowsAsync<ConflictException>(() => ActAsync(agentId,
            (svc, _) => svc.UpdateAsync(customerId,
                new PatchCustomerRequest { Email = "a19.case4.staff@test.local" }, default)));

        Assert.Equal("user-already-exists", error.ProblemType);

        Assert.Equal("a19.case4@test.local", await CustomerEmailAsync(customerId));
        Assert.Equal("a19.case4@test.local", await UserEmailAsync(userId));
        Assert.Equal(before, await CountEmailAuditEntriesAsync());
    }

    /// <summary>
    /// <b>A-19 case 5 — the propagation itself, and the heart of this slice.</b> Both rows change,
    /// in <b>one</b> <c>SaveChangesAsync</c>, and <b>exactly one</b> <c>UserEmailChanged</c> entry
    /// is written: actor = the agent who called, target = the <b>linked user</b>, not the customer.
    /// </summary>
    [Fact]
    public async Task Changing_the_email_of_a_customer_with_a_login_changes_both_rows_and_audits_once()
    {
        var (agentId, customerId, userId, _) = await SeedLinkedPairAsync("a19.case5");

        var commits = 0;

        var updated = await ActAsync(agentId, async (svc, db) =>
        {
            // Counting real commits is how "one SaveChangesAsync in the success path" is proven.
            // Two commits are exactly the divergence A-19 exists to prevent.
            db.SavedChanges += (_, _) => commits++;

            return await svc.UpdateAsync(
                customerId, new PatchCustomerRequest { Email = "a19.case5.new@test.local" }, default);
        });

        Assert.Equal(1, commits);

        // There is no committed state in which the two differ.
        Assert.Equal("a19.case5.new@test.local", updated.Email);
        Assert.Equal("a19.case5.new@test.local", await CustomerEmailAsync(customerId));
        Assert.Equal("a19.case5.new@test.local", await UserEmailAsync(userId));

        var entry = Assert.Single(
            await factory.WithDbAsync(db => db.AuditEntries.AsNoTracking()
                .Where(a => a.Action == AuditAction.UserEmailChanged && a.TargetId == userId)
                .ToListAsync()));

        Assert.Equal(AuditOutcome.Success, entry.Outcome);

        // The actor is the agent who issued the PATCH, resolved from ICurrentUser — not the customer
        // and not the linked user.
        Assert.Equal(agentId, entry.ActorUserId);

        // The audited fact is that a SIGN-IN IDENTIFIER changed, so the target is the user.
        Assert.Equal(AuditTargetType.User, entry.TargetType);
        Assert.NotEqual(customerId, entry.TargetId);

        // The entry records no address, old or new: AuditEntry has no value columns (§2.14), and the
        // address must not be smuggled into actorDescriptor, which is the failed-sign-in identifier.
        Assert.Null(entry.ActorDescriptor);
    }

    /// <summary>
    /// <b>A-19 case 6</b> — a profile-only customer, which DM-1 makes the <em>ordinary</em> case.
    /// The profile changes, no login exists to propagate to, and <b>no audit entry is written</b>.
    /// </summary>
    [Fact]
    public async Task Changing_the_email_of_a_customer_without_a_login_audits_nothing()
    {
        var branchId = await factory.EnsureBranchAsync("A19 Case6 Branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, "cs.case6@test.local");

        var created = await ActAsync(agentId, (svc, _) =>
            svc.CreateAsync(NewCustomer("a19.case6@test.local", branchId), default));

        var before = await CountEmailAuditEntriesAsync();

        var updated = await ActAsync(agentId, (svc, _) => svc.UpdateAsync(
            created.Id, new PatchCustomerRequest { Email = "a19.case6.new@test.local" }, default));

        Assert.Equal("a19.case6.new@test.local", updated.Email);
        Assert.Equal(before, await CountEmailAuditEntriesAsync());
    }

    /// <summary>
    /// The linked user is found by <c>User.CustomerId</c>, <b>never</b> by matching on the old email
    /// — an email match would be the very assumption A-19 removes. Proven by driving the propagation
    /// twice: after the first change the user's old address no longer exists anywhere, so a
    /// match-on-email implementation would silently stop propagating on the second.
    /// </summary>
    [Fact]
    public async Task The_linked_login_is_found_by_customerId_so_propagation_survives_a_second_change()
    {
        var (agentId, customerId, userId, _) = await SeedLinkedPairAsync("a19.twice");

        await ActAsync(agentId, (svc, _) => svc.UpdateAsync(
            customerId, new PatchCustomerRequest { Email = "a19.twice.first@test.local" }, default));

        await ActAsync(agentId, (svc, _) => svc.UpdateAsync(
            customerId, new PatchCustomerRequest { Email = "a19.twice.second@test.local" }, default));

        Assert.Equal("a19.twice.second@test.local", await UserEmailAsync(userId));

        var entries = await factory.WithDbAsync(db => db.AuditEntries.AsNoTracking()
            .Where(a => a.Action == AuditAction.UserEmailChanged && a.TargetId == userId)
            .ToListAsync());

        Assert.Equal(2, entries.Count);
    }

    /// <summary>
    /// <c>externalReference</c> is returned and is settable through nothing: DM-6 and
    /// docs/api-design.md §8.3 make it read-only, and the guarantee is that neither request model
    /// has a property for it (AP-10) — a request carrying one is a <c>400</c> at binding.
    /// </summary>
    [Fact]
    public void Neither_request_model_can_set_externalReference_or_any_server_derived_field()
    {
        string[] createProperties = [.. typeof(CreateCustomerRequest).GetProperties().Select(p => p.Name)];
        string[] patchProperties = [.. typeof(PatchCustomerRequest).GetProperties().Select(p => p.Name)];

        // Exactly the four fields docs/api-design.md §5.5 names for each.
        Assert.Equal(["FullName", "Email", "Phone", "BranchId"], createProperties);
        Assert.Equal(["FullName", "Email", "Phone", "BranchId"], patchProperties);

        foreach (var forbidden in new[] { "ExternalReference", "Id", "CreatedAt", "CustomerId", "Role" })
        {
            Assert.DoesNotContain(forbidden, createProperties);
            Assert.DoesNotContain(forbidden, patchProperties);
        }
    }

    /// <summary>
    /// <c>PATCH /users/{id}</c> still cannot change an email: A-19 is a server-side consequence of
    /// the customer patch, not a new writable field, and <c>PatchUserRequest</c> gains no
    /// <c>email</c> property (AP-10, docs/api-design.md §5.3).
    /// </summary>
    [Fact]
    public void A19_does_not_make_user_email_patchable_through_the_users_endpoint()
    {
        Assert.DoesNotContain(
            "Email",
            typeof(SupportCrm.Application.Modules.Identity.PatchUserRequest)
                .GetProperties().Select(p => p.Name));
    }

    // ---------------------------------------------------------------- Harness

    private static CreateCustomerRequest NewCustomer(string email, Guid branchId) => new()
    {
        FullName = email.Split('@')[0],
        Email = email,
        BranchId = branchId,
    };

    /// <summary>
    /// Creates a customer profile <b>and</b> a linked portal login for it — the shape A-19 is about.
    /// Returns the acting agent, the customer, the linked user, and the branch.
    /// </summary>
    private async Task<(Guid AgentId, Guid CustomerId, Guid UserId, Guid BranchId)> SeedLinkedPairAsync(
        string slug)
    {
        var branchId = await factory.EnsureBranchAsync($"{slug} branch");
        var agentId = await factory.AddStaffUserAsync(UserRole.Agent, $"{slug}.agent@test.local");
        var email = $"{slug}@test.local";

        var (customerId, userId) = await factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), slug, email, null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            // The Customer-role user is written beneath the Domain for the same reason
            // SupportCrmApiFactory does it: User.CreateCustomerUser is plan task 7 and does not
            // exist yet, and pre-empting it here would be starting a later slice.
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO Users (Id, Email, PasswordHash, DisplayName, Role, DepartmentId, CustomerId, BranchId, IsActive, CreatedAt) VALUES ({id}, {email}, {"!unhashed"}, {slug}, {nameof(UserRole.Customer)}, NULL, {customer.Id}, NULL, 1, {DateTimeOffset.UtcNow})");

            return (customer.Id, id);
        });

        return (agentId, customerId, userId, branchId);
    }

    /// <summary>
    /// Runs work against a <see cref="CustomerService"/> built from the real composition root's
    /// scope, with <paramref name="actingUserId"/> standing in for the identity
    /// <c>CurrentUserMiddleware</c> would have resolved.
    /// </summary>
    private async Task<T> ActAsync<T>(
        Guid actingUserId, Func<CustomerService, SupportCrmDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportCrmDbContext>();

        var audit = new AuditRecorder(
            db, new ActingUser(actingUserId), TimeProvider.System, NullLogger<AuditRecorder>.Instance);

        return await work(new CustomerService(db, audit, TimeProvider.System), db);
    }

    private Task<string> CustomerEmailAsync(Guid id) => factory.WithDbAsync(db =>
        db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Email).SingleAsync());

    private Task<string> UserEmailAsync(Guid id) => factory.WithDbAsync(db =>
        db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Email).SingleAsync());

    private Task<int> CountEmailAuditEntriesAsync() => factory.WithDbAsync(db =>
        db.AuditEntries.AsNoTracking().CountAsync(a => a.Action == AuditAction.UserEmailChanged));

    /// <summary>
    /// The agent who issued the PATCH. Only <see cref="ICurrentUser.Id"/> and
    /// <see cref="ICurrentUser.IsAuthenticated"/> are read by <see cref="AuditRecorder"/>; the rest
    /// throw, which is the point — a service reaching for more would fail loudly here.
    /// </summary>
    private sealed class ActingUser(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public Guid Id { get; } = id;

        public UserRole Role => UserRole.Agent;

        public Guid? DepartmentId => throw new NotSupportedException();

        public Guid? CustomerId => throw new NotSupportedException();

        public string DisplayName => throw new NotSupportedException();

        public string Email => throw new NotSupportedException();

        public bool IsInRoleAtLeast(UserRole minimum) => Role.RankAtLeast(minimum);
    }
}
