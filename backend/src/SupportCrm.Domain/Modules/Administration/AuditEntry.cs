namespace SupportCrm.Domain.Modules.Administration;

/// <summary>
/// The security and administration record (requirements §10.4, T2-H, docs/data-model.md §2.14).
/// <b>Separate from ticket history by AD-10</b> — different actors, different questions, different
/// visibility.
/// <para>
/// <b>Append-only by construction</b> (docs/architecture.md §2.4): this type exposes no mutator at
/// all, so there is no update or delete path to expose later by accident. Targets are referenced by
/// type + id rather than by foreign key, because the target may be any entity.
/// </para>
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Null when no user could be resolved — a failed sign-in.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>
    /// The submitted identifier when <see cref="ActorUserId"/> is null, so a failed sign-in is still
    /// attributable.
    /// <para>
    /// This is <b>unvalidated client input</b>. docs/data-model.md §6.1 requires the recorder to
    /// <b>truncate it to fit rather than throw</b>: a sign-in attempt with an absurd identifier must
    /// still be recorded, because recording it is the whole point of the column. The truncation
    /// happens in <c>AuditRecorder</c>, the one writer.
    /// </para>
    /// </summary>
    public string? ActorDescriptor { get; private set; }

    /// <summary>A stable action code — see <see cref="AuditAction"/>.</summary>
    public string Action { get; private set; } = default!;

    public string? TargetType { get; private set; }

    public Guid? TargetId { get; private set; }

    public AuditOutcome Outcome { get; private set; }

    /// <summary>
    /// The only way an entry comes into existence. Called by <c>AuditRecorder</c> and by nothing
    /// else — architecture §2.4 requires exactly one writer.
    /// </summary>
    public static AuditEntry Record(
        Guid id,
        DateTimeOffset occurredAt,
        string action,
        AuditOutcome outcome,
        Guid? actorUserId,
        string? actorDescriptor,
        string? targetType,
        Guid? targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        return new AuditEntry
        {
            Id = id,
            OccurredAt = occurredAt,
            Action = action,
            Outcome = outcome,
            ActorUserId = actorUserId,
            ActorDescriptor = actorDescriptor,
            TargetType = targetType,
            TargetId = targetId,
        };
    }
}
