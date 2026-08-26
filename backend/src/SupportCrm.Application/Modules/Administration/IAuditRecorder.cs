using SupportCrm.Domain.Modules.Administration;

namespace SupportCrm.Application.Modules.Administration;

/// <summary>
/// <b>The single audit writer</b> (docs/architecture.md §2.4). Every audit entry in the system
/// passes through this one interface.
/// <para>
/// <b>It exposes no update and no delete method</b> — not merely no UI, but no service method. That
/// is what makes the log append-only by construction rather than by policy.
/// </para>
/// Called from Application services only, never from a controller and never from Infrastructure.
/// </summary>
public interface IAuditRecorder
{
    /// <param name="action">A stable action code — see <see cref="AuditAction"/>.</param>
    /// <param name="outcome">Success or Failure (docs/data-model.md §2.14).</param>
    /// <param name="targetType">The audited entity's type code, e.g. <c>User</c>.</param>
    /// <param name="targetId">The audited entity's id. Not a foreign key (§2.14).</param>
    /// <param name="actorDescriptor">
    /// The submitted identifier, used when no actor could be resolved — a failed sign-in. The
    /// implementation <b>truncates it to fit rather than throwing</b> (docs/data-model.md §6.1).
    /// </param>
    /// <param name="actorUserId">
    /// An explicit actor, for the one case where the caller knows who acted but no request identity
    /// exists yet: <b>a successful sign-in</b>. <c>POST /auth/login</c> is anonymous, so
    /// <see cref="ICurrentUser"/> is empty while the user has in fact just been resolved.
    /// <para>
    /// Without this, every successful sign-in would record <c>actorUserId = null</c> — and
    /// docs/data-model.md §2.14 permits null for exactly one reason, "no user could be resolved —
    /// a failed sign-in". Leave it null everywhere else and the actor comes from the request.
    /// </para>
    /// </param>
    Task RecordAsync(
        string action,
        AuditOutcome outcome,
        string? targetType = null,
        Guid? targetId = null,
        string? actorDescriptor = null,
        Guid? actorUserId = null,
        CancellationToken ct = default);
}
