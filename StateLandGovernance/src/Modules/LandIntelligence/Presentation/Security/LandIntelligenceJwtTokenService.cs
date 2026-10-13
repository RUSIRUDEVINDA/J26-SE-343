using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Security;

public sealed class LandIntelligenceJwtTokenService
{
    private readonly LandIntelligenceAuthenticationOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    public LandIntelligenceJwtTokenService(IOptions<LandIntelligenceAuthenticationOptions> options)
    {
        _options = options.Value;
        _signingKey = new SymmetricSecurityKey(ResolveSigningKeyBytes(_options));
    }

    public string CreateAccessToken(Guid subjectId, IReadOnlyList<string> roles)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subjectId.ToString("D")),
            new(LandIntelligenceAuth.ClaimTypes.OwnerId, subjectId.ToString("D")),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenLifetimeMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationParameters CreateValidationParameters() =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = JwtRegisteredClaimNames.Sub,
        };

    private static byte[] ResolveSigningKeyBytes(LandIntelligenceAuthenticationOptions options)
    {
        var raw = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_JWT_SIGNING_KEY"))
            ? Environment.GetEnvironmentVariable("LAND_INTELLIGENCE_JWT_SIGNING_KEY")
            : options.JwtSigningKeyBase64;

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "Configure LandIntelligence:Authentication:JwtSigningKeyBase64 or LAND_INTELLIGENCE_JWT_SIGNING_KEY.");
        }

        try
        {
            var bytes = Convert.FromBase64String(raw.Trim());
            if (bytes.Length < 32)
            {
                throw new InvalidOperationException("JWT signing key must decode to at least 32 bytes.");
            }

            return bytes;
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("JWT signing key must be valid base64.", ex);
        }
    }
}
