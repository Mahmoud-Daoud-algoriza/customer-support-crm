using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Modules.Integrations;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Infrastructure.Seams.Erp;

namespace SupportCrm.Tests.Integrations;

/// <summary>
/// <b>The ERP / external-system seam</b> — docs/architecture.md §5.3, T3-D.
///
/// <para>
/// Two claims, and both are the kind that quietly stop being true: the gateway <b>does nothing</b>,
/// and <c>Customer.externalReference</c> is <b>settable through no endpoint</b>
/// (docs/api-design.md §8.3, DM-6). The second is the one worth a test — it is one
/// <c>[JsonPropertyName]</c> away from becoming writable by accident.
/// </para>
/// </summary>
public sealed class ErpSeamTests(IntegrationSeamFixture fixture) : IClassFixture<IntegrationSeamFixture>
{
    /// <summary>
    /// <b>Test 5 — the no-op returns <c>null</c> and performs no side effect.</b>
    ///
    /// <para>
    /// "No side effect" is asserted against the one thing this seam could possibly touch: the
    /// customer row's <c>externalReference</c>. It is <c>null</c> before and <c>null</c> after, which
    /// is what *"unused by default"* means (DM-6). The call also runs in a host where any outbound
    /// HTTP request throws, so "no connection" is carried by the fixture.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_no_op_gateway_returns_null_and_changes_nothing()
    {
        var before = await ReadExternalReferenceAsync();

        Assert.Null(before);

        var lookup = await fixture.WithAgentScopeAsync(async provider =>
        {
            var gateway = provider.GetRequiredService<IExternalSystemGateway>();

            var reference = await gateway.LookupExternalReferenceAsync(
                fixture.CustomerId, CancellationToken.None);

            await gateway.PushCustomerChangedAsync(fixture.CustomerId, CancellationToken.None);

            return reference;
        });

        Assert.Null(lookup);
        Assert.Null(await ReadExternalReferenceAsync());
    }

    /// <summary>
    /// The shipped gateway is what configuration selects with <b>nothing configured</b> —
    /// <c>SupportCrm:Erp</c> appears in no settings file (product-scope §10 item 5).
    /// </summary>
    [Fact]
    public async Task The_no_op_gateway_is_what_resolves_with_no_configuration()
    {
        var gateway = await fixture.WithAgentScopeAsync(provider =>
            Task.FromResult(provider.GetRequiredService<IExternalSystemGateway>()));

        Assert.IsType<NoOpExternalSystemGateway>(gateway);
    }

