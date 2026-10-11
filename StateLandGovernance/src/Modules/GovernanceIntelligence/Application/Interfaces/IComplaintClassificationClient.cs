using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Application boundary for advisory English complaint classification.
/// </summary>
public interface IComplaintClassificationClient
{
    Task<ComplaintClassificationResult> ClassifyAsync(
        string complaintText,
        string? caseId = null,
        CancellationToken cancellationToken = default);
}
