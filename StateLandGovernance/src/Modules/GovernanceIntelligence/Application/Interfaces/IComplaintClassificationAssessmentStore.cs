using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application-owned persistence boundary for complaint-classification assessment history.
/// </summary>
public interface IComplaintClassificationAssessmentStore
{
    Task AddAsync(
        ComplaintClassificationAssessment assessment,
        CancellationToken cancellationToken = default);

    Task<ComplaintClassificationAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);
}
