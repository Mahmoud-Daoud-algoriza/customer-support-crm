namespace SupportCrm.Application.Modules.Integrations;

/// <summary>
/// <b>The one outbound ERP / external-system gateway</b> — docs/architecture.md §5.3, requirements
/// §11.2 and §11.4, T3-D.
///
/// <h3>A boundary, not a connector</h3>
/// §5.3: <em>"one outbound gateway interface in Application isolating any external-system call, with
/// a no-op implementation selected by configuration."</em> The shipped implementation is
/// <c>NoOpExternalSystemGateway</c>: it returns <c>null</c>, does nothing, and reads no credential.
/// <b>No real connection, no field mapping, no sync strategy, no conflict resolution.</b>
///
/// <h3>No system is named, and that is deliberate</h3>
/// Requirements §11.4 <em>"External systems"</em> is <b>unbounded on purpose</b>: product-scope §9
/// <b>questions 2 and 3 stay open</b>, and cannot be scoped until a system is named. <b>Do not name
/// one here.</b> This interface is the place such a system would attach, and holding it at exactly
/// two methods is what keeps the open question open instead of quietly answering it.
///
/// <h3>The one persisted trace</h3>
/// <c>Customer.externalReference</c> — optional, unused by default, and <b>settable through no
/// endpoint</b> (DM-6, docs/api-design.md §8.3). It is returned read-only on the customer payload
/// and refused on <c>POST</c> and <c>PATCH</c> by AP-10's unmapped-member rule, which a test
/// asserts. <b>There is no ERP endpoint of any kind</b> (§8.3).
///
/// <para>
/// What a real implementation would have to add is written down in
/// <c>docs/integration-seams.md</c>.
/// </para>
/// </summary>
public interface IExternalSystemGateway
{
    /// <summary>
    /// Asks the external system for its own identifier for a customer.
    ///
    /// <para>
    /// <b>Returns <c>null</c> when there is nothing to report</b>, which is the shipped answer
    /// always. A null is "no external reference", never an error — the whole application runs with
    /// every real integration absent (product-scope §10 item 5).
    /// </para>
    /// </summary>
    Task<string?> LookupExternalReferenceAsync(Guid customerId, CancellationToken ct);

    /// <summary>
    /// Tells the external system a customer record changed.
    ///
    /// <para>
    /// <b>Fire-and-state-nothing.</b> There is no return value because there is no sync contract to
    /// report against: direction of sync, the object set, conflict resolution and idempotence are
    /// all undecided and are listed in <c>docs/integration-seams.md</c> as what a real
    /// implementation must add.
    /// </para>
    /// </summary>
    Task PushCustomerChangedAsync(Guid customerId, CancellationToken ct);
}
