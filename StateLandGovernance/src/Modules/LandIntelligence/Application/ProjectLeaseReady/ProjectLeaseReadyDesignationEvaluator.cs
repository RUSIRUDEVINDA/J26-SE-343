using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Mappings;
using StateLandGovernance.LandIntelligence.Application.Recommendations;
using static StateLandGovernance.LandIntelligence.Application.Recommendations.StateLandCandidateScreeningOutcome;

namespace StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;

public static class ProjectLeaseReadyDesignationEvaluator
{
    public const string PrivateExclusionLeaseLabel = "Not lease-ready — source-reported Private";

    public sealed record Evaluation(
        bool IsDesignatedInProjectDataset,
        bool IsEffectiveForLeaseFlow,
        string DisplayLeaseAvailabilityLabel,
        ProjectLeaseReadyDesignationDto Designation);

    public static Evaluation Evaluate(
        ProjectLeaseReadyDesignationRecord designation,
        CadastralSourceMetadataDto? cadastralSource)
    {
        var screening = SourceReportedClaimantScreening.Evaluate(cadastralSource);
        var excludedPrivate = screening.Outcome == ExcludedPrivate;

        var isEffective = !excludedPrivate;
        var displayLabel = isEffective
            ? ColomboStateResearchProjectLeaseReady.DesignationLabel
            : excludedPrivate
                ? PrivateExclusionLeaseLabel
                : LandParcelAvailabilityMapper.NotVerifiedLabel;

        var dto = new ProjectLeaseReadyDesignationDto(
            designation.DatasetKey,
            designation.ResearchRecordId,
            ColomboStateResearchProjectLeaseReady.DesignationLabel,
            ColomboStateResearchProjectLeaseReady.DesignationNotice,
            IsDesignatedInProjectDataset: true,
            IsEffectiveForLeaseFlow: isEffective,
            ExcludedBecauseSourceReportedPrivate: excludedPrivate);

        return new Evaluation(true, isEffective, displayLabel, dto);
    }

    public static bool IsSourceReportedPrivate(CadastralSourceMetadataDto? cadastralSource)
    {
        var screening = SourceReportedClaimantScreening.Evaluate(cadastralSource);
        return screening.Outcome == ExcludedPrivate;
    }
}
