using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Domain.Services;

/// <summary>
/// Domain contract for evaluating land suitability from parcel context and criteria.
/// Implementation belongs in Application or Infrastructure layers.
/// </summary>
public interface ILandSuitabilityEvaluator
{
    LandRecommendation Evaluate(LandParcel parcel, IReadOnlyCollection<RecommendationCriterion> criteria);
}
