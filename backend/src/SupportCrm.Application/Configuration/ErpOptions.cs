namespace SupportCrm.Application.Configuration;

/// <summary>
/// Which implementation of the ERP / external-system seam runs (docs/architecture.md §5.3, §6.3).
///
/// <para>
/// <b>One value, and no product is named.</b> Requirements §11.4 is unbounded on purpose and
/// product-scope §9 <b>questions 2 and 3 stay open</b> — naming a system in this enum would answer
/// them by accident, in the place a reviewer is least likely to look for a product decision.
/// </para>
/// </summary>
public enum ExternalSystemGatewayKind
{
    /// <summary>
    /// The no-op gateway. <b>The only shipped value, and the default</b> — the whole application
    /// runs and demos with every real integration absent (product-scope §10 item 5, T3-D).
    /// </summary>
    NoOp = 0,
}

/// <summary>
/// <c>SupportCrm:Erp</c> — the ERP seam's configuration (docs/architecture.md §6.3).
///
/// <para>
/// <b>Absent from <c>appsettings.json</c> by design</b>, like the AI and channel seams. There is no
/// connection string, base address, credential or field map here: a real connection is out of scope
/// (T3-D), and configuration for a connector nobody wrote would be the "designed for" claim
/// pretending to be delivery.
/// </para>
/// </summary>
public sealed class ErpOptions
{
    public const string SectionName = "SupportCrm:Erp";

    /// <summary>
    /// Defaults to <see cref="ExternalSystemGatewayKind.NoOp"/>, so the app starts with no
    /// configuration.
    /// </summary>
    public ExternalSystemGatewayKind Gateway { get; init; } = ExternalSystemGatewayKind.NoOp;
}
