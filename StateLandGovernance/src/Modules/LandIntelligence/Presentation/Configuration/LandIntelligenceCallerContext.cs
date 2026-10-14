using Microsoft.AspNetCore.Http;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Presentation.Security;

namespace StateLandGovernance.LandIntelligence.Presentation.Configuration;

public sealed class LandIntelligenceCallerContext : ILandIntelligenceCallerContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LandIntelligenceCallerContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? OwnerId => LandIntelligenceOwnerIdResolver.ResolveOwnerId(_httpContextAccessor.HttpContext);
}
