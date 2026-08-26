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

    private ResolvedUser Require() => _resolved ?? throw new NoCurrentUserException();

    private sealed record ResolvedUser(
        Guid Id, string Email, string DisplayName, UserRole Role, Guid? DepartmentId, Guid? CustomerId);
}
