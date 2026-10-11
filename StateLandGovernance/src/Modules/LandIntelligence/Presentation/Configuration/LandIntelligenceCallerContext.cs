using Microsoft.AspNetCore.Http;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;

namespace StateLandGovernance.LandIntelligence.Presentation.Configuration;

public sealed class LandIntelligenceCallerContext : ILandIntelligenceCallerContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LandIntelligenceCallerContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? OwnerId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.Items.TryGetValue(LandIntelligenceCallerHeaders.OwnerIdItemKey, out var value) == true
                && value is Guid ownerId
                && ownerId != Guid.Empty)
            {
                return ownerId;
            }

            return null;
        }
    }
}
