namespace StateLandGovernance.LandIntelligence.Application.DTOs;

/// <summary>
/// Fabricated workflow status for research demonstrations only.
/// </summary>
public sealed record ResearchSimulatedLeaseWorkflowDto(
    string ResearchRecordId,
    string ScenarioId,
    string SimulatedLeaseStatus,
    bool IsSimulatedLeaseStatus,
    string? DatasetUsage,
    string? Notice);

public sealed record LandParcelAvailabilityDto(
    string ActualLeaseAvailabilityLabel,
    string? ResearchRecordId,
    ResearchSimulatedLeaseWorkflowDto? SimulatedWorkflow,
    ProjectLeaseReadyDesignationDto? ProjectLeaseReady = null);
