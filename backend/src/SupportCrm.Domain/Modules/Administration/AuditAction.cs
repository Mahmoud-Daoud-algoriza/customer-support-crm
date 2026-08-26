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
}

/// <summary>Audit target type codes. Referenced by type + id rather than by foreign key (§2.14).</summary>
public static class AuditTargetType
{
    public const string User = nameof(User);
}
