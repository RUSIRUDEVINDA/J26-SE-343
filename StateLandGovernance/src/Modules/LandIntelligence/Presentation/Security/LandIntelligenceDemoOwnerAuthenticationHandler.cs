using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Security;

/// <summary>
/// Development-only authentication that treats a trusted-format owner header as an applicant principal.
/// Disabled outside Development or when AllowDemoOwnerHeader is false.
/// </summary>
public sealed class LandIntelligenceDemoOwnerAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly LandIntelligenceAuthenticationOptions _landOptions;
    private readonly IHostEnvironment _environment;

    public LandIntelligenceDemoOwnerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<LandIntelligenceAuthenticationOptions> landOptions,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _landOptions = landOptions.Value;
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_environment.IsDevelopment() || !_landOptions.AllowDemoOwnerHeader)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Request.Headers.TryGetValue(
                LandIntelligenceCallerHeaders.OwnerIdHeaderName,
                out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var raw = headerValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var ownerId) || ownerId == Guid.Empty)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid demo owner header."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ownerId.ToString("D")),
            new Claim(LandIntelligenceAuth.ClaimTypes.OwnerId, ownerId.ToString("D")),
            new Claim(ClaimTypes.Role, LandIntelligenceAuth.Roles.Applicant),
        };

        var identity = new ClaimsIdentity(claims, LandIntelligenceAuth.Schemes.DemoOwner);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, LandIntelligenceAuth.Schemes.DemoOwner);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
