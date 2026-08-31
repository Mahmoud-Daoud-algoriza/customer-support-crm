using SupportCrm.Application.Abstractions;
using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Api.Auth;

/// <summary>
/// The request-scoped holder that <see cref="CurrentUserMiddleware"/> fills and Application
/// services read through <see cref="ICurrentUser"/>.
/// <para>
/// Every value here was read from the database <b>during this request</b>. Nothing is cached across
/// requests — a cache would reintroduce exactly the staleness AD-15 removes.
/// </para>
/// </summary>
public sealed class CurrentUserAccessor : ICurrentUser
{
    private ResolvedUser? _resolved;

    public Guid Id => Require().Id;
    public UserRole Role => Require().Role;
    public Guid? DepartmentId => Require().DepartmentId;
    public Guid? CustomerId => Require().CustomerId;
    public string DisplayName => Require().DisplayName;
    public string Email => Require().Email;

    /// <summary>True once the middleware has resolved an active user for this request.</summary>
    public bool IsAuthenticated => _resolved is not null;

    public bool IsInRoleAtLeast(UserRole minimum) => Require().Role.RankAtLeast(minimum);

    internal void Set(Guid id, string email, string displayName, UserRole role, Guid? departmentId, Guid? customerId) =>
        _resolved = new ResolvedUser(id, email, displayName, role, departmentId, customerId);

    /// <summary>
    /// Installs the <b>system identity</b> for a background scope — Story 09's SLA sweep, the only
    /// caller (AD-6, docs/architecture.md §3: <em>"two flows do not start in the browser"</em>).
    ///
    /// <para>
    /// <b>Why this exists at all.</b> The sweep must reuse <c>TicketLifecycleService.EscalateAsync</c>
    /// rather than duplicate escalation, and that method loads its ticket through
    /// <c>TicketScope.LoadScopedAsync</c>, which asks <see cref="ICurrentUser"/> who is calling. There
    /// is no caller on a timer tick, so without an identity the reused path throws
    /// <see cref="NoCurrentUserException"/> — the alternative would be a second, unscoped escalation
    /// path, which is exactly what the intake forbids.
    /// </para>
    ///
    /// <para>
    /// <b>The role is <c>Administrator</c> because the sweep is department-blind.</b> A breach in
    /// Billing must be escalated whether or not any human can see Billing, and
    /// <c>TicketScope.ForCaller</c> leaves Manager and Administrator unrestricted (A-4). This is not a
    /// privilege escalation for a user: no request can reach this method, and it is only ever called
    /// on a scope that no HTTP request owns.
    /// </para>
    ///
    /// <para>
    /// <b><c>Id</c> is <see cref="Guid.Empty"/> and is never attributed to anything.</b> The system
    /// path writes its history through <c>RecordBySystemAsync</c> — <c>actorKind = System</c>,
    /// <c>actorUserId = null</c> (docs/data-model.md §2.7) — and <c>EscalateAsync</c> skips the audit
    /// entry entirely for a system actor, so this id reaches no column. It is not a user and there is
    /// deliberately no <c>User</c> row for it: one would be a login nobody owns.
    /// </para>
    /// </summary>
    internal void SetSystem() =>
        _resolved = new ResolvedUser(
            Guid.Empty, "system@supportcrm.local", "System", UserRole.Administrator, null, null);

    private ResolvedUser Require() => _resolved ?? throw new NoCurrentUserException();

    private sealed record ResolvedUser(
        Guid Id, string Email, string DisplayName, UserRole Role, Guid? DepartmentId, Guid? CustomerId);
}
