using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestCostAssessmentService
{
    LeaseInterestCostSummaryDto BuildCostSummary(Guid landParcelId);
}
