using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

/// <summary>
/// Pluggable criterion evaluator for transparent, rule-based scoring.
/// </summary>
public interface IRecommendationCriterionEvaluator
{
    string Key { get; }
    int Order { get; }
    bool IsApplicable(LandRecommendationSearchRequest request);
    CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request);
}
