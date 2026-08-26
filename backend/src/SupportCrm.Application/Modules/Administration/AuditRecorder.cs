using Microsoft.Extensions.Logging;
using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Administration;

namespace SupportCrm.Application.Modules.Administration;

/// <summary>
/// The one writer of <see cref="AuditEntry"/> (docs/architecture.md §2.4).
/// <para>
/// It resolves the actor from <see cref="ICurrentUser"/> when a request has one, and falls back to
/// <c>actorDescriptor</c> when it does not — which is the failed-sign-in case that
/// docs/data-model.md §2.14 exists to keep attributable.
/// </para>
/// The entry is added to the change tracker; the caller's single <c>SaveChangesAsync</c> commits it
/// with the action it describes, so an audited action and its record land in one transaction
/// (docs/architecture.md §3 — one unit of work per request, committed once).
/// </summary>
public sealed class AuditRecorder(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock,
    ILogger<AuditRecorder> logger) : IAuditRecorder
{
    /// <summary>
    /// Matches the <c>ActorDescriptor</c> column width (Email tier, docs/data-model.md §6.1).
    /// </summary>
    private const int ActorDescriptorMaxLength = 256;

    public Task RecordAsync(
        string action,
        AuditOutcome outcome,
        string? targetType = null,
        Guid? targetId = null,
        string? actorDescriptor = null,
        Guid? actorUserId = null,
        CancellationToken ct = default)
    {
        // An explicit actor wins: the caller knows something the request does not, which is the
        // successful-sign-in case. Otherwise the actor is this request's resolved identity.
        var resolvedActor = actorUserId
            ?? (currentUser.IsAuthenticated ? currentUser.Id : null);

        var entry = AuditEntry.Record(
            id: Guid.NewGuid(),
            occurredAt: clock.GetUtcNow(),
            action: action,
            outcome: outcome,
            actorUserId: resolvedActor,
            actorDescriptor: Fit(actorDescriptor),
            targetType: targetType,
            targetId: targetId);

        db.AuditEntries.Add(entry);

        logger.LogInformation(
            "Audit: {Action} {Outcome} actor={Actor} target={TargetType}/{TargetId}",
            action, outcome, resolvedActor?.ToString() ?? actorDescriptor ?? "anonymous", targetType, targetId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// <c>actorDescriptor</c> is unvalidated client input — whatever was typed into the sign-in
    /// form. docs/data-model.md §6.1 requires truncation rather than an exception: a sign-in attempt
    /// with an absurd identifier must still be recorded, because recording it is the whole point of
    /// the column. Throwing here would lose exactly the entry that matters.
    /// </summary>
    private static string? Fit(string? actorDescriptor)
    {
        if (string.IsNullOrWhiteSpace(actorDescriptor))
        {
            return null;
        }

        var trimmed = actorDescriptor.Trim();

        return trimmed.Length <= ActorDescriptorMaxLength
            ? trimmed
            : trimmed[..ActorDescriptorMaxLength];
    }
}
