namespace SupportCrm.Domain.Modules.Administration;

/// <summary>
/// The audit action codes written by Story 02, as constants so a typo cannot silently create a new
/// action that no filter will ever match.
/// <para>
/// <b>This is not a closed set.</b> docs/data-model.md §2.14 gives the actions as examples and
/// names ticket actions (<c>TicketStatusChanged</c>, <c>TicketEscalated</c>) that later stories add
/// here. The column stays a string code for exactly that reason.
/// </para>
/// </summary>
public static class AuditAction
{
    public const string SignInSucceeded = nameof(SignInSucceeded);
    public const string SignInFailed = nameof(SignInFailed);
    public const string UserCreated = nameof(UserCreated);
    public const string UserDeactivated = nameof(UserDeactivated);
    public const string UserRoleChanged = nameof(UserRoleChanged);
    public const string UserDepartmentChanged = nameof(UserDepartmentChanged);

    /// <summary>
    /// Story 04, <b>A-19</b> — written when, and only when, changing a customer's email actually
    /// changes a linked portal login's sign-in address (docs/data-model.md §5 constraint 1b, §2.14,
    /// docs/api-design.md §5.5).
    /// <para>
    /// <b>The audited fact is that a sign-in identifier changed</b>, so the target is the linked
    /// <c>User</c>, not the customer — and the customer-profile edit on its own is <b>not</b>
    /// audited, because business data is not a security event (AD-10).
    /// </para>
    /// <para>
    /// This constant is <b>the entire schema change</b> A-19 needs: <see cref="AuditEntry"/>,
    /// <see cref="AuditTargetType"/>, <c>IAuditRecorder</c> and the migration are all untouched. The
    /// entry records <b>no email address, old or new</b> — <see cref="AuditEntry"/> has no value
    /// columns (docs/data-model.md §2.14), and the address must not be smuggled into
    /// <c>actorDescriptor</c>, which is the failed-sign-in identifier and nothing else.
    /// </para>
    /// </summary>
    public const string UserEmailChanged = nameof(UserEmailChanged);

    /// <summary>
    /// Story 06 — a ticket moved between statuses through <c>POST /tickets/{id}/transition</c>.
    /// <para>
    /// <b>This is not the ticket's history.</b> <c>TicketActivity</c> carries the business trail
    /// with before/after values; this is the security log, and the two stay independently queryable
    /// and neither derived from the other (AD-10, docs/data-model.md §2.14). The audit entry records
    /// <b>that</b> a status changed and by whom; <b>which</b> statuses is the activity row's job,
    /// because <see cref="AuditEntry"/> has no value columns.
    /// </para>
    /// </summary>
    public const string TicketStatusChanged = nameof(TicketStatusChanged);

    /// <summary>
    /// Story 06 — a ticket was escalated through <c>POST /tickets/{id}/escalate</c> (AP-7).
    /// <para>
    /// Written on the <b>user</b> path only. A system-initiated escalation — Story 09's breach
    /// sweep — has no actor, and §2.14 admits a null <c>actorUserId</c> for one reason only, a
    /// failed sign-in. What that path audits is Story 09's to decide (finding <b>I-22</b>).
    /// </para>
    /// </summary>
    public const string TicketEscalated = nameof(TicketEscalated);
}

/// <summary>Audit target type codes. Referenced by type + id rather than by foreign key (§2.14).</summary>
public static class AuditTargetType
{
    public const string User = nameof(User);

    /// <summary>Story 06 — the target of <c>TicketStatusChanged</c> and <c>TicketEscalated</c>.</summary>
    public const string Ticket = nameof(Ticket);
}
