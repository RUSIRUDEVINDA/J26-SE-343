using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class RegulatoryCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.05m;
    public string Key => "regulatory";
    public int Order => 70;

    public bool IsApplicable(LandRecommendationSearchRequest request) =>
        request.Regulatory is not null;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var criteria = request.Regulatory!;
        var referenceCount = parcel.RegulatoryReferences.Count;
        var regulatoryProvenance = ParcelAttributeProvenanceResolver.ResolveRegulatoryProvenance(parcel);

        if (referenceCount == 0)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Regulatory References",
                CriterionCategory.RegulatoryRequirement,
                isMet: true,
                score: 100m,
                DefaultWeight,
                "No regulatory references are recorded for this parcel.",
                AttributeProvenance.Unknown("Regulatory references"));
        }

        if (referenceCount <= criteria.MaxRegulatoryReferences)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Regulatory References",
                CriterionCategory.RegulatoryRequirement,
                isMet: true,
                score: 90m,
                DefaultWeight,
                $"{referenceCount} regulatory reference(s) recorded (within limit of {criteria.MaxRegulatoryReferences}).",
                regulatoryProvenance,
                "regulatory.references");
        }

        if (!criteria.PenalizeMultipleReferences)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Regulatory References",
                CriterionCategory.RegulatoryRequirement,
                isMet: true,
                score: 80m,
                DefaultWeight,
                $"{referenceCount} regulatory references recorded.",
                regulatoryProvenance,
                "regulatory.references");
        }

        var excess = referenceCount - criteria.MaxRegulatoryReferences;
        var score = Math.Max(0m, 70m - (excess * 15m));

        return CriterionEvaluationFactory.Create(
            Key,
            "Regulatory References",
            CriterionCategory.RegulatoryRequirement,
            isMet: false,
            score,
            DefaultWeight,
            $"{referenceCount} regulatory references exceed configured limit ({criteria.MaxRegulatoryReferences}).",
            regulatoryProvenance,
            "regulatory.references");
    }
}
