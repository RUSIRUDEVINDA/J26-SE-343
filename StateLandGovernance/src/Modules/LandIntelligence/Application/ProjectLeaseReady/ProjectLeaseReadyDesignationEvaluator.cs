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
        ProjectLeaseReadyDesignationDto Designation)
    {
        /// <summary>
        /// Project dataset designation label (not official lease availability).
        /// Source-reported Private parcels show the exclusion label instead of the lease-ready designation.
        /// </summary>
        public string ProjectDesignationLabel => IsEffectiveForLeaseFlow
            ? Designation.DesignationLabel
            : PrivateExclusionLeaseLabel;
    }

    public static Evaluation Evaluate(
        ProjectLeaseReadyDesignationRecord designation,
        CadastralSourceMetadataDto? cadastralSource)
    {
        var screening = SourceReportedClaimantScreening.Evaluate(cadastralSource);
        var excludedPrivate = screening.Outcome == ExcludedPrivate;

        var isEffective = !excludedPrivate;
        // The actual lease availability label remains "Not verified" for all research parcels
        // unless an independent authoritative availability record exists.
        // Project designations are separate and never populate this field.
        var displayLabel = LandParcelAvailabilityMapper.NotVerifiedLabel;

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
