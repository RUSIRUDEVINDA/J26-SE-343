using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Configuration;

public sealed class LeaseInterestOfficerActorContext : ILeaseInterestOfficerActorContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LeaseInterestOfficerActorContext(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public Guid? OfficerActorId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            if (!user.IsInRole(LandIntelligenceAuth.Roles.Officer))
            {
                return null;
            }

            var claim =
                user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(claim, out var officerId) && officerId != Guid.Empty
                ? officerId
                : null;
        }
    }
}
