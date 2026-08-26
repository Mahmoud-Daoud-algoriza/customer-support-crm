using SupportCrm.Domain.Modules.Identity;

namespace SupportCrm.Application.Abstractions;

/// <summary>An issued access token and the moment it stops being valid.</summary>
public sealed record IssuedToken(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Declared by Application, implemented in Infrastructure (AD-11) so token mechanics stay out of
/// the use-case layer.
/// <para>
/// <b>The signature is the enforcement of AD-7 and AD-15:</b> it takes a user id and returns a
/// token. There is no parameter through which a role, a department or an active flag could reach the
/// token, so the staleness defect §4.1.1 describes is unreachable by construction rather than by a
/// reviewer noticing.
/// </para>
/// </summary>
public interface ITokenIssuer
{
    IssuedToken Issue(Guid userId);
}
