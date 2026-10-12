using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.ProjectLeaseReady;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class LandParcelAvailabilityMapper
{
    public const string NotVerifiedLabel = "Not verified";

    public static LandParcelAvailabilityDto Build(
        CadastralSourceMetadataDto? cadastralSource,
        ResearchSimulatedLeaseWorkflowDto? simulatedWorkflow,
        bool includeSimulatedWorkflow,
        ProjectLeaseReadyDesignationRecord? projectDesignation = null)
    {
        var simulated = includeSimulatedWorkflow ? simulatedWorkflow : null;
        var researchRecordId = cadastralSource?.ResearchRecordId;

        if (projectDesignation is not null)
        {
            var evaluation = ProjectLeaseReadyDesignationEvaluator.Evaluate(
                projectDesignation,
                cadastralSource);

            return new LandParcelAvailabilityDto(
                evaluation.DisplayLeaseAvailabilityLabel,
                researchRecordId ?? projectDesignation.ResearchRecordId,
                simulated,
                evaluation.Designation);
        }

        if (cadastralSource is null)
        {
            return new LandParcelAvailabilityDto("Unknown", null, null);
        }

        return new LandParcelAvailabilityDto(NotVerifiedLabel, researchRecordId, simulated);
    }
}
