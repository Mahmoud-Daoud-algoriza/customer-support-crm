using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Infrastructure.Persistence;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The only endpoint in this API that reports its own dependency state
/// (docs/api-design.md §5.1, requirements §11.1).
/// </summary>
public sealed class HealthController(SupportCrmDbContext db) : ApiControllerBase
{
    /// <summary>Liveness plus database reachability. Anonymous.</summary>
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken ct)
    {
        var reachable = await db.Database.CanConnectAsync(ct);

        var response = new HealthResponse(
            Status: reachable ? "ok" : "degraded",
            Database: reachable ? "reachable" : "unreachable",
            UtcNow: DateTimeOffset.UtcNow);

        return reachable
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}

public sealed record HealthResponse(string Status, string Database, DateTimeOffset UtcNow);
