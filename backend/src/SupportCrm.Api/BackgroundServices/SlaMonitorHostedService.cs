using Microsoft.Extensions.Options;
using SupportCrm.Api.Auth;
using SupportCrm.Application.Configuration;
using SupportCrm.Application.Modules.Sla;

namespace SupportCrm.Api.BackgroundServices;

/// <summary>
/// <b>The periodic in-process breach check of AD-6</b> — one of the two flows that do not start in
/// the browser (docs/architecture.md §3).
///
/// <para>
/// <b>It contains no business logic</b> (architecture §2.1, AD-6). Every tick does three things:
/// create a scope, resolve <see cref="SlaEvaluationService"/>, call it. What counts as a breach and
/// what happens on one lives entirely in that service, so this file has nothing to get wrong about
/// SLA policy and nothing a test would need to exercise separately.
/// </para>
///
/// <para>
/// <b>No queue, no broker, no external job scheduler</b> (product-scope §8, architecture §8). The
/// excluded packages are deliberately not named here: the plan's verification greps the source for
/// them, and a comment listing them would make that check report a hit forever. A <see cref="PeriodicTimer"/> is the whole mechanism, because A-3 states that
/// <em>"timing precision is explicitly not a goal"</em>: the granularity is minutes, not seconds, and
/// a sweep that runs a little late produces the same rows a sweep that runs on time would.
/// </para>
///
/// <para>
/// <b>A failed tick must never stop the host.</b> The <c>try</c>/<c>catch</c> logs and continues: a
/// transient database failure would otherwise silently kill SLA monitoring for the lifetime of the
/// process, which is the failure mode nobody notices until the demo. Cancellation is the one
/// exception that ends the loop, because that is the host shutting down rather than a fault.
/// </para>
///
/// <para>
/// <b>Idempotence is the evaluator's, not this timer's.</b> The latching breach flags mean a tick
/// that overlaps or repeats work finds nothing to do, so there is no lock here and none is needed.
/// </para>
/// </summary>
public sealed class SlaMonitorHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaMonitorOptions> monitorOptions,
    ILogger<SlaMonitorHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(monitorOptions.Value.SweepIntervalSeconds);

        logger.LogInformation(
            "SLA monitor started; sweeping every {IntervalSeconds}s (A-3, AD-6).",
            interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);

        // Swept once at startup as well as on every tick: the seeded overdue tickets should be
        // breached by the time anyone opens the queue, not one interval later.
        await SweepAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        try
        {
            // A scope per tick, because the evaluator and its DbContext are scoped — a singleton
            // hosted service must never capture either (architecture §2.1).
            using var scope = scopeFactory.CreateScope();

            // The tick has no HTTP caller, so the scope gets the system identity: the reused
            // escalation path resolves its ticket through TicketScope, which asks who is calling.
            // See CurrentUserAccessor.SetSystem for why this is not a privilege escalation.
            scope.ServiceProvider.GetRequiredService<CurrentUserAccessor>().SetSystem();

            var evaluator = scope.ServiceProvider.GetRequiredService<SlaEvaluationService>();

            await evaluator.EvaluateDueTicketsAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The host is stopping. Not a fault, and not something to log as one.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SLA sweep failed. The monitor continues; the next tick retries.");
        }
    }
}