    /// <summary>
    /// <b>Test 6, first half — <c>externalReference</c> is settable through no endpoint.</b>
    ///
    /// <para>
    /// Both write endpoints refuse it with a <c>400</c>, under AP-10's unmapped-member rule. The
    /// refusal is asserted to be <b>whole</b>: the legitimate field sent beside it is <em>not</em>
    /// applied, so a caller cannot smuggle a partial write through a rejected request.
    /// </para>
    /// </summary>
    [Fact]
    public async Task External_reference_is_refused_by_both_customer_write_endpoints()
    {
        var administratorId = await fixture.Factory.AddStaffUserAsync(
            UserRole.Administrator, "erp.admin@integrations.local");

        using var client = fixture.Factory.CreateClientFor(administratorId);

        // POST /customers — the field is not on the create model at all.
        var create = await client.PostAsJsonAsync("/api/v1/customers", new
        {
            fullName = "Smuggled Create",
            email = "smuggled.create@integrations.local",
            branchId = await AnyBranchIdAsync(),
            externalReference = "ERP-9999",
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);

        // Nothing was written — the refusal is whole, not partial.
        Assert.False(await fixture.Factory.WithDbAsync(db =>
            db.Customers.AnyAsync(c => c.Email == "smuggled.create@integrations.local")));

        // PATCH /customers/{id} — likewise.
        var patch = await client.PatchAsJsonAsync(
            $"/api/v1/customers/{fixture.CustomerId}",
            new { fullName = "Smuggled Patch", externalReference = "ERP-9999" });

        Assert.Equal(HttpStatusCode.BadRequest, patch.StatusCode);

        var customer = await fixture.Factory.WithDbAsync(db =>
            db.Customers.FirstAsync(c => c.Id == fixture.CustomerId));

        Assert.NotEqual("Smuggled Patch", customer.FullName);
        Assert.Null(customer.ExternalReference);
    }

    /// <summary>
    /// <b>Test 6, second half — it is returned read-only on <c>GET</c>.</b>
    ///
    /// <para>
    /// <b>Two different facts, and both matter.</b> When the field is <c>null</c> — which is always,
    /// because nothing writes it — it is <b>omitted from the payload</b>, because docs/api-design.md
    /// §2 omits nulls rather than sending them, and a null here is not meaningful. When it does hold
    /// a value it is <b>returned</b>, which is what api-design §8.3 means by *"the only trace in this
    /// API"*.
    /// </para>
    ///
    /// <para>
    /// The value is written <b>directly to the row</b>, bypassing every endpoint — because that is
    /// the only way it can be written at all, and demonstrating that is half the point. Story 04's
    /// <c>CustomerAccessTests.External_reference_is_returned_but_settable_through_no_endpoint</c>
    /// covers the same ground from the Customers side; this asserts it from the seam's, where the
    /// field's reason for existing lives.
    /// </para>
    /// </summary>
    [Fact]
    public async Task External_reference_is_omitted_when_null_and_returned_when_set()
    {
        using var client = fixture.Factory.CreateClientFor(fixture.AgentId);

        // Unused by default (DM-6): null, and therefore omitted (api-design §2).
        using (var before = await client.GetAsync($"/api/v1/customers/{fixture.CustomerId}"))
        {
            before.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await before.Content.ReadAsStringAsync());

            Assert.False(
                payload.RootElement.TryGetProperty("externalReference", out _),
                "A null externalReference is omitted, not sent as null (api-design §2).");
        }

        // The ONLY way this field can acquire a value: written straight to the row. No endpoint,
        // no service and no seam implementation writes it.
        await SetExternalReferenceDirectlyAsync("EXT-4711");

        using (var after = await client.GetAsync($"/api/v1/customers/{fixture.CustomerId}"))
        {
            after.EnsureSuccessStatusCode();

            using var payload = JsonDocument.Parse(await after.Content.ReadAsStringAsync());

            Assert.Equal("EXT-4711", payload.RootElement.GetProperty("externalReference").GetString());
        }

        // Restored, so "unused by default" stays true for every other test on this fixture.
        await SetExternalReferenceDirectlyAsync(null);

        Assert.Null(await ReadExternalReferenceAsync());
    }

    /// <summary>
    /// <b>No system is named — anywhere.</b> Product-scope §9 questions 2 and 3 stay open, and the
    /// enum is the least-watched place a product name could settle one by accident.
    /// </summary>
    [Fact]
    public void The_gateway_kind_enum_names_no_product()
    {
        var names = Enum.GetNames<Application.Configuration.ExternalSystemGatewayKind>();

        Assert.Equal(["NoOp"], names);
    }

    /// <summary>The gateway holds nothing that could open a connection.</summary>
    [Fact]
    public void The_no_op_gateway_has_no_http_dependency()
    {
        var parameters = typeof(NoOpExternalSystemGateway)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(HttpClient), parameters);
        Assert.DoesNotContain(typeof(IHttpClientFactory), parameters);
    }

    private Task<string?> ReadExternalReferenceAsync() => fixture.Factory.WithDbAsync(db =>
        db.Customers.Where(c => c.Id == fixture.CustomerId)
            .Select(c => c.ExternalReference)
            .FirstAsync());

    private Task<Guid> AnyBranchIdAsync() => fixture.Factory.WithDbAsync(db =>
        db.Branches.Select(b => b.Id).FirstAsync());

    /// <summary>
    /// Writes the field through EF's change tracker. <c>Customer.ExternalReference</c> has a private
    /// setter and <b>no mutator</b> — deliberately, because nothing in the application may write it
    /// (DM-6). Reaching past that in a test is the honest way to prove the read side without
    /// inventing a writer that must not exist.
    /// </summary>
    private Task SetExternalReferenceDirectlyAsync(string? value) => fixture.Factory.WithDbAsync(async db =>
    {
        var customer = await db.Customers.FirstAsync(c => c.Id == fixture.CustomerId);

        db.Entry(customer).Property(nameof(Customer.ExternalReference)).CurrentValue = value;

        return await db.SaveChangesAsync();
    });
}
