using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Configuration;

namespace SupportCrm.Infrastructure.Security;

/// <summary>
/// Issues the JWT bearer token of AD-7.
/// <para>
/// <b>Claims: <c>sub</c>, <c>jti</c>, <c>iat</c>, <c>exp</c> — and nothing else.</b> No role, no
/// department, no email, no active flag. AD-15 is enforced by the shape of this method: there is
/// nothing here to make stale, because nothing an authorization decision reads is present. Role,
/// department and active status are resolved per request from the authoritative row
/// (docs/architecture.md §4.1.1).
/// </para>
/// No refresh rotation and no logout endpoint (AP-8) — expiry means signing in again, and the client
/// discards the token.
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : ITokenIssuer
{
    public IssuedToken Issue(Guid userId)
    {
        var settings = options.Value;
        var issuedAt = clock.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(settings.AccessTokenMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        // JwtRegisteredClaimNames.Sub carries the user id — the only claim any decision reads.
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

/// <summary>
/// The registered claim names used above, spelled out so the set is auditable at a glance rather
/// than hidden behind a library constant.
/// </summary>
internal static class JwtRegisteredClaimNames
{
    internal const string Sub = "sub";
    internal const string Jti = "jti";
}
