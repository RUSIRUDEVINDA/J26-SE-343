using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Recommendations;

public static class LandRecommendationSearchRequestExtensions
{
    /// <summary>
    /// Resolves the target area for closeness scoring from the current or legacy request fields.
    /// Target area is never used as a hard minimum filter.
    /// </summary>
    public static decimal? ResolveTargetAreaHectares(this LandRecommendationSearchRequest request)
    {
        if (request.TargetAreaHectares is > 0)
        {
            return request.TargetAreaHectares;
        }

        if (request.PreferredAreaHectares is > 0)
        {
            return request.PreferredAreaHectares;
        }

        if (request.RequiredAreaHectares is > 0)
        {
            return request.RequiredAreaHectares;
        }

        return null;
    }
}
