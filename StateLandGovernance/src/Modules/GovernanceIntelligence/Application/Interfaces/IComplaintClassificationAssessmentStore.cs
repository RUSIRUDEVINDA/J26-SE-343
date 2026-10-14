using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application-owned persistence boundary for complaint-classification assessment history.
/// </summary>
public interface IComplaintClassificationAssessmentStore
{
    public const int DefaultHistoryLimit = 100;
    public const int MaxHistoryLimit = 200;

    Task AddAsync(
        ComplaintClassificationAssessment assessment,
        CancellationToken cancellationToken = default);

    Task<ComplaintClassificationAssessment?> GetByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ComplaintClassificationAssessment>> GetByCaseIdAsync(
        string caseId,
        int limit = DefaultHistoryLimit,
        CancellationToken cancellationToken = default);
}
