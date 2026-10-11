using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Mappings;

public static class LandParcelAvailabilityMapper
{
    public const string NotVerifiedLabel = "Not verified";

    public static LandParcelAvailabilityDto Build(
        CadastralSourceMetadataDto? cadastralSource,
        ResearchSimulatedLeaseWorkflowDto? simulatedWorkflow,
        bool includeSimulatedWorkflow)
    {
        if (cadastralSource is null)
        {
            return new LandParcelAvailabilityDto("Unknown", null, null);
        }

        var researchRecordId = cadastralSource.ResearchRecordId;
        var simulated = includeSimulatedWorkflow ? simulatedWorkflow : null;
        return new LandParcelAvailabilityDto(NotVerifiedLabel, researchRecordId, simulated);
    }
}
