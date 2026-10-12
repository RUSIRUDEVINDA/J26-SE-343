using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface IResearchSimulatedLeaseReadRepository
{
    Task<ResearchSimulatedLeaseWorkflowDto?> GetByParcelIdAsync(
        Guid landParcelId,
        CancellationToken cancellationToken = default);
}
