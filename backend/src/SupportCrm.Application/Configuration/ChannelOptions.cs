namespace SupportCrm.Application.Configuration;

/// <summary>
/// Which implementation of the outbound channel seam runs (docs/architecture.md §5.2, §6.3).
///
/// <para>
/// <b>The enum has one value on purpose, and that is the seam stated in the type.</b> A second value
/// arrives with the adapter that implements it — an email, WhatsApp or SMS provider — and nothing
/// else moves when it does. An enum listing providers nobody wrote would be a promise the codebase
/// cannot keep.
/// </para>
/// </summary>
public enum ChannelAdapterKind
{
    /// <summary>
    /// The console/log adapter. <b>The only shipped value, and the default</b>: product-scope §10
    /// item 5 requires the whole system to run with no external accounts or credentials, so the
    /// implementation that needs neither has to be the one selected when nothing is configured.
    /// </summary>
    Console = 0,
}

/// <summary>
/// <c>SupportCrm:Channels</c> — the outbound channel seam's configuration (docs/architecture.md
/// §6.3).
///
/// <para>
/// <b>Absent from <c>appsettings.json</c> by design</b>, exactly as the AI seam's section is. The
/// default below is the shipped behaviour, so the application starts, demos and passes its whole
/// suite with this section never written down — which is the observable form of T3's rule of
/// engagement.
/// </para>
///
/// <para>
/// <b>No provider is named here or anywhere.</b> There is no endpoint, key, sender id or template
/// key, because no adapter that would read one exists. What a real provider adapter must bring with
/// it is listed in <c>docs/integration-seams.md</c>, not stubbed here as configuration nothing
/// reads.
/// </para>
/// </summary>
public sealed class ChannelOptions
{
    public const string SectionName = "SupportCrm:Channels";

    /// <summary>
    /// Defaults to <see cref="ChannelAdapterKind.Console"/>, so the app starts with no
    /// configuration.
    /// </summary>
    public ChannelAdapterKind Outbound { get; init; } = ChannelAdapterKind.Console;
}
