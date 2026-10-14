using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Security;
using StateLandGovernance.LandIntelligence.Presentation.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Controllers;

[ApiController]
[ApiExplorerSettings(GroupName = LandIntelligenceApiGroups.External)]
[Route("api/v1/land/auth")]
[Produces("application/json")]
public sealed class LandIntelligenceAuthController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly LandIntelligenceAuthenticationOptions _options;
    private readonly LandIntelligenceJwtTokenService _tokenService;

    public LandIntelligenceAuthController(
        IHostEnvironment environment,
        IOptions<LandIntelligenceAuthenticationOptions> options,
        LandIntelligenceJwtTokenService tokenService)
    {
        _environment = environment;
        _options = options.Value;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Development-only helper to obtain JWT access tokens for local Component 1 testing.
    /// Not available in production.
    /// </summary>
    [HttpPost("session")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LandIntelligenceSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LandIntelligenceSessionResponse> CreateDevelopmentSession(
        [FromBody] LandIntelligenceSessionRequest request)
    {
        if (!_environment.IsDevelopment() || !_options.AllowDevelopmentSessionEndpoint)
        {
            return NotFound();
        }

        var subjectId = request.SubjectId is { } id && id != Guid.Empty
            ? id
            : Guid.NewGuid();

        var roles = ResolveRoles(request.Profile);
        var accessToken = _tokenService.CreateAccessToken(subjectId, roles);

        return Ok(
            new LandIntelligenceSessionResponse(
                accessToken,
                _options.AccessTokenLifetimeMinutes * 60,
                subjectId,
                roles));
    }

    private static IReadOnlyList<string> ResolveRoles(string? profile) =>
        profile?.Trim().ToLowerInvariant() switch
        {
            "officer" => [LandIntelligenceAuth.Roles.Officer],
            "internal" => [LandIntelligenceAuth.Roles.InternalMaintainer],
            _ => [LandIntelligenceAuth.Roles.Applicant],
        };
}

public sealed record LandIntelligenceSessionRequest(string? Profile, Guid? SubjectId);

public sealed record LandIntelligenceSessionResponse(
    string AccessToken,
    int ExpiresInSeconds,
    Guid SubjectId,
    IReadOnlyList<string> Roles);
