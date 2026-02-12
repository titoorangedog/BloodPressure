using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BloodPressure.Persistence.Entities;
using BloodPressure.Shared.Auth;
using BloodPressure.Shared.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BloodPressure.AuthService.Services;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTimeOffset ExpiresAtUtc) CreateToken(UserEntity user, LicenseType licenseType, bool rememberMe)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var accessMinutes = rememberMe ? _options.RememberAccessTokenMinutes : _options.AccessTokenMinutes;
        var expires = nowUtc.AddMinutes(accessMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(AuthConstants.RoleClaim, user.Role.ToString()),
            new(AuthConstants.LicenseClaim, licenseType.ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: nowUtc.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
