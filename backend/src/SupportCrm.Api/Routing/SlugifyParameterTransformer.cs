using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace SupportCrm.Api.Routing;

/// <summary>
/// Rewrites the <c>[controller]</c> route token to a lower-case slug, so <c>HealthController</c>
/// serves — and <b>documents</b> — <c>/api/v1/health</c> rather than <c>/api/v1/Health</c>.
/// <para>
/// Applied as a route-token transformer rather than <c>RouteOptions.LowercaseUrls</c> because that
/// setting only affects generated links: the route template, and therefore the OpenAPI document,
/// would still carry the class name's casing. The paths in docs/api-design.md are lower case, and
/// the published contract has to match them exactly.
/// </para>
/// A multi-word controller becomes kebab-case (<c>KnowledgeArticlesController</c> ->
/// <c>knowledge-articles</c>).
/// </summary>
public sealed partial class SlugifyParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value) =>
        value is null ? null : WordBoundary().Replace(value.ToString()!, "$1-$2").ToLowerInvariant();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex WordBoundary();
}
