namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>Source-backed complaint, concurrent-application, lease/overlap review coverage.
/// Null means unavailable, never verified absence. Not an applicant/browser input port.</summary>
public interface IScreeningEvidenceProvider
{
    Task<ScreeningEvidenceContext> ReadAsync(Guid caseId, Guid? parcelId, CancellationToken cancellationToken);
}
