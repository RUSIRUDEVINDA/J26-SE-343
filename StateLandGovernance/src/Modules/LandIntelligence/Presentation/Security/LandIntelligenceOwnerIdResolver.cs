using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Security;

public static class LandIntelligenceOwnerIdResolver
{
    public static Guid? ResolveOwnerId(HttpContext? context)
    {
        if (context?.User.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var ownerClaim =
            context.User.FindFirst(LandIntelligenceAuth.ClaimTypes.OwnerId)?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (Guid.TryParse(ownerClaim, out var ownerId) && ownerId != Guid.Empty)
        {
            return ownerId;
        }

        return null;
    }
}
