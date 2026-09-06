using Microsoft.Extensions.Logging;
using SupportCrm.Application.Modules.Integrations;

namespace SupportCrm.Infrastructure.Seams.Erp;

/// <summary>
/// <b>The shipped ERP / external-system gateway</b> — docs/architecture.md §5.3: <em>"one outbound
/// gateway interface in Application isolating any external-system call, with a no-op implementation
/// selected by configuration."</em>
///
/// <h3>It does nothing, and doing nothing is the delivered behaviour</h3>
/// <see cref="LookupExternalReferenceAsync"/> returns <c>null</c> and
/// <see cref="PushCustomerChangedAsync"/> completes without a side effect. There is <b>no
/// connection, no field mapping, no sync strategy and no conflict resolution</b> (T3-D, §5.3), and
/// no credential is read — the whole application runs and demos with every real integration absent
/// (product-scope §10 item 5).
///
/// <h3>It logs at <c>Debug</c>, deliberately</h3>
/// A no-op that logged at <c>Information</c> would fill a demo's log with lines about a system
/// nobody connected. <c>Debug</c> is loud enough for someone looking for the seam and quiet enough
/// to stay out of the way — and the composition root records <em>once</em>, at startup, that the
/// no-op is the selected implementation, which is where a reviewer reads it.
///
/// <h3>No system is named</h3>
/// Product-scope §9 <b>questions 2 and 3 stay open</b> and requirements §11.4 is unbounded on
/// purpose. Nothing in this file names a product, and nothing should.
///
/// <para>
/// The one persisted trace of this seam is <c>Customer.externalReference</c> — optional, unused by
/// default, and settable through no endpoint (DM-6, api-design §8.3). <b>This class does not write
/// it</b>: there is no writer anywhere, which is what "unused by default" means.
/// </para>
/// </summary>
public sealed class NoOpExternalSystemGateway(ILogger<NoOpExternalSystemGateway> logger)
    : IExternalSystemGateway
{
    public Task<string?> LookupExternalReferenceAsync(Guid customerId, CancellationToken ct)
    {
        logger.LogDebug(
            "External-system lookup skipped: the no-op gateway is in effect (customer={CustomerId}).",
            customerId);

        _ = ct;

        return Task.FromResult<string?>(null);
    }

    public Task PushCustomerChangedAsync(Guid customerId, CancellationToken ct)
    {
        logger.LogDebug(
            "External-system push skipped: the no-op gateway is in effect (customer={CustomerId}).",
            customerId);

        _ = ct;

        return Task.CompletedTask;
    }
}
