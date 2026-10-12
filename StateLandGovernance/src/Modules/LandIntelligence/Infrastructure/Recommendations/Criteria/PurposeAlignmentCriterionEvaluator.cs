using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Recommendations;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

internal sealed class PurposeAlignmentCriterionEvaluator : IRecommendationCriterionEvaluator
{
    public const decimal DefaultWeight = 0.15m;
    public string Key => "purpose-alignment";
    public int Order => 15;

    private readonly IRecommendationParcelContext _parcelContext;

    public PurposeAlignmentCriterionEvaluator(IRecommendationParcelContext parcelContext)
    {
        _parcelContext = parcelContext;
    }

    public bool IsApplicable(LandRecommendationSearchRequest request) => true;

    public CriterionEvaluationDto Evaluate(LandParcel parcel, LandRecommendationSearchRequest request)
    {
        var purpose = request.RequiredPurpose;
        var currentUse = parcel.CurrentUse?.Type;

        // Legacy / domain-managed parcels with CurrentUse keep the original domain comparison.
        if (currentUse is not null)
        {
            return EvaluateDomainCurrentUse(currentUse.Value, purpose);
        }

        var source = _parcelContext.GetSource(parcel.Id);
        if (source is null)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Purpose Alignment",
                CriterionCategory.LandUseMatch,
                isMet: false,
                score: 30m,
                DefaultWeight,
                $"Parcel use (unspecified) has limited alignment with requested purpose ({purpose}).",
                AttributeProvenance.Unknown("Purpose alignment"),
                "cadastralSource.sourceReportedLandUse");
        }

        var band = SourceReportedPurposeAlignment.Classify(
            purpose,
            source.SourceReportedLandUseCode,
            source.SourceReportedLandUseEnglishLabel);

        var display = !string.IsNullOrWhiteSpace(source.SourceReportedLandUseDisplay)
            ? source.SourceReportedLandUseDisplay
            : source.SourceReportedLandUseEnglishLabel ?? source.SourceReportedLandUseCode;

        var (score, isMet, summary) = SourceReportedPurposeAlignment.ToEvaluation(purpose, band, display);

        return CriterionEvaluationFactory.Create(
            Key,
            "Purpose Alignment",
            CriterionCategory.LandUseMatch,
            isMet,
            score,
            DefaultWeight,
            summary,
            AttributeProvenance.Official(source.SourceProvenanceLabel),
            "cadastralSource.sourceReportedLandUse",
            wasAssessed: band != SourceReportedPurposeAlignment.AlignmentBand.Unavailable,
            notAssessedReason: band == SourceReportedPurposeAlignment.AlignmentBand.Unavailable
                ? "Source-reported land use unavailable"
                : null);
    }

    private CriterionEvaluationDto EvaluateDomainCurrentUse(LandUseType currentUse, LandUseType purpose)
    {
        if (currentUse == purpose)
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Purpose Alignment",
                CriterionCategory.LandUseMatch,
                isMet: true,
                score: 100m,
                DefaultWeight,
                $"Parcel current use ({purpose}) aligns with requested purpose.");
        }

        if (IsCompatibleUse(currentUse, purpose))
        {
            return CriterionEvaluationFactory.Create(
                Key,
                "Purpose Alignment",
                CriterionCategory.LandUseMatch,
                isMet: true,
                score: 70m,
                DefaultWeight,
                $"Parcel use ({currentUse}) is compatible with requested purpose ({purpose}).");
        }

        return CriterionEvaluationFactory.Create(
            Key,
            "Purpose Alignment",
            CriterionCategory.LandUseMatch,
            isMet: false,
            score: 30m,
            DefaultWeight,
            $"Parcel use ({currentUse}) has limited alignment with requested purpose ({purpose}).");
    }

    private static bool IsCompatibleUse(LandUseType current, LandUseType purpose) => (current, purpose) switch
    {
        (LandUseType.MixedUse, _) => true,
        (_, LandUseType.MixedUse) => true,
        (LandUseType.Agricultural, LandUseType.Tourism) => true,
        (LandUseType.Tourism, LandUseType.Agricultural) => true,
        (LandUseType.Commercial, LandUseType.Industrial) => true,
        (LandUseType.Industrial, LandUseType.Commercial) => true,
        _ => false
    };
}
