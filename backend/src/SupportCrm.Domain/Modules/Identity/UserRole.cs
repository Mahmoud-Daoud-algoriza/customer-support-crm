namespace SupportCrm.Domain.Modules.Identity;

/// <summary>
/// The four fixed roles of A-4. There is no role table, no role editor and no per-field permission
/// (docs/architecture.md §4.2).
/// <para>
/// <b>The numeric order is the hierarchy.</b> An endpoint marked <c>Agent</c> is also reachable by
/// Manager and Administrator unless a narrower rule is stated (docs/api-design.md §4.2), and
/// <see cref="UserRoleExtensions.RankAtLeast"/> is the only comparison used anywhere.
/// </para>
/// Persisted as a <b>stable string code</b>, never as an integer (docs/api-design.md §2), so
/// renumbering this enum could never silently re-grant access.
/// </summary>
public enum UserRole
{
    Customer = 0,
    Agent = 1,
    Manager = 2,
    Administrator = 3,
}

public static class UserRoleExtensions
{
    /// <summary>The single role comparison in the codebase. Nothing compares roles inline.</summary>
    public static bool RankAtLeast(this UserRole role, UserRole minimum) => role >= minimum;

    /// <summary>
    /// True for <c>Agent</c>, <c>Manager</c> and <c>Administrator</c>. A staff user requires a
    /// department and forbids a customer link; <c>Customer</c> is the reverse (DM-1,
    /// docs/data-model.md §5 constraint 2).
    /// </summary>
    public static bool IsStaff(this UserRole role) => role != UserRole.Customer;
}
